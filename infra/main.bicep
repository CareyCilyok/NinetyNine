// NinetyNine production infrastructure — Azure Bicep template (resource-group scope).
//
// Deploys the full single-VM stack documented in docs/deployment.md:
//   - Standard SKU static public IP
//   - Network Security Group with allow-SSH (operator IP only) + allow-HTTP :80 (any)
//   - Virtual network and subnet
//   - Network interface bound to the public IP, subnet, and NSG
//   - Standard_B2s Linux VM (Ubuntu 22.04 LTS) with SSH key auth and cloud-init bootstrap
//
// Idempotent — re-running az deployment group create updates in place.
//
// The resource group itself is created by scripts/provision-azure.sh before this
// template runs, because Bicep at resource-group scope cannot create its own RG
// (that requires a subscription-scope deployment, which adds avoidable complexity
// for a single-RG personal project).
//
// Cloud-init bootstrap is supplied via the cloudInitContent parameter. The
// provision script reads infra/cloud-init.yaml and base64-encodes it before
// passing it in.

targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────────────────────

@description('Azure region for all resources. Defaults to East US (cheapest VM tier, full service availability).')
param location string = resourceGroup().location

@description('Resource name prefix; used for VM, NIC, NSG, IP, vnet, subnet names.')
param namePrefix string = 'ninetynine-prod'

@description('VM size. Standard_B2s = 2 vCPU / 4 GiB, burstable; matches docs/deployment.md.')
param vmSize string = 'Standard_B2s'

@description('Linux admin username for the VM.')
param adminUsername string = 'azureuser'

@description('SSH public key (full ssh-ed25519 / ssh-rsa string) authorized for the admin user.')
@secure()
param sshPublicKey string

@description('Operator public IP in CIDR /32 form, used to scope the SSH NSG rule. Auto-detected by provision-azure.sh.')
param operatorIpCidr string

@description('Cloud-init user-data, base64-encoded. Provision script encodes infra/cloud-init.yaml.')
param cloudInitBase64 string

@description('OS disk size in GB. 30 GB is enough for Docker images + app logs and stays inside the 12-month free Standard SSD allowance.')
@minValue(30)
@maxValue(1023)
param osDiskSizeGb int = 30

// ── Variables ─────────────────────────────────────────────────────────────────

var vmName = 'vm-${namePrefix}'
var nicName = 'nic-${namePrefix}'
var nsgName = 'nsg-${namePrefix}'
var publicIpName = 'pip-${namePrefix}'
var vnetName = 'vnet-${namePrefix}'
var subnetName = 'subnet-${namePrefix}'
var osDiskName = 'osdisk-${namePrefix}'

var commonTags = {
  application: 'NinetyNine'
  environment: 'production'
  managedBy: 'bicep'
}

// ── Network Security Group ───────────────────────────────────────────────────

resource nsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: nsgName
  location: location
  tags: commonTags
  properties: {
    securityRules: [
      {
        name: 'allow-ssh-operator'
        properties: {
          description: 'SSH from operator IP only. Update operatorIpCidr parameter if your IP changes.'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '22'
          sourceAddressPrefix: operatorIpCidr
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 100
          direction: 'Inbound'
        }
      }
      {
        name: 'allow-http-internet'
        properties: {
          description: 'HTTP :80 to the web container. TLS termination is a future hardening item (Caddy sidecar).'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          sourceAddressPrefix: 'Internet'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 110
          direction: 'Inbound'
        }
      }
    ]
  }
}

// ── Virtual Network + Subnet ─────────────────────────────────────────────────

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  tags: commonTags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: '10.0.1.0/24'
          networkSecurityGroup: {
            id: nsg.id
          }
        }
      }
    ]
  }
}

// Reference the subnet child resource explicitly so the NIC dependency is unambiguous.
resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = {
  parent: vnet
  name: subnetName
}

// ── Public IP (Standard SKU, Static allocation) ──────────────────────────────

resource publicIp 'Microsoft.Network/publicIPAddresses@2024-05-01' = {
  name: publicIpName
  location: location
  tags: commonTags
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
    idleTimeoutInMinutes: 4
  }
}

// ── Network Interface ────────────────────────────────────────────────────────

resource nic 'Microsoft.Network/networkInterfaces@2024-05-01' = {
  name: nicName
  location: location
  tags: commonTags
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Dynamic'
          subnet: {
            id: subnet.id
          }
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
  }
}

// ── Virtual Machine ──────────────────────────────────────────────────────────

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: vmName
  location: location
  tags: commonTags
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    osProfile: {
      computerName: vmName
      adminUsername: adminUsername
      // SSH key auth only — password auth is disabled below for security.
      linuxConfiguration: {
        disablePasswordAuthentication: true
        ssh: {
          publicKeys: [
            {
              path: '/home/${adminUsername}/.ssh/authorized_keys'
              keyData: sshPublicKey
            }
          ]
        }
        provisionVMAgent: true
        patchSettings: {
          patchMode: 'ImageDefault'
        }
      }
      // cloud-init runs on first boot to install Docker, fail2ban, etc.
      // The provision script base64-encodes infra/cloud-init.yaml into this field.
      customData: cloudInitBase64
    }
    storageProfile: {
      // Ubuntu 22.04 LTS Gen2 — matches the --image Ubuntu2204 alias used in
      // docs/deployment.md. Supported by Canonical until April 2027.
      imageReference: {
        publisher: 'Canonical'
        offer: '0001-com-ubuntu-server-jammy'
        sku: '22_04-lts-gen2'
        version: 'latest'
      }
      osDisk: {
        name: osDiskName
        createOption: 'FromImage'
        caching: 'ReadWrite'
        diskSizeGB: osDiskSizeGb
        managedDisk: {
          storageAccountType: 'StandardSSD_LRS'
        }
        deleteOption: 'Delete'
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
          properties: {
            deleteOption: 'Delete'
          }
        }
      ]
    }
    diagnosticsProfile: {
      bootDiagnostics: {
        enabled: true
      }
    }
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────

@description('Public IP address assigned to the VM. Use this for AZURE_VM_HOST GitHub secret, Atlas allowlist, and Google OAuth redirect URI.')
output vmPublicIp string = publicIp.properties.ipAddress

@description('Suggested SSH command using the deploy keypair.')
output sshConnectionCommand string = 'ssh -i ~/.ssh/ninetynine_deploy ${adminUsername}@${publicIp.properties.ipAddress}'

@description('Resource group name (echoed for downstream scripts).')
output resourceGroupName string = resourceGroup().name

@description('VM name (echoed for downstream scripts).')
output vmName string = vmName

@description('Admin username (echoed for AZURE_VM_USER GitHub secret).')
output adminUsername string = adminUsername

#!/usr/bin/env bash
# provision-azure.sh — One-shot Azure infrastructure stand-up for NinetyNine.
#
# Replaces the manual Part 1 + Part 2 walkthrough in docs/deployment.md with a
# Bicep deployment. After this script succeeds you have a fully bootstrapped
# B2s VM in your subscription, ready for the deploy.yml GitHub workflow to SSH
# in and start `docker compose pull && up -d`.
#
# What it does:
#   1. Validates az CLI installed, logged in, and pointed at the right subscription
#   2. Validates the SSH public key exists at ~/.ssh/ninetynine_deploy.pub
#   3. Validates the Bicep template and cloud-init bootstrap files exist
#   4. Auto-detects your operator IP for the SSH NSG rule (override with --operator-ip)
#   5. Prints the planned parameters and prompts for confirmation
#   6. Creates the resource group (idempotent)
#   7. Runs az deployment group create with the Bicep template
#   8. Prints the VM IP, SSH command, and the next-step instructions
#
# Usage:
#   ./scripts/provision-azure.sh [options]
#
# Options:
#   --location <region>       Azure region (default: eastus)
#   --resource-group <name>   Resource group name (default: rg-ninetynine-prod-<location>)
#   --subscription <id>       Subscription ID override (default: current az context)
#   --operator-ip <ip>        Your public IP for the SSH NSG rule (default: auto-detect)
#   --ssh-key <path>          SSH public key path (default: ~/.ssh/ninetynine_deploy.pub)
#   --vm-size <size>          VM size override (default: Standard_B2s)
#   --no-confirm              Skip the confirmation prompt (DANGEROUS — for CI)
#   --help                    Show this message

set -euo pipefail

# ── Bootstrap: always run from the repository root ───────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# ── Helpers ───────────────────────────────────────────────────────────────────
_info()  { printf '\033[0;34m[provision]\033[0m %s\n' "$*"; }
_ok()    { printf '\033[0;32m[provision]\033[0m %s\n' "$*"; }
_warn()  { printf '\033[0;33m[provision]\033[0m WARNING: %s\n' "$*" >&2; }
_error() { printf '\033[0;31m[provision]\033[0m ERROR: %s\n' "$*" >&2; }

# ── Defaults ──────────────────────────────────────────────────────────────────
LOCATION="eastus"
RESOURCE_GROUP=""
SUBSCRIPTION_ID=""
OPERATOR_IP=""
SSH_KEY_PATH="$HOME/.ssh/ninetynine_deploy.pub"
VM_SIZE="Standard_B2s"
NAME_PREFIX="ninetynine-prod"
CONFIRM=true

BICEP_FILE="$REPO_ROOT/infra/main.bicep"
CLOUD_INIT_FILE="$REPO_ROOT/infra/cloud-init.yaml"

# ── Parse args ────────────────────────────────────────────────────────────────
while [ $# -gt 0 ]; do
    case "$1" in
        --location)        LOCATION="$2"; shift 2 ;;
        --resource-group)  RESOURCE_GROUP="$2"; shift 2 ;;
        --subscription)    SUBSCRIPTION_ID="$2"; shift 2 ;;
        --operator-ip)     OPERATOR_IP="$2"; shift 2 ;;
        --ssh-key)         SSH_KEY_PATH="$2"; shift 2 ;;
        --vm-size)         VM_SIZE="$2"; shift 2 ;;
        --no-confirm)      CONFIRM=false; shift ;;
        --help|-h)
            sed -n '2,/^set -euo/p' "$0" | sed 's/^# \?//' | head -n -1
            exit 0
            ;;
        *)
            _error "Unknown option: $1"
            _error "Run with --help for usage."
            exit 2
            ;;
    esac
done

# Default RG name uses the location to make region implicit in the resource name.
if [ -z "$RESOURCE_GROUP" ]; then
    RESOURCE_GROUP="rg-${NAME_PREFIX}-${LOCATION}"
fi

# ── Preflight: az CLI ─────────────────────────────────────────────────────────
if ! command -v az &>/dev/null; then
    _error "'az' CLI not found. Install: https://learn.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi

if ! az account show &>/dev/null; then
    _error "Not logged in. Run: az login"
    exit 1
fi

# ── Preflight: subscription selection ────────────────────────────────────────
CURRENT_SUB_ID="$(az account show --query id -o tsv)"
CURRENT_SUB_NAME="$(az account show --query name -o tsv)"

if [ -n "$SUBSCRIPTION_ID" ] && [ "$SUBSCRIPTION_ID" != "$CURRENT_SUB_ID" ]; then
    _info "Switching az context to subscription: $SUBSCRIPTION_ID"
    az account set --subscription "$SUBSCRIPTION_ID"
    CURRENT_SUB_ID="$SUBSCRIPTION_ID"
    CURRENT_SUB_NAME="$(az account show --query name -o tsv)"
fi

# ── Preflight: SSH public key ────────────────────────────────────────────────
if [ ! -f "$SSH_KEY_PATH" ]; then
    _error "SSH public key not found at: $SSH_KEY_PATH"
    _error "Generate one: ssh-keygen -t ed25519 -C 'ninetynine-deploy' -f ${SSH_KEY_PATH%.pub}"
    exit 1
fi
SSH_PUBLIC_KEY="$(cat "$SSH_KEY_PATH")"

# ── Preflight: Bicep + cloud-init files ──────────────────────────────────────
[ -f "$BICEP_FILE" ]      || { _error "Missing: $BICEP_FILE"; exit 1; }
[ -f "$CLOUD_INIT_FILE" ] || { _error "Missing: $CLOUD_INIT_FILE"; exit 1; }

# ── Preflight: bicep CLI bundled with az ─────────────────────────────────────
if ! az bicep version &>/dev/null; then
    _info "Bicep CLI not found in az; installing (one-time, user-scope)..."
    az bicep install >/dev/null
fi

# ── Operator IP detection ────────────────────────────────────────────────────
if [ -z "$OPERATOR_IP" ]; then
    _info "Auto-detecting operator IP via ifconfig.me..."
    OPERATOR_IP="$(curl -fsSL --max-time 5 https://ifconfig.me 2>/dev/null || true)"
    if [ -z "$OPERATOR_IP" ]; then
        _error "Could not auto-detect operator IP. Pass it explicitly: --operator-ip <ip>"
        exit 1
    fi
fi

# Validate IPv4 format minimally.
if ! [[ "$OPERATOR_IP" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]]; then
    _error "Operator IP doesn't look like IPv4: $OPERATOR_IP"
    exit 1
fi
OPERATOR_IP_CIDR="${OPERATOR_IP}/32"

# ── Encode cloud-init ────────────────────────────────────────────────────────
# Azure VM customData expects base64. base64 with no line wrap (-w0 on GNU,
# absent on BSD/macOS — fall back to tr).
if base64 --help 2>&1 | grep -q -- '-w'; then
    CLOUD_INIT_BASE64="$(base64 -w0 < "$CLOUD_INIT_FILE")"
else
    CLOUD_INIT_BASE64="$(base64 < "$CLOUD_INIT_FILE" | tr -d '\n')"
fi

# ── Confirmation ──────────────────────────────────────────────────────────────
cat <<EOF

──────────────────────────────────────────────────────────────────────────────
NinetyNine Azure provisioning — about to deploy:

  Subscription:    $CURRENT_SUB_NAME ($CURRENT_SUB_ID)
  Resource group:  $RESOURCE_GROUP   (will be created if missing)
  Location:        $LOCATION
  VM size:         $VM_SIZE
  Name prefix:     $NAME_PREFIX
  SSH key:         $SSH_KEY_PATH
  Operator IP:     $OPERATOR_IP_CIDR  (SSH NSG rule will allow only this address)

This will create billable Azure resources:
  - Standard_B2s VM (~\$30/mo PAYG)
  - Standard SKU static public IP (~\$4/mo)
  - Standard SSD OS disk (free first 12 mo on free account)
  - Virtual network + NSG (no charge)

──────────────────────────────────────────────────────────────────────────────
EOF

if [ "$CONFIRM" = true ]; then
    read -r -p "Proceed with deployment? [y/N] " response
    case "$response" in
        [yY]|[yY][eE][sS]) ;;
        *) _info "Aborted by user."; exit 0 ;;
    esac
fi

# ── Resource group (idempotent) ──────────────────────────────────────────────
_info "Ensuring resource group exists: $RESOURCE_GROUP"
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --tags application=NinetyNine environment=production managedBy=bicep \
    --output none

# ── Bicep deployment ─────────────────────────────────────────────────────────
DEPLOYMENT_NAME="ninetynine-$(date +%Y%m%d-%H%M%S)"
_info "Deploying Bicep template (deployment name: $DEPLOYMENT_NAME)..."
_info "This typically takes 2-4 minutes."

az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --template-file "$BICEP_FILE" \
    --parameters \
        location="$LOCATION" \
        namePrefix="$NAME_PREFIX" \
        vmSize="$VM_SIZE" \
        sshPublicKey="$SSH_PUBLIC_KEY" \
        operatorIpCidr="$OPERATOR_IP_CIDR" \
        cloudInitBase64="$CLOUD_INIT_BASE64" \
    --output none

_ok "Deployment complete."

# ── Outputs ──────────────────────────────────────────────────────────────────
VM_PUBLIC_IP="$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.vmPublicIp.value' -o tsv)"
SSH_CMD="$(az deployment group show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$DEPLOYMENT_NAME" \
    --query 'properties.outputs.sshConnectionCommand.value' -o tsv)"

cat <<EOF

──────────────────────────────────────────────────────────────────────────────
VM provisioned.

  Public IP:       $VM_PUBLIC_IP
  SSH command:     $SSH_CMD
  Resource group:  $RESOURCE_GROUP

The cloud-init bootstrap is running on the VM right now (Docker install,
fail2ban, etc.). It typically completes within 3-5 minutes after VM creation.
You can SSH in immediately, but Docker may not be ready until cloud-init
finishes — check progress with:

  $SSH_CMD
  sudo cloud-init status --wait     # blocks until cloud-init done
  sudo tail -f /var/log/cloud-init-output.log

──────────────────────────────────────────────────────────────────────────────
Next steps:

  1. Update MongoDB Atlas Network Access — add this VM IP to the allowlist:
       $VM_PUBLIC_IP

  2. Update Google Cloud OAuth credentials — add authorized redirect URI:
       http://$VM_PUBLIC_IP/signin-google

  3. Populate GitHub Actions secrets:
       ./scripts/bootstrap-secrets.sh --vm-ip $VM_PUBLIC_IP

  4. Push to master to trigger deploy.yml:
       git push origin master

──────────────────────────────────────────────────────────────────────────────
EOF

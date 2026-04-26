#!/usr/bin/env bash
# teardown-azure.sh — Delete the NinetyNine Azure resource group.
#
# Destroys ALL resources in the resource group (VM, public IP, NSG, vnet, OS
# disk). Use this for cost control or to rebuild the stack from scratch.
#
# What this DOES delete:
#   - The resource group and every resource in it
#   - The public IP (so the VM IP changes on next provision)
#   - The OS disk and any data on it (cloud-init bootstrap, /opt/ninetynine)
#
# What this does NOT delete (these are external to Azure):
#   - MongoDB Atlas cluster + data (manage from cloud.mongodb.com)
#   - Google Cloud OAuth credentials (manage from console.cloud.google.com)
#   - GHCR images (manage from ghcr.io / github.com Packages tab)
#   - GitHub Actions secrets (run scripts/bootstrap-secrets.sh again to update,
#     or delete from Settings → Secrets and variables → Actions)
#
# Safety:
#   - Lists every resource in the RG first
#   - Requires you to type the literal RG name to confirm
#   - Uses az group delete --no-wait so the script returns immediately;
#     deletion continues in the background and takes 5-10 minutes
#
# Usage:
#   ./scripts/teardown-azure.sh [--resource-group <name>]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

_info()  { printf '\033[0;34m[teardown]\033[0m %s\n' "$*"; }
_ok()    { printf '\033[0;32m[teardown]\033[0m %s\n' "$*"; }
_warn()  { printf '\033[0;33m[teardown]\033[0m WARNING: %s\n' "$*" >&2; }
_error() { printf '\033[0;31m[teardown]\033[0m ERROR: %s\n' "$*" >&2; }

RESOURCE_GROUP="rg-ninetynine-prod-eastus"

while [ $# -gt 0 ]; do
    case "$1" in
        --resource-group)  RESOURCE_GROUP="$2"; shift 2 ;;
        --help|-h)
            sed -n '2,/^set -euo/p' "$0" | sed 's/^# \?//' | head -n -1
            exit 0
            ;;
        *)
            _error "Unknown option: $1"
            exit 2
            ;;
    esac
done

# ── Preflight ────────────────────────────────────────────────────────────────
if ! command -v az &>/dev/null; then
    _error "'az' CLI not found."
    exit 1
fi
if ! az account show &>/dev/null; then
    _error "Not logged in. Run: az login"
    exit 1
fi

if ! az group show --name "$RESOURCE_GROUP" &>/dev/null; then
    _warn "Resource group '$RESOURCE_GROUP' does not exist. Nothing to delete."
    exit 0
fi

SUB_NAME="$(az account show --query name -o tsv)"
SUB_ID="$(az account show --query id -o tsv)"

# ── Inventory ────────────────────────────────────────────────────────────────
cat <<EOF

──────────────────────────────────────────────────────────────────────────────
About to DELETE everything in this resource group:

  Subscription:    $SUB_NAME ($SUB_ID)
  Resource group:  $RESOURCE_GROUP

Current contents:
EOF

az resource list \
    --resource-group "$RESOURCE_GROUP" \
    --output table

cat <<EOF

This is irreversible. The OS disk and all data on the VM will be permanently
lost. External services (Atlas, Google OAuth, GHCR) are not affected.

──────────────────────────────────────────────────────────────────────────────
EOF

# ── Typed confirmation ───────────────────────────────────────────────────────
read -r -p "Type the resource group name to confirm deletion: " response
if [ "$response" != "$RESOURCE_GROUP" ]; then
    _info "Confirmation did not match. Aborted — nothing deleted."
    exit 0
fi

# ── Delete ───────────────────────────────────────────────────────────────────
_info "Initiating async deletion of $RESOURCE_GROUP..."
az group delete \
    --name "$RESOURCE_GROUP" \
    --yes \
    --no-wait

cat <<EOF

──────────────────────────────────────────────────────────────────────────────
Deletion submitted. This runs in the background and typically takes 5-10
minutes. Check status with:

  az group show --name $RESOURCE_GROUP --query properties.provisioningState -o tsv
  # Returns "Deleting" until done, then "ResourceNotFound" error.

Or watch in the Azure portal under Resource Groups.

To re-provision after deletion completes:
  ./scripts/provision-azure.sh
──────────────────────────────────────────────────────────────────────────────
EOF

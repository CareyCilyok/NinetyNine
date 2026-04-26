#!/usr/bin/env bash
# bootstrap-secrets.sh — Populate GitHub Actions secrets for the deploy workflow.
#
# Sets all six secrets that .github/workflows/deploy.yml reads:
#   AZURE_VM_HOST            VM public IP from Bicep deployment
#   AZURE_VM_USER            azureuser (default; matches Bicep adminUsername)
#   AZURE_VM_SSH_KEY         Private key contents from ~/.ssh/ninetynine_deploy
#   MONGO_CONNECTION_STRING  Atlas connection string with /NinetyNine in path
#   GOOGLE_CLIENT_ID         Placeholder — Google OAuth deferred (see v2-roadmap)
#   GOOGLE_CLIENT_SECRET     Placeholder — Google OAuth deferred (see v2-roadmap)
#
# Google OAuth note:
#   The app does not currently consume Auth:Google config (no AddGoogle() in
#   Program.cs, no Google.AspNetCore.Authentication.Google package). Google
#   OAuth integration is tracked as deferred work in
#   docs/plans/v2-roadmap.md → Deferred / unscheduled backlog → Google OAuth.
#   This script writes documented placeholder strings for the two GOOGLE_*
#   secrets so deploy.yml's .env rendering stays valid.
#
# What it does:
#   1. Validates gh CLI installed and authenticated
#   2. Resolves the VM IP (from --vm-ip arg, or auto-detected from latest deployment)
#   3. Reads the SSH private key from disk
#   4. Prompts (no echo) for MONGO_CONNECTION_STRING (the only external secret
#      currently required)
#   5. Pipes all six secrets through `gh secret set` to your repo (Google ones
#      use placeholder strings)
#   6. Verifies all six secrets are set
#
# Usage:
#   ./scripts/bootstrap-secrets.sh [options]
#
# Options:
#   --vm-ip <ip>              VM public IP (default: auto-detect from latest deployment)
#   --resource-group <name>   RG to query for VM IP (default: rg-ninetynine-prod-eastus)
#   --vm-user <name>          SSH login user (default: azureuser)
#   --ssh-private-key <path>  SSH private key file (default: ~/.ssh/ninetynine_deploy)
#   --repo <owner/name>       GitHub repo (default: gh repo view current dir)
#   --help                    Show this message

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

_info()  { printf '\033[0;34m[secrets]\033[0m %s\n' "$*"; }
_ok()    { printf '\033[0;32m[secrets]\033[0m %s\n' "$*"; }
_warn()  { printf '\033[0;33m[secrets]\033[0m WARNING: %s\n' "$*" >&2; }
_error() { printf '\033[0;31m[secrets]\033[0m ERROR: %s\n' "$*" >&2; }

VM_IP=""
RESOURCE_GROUP="rg-ninetynine-prod-eastus"
VM_USER="azureuser"
SSH_PRIVATE_KEY_PATH="$HOME/.ssh/ninetynine_deploy"
REPO=""

while [ $# -gt 0 ]; do
    case "$1" in
        --vm-ip)            VM_IP="$2"; shift 2 ;;
        --resource-group)   RESOURCE_GROUP="$2"; shift 2 ;;
        --vm-user)          VM_USER="$2"; shift 2 ;;
        --ssh-private-key)  SSH_PRIVATE_KEY_PATH="$2"; shift 2 ;;
        --repo)             REPO="$2"; shift 2 ;;
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

# ── Preflight: gh CLI ────────────────────────────────────────────────────────
if ! command -v gh &>/dev/null; then
    _error "'gh' CLI not found. Install: https://cli.github.com/"
    exit 1
fi

if ! gh auth status &>/dev/null; then
    _error "gh CLI not authenticated. Run: gh auth login"
    exit 1
fi

# ── Resolve repo ─────────────────────────────────────────────────────────────
if [ -z "$REPO" ]; then
    REPO="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || true)"
    if [ -z "$REPO" ]; then
        _error "Could not determine GitHub repo. Pass --repo owner/name."
        exit 1
    fi
fi

# ── Resolve VM IP if not provided ────────────────────────────────────────────
if [ -z "$VM_IP" ]; then
    if ! command -v az &>/dev/null || ! az account show &>/dev/null; then
        _error "VM IP not provided and az CLI unavailable for auto-detect."
        _error "Pass it explicitly: --vm-ip <ip>"
        exit 1
    fi
    _info "Auto-detecting VM IP from latest deployment in $RESOURCE_GROUP..."
    LATEST_DEPLOYMENT="$(az deployment group list \
        --resource-group "$RESOURCE_GROUP" \
        --query "sort_by([?starts_with(name, 'ninetynine-')], &properties.timestamp)[-1].name" \
        -o tsv 2>/dev/null || true)"
    if [ -z "$LATEST_DEPLOYMENT" ]; then
        _error "No deployment found in $RESOURCE_GROUP. Run provision-azure.sh first, or pass --vm-ip."
        exit 1
    fi
    VM_IP="$(az deployment group show \
        --resource-group "$RESOURCE_GROUP" \
        --name "$LATEST_DEPLOYMENT" \
        --query 'properties.outputs.vmPublicIp.value' -o tsv 2>/dev/null || true)"
    if [ -z "$VM_IP" ]; then
        _error "Could not extract VM IP from deployment $LATEST_DEPLOYMENT."
        exit 1
    fi
    _info "Resolved VM IP: $VM_IP (from deployment $LATEST_DEPLOYMENT)"
fi

# ── SSH private key ──────────────────────────────────────────────────────────
if [ ! -f "$SSH_PRIVATE_KEY_PATH" ]; then
    _error "SSH private key not found at: $SSH_PRIVATE_KEY_PATH"
    exit 1
fi

KEY_PERMS="$(stat -c '%a' "$SSH_PRIVATE_KEY_PATH" 2>/dev/null || stat -f '%Lp' "$SSH_PRIVATE_KEY_PATH")"
if [ "$KEY_PERMS" != "600" ] && [ "$KEY_PERMS" != "400" ]; then
    _warn "SSH private key has permissions $KEY_PERMS (expected 600 or 400)."
fi

# ── Prompt for external secrets ──────────────────────────────────────────────
# Google OAuth is deferred for the initial deployment — see
# docs/plans/v2-roadmap.md "Deferred / unscheduled backlog" for the tracked
# work. The app does not currently consume Auth:Google config (no AddGoogle()
# in Program.cs, no Google.AspNetCore.Authentication.Google package). The
# deploy.yml workflow still renders these two env vars into the .env, so we
# write documented placeholder strings here to keep the rendering valid; the
# app reads neither.
GOOGLE_OAUTH_PLACEHOLDER="deferred-see-v2-roadmap-backlog"

cat <<EOF

──────────────────────────────────────────────────────────────────────────────
About to set six secrets on $REPO:

  AZURE_VM_HOST           = $VM_IP
  AZURE_VM_USER           = $VM_USER
  AZURE_VM_SSH_KEY        = (private key contents from $SSH_PRIVATE_KEY_PATH)
  MONGO_CONNECTION_STRING = (you'll be prompted)
  GOOGLE_CLIENT_ID        = $GOOGLE_OAUTH_PLACEHOLDER  (deferred — see v2-roadmap)
  GOOGLE_CLIENT_SECRET    = $GOOGLE_OAUTH_PLACEHOLDER  (deferred — see v2-roadmap)

Google OAuth integration is tracked in
  docs/plans/v2-roadmap.md → Deferred / unscheduled backlog → Google OAuth
The app does not currently consume Auth:Google config — these placeholders
exist only to satisfy deploy.yml's .env rendering.

Existing secret values will be overwritten silently. The new values are sent
to GitHub via gh secret set and never logged.
──────────────────────────────────────────────────────────────────────────────

EOF

read -r -p "Continue? [y/N] " response
case "$response" in
    [yY]|[yY][eE][sS]) ;;
    *) _info "Aborted."; exit 0 ;;
esac

# Bash read -s suppresses echo. We re-prompt on empty input.
_prompt_secret() {
    local name="$1" hint="$2" value=""
    while [ -z "$value" ]; do
        printf '%s\n%s: ' "$hint" "$name" >&2
        read -r -s value
        printf '\n' >&2
        if [ -z "$value" ]; then
            _warn "Empty value rejected. Try again."
        fi
    done
    printf '%s' "$value"
}

MONGO_CS="$(_prompt_secret 'MONGO_CONNECTION_STRING' \
    'Atlas connection string. Format: mongodb+srv://user:pass@host/NinetyNine?retryWrites=true&w=majority')"

# ── Push secrets ─────────────────────────────────────────────────────────────
# NOTE: Do NOT use `gh secret set ... --body -`. Counterintuitively, gh CLI
# (verified on v2.91.0) treats `--body -` as the literal value "-" rather than
# "read from stdin", silently corrupting the secret. The correct patterns are:
#   - `printf '%s' "$x" | gh secret set NAME --repo R`  (no --body, stdin reads)
#   - `gh secret set NAME --repo R --body "$x"`         (explicit value)
# The first form keeps the secret out of the process arg list and shell history.

_info "Setting AZURE_VM_HOST..."
printf '%s' "$VM_IP" | gh secret set AZURE_VM_HOST --repo "$REPO"

_info "Setting AZURE_VM_USER..."
printf '%s' "$VM_USER" | gh secret set AZURE_VM_USER --repo "$REPO"

_info "Setting AZURE_VM_SSH_KEY..."
gh secret set AZURE_VM_SSH_KEY --repo "$REPO" < "$SSH_PRIVATE_KEY_PATH"

_info "Setting MONGO_CONNECTION_STRING..."
printf '%s' "$MONGO_CS" | gh secret set MONGO_CONNECTION_STRING --repo "$REPO"

_info "Setting GOOGLE_CLIENT_ID (deferred placeholder — Google OAuth not yet wired in app)..."
printf '%s' "$GOOGLE_OAUTH_PLACEHOLDER" | gh secret set GOOGLE_CLIENT_ID --repo "$REPO"

_info "Setting GOOGLE_CLIENT_SECRET (deferred placeholder — Google OAuth not yet wired in app)..."
printf '%s' "$GOOGLE_OAUTH_PLACEHOLDER" | gh secret set GOOGLE_CLIENT_SECRET --repo "$REPO"

# Clear from environment ASAP. Variable is still in script memory until exit.
unset MONGO_CS

# ── Verify ───────────────────────────────────────────────────────────────────
_info "Verifying secrets are set on $REPO..."
EXPECTED=(AZURE_VM_HOST AZURE_VM_USER AZURE_VM_SSH_KEY MONGO_CONNECTION_STRING GOOGLE_CLIENT_ID GOOGLE_CLIENT_SECRET)
ACTUAL="$(gh secret list --repo "$REPO" --json name -q '.[].name')"

MISSING=()
for s in "${EXPECTED[@]}"; do
    if ! echo "$ACTUAL" | grep -qx "$s"; then
        MISSING+=("$s")
    fi
done

if [ "${#MISSING[@]}" -gt 0 ]; then
    _error "These secrets failed to set: ${MISSING[*]}"
    exit 1
fi

_ok "All six secrets set successfully."

cat <<EOF

──────────────────────────────────────────────────────────────────────────────
Next step — trigger the first deployment:

  git push origin master                        # if there are unpushed commits
  # or
  gh workflow run deploy.yml --ref master       # manual dispatch

Watch the run:
  gh run watch

Then verify health:
  curl -fsS http://$VM_IP/healthz

Optional hardening:
  - Configure the 'production' GitHub Environment for required reviewers:
    Settings → Environments → production → Required reviewers
    (deploy.yml references this environment but works without it configured)
──────────────────────────────────────────────────────────────────────────────
EOF

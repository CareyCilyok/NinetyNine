#!/usr/bin/env bash
# deploy.sh — Local development stand-up / teardown utility for NinetyNine.
#
# Usage:
#   ./deploy.sh [command] [args...]
#
# Commands:
#   up              Build and start all services (default)
#   down            Stop all services (volumes preserved)
#   rebuild         Force-rebuild the web image, then start
#   logs [service]  Tail logs (default: web)
#   seed            Run data seed inside the web container
#   clean           Stop and remove all volumes (prompts for confirmation)
#   ps              Show running service status
#   shell [service] Open a shell in a service container (default: web)
#   help            Print this message
#
# Environment:
#   .env is auto-created from .env.example on first run if it does not exist.

set -euo pipefail

# ── Bootstrap: always run from the repository root ───────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ── Helpers ───────────────────────────────────────────────────────────────────
_info()  { printf '\033[0;34m[deploy]\033[0m %s\n' "$*"; }
_ok()    { printf '\033[0;32m[deploy]\033[0m %s\n' "$*"; }
_warn()  { printf '\033[0;33m[deploy]\033[0m WARNING: %s\n' "$*" >&2; }
_error() { printf '\033[0;31m[deploy]\033[0m ERROR: %s\n' "$*" >&2; }

# ── Dependency checks ─────────────────────────────────────────────────────────
_check_docker() {
    if ! command -v docker &>/dev/null; then
        _error "'docker' CLI not found."
        _error "Install Docker Engine: https://docs.docker.com/engine/install/"
        exit 1
    fi
}

_check_compose() {
    # Require Compose v2 (`docker compose`, not the legacy `docker-compose`).
    if ! docker compose version &>/dev/null; then
        _error "Docker Compose v2 plugin not found."
        _error "Upgrade Docker Desktop or install the compose plugin:"
        _error "  https://docs.docker.com/compose/install/"
        exit 1
    fi
}

_check_docker
_check_compose

# ── Compose command shorthand ─────────────────────────────────────────────────
COMPOSE="docker compose -f docker-compose.dev.yml"

# ── Ensure .env exists ────────────────────────────────────────────────────────
_ensure_env() {
    if [ ! -f .env ]; then
        if [ -f .env.example ]; then
            _warn ".env not found — copying .env.example to .env"
            _warn "Edit .env and set GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET for OAuth."
            cp .env.example .env
        else
            _error "Neither .env nor .env.example found. Cannot continue."
            exit 1
        fi
    fi
}

# ── Subcommands ───────────────────────────────────────────────────────────────

cmd_up() {
    _ensure_env
    _info "Starting all services (building web image if needed)..."
    $COMPOSE up -d --build
    _ok "Services started."
    echo ""
    _ok "Web app:       http://localhost:8080"
    _ok "Mongo Express: http://localhost:8081"
    echo ""
    _info "Run './deploy.sh logs' to tail application logs."
}

cmd_down() {
    _info "Stopping all services (volumes preserved)..."
    $COMPOSE down
    _ok "Services stopped."
}

cmd_rebuild() {
    _ensure_env
    _info "Rebuilding web image (no cache)..."
    $COMPOSE build --no-cache web
    _info "Starting services with freshly built image..."
    $COMPOSE up -d
    _ok "Rebuild complete. Web app: http://localhost:8080"
}

cmd_logs() {
    local service="${1:-web}"
    _info "Tailing logs for service: $service (Ctrl-C to stop)..."
    $COMPOSE logs -f "$service"
}

cmd_seed() {
    _info "Running data seed in the web container..."
    # Plumbing is wired; the actual --seed flag is a future CLI implementation
    # in NinetyNine.Web. When implemented, it will populate the local MongoDB with
    # sample venues, players, and games.
    if ! $COMPOSE ps --quiet web | grep -q .; then
        _error "The 'web' container is not running. Start it first with: ./deploy.sh up"
        exit 1
    fi
    $COMPOSE exec web dotnet NinetyNine.Web.dll --seed
    _ok "Seed complete."
}

cmd_clean() {
    printf '\033[0;33m[deploy]\033[0m Remove all volumes? This deletes all local MongoDB data. [y/N] '
    read -r answer
    case "$answer" in
        [Yy]|[Yy][Ee][Ss])
            _info "Stopping services and removing volumes..."
            $COMPOSE down -v
            _ok "Volumes removed. Next './deploy.sh up' starts with a clean database."
            ;;
        *)
            _info "Aborted — volumes preserved."
            ;;
    esac
}

cmd_ps() {
    _info "Service status:"
    $COMPOSE ps
}

cmd_shell() {
    local service="${1:-web}"
    _info "Opening shell in service: $service"
    $COMPOSE exec "$service" /bin/sh
}

cmd_help() {
    cat <<'EOF'

  NinetyNine local development utility

  Usage:
    ./deploy.sh [command] [args...]

  Commands:
    up              Build and start all services (default if no command given)
    down            Stop all services; volumes are preserved
    rebuild         Force-rebuild the web image from scratch, then start
    logs [service]  Follow logs; defaults to 'web' if no service given
    seed            Run the data seed inside the running web container
    clean           Stop services and remove volumes (prompts for confirmation)
    ps              Show current service status
    shell [service] Open /bin/sh in a container; defaults to 'web'
    help            Show this help

  Service names:
    web             Blazor web application  (http://localhost:8080)
    mongo           MongoDB 7               (localhost:27017)
    mongo-express   MongoDB admin UI        (http://localhost:8081)

  First run:
    .env is created automatically from .env.example.
    Edit .env to add GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET for OAuth.

EOF
}

# ── Dispatch ──────────────────────────────────────────────────────────────────
CMD="${1:-up}"
shift || true   # shift away the command; remaining $@ are sub-arguments

case "$CMD" in
    up)      cmd_up      ;;
    down)    cmd_down    ;;
    rebuild) cmd_rebuild ;;
    logs)    cmd_logs    "$@" ;;
    seed)    cmd_seed    ;;
    clean)   cmd_clean   ;;
    ps)      cmd_ps      ;;
    shell)   cmd_shell   "$@" ;;
    help|-h|--help) cmd_help ;;
    *)
        _error "Unknown command: $CMD"
        cmd_help
        exit 1
        ;;
esac

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

# ── Live service health poller ───────────────────────────────────────────────
# Docker Compose's own progress UI uses in-place cursor rewrites which some
# terminals swallow, making the 15-45s wait for healthchecks look like a hang.
# This function polls `docker inspect` directly and prints a live status line
# per service so the developer sees continuous progress.
#
# Usage: _wait_for_services <service1> [service2 ...]
#   - Polls each named compose service in turn
#   - Prints a single overwriting line per service while it transitions
#   - Succeeds when every service reports 'healthy' (or 'running' if the
#     service has no healthcheck)
#   - Times out after 120 s total and dumps the failing service's tail logs
_wait_for_services() {
    local services=("$@")
    local deadline=$(( $(date +%s) + 120 ))
    local spin_idx=0
    local spinner='|/-\'

    for service in "${services[@]}"; do
        local start=$(date +%s)
        local status='(unknown)'
        local has_healthcheck='unknown'

        while true; do
            local container
            container=$($COMPOSE ps -q "$service" 2>/dev/null | head -1)

            if [ -z "$container" ]; then
                status='(no container yet)'
            else
                # Figure out if this container has a healthcheck defined.
                if [ "$has_healthcheck" = 'unknown' ]; then
                    if docker inspect --format '{{.State.Health.Status}}' "$container" \
                            &>/dev/null; then
                        has_healthcheck='yes'
                    else
                        has_healthcheck='no'
                    fi
                fi

                if [ "$has_healthcheck" = 'yes' ]; then
                    status=$(docker inspect --format '{{.State.Health.Status}}' \
                        "$container" 2>/dev/null || echo 'starting')
                else
                    status=$(docker inspect --format '{{.State.Status}}' \
                        "$container" 2>/dev/null || echo 'starting')
                fi
            fi

            local elapsed=$(( $(date +%s) - start ))
            local spin_char=${spinner:$spin_idx:1}
            spin_idx=$(( (spin_idx + 1) % 4 ))

            # Overwriting line: carriage return + clear to end of line + write
            printf '\r\033[K  %s %-16s %-10s %ds' \
                "$spin_char" "$service" "$status" "$elapsed"

            case "$status" in
                healthy|running)
                    # Print a permanent OK line and move to the next service.
                    printf '\r\033[K  \033[0;32m✓\033[0m %-16s %-10s %ds\n' \
                        "$service" "$status" "$elapsed"
                    break
                    ;;
                unhealthy|exited|dead)
                    printf '\r\033[K  \033[0;31m✗\033[0m %-16s %-10s\n' \
                        "$service" "$status"
                    _error "Service '$service' failed to become healthy."
                    _error "Last 30 lines of '$service' logs:"
                    $COMPOSE logs --tail 30 "$service" >&2 || true
                    return 1
                    ;;
            esac

            if [ "$(date +%s)" -ge "$deadline" ]; then
                printf '\r\033[K  \033[0;31m✗\033[0m %-16s timed out after 120s\n' "$service"
                _error "Timed out waiting for '$service' to become healthy."
                _error "Current status: $status"
                _error "Last 30 lines of '$service' logs:"
                $COMPOSE logs --tail 30 "$service" >&2 || true
                return 1
            fi

            sleep 1
        done
    done

    return 0
}

# ── Sanity check: hit the web healthz endpoint ───────────────────────────────
_check_web_reachable() {
    local url='http://localhost:8080/healthz'
    if command -v curl &>/dev/null; then
        if curl --max-time 5 -fsS "$url" >/dev/null 2>&1; then
            _ok "Web app is responding at http://localhost:8080"
            return 0
        fi
        _warn "Web app is not yet responding at $url."
        _warn "Container is healthy but the HTTP endpoint did not return 200."
        _warn "Try './deploy.sh logs' to see what's happening."
        return 1
    fi
    # No curl — skip the check silently; _wait_for_services already verified
    # container health via docker inspect.
    return 0
}

# ── Subcommands ───────────────────────────────────────────────────────────────

cmd_up() {
    _ensure_env
    _info "Starting all services (building web image if needed)..."
    _info "First start takes ~20-60 s while MongoDB's healthcheck passes."
    echo ""

    # --wait blocks until healthchecks pass (or timeout). --wait-timeout 120
    # matches our in-script poller timeout so both fail at the same moment.
    # 2>&1 merges buildkit's progress into our stream so terminals that
    # swallow in-place rewrites still see the build log.
    if ! $COMPOSE up -d --build --wait --wait-timeout 120; then
        _error "docker compose up failed. See output above."
        exit 1
    fi

    echo ""
    _info "Verifying service health..."
    if ! _wait_for_services mongo web mongo-express; then
        exit 1
    fi

    echo ""
    _check_web_reachable || true
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
    _info "Waiting for healthchecks (~20-60 s)..."
    echo ""

    if ! $COMPOSE up -d --wait --wait-timeout 120; then
        _error "docker compose up failed. See output above."
        exit 1
    fi

    echo ""
    _info "Verifying service health..."
    if ! _wait_for_services mongo web mongo-express; then
        exit 1
    fi

    echo ""
    _check_web_reachable || true
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

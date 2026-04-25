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
# Flags (apply to: up, rebuild):
#   --production    Seed production data only (real venues; no mock players /
#                   games / matches / communities). Default is Development —
#                   the full mock dataset is loaded. Equivalent to setting
#                   SEED_MODE=Production in the environment before invocation.
#
# Environment:
#   .env is auto-created from .env.example on first run if it does not exist.
#   SEED_MODE       Override the seed mode without using --production.
#                   Values: Development (default), Production, or any other
#                   value to disable seeding.

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

# ── Seed-mode flag parser ─────────────────────────────────────────────────────
# Walks the remaining $@ args and sets SEED_MODE accordingly. Default
# Development; --production sets Production. Exports the variable so the
# downstream `docker compose` invocation picks it up via the
# `Seed__Mode: ${SEED_MODE:-Development}` substitution in docker-compose.dev.yml.
# Unknown args are left in $@ for the caller (currently no other flags exist
# on `up` / `rebuild`, but this keeps the parser future-proof).
_parse_seed_flags() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --production)
                export SEED_MODE=Production
                shift
                ;;
            --development)
                export SEED_MODE=Development
                shift
                ;;
            *)
                # Unknown — stop parsing; let the caller handle it (or
                # ignore if they don't take args).
                break
                ;;
        esac
    done
}

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

# ── Live service readiness poller ────────────────────────────────────────────
# We DO NOT trust Docker's internal healthcheck scheduler — observed stalling
# for 8+ minutes on cold start on some hosts (the scheduler accepts the
# healthcheck definition but doesn't run the probe). Instead we start the
# containers, then poll the services directly for readiness: TCP connect for
# mongo, HTTP GET for the web endpoints. This is what the user actually cares
# about ("can I talk to the service?") and it's immune to Docker's bugs.
#
# Each call to _probe_service takes a service name and runs a service-specific
# reachability check. The poller prints a live overwriting status line per
# service with a spinner and elapsed time, then a permanent ✓ on success or
# ✗ on failure.
#
# Design notes:
#   - We poll from INSIDE the target container for HTTP checks because Docker
#     Desktop port forwarding is occasionally flaky (same run, host→container
#     can return "Connection reset" while in-container responds fine).
#   - mongo is probed via `docker exec ... mongosh --eval "ping"` — the same
#     command Docker's healthcheck runs, but we call it ourselves so we don't
#     depend on Docker's scheduler firing it.
#   - Timeout is 90 s per service with a floor of 3 s between probes. Worst
#     case for the whole stack: 270 s, but in practice each service is ready
#     in under 10 s.

# Probe a single compose service for readiness. Returns 0 on success, 1 on
# failure. Echoes nothing — the caller handles output.
_probe_service() {
    local service="$1"
    local container
    container=$($COMPOSE ps -q "$service" 2>/dev/null | head -1)
    [ -z "$container" ] && return 1

    # Is the container actually running?
    local state
    state=$(docker inspect --format '{{.State.Status}}' "$container" 2>/dev/null)
    case "$state" in
        running) ;;
        exited|dead|paused|removing) return 2 ;;  # fatal, don't keep polling
        *) return 1 ;;                              # not ready yet, keep polling
    esac

    # Service-specific probe. Runs inside the container via docker exec so we
    # don't depend on Docker Desktop's host port mapping being functional.
    case "$service" in
        mongo)
            docker exec "$container" mongosh --quiet \
                --eval "db.adminCommand('ping').ok" 2>/dev/null \
                | grep -q '^1$'
            ;;
        web)
            # Prefer the framework's /healthz. Use wget (busybox, already in
            # the aspnet:8.0-alpine image); --spider exits 0 on 2xx.
            docker exec "$container" wget --quiet --spider --timeout=3 \
                http://localhost:8080/healthz 2>/dev/null
            ;;
        mongo-express)
            # mongo-express listens on 0.0.0.0:8081 explicitly (NOT on
            # 127.0.0.1), so we must hit 0.0.0.0 from inside the container.
            # Using 'localhost' resolves to 127.0.0.1 and gets "connection
            # refused" even though the service is serving correctly.
            # Basic auth is disabled in dev (ME_CONFIG_BASICAUTH=false) so
            # GET / returns 200.
            docker exec "$container" wget --quiet --spider --timeout=3 \
                http://0.0.0.0:8081/ 2>/dev/null
            ;;
        *)
            # Unknown service — just accept 'running' as ready.
            return 0
            ;;
    esac
}

# Wait for a list of services to become ready. Prints a per-service live
# status line. Returns 0 if every service becomes ready within the per-service
# timeout; returns 1 on the first failure/timeout.
#
# Usage: _wait_for_services <service1> [service2 ...]
_wait_for_services() {
    local services=("$@")
    # 15 minutes per service — Docker Desktop's Create→Running transition
    # has been observed taking 5-10 minutes on some cold starts. We want
    # to outlast that but still error out eventually if something is
    # actually broken. Because the poller prints a live status line every
    # second, the developer sees continuous feedback during the wait.
    local per_service_timeout=900
    local spin_idx=0
    local spinner='|/-\'

    for service in "${services[@]}"; do
        local start
        start=$(date +%s)
        local deadline=$(( start + per_service_timeout ))

        while true; do
            local elapsed=$(( $(date +%s) - start ))
            local spin_char=${spinner:$spin_idx:1}
            spin_idx=$(( (spin_idx + 1) % 4 ))

            # Running status line — overwrites previous.
            printf '\r\033[K  %s %-16s  booting…  %2ds' \
                "$spin_char" "$service" "$elapsed"

            if _probe_service "$service"; then
                printf '\r\033[K  \033[0;32m✓\033[0m %-16s  ready     %2ds\n' \
                    "$service" "$elapsed"
                break
            fi

            local probe_rc=$?
            if [ "$probe_rc" = 2 ]; then
                printf '\r\033[K  \033[0;31m✗\033[0m %-16s  exited\n' "$service"
                _error "Service '$service' exited before becoming ready."
                _error "Last 30 lines of '$service' logs:"
                $COMPOSE logs --tail 30 "$service" >&2 || true
                return 1
            fi

            if [ "$(date +%s)" -ge "$deadline" ]; then
                printf '\r\033[K  \033[0;31m✗\033[0m %-16s  timed out after %ds\n' \
                    "$service" "$per_service_timeout"
                _error "Timed out waiting for '$service' to become ready."
                _error "Last 30 lines of '$service' logs:"
                $COMPOSE logs --tail 30 "$service" >&2 || true
                return 1
            fi

            sleep 1
        done
    done

    return 0
}

# ── Host-side reachability sanity check ──────────────────────────────────────
# Tries the published port from the host; falls back to warning if Docker
# Desktop's port forwarding isn't cooperating (the in-container probe already
# confirmed the service is actually working).
_check_web_reachable() {
    local url='http://localhost:8080/healthz'
    if ! command -v curl &>/dev/null; then
        return 0
    fi
    if curl --max-time 5 -fsS "$url" >/dev/null 2>&1; then
        _ok "Web app is responding at http://localhost:8080"
        return 0
    fi
    _warn "Host curl to $url failed, but the container reported ready."
    _warn "This is usually a transient Docker Desktop port-forwarding glitch."
    _warn "Retry in a few seconds or restart Docker Desktop if it persists."
    return 0  # non-fatal — in-container readiness was already confirmed
}

# ── Subcommands ───────────────────────────────────────────────────────────────

cmd_up() {
    _ensure_env
    _parse_seed_flags "$@"

    # ── Stage 1: build (synchronous, streams buildkit output to the user) ──
    _info "Seed mode: ${SEED_MODE:-Development}$([ "${SEED_MODE:-Development}" = "Production" ] && echo "  (real venues only, no mock data)" || echo "  (full mock dataset)")"
    _info "Building web image (if needed)..."
    if ! $COMPOSE build; then
        _error "docker compose build failed. See output above."
        exit 1
    fi

    # ── Stage 2: run compose up -d in the background ──
    # Docker Desktop has been observed stalling 5-10 minutes in the
    # Create→Running transition on cold starts. During that stall, compose
    # produces no output. We run `compose up -d` in the background and let
    # our foreground poller show live per-service status via direct probes.
    # Our poller is what the user watches; compose's output is captured to
    # a temp file for the audit trail.
    local log_file
    log_file=$(mktemp -t ninetynine-compose-up.XXXXXX)
    local compose_pid
    _info "Starting containers (compose running in background)..."
    echo ""
    $COMPOSE up -d > "$log_file" 2>&1 &
    compose_pid=$!

    # ── Stage 3: foreground poll for service readiness ──
    # _wait_for_services tolerates "no container yet" and "booting" states,
    # so it starts running immediately even before compose has created the
    # containers. It completes when every service is actually reachable.
    if ! _wait_for_services mongo web mongo-express; then
        # Make sure compose is not still running before we exit
        if kill -0 "$compose_pid" 2>/dev/null; then
            _warn "Compose is still running. Last 20 lines of compose output:"
            tail -20 "$log_file" >&2 || true
        fi
        rm -f "$log_file"
        exit 1
    fi

    # ── Stage 4: wait for compose's background process to complete ──
    # It should be done by now (containers are reachable). If it isn't,
    # give it a few seconds to finalize its bookkeeping.
    local wait_count=0
    while kill -0 "$compose_pid" 2>/dev/null; do
        wait_count=$((wait_count + 1))
        [ "$wait_count" -ge 10 ] && break
        sleep 1
    done

    # Reap the background process
    if ! wait "$compose_pid" 2>/dev/null; then
        # compose returned non-zero but services are reachable — treat as a
        # warning, not a fatal. This happens on Docker Desktop quirks where
        # compose's own healthcheck wait fails AFTER the containers are
        # already serving.
        _warn "compose up returned non-zero but all services are reachable."
        _warn "Last 10 lines of compose output (for diagnostics):"
        tail -10 "$log_file" >&2 || true
    fi
    rm -f "$log_file"

    echo ""
    _check_web_reachable
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
    _parse_seed_flags "$@"
    _info "Seed mode: ${SEED_MODE:-Development}$([ "${SEED_MODE:-Development}" = "Production" ] && echo "  (real venues only, no mock data)" || echo "  (full mock dataset)")"
    _info "Rebuilding web image (no cache)..."
    if ! $COMPOSE build --no-cache web; then
        _error "docker compose build failed."
        exit 1
    fi
    _info "Starting services with freshly built image..."
    echo ""

    local log_file
    log_file=$(mktemp -t ninetynine-compose-up.XXXXXX)
    $COMPOSE up -d > "$log_file" 2>&1 &
    local compose_pid=$!

    if ! _wait_for_services mongo web mongo-express; then
        if kill -0 "$compose_pid" 2>/dev/null; then
            tail -20 "$log_file" >&2 || true
        fi
        rm -f "$log_file"
        exit 1
    fi

    local wait_count=0
    while kill -0 "$compose_pid" 2>/dev/null; do
        wait_count=$((wait_count + 1))
        [ "$wait_count" -ge 10 ] && break
        sleep 1
    done
    wait "$compose_pid" 2>/dev/null || true
    rm -f "$log_file"

    echo ""
    _check_web_reachable
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
    up [flags]      Build and start all services (default if no command given)
    down            Stop all services; volumes are preserved
    rebuild [flags] Force-rebuild the web image from scratch, then start
    logs [service]  Follow logs; defaults to 'web' if no service given
    seed            Run the data seed inside the running web container
    clean           Stop services and remove volumes (prompts for confirmation)
    ps              Show current service status
    shell [service] Open /bin/sh in a container; defaults to 'web'
    help            Show this help

  Flags (apply to: up, rebuild):
    --production    Seed production data only (real venues, no mock players /
                    games / matches / communities).
    --development   Seed the full mock dataset (default).

  Service names:
    web             Blazor web application  (http://localhost:8080)
    mongo           MongoDB 7               (localhost:27017)
    mongo-express   MongoDB admin UI        (http://localhost:8081)

  First run:
    .env is created automatically from .env.example.
    Edit .env to add GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET for OAuth.

  Seed mode:
    Default is Development — full mock dataset (33 mock players, 243 games,
    20 matches, 5 themed communities, plus the original 3 dev test players
    carey/george/carey_b). Use --production to seed only the real public
    venues; useful for verifying the app works without the mock crutch.

EOF
}

# ── Dispatch ──────────────────────────────────────────────────────────────────
CMD="${1:-up}"
shift || true   # shift away the command; remaining $@ are sub-arguments

case "$CMD" in
    up)      cmd_up      "$@" ;;
    down)    cmd_down    ;;
    rebuild) cmd_rebuild "$@" ;;
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

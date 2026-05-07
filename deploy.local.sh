#!/usr/bin/env bash
# =============================================================================
# UMaT Alumni – Local Development Deploy Script (macOS)
# =============================================================================
# Usage:
#   ./deploy.local.sh            Build and start the full local stack
#   ./deploy.local.sh --update   Rebuild changed images and restart
#   ./deploy.local.sh --stop     Stop all containers (preserves volumes)
#   ./deploy.local.sh --reset    Stop containers AND wipe all local volumes
#   ./deploy.local.sh --status   Print live status and access URLs
#   ./deploy.local.sh --logs     Tail logs for all services (Ctrl-C to exit)
# =============================================================================
set -euo pipefail

readonly COMPOSE_FILE="docker-compose.yml"
readonly ENV_FILE=".env"

# ── Colours ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; BOLD='\033[1m'; NC='\033[0m'

log()    { echo -e "${BLUE}[INFO]${NC}  $*"; }
ok()     { echo -e "${GREEN}[ OK ]${NC}  $*"; }
warn()   { echo -e "${YELLOW}[WARN]${NC}  $*"; }
err()    { echo -e "${RED}[ERR ]${NC}  $*" >&2; }
die()    { err "$*"; exit 1; }
banner() { echo -e "\n${BOLD}=== $* ===${NC}"; }

# ── Argument parsing ───────────────────────────────────────────────────────────
MODE="deploy"
for arg in "$@"; do
  case "$arg" in
    --update) MODE="update" ;;
    --stop)   MODE="stop"   ;;
    --reset)  MODE="reset"  ;;
    --status) MODE="status" ;;
    --logs)   MODE="logs"   ;;
    --help|-h)
      sed -n '/^# Usage/,/^# ===/p' "$0" | head -n 9 | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) die "Unknown argument: $arg. Use --help for usage." ;;
  esac
done

# ── 1. Prerequisites ───────────────────────────────────────────────────────────
check_prerequisites() {
  banner "Prerequisites"

  command -v docker >/dev/null 2>&1 \
    || die "Docker not found. Install Docker Desktop: https://www.docker.com/products/docker-desktop/"
  docker compose version >/dev/null 2>&1 \
    || die "Docker Compose plugin not found. Update Docker Desktop."
  docker info >/dev/null 2>&1 \
    || die "Docker daemon not running. Start Docker Desktop and try again."

  ok "Docker       $(docker --version | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
  ok "Compose      $(docker compose version --short)"
}

# ── 2. Validate .env ───────────────────────────────────────────────────────────
validate_env() {
  banner "Environment"

  if [[ ! -f "$ENV_FILE" ]]; then
    warn "$ENV_FILE not found – creating from .env.example"
    cp .env.example "$ENV_FILE"
    ok "Created $ENV_FILE from .env.example (defaults are fine for local use)."
  else
    ok "$ENV_FILE found."
  fi
}

# ── 3. Validate compose syntax ─────────────────────────────────────────────────
validate_compose() {
  banner "Compose file"
  docker compose -f "$COMPOSE_FILE" config --quiet \
    || die "$COMPOSE_FILE has syntax errors."
  ok "$COMPOSE_FILE syntax OK."
}

# ── 4. Pull base images ────────────────────────────────────────────────────────
pull_images() {
  banner "Pulling base images"
  docker compose -f "$COMPOSE_FILE" pull --ignore-buildable --quiet
  ok "Base images pulled."
}

# ── 5. Build application images ────────────────────────────────────────────────
build_images() {
  banner "Building application images"
  log "First build takes ~5-15 min depending on your machine…"
  docker compose -f "$COMPOSE_FILE" build --parallel
  ok "All images built."
}

# ── 6. Start services ──────────────────────────────────────────────────────────
start_services() {
  banner "Starting services"
  docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
  ok "Services started."
}

# ── 7. Health checks ───────────────────────────────────────────────────────────
health_checks() {
  banner "Health checks"
  log "Waiting 20 s for containers to initialise…"
  sleep 20

  local containers=(
    alumni-postgres alumni-redis alumni-minio
    alumni-admin-api alumni-member-api
    alumni-frontend-admin alumni-frontend-member
  )

  local failed=0
  for c in "${containers[@]}"; do
    local state
    state=$(docker inspect --format='{{.State.Status}}' "$c" 2>/dev/null || echo "missing")
    if [[ "$state" == "running" ]]; then
      ok "$c → running"
    else
      err "$c → $state"
      (( failed++ )) || true
    fi
  done

  # HTTP smoke tests
  local -A checks=(
    ["http://localhost:3100"]="frontend-admin"
    ["http://localhost:3200"]="frontend-member"
    ["http://localhost:5100/health"]="admin-api"
    ["http://localhost:5200/health"]="member-api"
  )
  for url in "${!checks[@]}"; do
    local code
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$url" 2>/dev/null || echo "000")
    if [[ "$code" =~ ^[23] ]]; then
      ok "${checks[$url]} ($url) → $code"
    else
      warn "${checks[$url]} ($url) → $code  (may still be starting up)"
    fi
  done

  (( failed == 0 )) \
    && ok "All containers are running." \
    || warn "$failed container(s) not running – run: docker logs <name>"
}

# ── 8. Summary ─────────────────────────────────────────────────────────────────
print_summary() {
  echo ""
  echo -e "${BOLD}╔══════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${BOLD}║        UMaT Alumni – Local Stack  (macOS)                   ║${NC}"
  echo -e "${BOLD}╚══════════════════════════════════════════════════════════════╝${NC}"
  echo ""

  echo -e "  ${BOLD}SERVICES:${NC}"
  docker compose -f "$COMPOSE_FILE" ps --format \
    "table {{.Name}}\t{{.Status}}" 2>/dev/null | sed 's/^/    /' \
    || docker ps --filter "name=alumni-" --format "    {{.Names}}\t{{.Status}}"

  echo ""
  echo -e "  ${BOLD}ACCESS URLS:${NC}"
  echo "  ┌────────────────────────────────────────────────────────────┐"
  printf "  │  %-30s  %-27s│\n" "Service"              "URL"
  echo "  ├────────────────────────────────────────────────────────────┤"
  printf "  │  %-30s  %-27s│\n" "Admin Frontend"        "http://localhost:3100"
  printf "  │  %-30s  %-27s│\n" "Member Frontend"       "http://localhost:3200"
  echo "  ├────────────────────────────────────────────────────────────┤"
  printf "  │  %-30s  %-27s│\n" "Admin API"             "http://localhost:5100"
  printf "  │  %-30s  %-27s│\n" "Member API"            "http://localhost:5200"
  echo "  ├────────────────────────────────────────────────────────────┤"
  printf "  │  %-30s  %-27s│\n" "MinIO Console"         "http://localhost:9001"
  printf "  │  %-30s  %-27s│\n" "MailHog Web UI"        "http://localhost:8025"
  printf "  │  %-30s  %-27s│\n" "Postgres"              "localhost:5432"
  printf "  │  %-30s  %-27s│\n" "Redis"                 "localhost:6379"
  echo "  └────────────────────────────────────────────────────────────┘"

  echo ""
  echo -e "  ${BOLD}USEFUL COMMANDS:${NC}"
  echo "    Logs (all):   ./deploy.local.sh --logs"
  echo "    Logs (one):   docker logs -f alumni-<service>"
  echo "    Restart one:  docker compose restart <service>"
  echo "    Stop stack:   ./deploy.local.sh --stop"
  echo "    Wipe volumes: ./deploy.local.sh --reset"
  echo ""
}

# ── Mode dispatch ──────────────────────────────────────────────────────────────
case "$MODE" in
  deploy)
    check_prerequisites
    validate_env
    validate_compose
    pull_images
    build_images
    start_services
    health_checks
    print_summary
    ;;

  update)
    banner "Update"
    check_prerequisites
    validate_env
    validate_compose
    build_images
    start_services
    health_checks
    print_summary
    ;;

  stop)
    banner "Stop"
    docker compose -f "$COMPOSE_FILE" down
    ok "All containers stopped (volumes preserved)."
    ;;

  reset)
    banner "Reset"
    warn "This will DELETE all local volumes (database data, minio data, redis data)!"
    read -r -p "Are you sure? [y/N] " confirm
    [[ "$confirm" =~ ^[yY]$ ]] || { log "Aborted."; exit 0; }
    docker compose -f "$COMPOSE_FILE" down -v
    ok "Containers stopped and volumes removed."
    ;;

  status)
    print_summary
    ;;

  logs)
    docker compose -f "$COMPOSE_FILE" logs -f
    ;;
esac

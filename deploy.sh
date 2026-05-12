#!/usr/bin/env bash
# =============================================================================
# UMaT Alumni – Production Deployment Script
# =============================================================================
# Usage:
#   ./deploy.sh              Full deploy: validate → build → start → health check
#   ./deploy.sh --ssl        Provision Let's Encrypt SSL (run after first deploy)
#   ./deploy.sh --update     Rebuild changed images and restart services
#   ./deploy.sh --status     Print live service status and routing map
# =============================================================================
set -euo pipefail

# ── Configuration ──────────────────────────────────────────────────────────────
readonly COMPOSE_FILE="docker-compose.prod.yml"
readonly ENV_FILE=".env"
readonly ALL_DOMAINS=(
  "alumat.umat.edu.gh"
  "admin-alumat.umat.edu.gh"
  "alumat-api.umat.edu.gh"
  "admin-alumat-api.umat.edu.gh"
  "cdn.alumat.umat.edu.gh"
)
CERTBOT_EMAIL="${CERTBOT_EMAIL:-admin@umat.edu.gh}"

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
    --ssl)    MODE="ssl"    ;;
    --update) MODE="update" ;;
    --status) MODE="status" ;;
    --help|-h)
      sed -n '/^# Usage/,/^# ===/p' "$0" | head -n 8 | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) die "Unknown argument: $arg. Use --help for usage." ;;
  esac
done

# ── 1. Prerequisites ───────────────────────────────────────────────────────────
check_prerequisites() {
  banner "Prerequisites"
  command -v docker >/dev/null 2>&1 \
    || die "Docker not installed. Install with: curl -fsSL https://get.docker.com | sh"
  docker compose version >/dev/null 2>&1 \
    || die "Docker Compose plugin not found. Install docker-compose-plugin."
  docker info >/dev/null 2>&1 \
    || die "Docker daemon not running. Run: sudo systemctl start docker"
  ok "Docker       $(docker --version | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)"
  ok "Compose      $(docker compose version --short)"
}

# ── 2. Validate .env ───────────────────────────────────────────────────────────
validate_env() {
  banner "Environment validation"
  [[ -f "$ENV_FILE" ]] \
    || die "$ENV_FILE not found. Copy .env.production.example to .env and fill in secrets."

  local required_vars=(
    POSTGRES_USER POSTGRES_PASSWORD POSTGRES_DB
    MINIO_ROOT_USER MINIO_ROOT_PASSWORD MINIO_BUCKET_NAME
    STORAGE_ACCESS_KEY STORAGE_SECRET_KEY STORAGE_BUCKET STORAGE_REGION
    STORAGE_ENDPOINT STORAGE_CDN_ENDPOINT
    JWT_ISSUER JWT_AUDIENCE ADMIN_SIGNING_KEY MEMBER_SIGNING_KEY
    SMTP_HOST SMTP_PORT SMTP_USERNAME SMTP_PASSWORD SMTP_SENDER_EMAIL
    PAYSTACK_SECRET_KEY PAYSTACK_PUBLIC_KEY PAYSTACK_CALLBACK_URL
    NEXT_PUBLIC_ADMIN_API_URL NEXT_PUBLIC_MEMBER_API_URL
  )

  local missing=()
  for var in "${required_vars[@]}"; do
    local val
    val=$(grep -E "^${var}=" "$ENV_FILE" 2>/dev/null | cut -d= -f2- | tr -d '"'"'" || true)
    [[ -z "$val" ]] && missing+=("$var")
  done

  if (( ${#missing[@]} > 0 )); then
    err "Missing or empty variables in $ENV_FILE:"
    printf '  - %s\n' "${missing[@]}"
    die "Fix the variables above and re-run."
  fi

  if grep -q "CHANGE_ME\|replace_with_secure" "$ENV_FILE" 2>/dev/null; then
    warn "Placeholder values detected in $ENV_FILE – replace all CHANGE_ME values before going live!"
  fi
  ok "$ENV_FILE validated (${#required_vars[@]} variables checked)."
}

# ── 3. Validate compose syntax ─────────────────────────────────────────────────
validate_compose() {
  banner "Compose file validation"
  set -a; source "$ENV_FILE"; set +a
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" config --quiet \
    || die "$COMPOSE_FILE has syntax errors."
  ok "$COMPOSE_FILE syntax OK."

  [[ -f "docker/nginx/nginx.conf" ]] \
    || die "docker/nginx/nginx.conf not found. Nginx gateway cannot start."
  ok "docker/nginx/nginx.conf present."
}

# ── 4. Prepare directories ─────────────────────────────────────────────────────
prepare_dirs() {
  banner "Directory setup"
  mkdir -p docker/nginx
  ok "Required directories ready."
}

# ── 5. Firewall (ufw) ──────────────────────────────────────────────────────────
configure_firewall() {
  banner "Firewall"
  if ! command -v ufw >/dev/null 2>&1; then
    warn "ufw not found – skipping firewall configuration."
    return
  fi

  sudo ufw allow 22/tcp  comment "SSH"   2>/dev/null || true
  sudo ufw allow 80/tcp  comment "HTTP"  2>/dev/null || true
  sudo ufw allow 443/tcp comment "HTTPS" 2>/dev/null || true

  # IP mode: open per-service ports
  local nginx_conf
  nginx_conf=$(grep -E "^NGINX_CONF_FILE=" "$ENV_FILE" 2>/dev/null | cut -d= -f2- || true)
  if [[ "$nginx_conf" == *nginx.ip.conf* ]]; then
    for port in 8001 8002 8003; do
      sudo ufw allow "$port"/tcp comment "Alumni IP-mode service port" 2>/dev/null || true
    done
    ok "Firewall: IP-mode ports 8001, 8002, 8003 opened."
  fi

  # MinIO is exposed publicly (CDN use), open it
  local minio_port
  minio_port=$(grep -E "^MINIO_API_PORT=" "$ENV_FILE" 2>/dev/null | cut -d= -f2- || echo "9000")
  sudo ufw allow "${minio_port:-9000}"/tcp comment "MinIO API" 2>/dev/null || true

  # Block direct access to infra ports (they bind to 127.0.0.1 in prod)
  for port in 5432 6379 9001; do
    sudo ufw deny "$port"/tcp comment "Block direct service port" 2>/dev/null || true
  done

  sudo ufw --force enable 2>/dev/null || true
  ok "Firewall: 22 (SSH), 80 (HTTP), 443 (HTTPS) allowed."
}

# ── 6. Pull base images ────────────────────────────────────────────────────────
pull_images() {
  banner "Pulling base images"
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" pull --ignore-buildable --quiet
  ok "Base images pulled."
}

# ── 7. Build application images ────────────────────────────────────────────────
build_images() {
  banner "Building application images"
  log "This may take 5-15 minutes on first run…"
  set -a; source "$ENV_FILE"; set +a
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" build --parallel
  ok "All images built."
}

# ── 8. Start services ──────────────────────────────────────────────────────────
start_services() {
  banner "Starting services"
  set -a; source "$ENV_FILE"; set +a
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" up -d --remove-orphans
  ok "Services started."
}

# ── 9. Health checks ───────────────────────────────────────────────────────────
health_checks() {
  banner "Health checks"
  log "Waiting 20 s for containers to initialise…"
  sleep 20

  local containers=(
    alumni-postgres alumni-redis alumni-minio
    alumni-admin-api alumni-member-api
    alumni-frontend-admin alumni-frontend-member
    alumni-nginx
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

  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 http://localhost/ 2>/dev/null || echo "000")
  if [[ "$http_code" =~ ^[23] ]]; then
    ok "HTTP http://localhost/ → $http_code"
  else
    warn "HTTP http://localhost/ → $http_code  (nginx may still be initialising)"
  fi

  (( failed == 0 )) && ok "All health checks passed." \
    || warn "$failed container(s) not running – check logs with: docker logs <name>"
}

# ── 10. SSL provisioning ───────────────────────────────────────────────────────
provision_ssl() {
  banner "SSL – Let's Encrypt"

  set -a; source "$ENV_FILE"; set +a

  # ── Step 1: Start nginx with HTTP-only bootstrap config ──────────────────────
  # nginx refuses to start if ssl_certificate files don't exist yet.
  # nginx.pre-ssl.conf has no SSL blocks, so it starts cleanly and can serve
  # ACME challenges for the webroot validation.
  log "Starting nginx in HTTP-only bootstrap mode (nginx.pre-ssl.conf)…"
  NGINX_CONF_FILE=./docker/nginx/nginx.pre-ssl.conf \
    docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" \
    up -d --no-deps --force-recreate nginx

  log "Waiting 8 s for nginx to become ready…"
  sleep 8

  local nginx_status
  nginx_status=$(docker inspect --format='{{.State.Status}}' alumni-nginx 2>/dev/null || echo "missing")
  if [[ "$nginx_status" != "running" ]]; then
    docker logs --tail 30 alumni-nginx 2>&1 || true
    die "nginx failed to start with pre-SSL config (status: $nginx_status). See logs above."
  fi
  ok "nginx is running (HTTP-only bootstrap mode)."

  # ── Step 2: Request certificates via webroot challenge ───────────────────────
  local domain_flags=()
  for d in "${ALL_DOMAINS[@]}"; do
    domain_flags+=(-d "$d")
  done

  log "Requesting certificates for: ${ALL_DOMAINS[*]}"
  log "ACME e-mail: $CERTBOT_EMAIL"

  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" \
    run --rm --no-deps --entrypoint "/bin/sh" certbot -c \
    "certbot certonly --webroot --webroot-path /var/www/certbot \
     --non-interactive --agree-tos --email '${CERTBOT_EMAIL}' \
     ${domain_flags[*]} --expand"

  ok "Certificates issued (or already valid)."

  # ── Step 3: Restart nginx with full HTTPS config, ensure all services are up ─
  log "Restarting nginx with full HTTPS configuration (nginx.conf)…"
  unset NGINX_CONF_FILE
  # Force-recreate nginx so it picks up the now-present certs.
  # Then run a plain 'up -d' (no --no-deps) so any backend/frontend
  # containers that aren't running get started without recreating healthy ones.
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" \
    up -d --no-deps --force-recreate nginx
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" \
    up -d

  log "Waiting 8 s for services to initialise…"
  sleep 8

  nginx_status=$(docker inspect --format='{{.State.Status}}' alumni-nginx 2>/dev/null || echo "missing")
  if [[ "$nginx_status" == "running" ]]; then
    ok "nginx is running with full HTTPS configuration."
  else
    docker logs --tail 30 alumni-nginx 2>&1 || true
    die "nginx failed to start with HTTPS config (status: $nginx_status). Check logs above."
  fi
}

# ── 11. Status / summary ───────────────────────────────────────────────────────
print_summary() {
  local server_ip
  server_ip=$(curl -s --max-time 5 https://ipv4.icanhazip.com 2>/dev/null || hostname -I | awk '{print $1}')

  echo ""
  echo -e "${BOLD}╔══════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${BOLD}║       UMaT Alumni – Platform Deployment Summary             ║${NC}"
  echo -e "${BOLD}╚══════════════════════════════════════════════════════════════╝${NC}"
  echo ""
  echo -e "  ${BOLD}Server IP:${NC}   $server_ip"
  echo ""

  echo -e "  ${BOLD}SERVICES:${NC}"
  docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps --format \
    "table {{.Name}}\t{{.Status}}" 2>/dev/null \
    | sed 's/^/    /' \
    || docker ps --filter "name=alumni-" --format "    {{.Names}}\t{{.Status}}"

  echo ""
  echo -e "  ${BOLD}ROUTING MAP:${NC}"
  echo "  ┌──────────────────────────────────────────────────────────────────┐"
  printf "  │  %-40s → %-22s│\n" "http://alumat.umat.edu.gh"             "frontend-member:3000"
  printf "  │  %-40s → %-22s│\n" "http://admin-alumat.umat.edu.gh"       "frontend-admin:3000"
  printf "  │  %-40s → %-22s│\n" "http://alumat-api.umat.edu.gh"         "member-api:8080"
  printf "  │  %-40s → %-22s│\n" "http://admin-alumat-api.umat.edu.gh"   "admin-api:8080"
  printf "  │  %-40s → %-22s│\n" "http://cdn.alumat.umat.edu.gh"         "minio:9000 (CDN)"
  printf "  │  %-40s → %-22s│\n" "http://$server_ip:9000"                "MinIO direct (CDN)"
  echo "  └──────────────────────────────────────────────────────────────────┘"

  echo ""
  echo -e "  ${BOLD}USEFUL COMMANDS:${NC}"
  echo "    Logs:    docker compose --env-file $ENV_FILE -f $COMPOSE_FILE logs -f <service>"
  echo "    Restart: docker compose --env-file $ENV_FILE -f $COMPOSE_FILE restart <service>"
  echo "    Stop:    docker compose --env-file $ENV_FILE -f $COMPOSE_FILE down"
  echo "    Update:  ./deploy.sh --update"
  echo "    SSL:     ./deploy.sh --ssl"
  echo ""
}

# ── Mode dispatch ──────────────────────────────────────────────────────────────
deploy_mode() {
  echo ""
  echo -e "${BOLD}UMaT Alumni – Production Deployment${NC}  ($(date '+%Y-%m-%d %H:%M:%S %Z'))"
  echo ""
  check_prerequisites
  validate_env
  validate_compose
  prepare_dirs
  configure_firewall
  pull_images
  build_images
  start_services
  health_checks
  print_summary
  ok "Deployment complete!"
  log "To enable HTTPS, run: ./deploy.sh --ssl"
}

update_mode() {
  check_prerequisites
  validate_env
  log "Pulling and rebuilding changed services…"
  pull_images
  build_images
  start_services
  health_checks
  print_summary
  ok "Update complete."
}

ssl_mode() {
  check_prerequisites
  validate_env
  provision_ssl
  print_summary
}

status_mode() {
  check_prerequisites
  print_summary
}

case "$MODE" in
  deploy) deploy_mode ;;
  ssl)    ssl_mode    ;;
  update) update_mode ;;
  status) status_mode ;;
esac

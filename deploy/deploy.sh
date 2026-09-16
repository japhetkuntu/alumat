#!/usr/bin/env bash
# Builds and (re)deploys the Alumni Portal from source already on the
# droplet. Run this for the first deploy (after install.sh) and for every
# subsequent update. Modeled on WishDem's backend/deploy/deploy.sh.
#
# Usage:
#   sudo bash /opt/alumunion-src/deploy/deploy.sh                    # everything (default)
#   sudo bash /opt/alumunion-src/deploy/deploy.sh operations-worker  # just one service
# Valid single targets: institution-api member-api platform-api operations-worker
#                        frontend-institution frontend-member frontend-platform
#
# A single-target run only stops/publishes/starts that one service — every
# other systemd unit keeps running untouched, so e.g. adding operations-worker
# for the first time (or picking up a later member-api-only fix) doesn't take
# the whole site down for the build.
set -euo pipefail

SRC_DIR="/opt/alumunion-src"
BACKEND_SRC="$SRC_DIR/backend"
FRONTEND_SRC="$SRC_DIR/frontend"
OUT="/var/www/alumunion"

TARGET="${1:-all}"
DO_INSTITUTION_API=0
DO_MEMBER_API=0
DO_PLATFORM_API=0
DO_OPERATIONS_WORKER=0
DO_FRONTEND_INSTITUTION=0
DO_FRONTEND_MEMBER=0
DO_FRONTEND_PLATFORM=0

case "$TARGET" in
  all)
    DO_INSTITUTION_API=1; DO_MEMBER_API=1; DO_PLATFORM_API=1; DO_OPERATIONS_WORKER=1
    DO_FRONTEND_INSTITUTION=1; DO_FRONTEND_MEMBER=1; DO_FRONTEND_PLATFORM=1
    ;;
  institution-api) DO_INSTITUTION_API=1 ;;
  member-api) DO_MEMBER_API=1 ;;
  platform-api) DO_PLATFORM_API=1 ;;
  operations-worker) DO_OPERATIONS_WORKER=1 ;;
  frontend-institution) DO_FRONTEND_INSTITUTION=1 ;;
  frontend-member) DO_FRONTEND_MEMBER=1 ;;
  frontend-platform) DO_FRONTEND_PLATFORM=1 ;;
  *)
    echo "Unknown target: $TARGET"
    echo "Valid: all institution-api member-api platform-api operations-worker frontend-institution frontend-member frontend-platform"
    exit 1
    ;;
esac
# Any .NET service touched at all needs the backend build/cache housekeeping
# below; any frontend touched needs the pnpm install.
DO_ANY_DOTNET=0
[[ "$DO_INSTITUTION_API$DO_MEMBER_API$DO_PLATFORM_API$DO_OPERATIONS_WORKER" == *1* ]] && DO_ANY_DOTNET=1
DO_ANY_FRONTEND=0
[[ "$DO_FRONTEND_INSTITUTION$DO_FRONTEND_MEMBER$DO_FRONTEND_PLATFORM" == *1* ]] && DO_ANY_FRONTEND=1

wait_healthy() {
  local url="$1" name="$2" tries=30
  echo "Waiting for $name to report healthy at $url ..."
  for ((i = 1; i <= tries; i++)); do
    if curl -fs "$url" >/dev/null 2>&1; then
      echo "$name is healthy."
      return 0
    fi
    sleep 2
  done
  echo "$name did not become healthy after $((tries * 2))s — check: journalctl -u $name -n 100 --no-pager"
  return 1
}

# operations-worker exposes no HTTP endpoint (it only long-polls Temporal), so
# it can't be checked with wait_healthy — instead, poll `systemctl is-active`
# for a bit: a crash loop (bad config, unreachable Temporal/Postgres) shows up
# as the unit leaving "active" during Restart=on-failure's backoff window,
# same failure mode wait_healthy is catching for the HTTP services above.
wait_active() {
  local name="$1" tries=15
  echo "Waiting for $name to stay active ..."
  for ((i = 1; i <= tries; i++)); do
    sleep 2
    if ! systemctl is-active --quiet "$name"; then
      echo "$name is not active — check: journalctl -u $name -n 100 --no-pager"
      return 1
    fi
  done
  echo "$name is active."
  return 0
}

# Override with e.g. `MIN_FREE_GB=2 sudo -E bash deploy.sh` if you're on a
# small droplet and have already freed what you reasonably can — this is a
# safety check, not a hard requirement; the real fix for a droplet that's
# consistently this tight is more disk, not a lower number here.
MIN_FREE_GB="${MIN_FREE_GB:-2}"
check_disk_space() {
  local free_gb
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if [[ "$free_gb" -lt "$MIN_FREE_GB" ]]; then
    echo "ERROR: only ${free_gb}G free on / — need at least ${MIN_FREE_GB}G to build."
    echo "Free up space first, e.g.:"
    echo "  dotnet nuget locals all --clear"
    echo "  find $BACKEND_SRC -type d \\( -name bin -o -name obj \\) -exec rm -rf {} +"
    echo "  apt-get clean && journalctl --vacuum-time=3d"
    echo "If that's not enough, resize the droplet's disk (DigitalOcean dashboard → Resize),"
    echo "or lower this check: MIN_FREE_GB=2 sudo -E bash deploy.sh"
    exit 1
  fi
}

echo "== pulling latest source =="
cd "$SRC_DIR"
git pull

# This script lives inside the repo it just pulled — if that pull changed
# deploy.sh itself (e.g. this very file was edited upstream), bash's read
# position for the rest of the currently-running script becomes misaligned
# with the new on-disk content, producing bizarre mid-script errors ("X:
# unbound variable" at a line that doesn't match the file). Re-exec fresh
# from disk once, so everything past this point always runs the version that
# was just pulled, not whatever bash had buffered before the pull. "$@" carries
# the target argument through the re-exec unchanged.
if [[ -z "${ALUMNI_DEPLOY_REEXECED:-}" ]]; then
  export ALUMNI_DEPLOY_REEXECED=1
  exec bash "$SRC_DIR/deploy/deploy.sh" "$@"
fi

if [[ "$DO_ANY_DOTNET" == 1 ]]; then
  # Every dotnet publish leaves bin/obj behind in the source tree (separate
  # from the actual published output in $OUT) — across enough redeploys
  # without this, these silently accumulate until a build fails with "No
  # space left on device" mid-restore, same as the NuGet http cache below.
  # Both are safe to wipe every time: publish always restores/rebuilds from
  # scratch regardless.
  echo "== clearing stale build artifacts =="
  find "$BACKEND_SRC" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
  dotnet nuget locals http-cache --clear
fi

check_disk_space

# install.sh only renders nginx.conf once, at first provisioning — a later
# change to the template never reaches already-provisioned droplets on its
# own. This project's nginx config also gets a manual SSL swap-in (see
# nginx.ssl.conf) that a blind re-render here would clobber, so — same as
# WishDem — this intentionally does NOT re-render nginx config. If you change
# deploy/nginx.conf or nginx.ssl.conf, re-apply it by hand.

# Stop only what's about to be rebuilt, before touching its files — dotnet
# publish and the frontend rsync both overwrite a running service's files in
# place. Linux generally tolerates that (an already-running process keeps its
# old file handles), but it's not guaranteed for every code path (lazy-loaded
# assemblies, dynamic Next.js chunks), and a clean stop/start is simpler to
# reason about than "maybe fine, maybe a transient 500". Everything not in
# today's target list keeps running the whole time.
echo "== stopping services for this deploy (target: $TARGET) =="
STOP_LIST=()
[[ "$DO_INSTITUTION_API" == 1 ]] && STOP_LIST+=(alumni-institution-api)
[[ "$DO_MEMBER_API" == 1 ]] && STOP_LIST+=(alumni-member-api)
[[ "$DO_PLATFORM_API" == 1 ]] && STOP_LIST+=(alumni-platform-api)
[[ "$DO_OPERATIONS_WORKER" == 1 ]] && STOP_LIST+=(alumni-operations-worker)
[[ "$DO_FRONTEND_INSTITUTION" == 1 ]] && STOP_LIST+=(alumni-frontend-institution)
[[ "$DO_FRONTEND_MEMBER" == 1 ]] && STOP_LIST+=(alumni-frontend-member)
[[ "$DO_FRONTEND_PLATFORM" == 1 ]] && STOP_LIST+=(alumni-frontend-platform)
systemctl stop "${STOP_LIST[@]}" 2>/dev/null || true

if [[ "$DO_ANY_FRONTEND" == 1 ]]; then
  echo "== installing workspace dependencies (pnpm) =="
  cd "$FRONTEND_SRC"
  pnpm install --frozen-lockfile
fi

if [[ "$DO_INSTITUTION_API" == 1 ]]; then
  echo "== publishing institution-api =="
  dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Institution.Api/ReservEase.Alumni.Institution.Api.csproj" \
    -c Release -o "$OUT/institution-api"
  chown -R www-data:www-data "$OUT/institution-api"
fi

if [[ "$DO_MEMBER_API" == 1 ]]; then
  echo "== publishing member-api =="
  dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Member.Api/ReservEase.Alumni.Member.Api.csproj" \
    -c Release -o "$OUT/member-api"
  chown -R www-data:www-data "$OUT/member-api"
fi

if [[ "$DO_PLATFORM_API" == 1 ]]; then
  echo "== publishing platform-api =="
  dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Platform.Api/ReservEase.Alumni.Platform.Api.csproj" \
    -c Release -o "$OUT/platform-api"
  chown -R www-data:www-data "$OUT/platform-api"
fi

if [[ "$DO_OPERATIONS_WORKER" == 1 ]]; then
  echo "== publishing operations-worker =="
  dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Operations.Worker/ReservEase.Alumni.Operations.Worker.csproj" \
    -c Release -o "$OUT/operations-worker"
  chown -R www-data:www-data "$OUT/operations-worker"
fi

# ---------------------------------------------------------------------------
# Frontends — each `next build` (output: "standalone") produces a
# self-contained server tree at apps/<app>/.next/standalone/, mirroring the
# monorepo layout (apps/<app>/server.js) because of outputFileTracingRoot —
# same three-directory copy as each app's own Dockerfile does.
# ---------------------------------------------------------------------------
publish_frontend() {
  local app="$1"
  local out="$OUT/frontend-${app}"
  echo "== building frontend: ${app} =="
  (cd "$FRONTEND_SRC" && pnpm --filter "@alumni/${app}" build)

  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/standalone/" "$out/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/static/" "$out/apps/${app}/.next/static/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/public/" "$out/apps/${app}/public/"
  chown -R www-data:www-data "$out"
}

[[ "$DO_FRONTEND_INSTITUTION" == 1 ]] && publish_frontend institution
[[ "$DO_FRONTEND_MEMBER" == 1 ]] && publish_frontend member
[[ "$DO_FRONTEND_PLATFORM" == 1 ]] && publish_frontend platform

if [[ "$DO_INSTITUTION_API" == 1 ]]; then
  echo "== starting institution-api (applies any new EF migrations on boot) =="
  systemctl start alumni-institution-api
  wait_healthy "http://127.0.0.1:5001/health" alumni-institution-api
fi

if [[ "$DO_MEMBER_API" == 1 ]]; then
  echo "== starting member-api =="
  systemctl start alumni-member-api
  wait_healthy "http://127.0.0.1:5002/health" alumni-member-api
fi

if [[ "$DO_PLATFORM_API" == 1 ]]; then
  echo "== starting platform-api =="
  systemctl start alumni-platform-api
  wait_healthy "http://127.0.0.1:5003/health" alumni-platform-api
fi

if [[ "$DO_OPERATIONS_WORKER" == 1 ]]; then
  echo "== starting operations-worker =="
  systemctl start alumni-operations-worker
  wait_active alumni-operations-worker
fi

if [[ "$DO_FRONTEND_INSTITUTION" == 1 ]]; then
  echo "== starting frontend-institution =="
  systemctl start alumni-frontend-institution
  wait_healthy "http://127.0.0.1:3001/" alumni-frontend-institution
fi

if [[ "$DO_FRONTEND_MEMBER" == 1 ]]; then
  echo "== starting frontend-member =="
  systemctl start alumni-frontend-member
  wait_healthy "http://127.0.0.1:3002/" alumni-frontend-member
fi

if [[ "$DO_FRONTEND_PLATFORM" == 1 ]]; then
  echo "== starting frontend-platform =="
  systemctl start alumni-frontend-platform
  wait_healthy "http://127.0.0.1:3003/" alumni-frontend-platform
fi

echo "Deploy complete (target: $TARGET)."

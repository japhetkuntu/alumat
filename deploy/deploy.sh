#!/usr/bin/env bash
# Builds and (re)deploys all 3 APIs and all 3 frontends from source already on
# the droplet. Run this for the first deploy (after install.sh) and for every
# subsequent update. Modeled on WishDem's backend/deploy/deploy.sh.
#
# Usage: sudo bash /opt/alumunion-src/deploy/deploy.sh
set -euo pipefail

SRC_DIR="/opt/alumunion-src"
BACKEND_SRC="$SRC_DIR/backend"
FRONTEND_SRC="$SRC_DIR/frontend"
OUT="/var/www/alumunion"

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

# Override with e.g. `MIN_FREE_GB=2 sudo -E bash deploy.sh` if you're on a
# small droplet and have already freed what you reasonably can — this is a
# safety check, not a hard requirement; the real fix for a droplet that's
# consistently this tight is more disk, not a lower number here.
MIN_FREE_GB="${MIN_FREE_GB:-2}"
check_disk_space() {
  local free_gb
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if [[ "$free_gb" -lt "$MIN_FREE_GB" ]]; then
    echo "ERROR: only ${free_gb}G free on / — need at least ${MIN_FREE_GB}G to build 3 .NET APIs + 3 Next.js apps."
    echo "Free up space first, e.g.:"
    echo "  dotnet nuget locals all --clear"
    echo "  find $BACKEND_SRC -type d \\( -name bin -o -name obj \\) -exec rm -rf {} +"
    echo "  apt-get clean && journalctl --vacuum-time=3d"
    echo "If that's not enough, resize the droplet's disk (DigitalOcean dashboard → Resize),"
    echo "or lower this check: MIN_FREE_GB=2 sudo -E bash deploy.sh"
    exit 1
  fi
}

echo "== checking disk space =="
check_disk_space

echo "== pulling latest source =="
cd "$SRC_DIR"
git pull

# This script lives inside the repo it just pulled — if that pull changed
# deploy.sh itself (e.g. this very file was edited upstream), bash's read
# position for the rest of the currently-running script becomes misaligned
# with the new on-disk content, producing bizarre mid-script errors ("X:
# unbound variable" at a line that doesn't match the file). Re-exec fresh
# from disk once, so everything past this point always runs the version that
# was just pulled, not whatever bash had buffered before the pull.
if [[ -z "${ALUMNI_DEPLOY_REEXECED:-}" ]]; then
  export ALUMNI_DEPLOY_REEXECED=1
  exec bash "$SRC_DIR/deploy/deploy.sh" "$@"
fi

# Every dotnet publish leaves bin/obj behind in the source tree (separate from
# the actual published output in $OUT) — across enough redeploys without this,
# these silently accumulate until a build fails with "No space left on
# device" mid-restore, same as the NuGet http cache below. Both are safe to
# wipe every time: publish always restores/rebuilds from scratch regardless.
echo "== clearing stale build artifacts =="
find "$BACKEND_SRC" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
dotnet nuget locals http-cache --clear

check_disk_space

# install.sh only renders nginx.conf once, at first provisioning — a later
# change to the template never reaches already-provisioned droplets on its
# own. This project's nginx config also gets a manual SSL swap-in (see
# nginx.ssl.conf) that a blind re-render here would clobber, so — same as
# WishDem — this intentionally does NOT re-render nginx config. If you change
# deploy/nginx.conf or nginx.ssl.conf, re-apply it by hand.

echo "== installing workspace dependencies (pnpm) =="
cd "$FRONTEND_SRC"
pnpm install --frozen-lockfile

echo "== publishing institution-api =="
dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Institution.Api/ReservEase.Alumni.Institution.Api.csproj" \
  -c Release -o "$OUT/institution-api"

echo "== publishing member-api =="
dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Member.Api/ReservEase.Alumni.Member.Api.csproj" \
  -c Release -o "$OUT/member-api"

echo "== publishing platform-api =="
dotnet publish "$BACKEND_SRC/src/ReservEase.Alumni.Platform.Api/ReservEase.Alumni.Platform.Api.csproj" \
  -c Release -o "$OUT/platform-api"

chown -R www-data:www-data "$OUT/institution-api" "$OUT/member-api" "$OUT/platform-api"

# ---------------------------------------------------------------------------
# Frontends — each `next build` (output: "standalone") produces a
# self-contained server tree at apps/<app>/.next/standalone/, mirroring the
# monorepo layout (apps/<app>/server.js) because of outputFileTracingRoot —
# same three-directory copy as each app's own Dockerfile does.
# ---------------------------------------------------------------------------
publish_frontend() {
  local app="$1" out="$OUT/frontend-${app}"
  echo "== building frontend: ${app} =="
  (cd "$FRONTEND_SRC" && pnpm --filter "@alumni/${app}" build)

  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/standalone/" "$out/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/static/" "$out/apps/${app}/.next/static/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/public/" "$out/apps/${app}/public/"
  chown -R www-data:www-data "$out"
}

publish_frontend institution
publish_frontend member
publish_frontend platform

echo "== restarting institution-api (applies any new EF migrations on boot) =="
systemctl restart alumni-institution-api
wait_healthy "http://127.0.0.1:5001/health" alumni-institution-api

echo "== restarting member-api =="
systemctl restart alumni-member-api
wait_healthy "http://127.0.0.1:5002/health" alumni-member-api

echo "== restarting platform-api =="
systemctl restart alumni-platform-api
wait_healthy "http://127.0.0.1:5003/health" alumni-platform-api

echo "== restarting frontend-institution =="
systemctl restart alumni-frontend-institution
wait_healthy "http://127.0.0.1:3001/" alumni-frontend-institution

echo "== restarting frontend-member =="
systemctl restart alumni-frontend-member
wait_healthy "http://127.0.0.1:3002/" alumni-frontend-member

echo "== restarting frontend-platform =="
systemctl restart alumni-frontend-platform
wait_healthy "http://127.0.0.1:3003/" alumni-frontend-platform

echo "Deploy complete."

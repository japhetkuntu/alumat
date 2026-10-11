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
#
# Speed (see also the "build first, stop late" comment further down):
#   - Everything is BUILT while the old version keeps serving; services are only
#     stopped for the few seconds it takes to copy the finished build in and
#     start it again.
#   - bin/obj and the NuGet cache are kept between deploys, so builds are
#     incremental. Wipe them only when needed:  CLEAN_BUILD=1 sudo -E bash deploy.sh
#   - `all` only rebuilds what changed since the last successful deploy (backend vs
#     frontend, from git). Force everything:   FORCE_ALL=1 sudo -E bash deploy.sh
#   - Each step prints how long it took.
set -euo pipefail

SRC_DIR="/opt/alumunion-src"
BACKEND_SRC="$SRC_DIR/backend"
FRONTEND_SRC="$SRC_DIR/frontend"
OUT="/var/www/alumunion"

TARGET="${1:-all}"
STARTED_AT=$SECONDS
step_start=$SECONDS
# Prints how long the step that just finished took, then starts timing the next one.
lap() { local now=$SECONDS; echo "   ($(( now - step_start ))s: $1)"; step_start=$now; }
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
  # A crash loop shows within a few seconds (Restart=on-failure backs off ~5s between tries), so
  # three checks over six seconds is enough; this used to always take 30s.
  local name="$1" tries=3
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

lap "git pull"

# `all` means "everything that changed": compare with the commit last deployed successfully and
# leave out the backend or the frontends when nothing under them changed. A single named target,
# FORCE_ALL=1, or no record of a previous deploy always builds what was asked for.
REV_FILE="$OUT/.last-deployed-rev"
if [[ "$TARGET" == "all" && -z "${FORCE_ALL:-}" && -f "$REV_FILE" ]]; then
  PREV_REV="$(cat "$REV_FILE")"
  if git -C "$SRC_DIR" cat-file -e "${PREV_REV}^{commit}" 2>/dev/null; then
    CHANGED="$(git -C "$SRC_DIR" diff --name-only "$PREV_REV" HEAD)"
    if ! grep -q '^backend/' <<<"$CHANGED"; then
      DO_INSTITUTION_API=0; DO_MEMBER_API=0; DO_PLATFORM_API=0; DO_OPERATIONS_WORKER=0; DO_ANY_DOTNET=0
      echo "No backend changes since ${PREV_REV:0:8}: skipping the .NET services."
    fi
    if ! grep -q '^frontend/' <<<"$CHANGED"; then
      DO_FRONTEND_INSTITUTION=0; DO_FRONTEND_MEMBER=0; DO_FRONTEND_PLATFORM=0; DO_ANY_FRONTEND=0
      echo "No frontend changes since ${PREV_REV:0:8}: skipping the frontends."
    fi
    if [[ "$DO_ANY_DOTNET$DO_ANY_FRONTEND" == "00" ]]; then
      echo "Nothing to deploy (only deploy/docs files changed). FORCE_ALL=1 to rebuild everything anyway."
      git -C "$SRC_DIR" rev-parse HEAD > "$REV_FILE"
      exit 0
    fi
  fi
fi

# bin/obj and the NuGet cache are KEPT so builds are incremental. They were once wiped on every
# deploy to stop the disk filling up; check_disk_space below still protects against that, and a
# full clean is one variable away when a build misbehaves.
if [[ "$DO_ANY_DOTNET" == 1 && -n "${CLEAN_BUILD:-}" ]]; then
  echo "== CLEAN_BUILD: clearing build artifacts and NuGet http cache =="
  find "$BACKEND_SRC" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
  dotnet nuget locals http-cache --clear
fi

check_disk_space
# Disk is the one thing the kept caches can eat: when it is tight, clear them rather than fail the build.
if [[ "$DO_ANY_DOTNET" == 1 ]]; then
  free_gb=$(df --output=avail -BG / | tail -1 | tr -dc '0-9')
  if [[ "$free_gb" -lt $(( MIN_FREE_GB + 3 )) ]]; then
    echo "== disk is getting tight (${free_gb}G free): clearing build artifacts before building =="
    find "$BACKEND_SRC" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
    dotnet nuget locals http-cache --clear
  fi
fi

# install.sh only renders nginx.conf once, at first provisioning — a later
# change to the template never reaches already-provisioned droplets on its
# own. This project's nginx config also gets a manual SSL swap-in (see
# nginx.ssl.conf) that a blind re-render here would clobber, so — same as
# WishDem — this intentionally does NOT re-render the whole nginx config. If
# you change anything else in deploy/nginx.conf or nginx.ssl.conf, re-apply
# it by hand.
#
# The maintenance page (deploy/maintenance/index.html) is the one exception:
# it's synced and wired in on every deploy, surgically and idempotently —
# only ever ADDING the specific "if (-f .../enabled) return 503;" /
# "error_page ... @maintenance" / "location @maintenance {...}" lines when
# they're not already present, never touching or replacing anything else in
# the live file (so it can't clobber the SSL swap-in or any other manual
# edit). This exists because forgetting the "re-apply nginx config by hand"
# step above is exactly what happened in practice — the page was written but
# never reached the live config.
echo "== syncing maintenance page =="
mkdir -p /var/www/alumunion/maintenance
cp "$SRC_DIR/deploy/maintenance/index.html" /var/www/alumunion/maintenance/index.html
chown -R www-data:www-data /var/www/alumunion/maintenance

NGINX_LIVE_CONF=/etc/nginx/sites-available/alumunion
if [[ -f "$NGINX_LIVE_CONF" ]] && ! grep -q "location @maintenance" "$NGINX_LIVE_CONF"; then
  echo "== wiring the maintenance page into the live Nginx config (one-time) =="
  cp "$NGINX_LIVE_CONF" "${NGINX_LIVE_CONF}.bak-pre-maintenance"
  awk '
  {
    if ($0 ~ /^\tgzip on;/) {
      print "\tlocation @maintenance {"
      print "\t\troot /var/www/alumunion/maintenance;"
      print "\t\tinternal;"
      print "\t\ttry_files /index.html =502;"
      print "\t}"
      print ""
    }
    print
    if ($0 ~ /limit_conn conn_per_ip 100;/) {
      print ""
      print "\t# Maintenance page — see deploy/maintenance/index.html and deploy/toggle-maintenance.sh."
      print "\tif (-f /var/www/alumunion/maintenance/enabled) { return 503; }"
      print "\terror_page 502 503 504 =503 @maintenance;"
    }
  }
  ' "$NGINX_LIVE_CONF" > "${NGINX_LIVE_CONF}.tmp"
  mv "${NGINX_LIVE_CONF}.tmp" "$NGINX_LIVE_CONF"
  if nginx -t; then
    systemctl reload nginx
    rm -f "${NGINX_LIVE_CONF}.bak-pre-maintenance"
    echo "Maintenance page wired in and Nginx reloaded."
  else
    mv "${NGINX_LIVE_CONF}.bak-pre-maintenance" "$NGINX_LIVE_CONF"
    echo "WARNING: auto-wiring the maintenance page produced an invalid Nginx config (nginx -t failed above)."
    echo "Restored your original $NGINX_LIVE_CONF unchanged — nothing was reloaded, nothing is broken."
    echo "This likely means your live config has diverged from deploy/nginx.conf / nginx.ssl.conf (e.g. hand-edited)."
    echo "Wire it in manually if you want the maintenance page, or tell me what's different about your live config."
  fi
fi

# ---------------------------------------------------------------------------
# BUILD FIRST, STOP LATE. Everything below builds into a staging area while the
# current version keeps serving. Services are stopped only after every build has
# succeeded, for just long enough to copy the finished output in and start them
# again. A failed build therefore leaves the site untouched and running, and the
# downtime is seconds instead of the whole build.
# ---------------------------------------------------------------------------
STAGE="$OUT/.staging"
mkdir -p "$STAGE"

if [[ "$DO_ANY_FRONTEND" == 1 ]]; then
  echo "== installing workspace dependencies (pnpm) =="
  cd "$FRONTEND_SRC"
  pnpm install --frozen-lockfile
  lap "pnpm install"
fi

# Publish into staging. After the first project, the shared libraries are already built
# (obj is kept), so the later publishes only compile their own code.
publish_dotnet() {
  local name="$1" project="$2"
  echo "== building ${name} =="
  rm -rf "${STAGE:?}/${name:?}"
  dotnet publish "$BACKEND_SRC/src/${project}/${project}.csproj" -c Release -o "$STAGE/${name}"
  lap "build ${name}"
}

[[ "$DO_INSTITUTION_API" == 1 ]] && publish_dotnet institution-api ReservEase.Alumni.Institution.Api
[[ "$DO_MEMBER_API" == 1 ]] && publish_dotnet member-api ReservEase.Alumni.Member.Api
[[ "$DO_PLATFORM_API" == 1 ]] && publish_dotnet platform-api ReservEase.Alumni.Platform.Api
[[ "$DO_OPERATIONS_WORKER" == 1 ]] && publish_dotnet operations-worker ReservEase.Alumni.Operations.Worker

# ---------------------------------------------------------------------------
# Frontends — each `next build` (output: "standalone") produces a self-contained server tree at
# apps/<app>/.next/standalone/, mirroring the monorepo layout (apps/<app>/server.js) because of
# outputFileTracingRoot — same three-directory copy as each app's own Dockerfile does.
# ---------------------------------------------------------------------------
# `next build` writes into the source tree (apps/<app>/.next), not into the live folder, so it is
# safe to run while the old version serves. Copying into $OUT happens later, during the swap.
build_frontend() {
  local app="$1"
  echo "== building frontend: ${app} =="
  (cd "$FRONTEND_SRC" && pnpm --filter "@alumni/${app}" build)
  lap "build frontend ${app}"
}

[[ "$DO_FRONTEND_INSTITUTION" == 1 ]] && build_frontend institution
[[ "$DO_FRONTEND_MEMBER" == 1 ]] && build_frontend member
[[ "$DO_FRONTEND_PLATFORM" == 1 ]] && build_frontend platform

# ---------------------------------------------------------------------------
# SWAP: stop what is about to change, copy the finished builds in, start again.
# ---------------------------------------------------------------------------
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
lap "stop"

install_dotnet() {
  local name="$1"
  mkdir -p "$OUT/${name}"
  rsync -a --delete "$STAGE/${name}/" "$OUT/${name}/"
  chown -R www-data:www-data "$OUT/${name}"
}
[[ "$DO_INSTITUTION_API" == 1 ]] && install_dotnet institution-api
[[ "$DO_MEMBER_API" == 1 ]] && install_dotnet member-api
[[ "$DO_PLATFORM_API" == 1 ]] && install_dotnet platform-api
[[ "$DO_OPERATIONS_WORKER" == 1 ]] && install_dotnet operations-worker

install_frontend() {
  local app="$1"
  local out="$OUT/frontend-${app}"
  mkdir -p "$out"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/standalone/" "$out/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/.next/static/" "$out/apps/${app}/.next/static/"
  rsync -a --delete "$FRONTEND_SRC/apps/${app}/public/" "$out/apps/${app}/public/"
  chown -R www-data:www-data "$out"
}
[[ "$DO_FRONTEND_INSTITUTION" == 1 ]] && install_frontend institution
[[ "$DO_FRONTEND_MEMBER" == 1 ]] && install_frontend member
[[ "$DO_FRONTEND_PLATFORM" == 1 ]] && install_frontend platform
lap "copy builds into place"

# Start everything together. The APIs apply EF migrations on boot behind an advisory lock, so
# starting them at once is safe; only the health checks are sequential.
echo "== starting: ${STOP_LIST[*]} =="
systemctl start "${STOP_LIST[@]}"

if [[ "$DO_INSTITUTION_API" == 1 ]]; then
  wait_healthy "http://127.0.0.1:5001/health" alumni-institution-api
fi

if [[ "$DO_MEMBER_API" == 1 ]]; then
  wait_healthy "http://127.0.0.1:5002/health" alumni-member-api
fi

if [[ "$DO_PLATFORM_API" == 1 ]]; then
  wait_healthy "http://127.0.0.1:5003/health" alumni-platform-api
fi

if [[ "$DO_OPERATIONS_WORKER" == 1 ]]; then
  wait_active alumni-operations-worker
fi

if [[ "$DO_FRONTEND_INSTITUTION" == 1 ]]; then
  wait_healthy "http://127.0.0.1:3001/" alumni-frontend-institution
fi

if [[ "$DO_FRONTEND_MEMBER" == 1 ]]; then
  wait_healthy "http://127.0.0.1:3002/" alumni-frontend-member
fi

if [[ "$DO_FRONTEND_PLATFORM" == 1 ]]; then
  wait_healthy "http://127.0.0.1:3003/" alumni-frontend-platform
fi

lap "services healthy"

# Remember what is live, so the next `all` can skip whatever did not change. Only a full ("all")
# deploy records it: a single-service deploy leaves the other parts as they were, so they must not be
# marked as up to date.
if [[ "$TARGET" == "all" ]]; then
  git -C "$SRC_DIR" rev-parse HEAD > "$REV_FILE"
fi
echo "Deploy complete (target: $TARGET) in $(( SECONDS - STARTED_AT ))s."

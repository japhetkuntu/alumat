#!/usr/bin/env bash
# Server side of the fast deploy. GitHub Actions has already BUILT the services and copied the finished builds into
# /var/www/alumunion/.staging; this only swaps them in and restarts, which takes seconds to a minute. Nothing is
# compiled on the server.
#
# Usage:  sudo bash /opt/alumunion-src/deploy/activate.sh <commit-sha> <service>...
# Services: institution-api member-api platform-api operations-worker frontend-institution frontend-member frontend-platform
#
# deploy.sh (build on the server) is still there as a fallback and does the same swap after building.
set -euo pipefail

SRC_DIR="/opt/alumunion-src"
OUT="/var/www/alumunion"
STAGE="$OUT/.staging"
REV_FILE="$OUT/.last-deployed-rev"

SHA="${1:?usage: activate.sh <commit-sha> <service>...}"; shift
SERVICES=("$@")
[[ ${#SERVICES[@]} -gt 0 ]] || { echo "Nothing to activate."; exit 0; }

# Pull the repo first so this script, the unit files and the maintenance page are current, then run the new copy.
if [[ -z "${ALUMNI_ACTIVATE_REEXECED:-}" ]]; then
  git -C "$SRC_DIR" pull --ff-only
  export ALUMNI_ACTIVATE_REEXECED=1
  exec bash "$SRC_DIR/deploy/activate.sh" "$SHA" "${SERVICES[@]}"
fi

STARTED_AT=$SECONDS
declare -A UNIT=(
  [institution-api]=alumni-institution-api [member-api]=alumni-member-api [platform-api]=alumni-platform-api
  [operations-worker]=alumni-operations-worker
  [frontend-institution]=alumni-frontend-institution [frontend-member]=alumni-frontend-member [frontend-platform]=alumni-frontend-platform
)
declare -A HEALTH=(
  [institution-api]=http://127.0.0.1:5001/health [member-api]=http://127.0.0.1:5002/health [platform-api]=http://127.0.0.1:5003/health
  [frontend-institution]=http://127.0.0.1:3001/ [frontend-member]=http://127.0.0.1:3002/ [frontend-platform]=http://127.0.0.1:3003/
)

STOP_LIST=()
for s in "${SERVICES[@]}"; do
  [[ -n "${UNIT[$s]:-}" ]] || { echo "Unknown service: $s"; exit 1; }
  [[ -d "$STAGE/$s" ]] || { echo "No finished build for $s in $STAGE. The build step did not copy it."; exit 1; }
  STOP_LIST+=("${UNIT[$s]}")
done

mkdir -p "$OUT/maintenance"
cp "$SRC_DIR/deploy/maintenance/index.html" "$OUT/maintenance/index.html"
chown -R www-data:www-data "$OUT/maintenance"

echo "== stopping ${STOP_LIST[*]} =="
systemctl stop "${STOP_LIST[@]}" 2>/dev/null || true

echo "== copying the new builds in =="
for s in "${SERVICES[@]}"; do
  mkdir -p "$OUT/$s"
  rsync -a --delete --chown=www-data:www-data "$STAGE/$s/" "$OUT/$s/"
done

echo "== starting ${STOP_LIST[*]} =="
systemctl start "${STOP_LIST[@]}"
# Recorded as soon as the new version is installed, so a slow start never makes the next deploy rebuild it.
echo "$SHA" > "$REV_FILE"

wait_healthy() {
  local url="$1" name="$2" tries=90
  echo "Waiting for $name ..."
  for ((i = 1; i <= tries; i++)); do
    if curl -fs --connect-timeout 2 --max-time 3 "$url" >/dev/null 2>&1; then echo "$name is healthy."; return 0; fi
    sleep 2
  done
  echo "$name did not become healthy after $((tries * 2))s. It may still be starting: journalctl -u $name -n 100 --no-pager"
  return 1
}
FAILED=0
for s in "${SERVICES[@]}"; do
  if [[ -n "${HEALTH[$s]:-}" ]]; then wait_healthy "${HEALTH[$s]}" "${UNIT[$s]}" || FAILED=1; fi
done
if [[ " ${SERVICES[*]} " == *" operations-worker "* ]]; then
  sleep 4
  systemctl is-active --quiet alumni-operations-worker && echo "alumni-operations-worker is active." || { echo "alumni-operations-worker is not active: journalctl -u alumni-operations-worker -n 100 --no-pager"; FAILED=1; }
fi

echo "Activated ${SERVICES[*]} at ${SHA:0:8} in $(( SECONDS - STARTED_AT ))s."
exit "$FAILED"

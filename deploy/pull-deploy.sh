#!/usr/bin/env bash
# THE way to deploy. Nothing is compiled on the server: GitHub builds on push (the "Build" workflow publishes the finished
# builds on the deploy-builds branch) and this script pulls them, then stops, swaps and starts only the services whose
# build changed. If a service does not come up healthy, it is put back to the version it had before.
#
# Usage:
#   sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --wait     # just pushed: wait for GitHub to finish building, then deploy
#   sudo bash /opt/alumunion-src/deploy/pull-deploy.sh            # deploy whatever is already built
#   sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --status   # show what is live and what is available; changes nothing
#   sudo bash /opt/alumunion-src/deploy/pull-deploy.sh member-api frontend-member    # only these services
#
# Services: institution-api member-api platform-api operations-worker frontend-institution frontend-member frontend-platform
set -euo pipefail

SRC_DIR="/opt/alumunion-src"
BUILDS="${BUILDS_DIR:-/opt/alumunion-builds}"
OUT="/var/www/alumunion"
PREVIOUS="$OUT/.previous"
BUILDS_REF="refs/heads/deploy-builds"
WAIT_MINUTES="${WAIT_MINUTES:-25}"

WAIT=0; STATUS=0
ONLY=()
for arg in "$@"; do
  case "$arg" in
    --wait) WAIT=1 ;;
    --status) STATUS=1 ;;
    -*) echo "Unknown option: $arg"; exit 1 ;;
    *) ONLY+=("$arg") ;;
  esac
done

# Pull the repo first so this script, the unit files and the maintenance page are current, then run the new copy.
if [[ -z "${ALUMNI_PULL_DEPLOY_REEXECED:-}" ]]; then
  git -C "$SRC_DIR" pull --ff-only
  export ALUMNI_PULL_DEPLOY_REEXECED=1
  exec bash "$SRC_DIR/deploy/pull-deploy.sh" "$@"
fi

# Two deploys at once would fight over the same folders and services.
if [[ $STATUS == 0 ]]; then
  exec 9>/var/lock/alumni-deploy.lock
  flock -n 9 || { echo "Another deploy is already running. Wait for it to finish."; exit 1; }
fi

STARTED_AT=$SECONDS
ALL=(institution-api member-api platform-api operations-worker frontend-institution frontend-member frontend-platform)
declare -A UNIT=(
  [institution-api]=alumni-institution-api [member-api]=alumni-member-api [platform-api]=alumni-platform-api
  [operations-worker]=alumni-operations-worker
  [frontend-institution]=alumni-frontend-institution [frontend-member]=alumni-frontend-member [frontend-platform]=alumni-frontend-platform
)
declare -A HEALTH=(
  [institution-api]=http://127.0.0.1:5001/health [member-api]=http://127.0.0.1:5002/health [platform-api]=http://127.0.0.1:5003/health
  [frontend-institution]=http://127.0.0.1:3001/ [frontend-member]=http://127.0.0.1:3002/ [frontend-platform]=http://127.0.0.1:3003/
)

CANDIDATES=("${ALL[@]}")
if [[ ${#ONLY[@]} -gt 0 ]]; then
  for s in "${ONLY[@]}"; do [[ -n "${UNIT[$s]:-}" ]] || { echo "Unknown service: $s"; exit 1; }; done
  CANDIDATES=("${ONLY[@]}")
fi

# A tiny local repository that only ever holds the latest builds.
if [[ ! -d "$BUILDS/.git" ]]; then
  mkdir -p "$BUILDS"
  git -C "$BUILDS" init -q
  git -C "$BUILDS" remote add origin "$(git -C "$SRC_DIR" remote get-url origin)"
fi

fetch_builds() {
  git -C "$BUILDS" fetch -q --depth 1 origin "+$BUILDS_REF:refs/builds/latest" 2>/dev/null || return 1
  git -C "$BUILDS" checkout -q -f --detach refs/builds/latest
  git -C "$BUILDS" clean -fdxq
}

short() { local f="$1"; if [[ -f "$f" ]]; then cut -c1-8 "$f"; else echo "none"; fi; }

# A service is out of date when its build here carries a different commit marker than the one installed.
stale_services() {
  local s
  for s in "${CANDIDATES[@]}"; do
    [[ -f "$BUILDS/$s/.built-sha" ]] || continue
    cmp -s "$BUILDS/$s/.built-sha" "$OUT/$s/.built-sha" 2>/dev/null || echo "$s"
  done
}

print_table() {
  printf '%-24s %-10s %-10s %s\n' SERVICE LIVE AVAILABLE STATE
  local s state
  for s in "${CANDIDATES[@]}"; do
    if [[ ! -f "$BUILDS/$s/.built-sha" ]]; then state="no build yet"
    elif cmp -s "$BUILDS/$s/.built-sha" "$OUT/$s/.built-sha" 2>/dev/null; then state="up to date"
    else state="NEW BUILD"; fi
    printf '%-24s %-10s %-10s %s\n' "$s" "$(short "$OUT/$s/.built-sha")" "$(short "$BUILDS/$s/.built-sha")" "$state"
  done
}

echo "== getting the latest builds =="
DEADLINE=$(( SECONDS + WAIT_MINUTES * 60 ))
SERVICES=()
while true; do
  if fetch_builds; then
    if [[ $STATUS == 1 ]]; then print_table; exit 0; fi
    mapfile -t SERVICES < <(stale_services)
    [[ ${#SERVICES[@]} -gt 0 ]] && break
  else
    [[ $STATUS == 1 ]] && { echo "No builds published yet (the Build workflow has not run successfully)."; exit 0; }
    echo "No builds published yet (the Build workflow has not run successfully)."
  fi
  if [[ $WAIT == 1 && $SECONDS -lt $DEADLINE ]]; then
    echo "Nothing new yet. Waiting for GitHub to finish building (checking every 20s, up to ${WAIT_MINUTES} min) ..."
    sleep 20
    continue
  fi
  echo "Nothing new to deploy: the server already has the latest builds."
  [[ $WAIT == 0 ]] && echo "(Just pushed? Run with --wait, or check the Build workflow in the Actions tab.)"
  exit 0
done

echo
print_table
echo

# Keep the local copy from growing: old downloaded builds are not needed.
if [[ "$(du -sm "$BUILDS/.git" | cut -f1)" -gt 1500 ]]; then
  git -C "$BUILDS" reflog expire --expire=now --all
  git -C "$BUILDS" gc --prune=now -q
fi

STOP_LIST=()
for s in "${SERVICES[@]}"; do STOP_LIST+=("${UNIT[$s]}"); done

mkdir -p "$OUT/maintenance" "$PREVIOUS"
cp "$SRC_DIR/deploy/maintenance/index.html" "$OUT/maintenance/index.html"
chown -R www-data:www-data "$OUT/maintenance"

# Keep what is live now, so a bad build can be put back.
for s in "${SERVICES[@]}"; do
  if [[ -d "$OUT/$s" ]]; then mkdir -p "$PREVIOUS/$s"; rsync -a --delete "$OUT/$s/" "$PREVIOUS/$s/"; else rm -rf "${PREVIOUS:?}/$s"; fi
done

echo "== stopping ${STOP_LIST[*]} =="
systemctl stop "${STOP_LIST[@]}" 2>/dev/null || true

echo "== copying the new builds in =="
for s in "${SERVICES[@]}"; do
  mkdir -p "$OUT/$s"
  rsync -a --delete --chown=www-data:www-data "$BUILDS/$s/" "$OUT/$s/"
done

echo "== starting ${STOP_LIST[*]} =="
systemctl start "${STOP_LIST[@]}"

wait_healthy() {
  local url="$1" name="$2" tries="${3:-90}"
  echo "Waiting for $name ..."
  for ((i = 1; i <= tries; i++)); do
    if curl -fs --connect-timeout 2 --max-time 3 "$url" >/dev/null 2>&1; then echo "$name is healthy."; return 0; fi
    sleep 2
  done
  echo "$name did not become healthy after $((tries * 2))s."
  return 1
}

worker_ok() { sleep 4; systemctl is-active --quiet alumni-operations-worker; }

# Puts one service back to the version it had before this deploy.
roll_back() {
  local s="$1"
  if [[ ! -d "$PREVIOUS/$s" ]]; then echo "No earlier version of $s to go back to. Look at: journalctl -u ${UNIT[$s]} -n 100 --no-pager"; return; fi
  echo "== rolling $s back to its previous version =="
  systemctl stop "${UNIT[$s]}" 2>/dev/null || true
  rsync -a --delete "$PREVIOUS/$s/" "$OUT/$s/"
  systemctl start "${UNIT[$s]}"
  if [[ -n "${HEALTH[$s]:-}" ]]; then wait_healthy "${HEALTH[$s]}" "${UNIT[$s]}" 45 || true; fi
}

FAILED=()
for s in "${SERVICES[@]}"; do
  if [[ -n "${HEALTH[$s]:-}" ]]; then
    wait_healthy "${HEALTH[$s]}" "${UNIT[$s]}" || FAILED+=("$s")
  elif ! worker_ok; then
    echo "${UNIT[$s]} is not active."; FAILED+=("$s")
  else
    echo "${UNIT[$s]} is active."
  fi
done

if [[ ${#FAILED[@]} -gt 0 ]]; then
  echo
  echo "!! ${FAILED[*]} did not start properly with the new build."
  for s in "${FAILED[@]}"; do roll_back "$s"; done
  echo
  echo "Rolled back: ${FAILED[*]}. The others were deployed. Database changes the new version already made are not undone."
  for s in "${FAILED[@]}"; do echo "Read why: journalctl -u ${UNIT[$s]} -n 100 --no-pager"; done
  exit 1
fi

echo
echo "Deployed ${SERVICES[*]} in $(( SECONDS - STARTED_AT ))s."

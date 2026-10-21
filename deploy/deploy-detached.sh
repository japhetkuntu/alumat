#!/usr/bin/env bash
# Runs deploy.sh in the background on the server, so closing the terminal, a dropped SSH session or pressing Ctrl-C
# does NOT stop the deploy. You only watch it. A long first build after a big change can take a while on a small server;
# with this you never need to sit through it, stop it and start it again.
#
# Usage (same targets and options as deploy.sh):
#   sudo bash /opt/alumunion-src/deploy/deploy-detached.sh            # everything that changed
#   sudo bash /opt/alumunion-src/deploy/deploy-detached.sh backend
#   sudo env FORCE_ALL=1 bash /opt/alumunion-src/deploy/deploy-detached.sh
#   sudo bash /opt/alumunion-src/deploy/deploy-detached.sh --log      # watch a deploy that is already running
#   sudo bash /opt/alumunion-src/deploy/deploy-detached.sh --status   # is one running, and how did the last one end
#
# Ctrl-C here only stops WATCHING. Everything is also written to /var/log/alumunion-deploy.log.
set -euo pipefail

UNIT="alumni-deploy"
LOG="/var/log/alumunion-deploy.log"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

running() { systemctl is-active --quiet "$UNIT.service"; }

watch_log() {
  echo "Watching $LOG (Ctrl-C stops watching, not the deploy)."
  tail -n 40 -F "$LOG" &
  local tail_pid=$!
  trap 'kill "$tail_pid" 2>/dev/null; echo; echo "Stopped watching. The deploy keeps running: sudo bash $0 --log"; exit 0' INT TERM
  while running; do sleep 2; done
  sleep 1
  kill "$tail_pid" 2>/dev/null || true
  trap - INT TERM
  local result
  result="$(systemctl show -p Result --value "$UNIT.service" 2>/dev/null || echo unknown)"
  echo
  if [[ "$result" == "success" ]]; then
    echo "Deploy finished successfully."
  else
    echo "Deploy ended with result: $result. Read the log above, fix it, and run this again."
    echo "Services already built for this commit are reused, so the next run carries on from there."
    exit 1
  fi
}

case "${1:-}" in
  --log) watch_log; exit 0 ;;
  --status)
    if running; then echo "A deploy is running now. Watch it: sudo bash $0 --log"; else
      echo "No deploy running. Last result: $(systemctl show -p Result --value "$UNIT.service" 2>/dev/null || echo unknown)"
      [[ -f "$LOG" ]] && tail -n 5 "$LOG"
    fi
    exit 0 ;;
esac

if running; then
  echo "A deploy is already running, so I will not start another."
  watch_log
  exit 0
fi

command -v systemd-run >/dev/null || { echo "systemd-run not found. Run deploy.sh directly instead."; exit 1; }

# A unit from a finished earlier run is cleared so the name can be reused.
systemctl reset-failed "$UNIT.service" 2>/dev/null || true
: > "$LOG"

# The options deploy.sh understands are passed through; HOME and PATH are needed by dotnet, pnpm and node.
ENV_ARGS=(-E "HOME=${HOME:-/root}" -E "PATH=$PATH")
for v in FORCE_ALL CLEAN_BUILD STOP_FIRST MIN_FREE_GB DOTNET_BUILD_JOBS; do
  [[ -n "${!v:-}" ]] && ENV_ARGS+=(-E "$v=${!v}")
done

echo "Starting the deploy in the background (target: ${1:-all})."
systemd-run --unit="$UNIT" --description="Alumni Portal deploy" \
  --property="StandardOutput=append:$LOG" --property="StandardError=append:$LOG" \
  --property="TimeoutStopSec=30" "${ENV_ARGS[@]}" \
  bash "$HERE/deploy.sh" "$@" >/dev/null

watch_log

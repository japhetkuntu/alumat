#!/usr/bin/env bash
# Manually show the maintenance page (deploy/maintenance/index.html) for a
# longer window than a normal deploy needs — e.g. before a slow migration,
# or a manual database change. deploy.sh's own stop/publish/start gap
# already shows this page automatically (Nginx's error_page for 502/503/504
# — see deploy/nginx.conf), so you don't need this for a routine deploy.
#
# Usage:
#   sudo bash /opt/alumunion-src/deploy/toggle-maintenance.sh on
#   sudo bash /opt/alumunion-src/deploy/toggle-maintenance.sh off
set -euo pipefail

FLAG=/var/www/alumunion/maintenance/enabled

case "${1:-}" in
  on)
    mkdir -p "$(dirname "$FLAG")"
    touch "$FLAG"
    echo "Maintenance page is now ON — every request gets deploy/maintenance/index.html."
    ;;
  off)
    rm -f "$FLAG"
    echo "Maintenance page is now OFF — normal traffic resumes."
    ;;
  *)
    echo "Usage: $0 on|off"
    exit 1
    ;;
esac

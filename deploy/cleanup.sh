#!/usr/bin/env bash
# Wipes a previous Alumni Portal deployment attempt off this droplet so install.sh
# can start clean. Run this once before re-running install.sh if you've deployed
# before and want a genuine fresh start. Modeled on WishDem's cleanup.sh.
#
# This DROPS THE POSTGRES DATABASE AND ROLE and FLUSHES REDIS — only run this if
# there's no real data on the droplet worth keeping.
#
# IMPORTANT: this script deletes /opt/alumunion-src, which is where it lives if
# you cloned the repo — run it from a copy outside that tree instead:
#   curl -o /tmp/cleanup.sh https://raw.githubusercontent.com/<you>/<repo>/main/deploy/cleanup.sh
#   sudo bash /tmp/cleanup.sh
set -euo pipefail

echo "== stopping/disabling services =="
systemctl stop alumni-institution-api alumni-member-api alumni-platform-api \
  alumni-frontend-institution alumni-frontend-member alumni-frontend-platform 2>/dev/null || true
systemctl disable alumni-institution-api alumni-member-api alumni-platform-api \
  alumni-frontend-institution alumni-frontend-member alumni-frontend-platform 2>/dev/null || true
rm -f /etc/systemd/system/alumni-institution-api.service /etc/systemd/system/alumni-member-api.service \
  /etc/systemd/system/alumni-platform-api.service /etc/systemd/system/alumni-frontend-institution.service \
  /etc/systemd/system/alumni-frontend-member.service /etc/systemd/system/alumni-frontend-platform.service
systemctl daemon-reload

echo "== removing Nginx site =="
rm -f /etc/nginx/sites-enabled/alumunion /etc/nginx/sites-available/alumunion
systemctl reload nginx 2>/dev/null || true

echo "== removing published apps + logs =="
rm -rf /var/www/alumunion

echo "== removing env files (JWT keys, API tokens, etc — you'll re-fill these) =="
rm -rf /etc/alumunion

echo "== removing source checkout =="
rm -rf /opt/alumunion-src

echo "== dropping the alumni Postgres role/database =="
sudo -u postgres psql -v ON_ERROR_STOP=1 <<'SQL'
DROP DATABASE IF EXISTS "AlumniDb";
DROP ROLE IF EXISTS alumni;
SQL

echo "== flushing Redis =="
redis-cli FLUSHALL 2>/dev/null || true

cat <<'EOF'

Cleanup done. The droplet is back to a bare state (dependencies like .NET, Node,
Postgres, Redis, Nginx, and Certbot are still installed — only the Alumni Portal
app/config/data was removed). TLS certificates under /etc/letsencrypt were left
alone — remove them yourself with `certbot delete` if you also want those gone.
Run install.sh next for a fresh setup.
EOF

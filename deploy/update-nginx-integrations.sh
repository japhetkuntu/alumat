#!/usr/bin/env bash
# One-time fix for the live Nginx config: exempts the partner status check (/api/v1/integrations/...) from the per-IP request
# limit. A school's server sends every call from one IP, so a bulk run (a whole graduating class) would be cut off by Nginx
# with 429 before the API's own per-API-key limit ever applied. The API still limits per key; this only removes the Nginx
# request cap on that one path and raises its connection cap.
#
# deploy.sh never rewrites the live Nginx config, so apply this by hand on the server, once:
#   sudo bash /opt/alumunion-src/deploy/update-nginx-integrations.sh
#
# Safe to run twice. It backs up the live file, tests the new config with `nginx -t`, and only then reloads. If the test
# fails it puts the backup back and changes nothing.
set -euo pipefail

LIVE="${NGINX_LIVE_CONF:-/etc/nginx/sites-available/alumunion}"
[[ -f "$LIVE" ]] || { echo "Cannot find $LIVE. Set NGINX_LIVE_CONF=/path/to/your/config and run again."; exit 1; }

if grep -q '/integrations/' "$LIVE"; then
  echo "Already updated: $LIVE has the integrations location."
  exit 0
fi

BACKUP="${LIVE}.bak-$(date +%Y%m%d-%H%M%S)"
cp "$LIVE" "$BACKUP"
echo "Backed up to $BACKUP"

# Put the new location just before the institution API's general "location /api/ {" (the one proxying to port 5001).
TMP="$(mktemp)"
awk '
  { line[NR] = $0 }
  END {
    target = 0
    for (i = 1; i <= NR; i++)
      if (line[i] ~ /^[ \t]*location \/api\/ \{/ && (line[i+1] line[i+2] line[i+3] line[i+4]) ~ /127\.0\.0\.1:5001/) { target = i; break }
    if (!target) exit 3
    for (i = 1; i <= NR; i++) {
      if (i == target) {
        print "\t# Partner status check: called by a school'"'"'s own server from one IP. The API limits per API key instead."
        print "\tlocation ~ ^/api/v\\d+/integrations/ {"
        print "\t\tlimit_conn conn_per_ip 1000;"
        print "\t\tproxy_pass http://127.0.0.1:5001;"
        print "\t\tproxy_http_version 1.1;"
        print "\t\tproxy_set_header Host $host;"
        print "\t\tproxy_set_header X-Real-IP $remote_addr;"
        print "\t\tproxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;"
        print "\t\tproxy_set_header X-Forwarded-Proto $scheme;"
        print "\t}"
        print ""
      }
      print line[i]
    }
  }
' "$LIVE" > "$TMP" || { rm -f "$TMP"; echo "Did not find the institution API's 'location /api/' block (proxy to 127.0.0.1:5001) in $LIVE, so nothing was changed. Copy the integrations location from deploy/nginx.ssl.conf by hand."; exit 1; }

cat "$TMP" > "$LIVE"
rm -f "$TMP"

if nginx -t; then
  systemctl reload nginx
  echo "Updated and reloaded. The partner status check no longer hits the per-IP request limit."
else
  cp "$BACKUP" "$LIVE"
  echo "nginx -t failed, so the original config was put back and nothing was reloaded."
  exit 1
fi

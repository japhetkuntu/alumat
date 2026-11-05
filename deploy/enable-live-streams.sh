#!/usr/bin/env bash
# One-time update for an EXISTING server so many members can keep the live chat connection open at once. New installs get all of
# this from the templates; deploy.sh never rewrites the live Nginx config or system limits, so apply it by hand, once:
#   sudo bash /opt/alumunion-src/deploy/enable-live-streams.sh
#
# What it does (safe to run twice; each step is skipped if already done):
#   1. Nginx site: a separate connection counter and location for /api/v1/introductions/stream, so streams do not use up the ordinary
#      100-connections-per-address limit (everyone behind one school or phone-network address shares it), and are never buffered.
#   2. Nginx workers: more connections per worker and open files. Each stream is two connections (browser and API) in Nginx.
#   3. The Member API service: 65536 open files instead of the usual 1024, so it does not stop accepting at ~1000 members online.
#
# It backs up what it changes, tests the Nginx config with `nginx -t`, and only then reloads. If the test fails it puts the backups back.
# Restart the Member API afterwards for the new file limit to apply:   sudo systemctl restart alumni-member-api
set -euo pipefail

LIVE="${NGINX_LIVE_CONF:-/etc/nginx/sites-available/alumunion}"
MAIN="${NGINX_MAIN_CONF:-/etc/nginx/nginx.conf}"
UNIT_DIR="/etc/systemd/system/alumni-member-api.service.d"
[[ -f "$LIVE" ]] || { echo "Cannot find $LIVE. Set NGINX_LIVE_CONF=/path/to/your/config and run again."; exit 1; }
[[ -f "$MAIN" ]] || { echo "Cannot find $MAIN. Set NGINX_MAIN_CONF=/path/to/nginx.conf and run again."; exit 1; }
command -v python3 >/dev/null || { echo "python3 is needed for this script (apt install python3)."; exit 1; }

STAMP="$(date +%Y%m%d-%H%M%S)"
cp "$LIVE" "${LIVE}.bak-${STAMP}"; cp "$MAIN" "${MAIN}.bak-${STAMP}"
echo "Backed up to ${LIVE}.bak-${STAMP} and ${MAIN}.bak-${STAMP}"

python3 - "$LIVE" "$MAIN" <<'PY'
import re, sys
live, main = sys.argv[1], sys.argv[2]

# 1. site config
s = open(live).read()
if 'stream_per_ip' in s:
    print("site: already has the stream location")
else:
    s = s.replace('limit_conn_zone $binary_remote_addr zone=conn_per_ip:10m;',
        'limit_conn_zone $binary_remote_addr zone=conn_per_ip:10m;\n# Separate counter for the live-notice streams, so they cannot crowd out ordinary requests from the same address.\nlimit_conn_zone $binary_remote_addr zone=stream_per_ip:10m;', 1)
    location = '''\t# Live introduction notices: one long-lived connection per signed-in member, with its own connection counter, no request-rate
\t# limit, and no buffering. The API sends a heartbeat every 25 seconds, well inside the 90-second read timeout.
\tlocation = /api/v1/introductions/stream {
\t\tlimit_conn stream_per_ip 2000;
\t\tproxy_pass http://127.0.0.1:5002;
\t\tproxy_http_version 1.1;
\t\tproxy_set_header Connection "";
\t\tproxy_set_header Host $host;
\t\tproxy_set_header X-Real-IP $remote_addr;
\t\tproxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
\t\tproxy_set_header X-Forwarded-Proto $scheme;
\t\tproxy_buffering off;
\t\tproxy_cache off;
\t\tgzip off;
\t\tproxy_read_timeout 90s;
\t\tproxy_send_timeout 90s;
\t}
'''
    lines = s.split('\n'); out = []; placed = 0
    for i, l in enumerate(lines):
        if re.match(r'^\s*location /api/ \{', l) and '127.0.0.1:5002' in '\n'.join(lines[i:i + 6]):
            out.append(location); placed += 1
        out.append(l)
    if not placed:
        sys.exit("site: did not find the member API's 'location /api/' block (proxy to 127.0.0.1:5002), so nothing was changed. Copy the stream location from deploy/nginx.ssl.conf by hand.")
    open(live, 'w').write('\n'.join(out))
    print(f"site: added the stream location in front of {placed} member API block(s)")

# 2. nginx workers
m = open(main).read()
changed = False
if re.search(r'^\s*worker_rlimit_nofile\s', m, re.M) is None:
    m = re.sub(r'^(\s*worker_processes[^\n]*\n)', r'\1worker_rlimit_nofile 65536;\n', m, count=1, flags=re.M); changed = True
cur = re.search(r'^\s*worker_connections\s+(\d+);', m, re.M)
if cur and int(cur.group(1)) < 8192:
    m = re.sub(r'^(\s*)worker_connections\s+\d+;', r'\1worker_connections 8192;', m, count=1, flags=re.M); changed = True
if changed:
    open(main, 'w').write(m); print("nginx: raised worker_rlimit_nofile and worker_connections")
else:
    print("nginx: worker limits already high enough")
PY

if nginx -t; then
  systemctl reload nginx
  echo "Nginx reloaded."
else
  echo "nginx -t failed: restoring the backups."
  cp "${LIVE}.bak-${STAMP}" "$LIVE"; cp "${MAIN}.bak-${STAMP}" "$MAIN"
  exit 1
fi

# 3. Member API file limit
mkdir -p "$UNIT_DIR"
if [[ -f "$UNIT_DIR/streams.conf" ]]; then
  echo "service: file limit already set"
else
  printf '[Service]\nLimitNOFILE=65536\n' > "$UNIT_DIR/streams.conf"
  systemctl daemon-reload
  echo "service: set LimitNOFILE=65536 for alumni-member-api"
fi
echo
echo "Done. Now restart the Member API so the new limit applies:   sudo systemctl restart alumni-member-api"
echo "Check it afterwards:   cat /proc/\$(systemctl show -p MainPID --value alumni-member-api)/limits | grep 'open files'"

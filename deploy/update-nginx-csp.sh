#!/usr/bin/env bash
# One-time fix for the live Nginx config: lets the portals show resource previews (PDF, Word/PowerPoint/Excel, YouTube,
# video, audio) that come from the file storage CDN. The Content-Security-Policy only allowed frames from Google, so the
# browser silently blocked every preview frame and people had to download the file instead.
#
# deploy.sh never rewrites the live Nginx config, so apply this by hand on the server, once:
#   sudo bash /opt/alumunion-src/deploy/update-nginx-csp.sh
#
# Safe to run twice. It backs up the live file, tests the new config with `nginx -t`, and only then reloads. If the test
# fails it puts the backup back and changes nothing.
set -euo pipefail

LIVE="${NGINX_LIVE_CONF:-/etc/nginx/sites-available/alumunion}"
[[ -f "$LIVE" ]] || { echo "Cannot find $LIVE. Set NGINX_LIVE_CONF=/path/to/your/config and run again."; exit 1; }

if grep -q "view.officeapps.live.com" "$LIVE"; then
  echo "Already updated: $LIVE allows preview frames."
  exit 0
fi

OLD_FRAME='frame-src https://accounts.google.com;'
grep -q "$OLD_FRAME" "$LIVE" || { echo "Did not find '$OLD_FRAME' in $LIVE, so there is nothing safe to change automatically."; echo "Add the hosts in deploy/nginx.ssl.conf's Content-Security-Policy by hand."; exit 1; }

BACKUP="${LIVE}.bak-$(date +%Y%m%d-%H%M%S)"
cp "$LIVE" "$BACKUP"
echo "Backed up to $BACKUP"

# Frames: Google sign-in (as before), YouTube, Google's PDF viewer for phones, Microsoft's Office viewer, and the file storage.
# Media: video and audio files from the file storage. Both are limited to named hosts, not "any https site".
NEW="media-src 'self' https://*.digitaloceanspaces.com; frame-src https://accounts.google.com https://www.youtube-nocookie.com https://www.youtube.com https://docs.google.com https://view.officeapps.live.com https://*.digitaloceanspaces.com;"
sed -i "s#${OLD_FRAME}#${NEW}#g" "$LIVE"

if nginx -t; then
  systemctl reload nginx
  echo "Updated and reloaded. Resource previews can load now (hard refresh the page once)."
else
  cp "$BACKUP" "$LIVE"
  echo "nginx -t failed, so the original config was put back and nothing was reloaded."
  exit 1
fi

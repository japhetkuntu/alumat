#!/usr/bin/env bash
# One-time droplet provisioning for a bare-metal (no containers) Alumni Portal
# deploy: 3 .NET APIs + 3 Next.js frontends, all behind one Nginx, with real
# per-institution wildcard subdomains. Modeled directly on WishDem's
# backend/deploy/install.sh — same philosophy, extended to cover the frontend
# apps and the wildcard routing this project needs.
#
# Read this before running it — meant to be run as root (or via sudo) on a
# fresh Ubuntu 22.04/24.04 droplet.
#
# Usage: sudo bash install.sh
# Can be scripted non-interactively by exporting REPO_URL, PLATFORM_BASE_DOMAIN,
# ADMIN_BASE_DOMAIN, PLATFORM_SUBDOMAIN and ACME_EMAIL beforehand (-E to
# preserve them under sudo).
set -euo pipefail

# ---------------------------------------------------------------------------
# 0. Fill these in before running, or export them beforehand (see note above).
# ---------------------------------------------------------------------------
REPO_URL="${REPO_URL:-<your-git-repo-url>}"
SRC_DIR="/opt/alumunion-src"

if [[ "$REPO_URL" == "<your-git-repo-url>" ]]; then
  echo "Set REPO_URL first: REPO_URL=git@github.com:you/alumunion.git sudo -E bash install.sh"
  exit 1
fi

read -rp "Member portal base domain, no slug/scheme (e.g. alumunion.com) [${PLATFORM_BASE_DOMAIN:-}]: " input
PLATFORM_BASE_DOMAIN="${input:-${PLATFORM_BASE_DOMAIN:-}}"
read -rp "Institution portal base domain (e.g. admin.alumunion.com) [${ADMIN_BASE_DOMAIN:-}]: " input
ADMIN_BASE_DOMAIN="${input:-${ADMIN_BASE_DOMAIN:-}}"
read -rp "Platform Portal fixed subdomain (e.g. platform.alumunion.com) [${PLATFORM_SUBDOMAIN:-}]: " input
PLATFORM_SUBDOMAIN="${input:-${PLATFORM_SUBDOMAIN:-}}"
read -rp "Email for Let's Encrypt renewal notices [${ACME_EMAIL:-}]: " input
ACME_EMAIL="${input:-${ACME_EMAIL:-}}"
unset input

if [[ -z "$PLATFORM_BASE_DOMAIN" || -z "$ADMIN_BASE_DOMAIN" || -z "$PLATFORM_SUBDOMAIN" || -z "$ACME_EMAIL" ]]; then
  echo "PLATFORM_BASE_DOMAIN, ADMIN_BASE_DOMAIN, PLATFORM_SUBDOMAIN and ACME_EMAIL are all required."
  exit 1
fi

echo "== 1/11: base packages =="
apt-get update
apt-get install -y curl wget gnupg apt-transport-https software-properties-common ca-certificates lsb-release git ufw rsync

echo "== 2/11: swap =="
# Six build jobs run on this droplet across a deploy (3x `dotnet publish` +
# 3x `next build`) — without swap, the smallest droplets get their compiler
# processes SIGKILLed by the OOM killer partway through. 4G is enough
# headroom regardless of droplet size, and idempotent: skipped if swap
# already exists.
if [[ "$(swapon --show | wc -l)" -eq 0 && ! -f /swapfile ]]; then
  fallocate -l 4G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "== 3/11: .NET 8 SDK (Microsoft package feed) =="
if ! command -v dotnet >/dev/null 2>&1; then
  UBUNTU_VERSION="$(lsb_release -rs)"
  wget "https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
  dpkg -i /tmp/packages-microsoft-prod.deb
  rm /tmp/packages-microsoft-prod.deb
  apt-get update
  apt-get install -y dotnet-sdk-8.0
fi
dotnet --version

echo "== 4/11: Node.js 20 + pnpm (NodeSource feed) =="
if ! command -v node >/dev/null 2>&1; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
  apt-get install -y nodejs
fi
node --version
corepack enable
corepack prepare pnpm@10.17.1 --activate
pnpm --version

echo "== 5/11: PostgreSQL =="
apt-get install -y postgresql postgresql-contrib
systemctl enable --now postgresql

echo "A Postgres role/database for the Alumni Portal will be created now."
read -rsp "Choose a password for the 'alumni' Postgres role (used in *-api.env later): " PG_PASSWORD
echo
sudo -u postgres psql -v ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'alumni') THEN
    CREATE ROLE alumni LOGIN PASSWORD '${PG_PASSWORD}';
  ELSE
    ALTER ROLE alumni WITH PASSWORD '${PG_PASSWORD}';
  END IF;
END
\$\$;
SELECT 'CREATE DATABASE "AlumniDb" OWNER alumni'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'AlumniDb')\gexec
SQL
echo "Remember this password — put it in the ConnectionStrings__AlumniConnection line of all three *-api.env files."
echo "(The 'alumni' schema itself is created automatically by EF Core's Initial migration on first boot — no manual CREATE SCHEMA needed.)"

echo "== 6/11: Redis (localhost only) =="
apt-get install -y redis-server
sed -i 's/^# *bind .*/bind 127.0.0.1 -::1/' /etc/redis/redis.conf
sed -i 's/^bind .*/bind 127.0.0.1 -::1/' /etc/redis/redis.conf
sed -i 's/^protected-mode .*/protected-mode yes/' /etc/redis/redis.conf
systemctl enable --now redis-server
systemctl restart redis-server

echo "== 7/11: Nginx + Certbot =="
apt-get install -y nginx certbot python3-certbot-nginx python3-certbot-dns-digitalocean
rm -f /etc/nginx/sites-enabled/default
systemctl enable --now nginx

echo "== 8/11: app directories =="
# Runs as www-data (the user Nginx already runs as) rather than a dedicated
# custom user — no home directory quirks to work around.
for d in institution-api member-api platform-api frontend-institution frontend-member frontend-platform; do
  mkdir -p "/var/www/alumunion/${d}/logs"
done
chown -R www-data:www-data /var/www/alumunion
mkdir -p /etc/alumunion
chmod 700 /etc/alumunion

echo "== 9/11: clone source, install service/proxy config =="
if [[ ! -d "$SRC_DIR/.git" ]]; then
  git clone "$REPO_URL" "$SRC_DIR"
fi

for f in alumni-institution-api alumni-member-api alumni-platform-api \
         alumni-frontend-institution alumni-frontend-member alumni-frontend-platform; do
  install -m 644 "$SRC_DIR/deploy/${f}.service" "/etc/systemd/system/${f}.service"
done

sed \
  -e "s/__PLATFORM_BASE_DOMAIN__/${PLATFORM_BASE_DOMAIN}/g" \
  -e "s/__ADMIN_BASE_DOMAIN__/${ADMIN_BASE_DOMAIN}/g" \
  -e "s/__PLATFORM_SUBDOMAIN__/${PLATFORM_SUBDOMAIN}/g" \
  "$SRC_DIR/deploy/nginx.conf" > /etc/nginx/sites-available/alumunion
ln -sf /etc/nginx/sites-available/alumunion /etc/nginx/sites-enabled/alumunion
nginx -t

for f in institution-api member-api platform-api; do
  target="/etc/alumunion/${f}.env"
  if [[ ! -f "$target" ]]; then
    install -m 600 "$SRC_DIR/deploy/${f}.env.example" "$target"
    echo "Created $target from the example — edit it before starting services."
  fi
done

systemctl daemon-reload
# `WantedBy=multi-user.target` only takes effect once explicitly enabled —
# without this, services would work right after install.sh but silently not
# come back on a droplet reboot. Not started (--now) here since nothing's
# published to /var/www/alumunion yet; deploy.sh's `systemctl restart` does that.
systemctl enable alumni-institution-api alumni-member-api alumni-platform-api \
  alumni-frontend-institution alumni-frontend-member alumni-frontend-platform
systemctl reload nginx

echo "== 10/11: firewall =="
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
echo "Run 'ufw enable' yourself once you've confirmed the SSH rule above is correct."

echo "== 11/11: done =="
cat <<EOF

Provisioning done. Remaining manual steps:
  1. Edit /etc/alumunion/institution-api.env, member-api.env and platform-api.env
     (JWT keys, the Postgres password you just set, DigitalOcean Spaces keys,
     Mailtrap/Arkesel/WaSender creds). See deploy/*.env.example for what each
     key means — the ConnectionStrings__AlumniConnection password must match
     what you set above.
  2. Point DNS at this droplet's IP (via whichever provider hosts your DNS —
     Netlify DNS works fine here, it's just an authoritative zone):
       *.${PLATFORM_BASE_DOMAIN}   (wildcard, member portal — one label only,
                                    does NOT also cover *.${ADMIN_BASE_DOMAIN})
       ${PLATFORM_BASE_DOMAIN}     (apex — same IP, falls back to
                                    DefaultInstitutionSlug)
       *.${ADMIN_BASE_DOMAIN}      (wildcard, institution portal)
       ${PLATFORM_SUBDOMAIN}       (plain A record, Platform Portal)
     Confirm with \`dig\` once set.
  3. Run 'ufw enable' if you haven't already.
  4. Run deploy/deploy.sh to build and start everything.
  5. Once DNS resolves, get certificates — TWO wildcard certs need DNS-01
     validation (HTTP-01 can't prove wildcard ownership); certbot's
     python3-certbot-dns-digitalocean plugin was installed above if your DNS
     is on DigitalOcean, otherwise use the plugin matching wherever your zone
     actually lives (e.g. Netlify DNS has no certbot plugin — use \`certbot
     certonly --manual --preferred-challenges dns\` and add the TXT records
     it prints by hand, once per cert):
       certbot certonly --dns-digitalocean -d '*.${PLATFORM_BASE_DOMAIN}' -d ${PLATFORM_BASE_DOMAIN} -d ${PLATFORM_SUBDOMAIN} -m ${ACME_EMAIL} --agree-tos -n
       certbot certonly --dns-digitalocean -d '*.${ADMIN_BASE_DOMAIN}' -m ${ACME_EMAIL} --agree-tos -n
     Then edit /etc/nginx/sites-available/alumunion to add the "listen 443
     ssl" blocks pointing at the two cert paths (see the comment at the top
     of deploy/nginx.conf), and \`nginx -t && systemctl reload nginx\`.
EOF

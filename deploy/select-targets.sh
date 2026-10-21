#!/usr/bin/env bash
# Works out which services need building: the ones whose last build is older than a change that affects them.
# Used by .github/workflows/build.yml. Each service carries the commit it was last built from, so a missed or failed run
# never leaves one behind. Rules (same as the "all" target of deploy.sh):
#   backend/src/<Service>/...            that service only
#   backend/tests, *.md, docs, designs   nothing
#   anything else under backend/         every .NET service (shared SDK projects, solution, props)
#   frontend/apps/<app>/...              that app only
#   frontend/packages, lockfile, ...     every frontend
#
# Usage: select-targets.sh <state-file>
#   <state-file> has one line per service already built:  "<service> <commit-it-was-built-from>"
#   A service that is missing, or built from an unknown commit, is always built. FORCE_ALL=1 builds everything.
# Prints two lines:  dotnet=<json array>   frontend=<json array>
set -euo pipefail

STATE="${1:-/dev/null}"
DOTNET=(institution-api member-api platform-api operations-worker)
FRONTEND=(institution member platform)

built_from() { awk -v s="$1" '$1 == s { print $2; exit }' "$STATE"; }

dotnet_dir() {
  case "$1" in
    institution-api) echo "ReservEase.Alumni.Institution.Api" ;;
    member-api) echo "ReservEase.Alumni.Member.Api" ;;
    platform-api) echo "ReservEase.Alumni.Platform.Api" ;;
    operations-worker) echo "ReservEase.Alumni.Operations.Worker" ;;
  esac
}

# 0 = this service needs building
stale() { # name kind
  local name="$1" kind="$2" prev; prev="$(built_from "$1")"
  [[ -n "${FORCE_ALL:-}" || -z "$prev" ]] && return 0
  git cat-file -e "${prev}^{commit}" 2>/dev/null || return 0
  local changed
  changed="$(git diff --name-only "$prev" HEAD | grep -v -E '(^|/)(docs|Designs|design-plans|marketing)/|\.md$|^backend/tests/' || true)"
  if [[ "$kind" == dotnet ]]; then
    local own; own="$(dotnet_dir "$name")"
    local backend; backend="$(grep '^backend/' <<<"$changed" || true)"
    [[ -z "$backend" ]] && return 1
    # Anything outside every service's own folder is shared code.
    grep -v -E '^backend/src/ReservEase\.Alumni\.(Institution\.Api|Member\.Api|Platform\.Api|Operations\.Worker)/' <<<"$backend" | grep -q . && return 0
    grep -q "^backend/src/${own//./\\.}/" <<<"$backend"
  else
    local frontend; frontend="$(grep '^frontend/' <<<"$changed" || true)"
    [[ -z "$frontend" ]] && return 1
    grep -v -E '^frontend/apps/(institution|member|platform)/' <<<"$frontend" | grep -q . && return 0
    grep -q "^frontend/apps/${name}/" <<<"$frontend"
  fi
}

json() { local out="[" first=1 i; for i in "$@"; do [[ $first == 0 ]] && out+=","; out+="\"$i\""; first=0; done; echo "${out}]"; }

D=(); F=()
for s in "${DOTNET[@]}"; do stale "$s" dotnet && D+=("$s"); done
for a in "${FRONTEND[@]}"; do stale "$a" frontend && F+=("$a"); done
echo "dotnet=$(json ${D[@]+"${D[@]}"})"
echo "frontend=$(json ${F[@]+"${F[@]}"})"

#!/usr/bin/env bash
# Works out which services a change affects, from the git diff against the revision that is live on the server.
# Used by .github/workflows/deploy.yml. Same rules as the "all" target of deploy.sh:
#   backend/src/<Service>/...            that service only
#   backend/tests, *.md, docs, designs   nothing
#   anything else under backend/         every .NET service (shared SDK projects, solution, props)
#   frontend/apps/<app>/...              that app only
#   frontend/packages, lockfile, ...     every frontend
#
# Usage: select-targets.sh <live-revision-or-empty>     (empty, or an unknown revision, means build everything)
# Prints two lines:  dotnet=<json array>   frontend=<json array>
set -euo pipefail

PREV="${1:-}"
DOTNET=(institution-api member-api platform-api operations-worker)
FRONTEND=(institution member platform)

emit() { # name, items...
  local name="$1"; shift
  local json="["; local first=1
  for i in "$@"; do [[ $first == 0 ]] && json+=","; json+="\"$i\""; first=0; done
  echo "${name}=${json}]"
}

if [[ -z "$PREV" ]] || ! git cat-file -e "${PREV}^{commit}" 2>/dev/null || [[ -n "${FORCE_ALL:-}" ]]; then
  emit dotnet "${DOTNET[@]}"; emit frontend "${FRONTEND[@]}"; exit 0
fi

CHANGED="$(git diff --name-only "$PREV" HEAD | grep -v -E '(^|/)(docs|Designs|design-plans|marketing)/|\.md$|^backend/tests/' || true)"

D=(); F=()
backend="$(grep '^backend/' <<<"$CHANGED" || true)"
if [[ -n "$backend" ]]; then
  shared="$(grep -v -E '^backend/src/ReservEase\.Alumni\.(Institution\.Api|Member\.Api|Platform\.Api|Operations\.Worker)/' <<<"$backend" || true)"
  if [[ -n "$shared" ]]; then D=("${DOTNET[@]}"); else
    grep -q '^backend/src/ReservEase\.Alumni\.Institution\.Api/' <<<"$backend" && D+=(institution-api)
    grep -q '^backend/src/ReservEase\.Alumni\.Member\.Api/' <<<"$backend" && D+=(member-api)
    grep -q '^backend/src/ReservEase\.Alumni\.Platform\.Api/' <<<"$backend" && D+=(platform-api)
    grep -q '^backend/src/ReservEase\.Alumni\.Operations\.Worker/' <<<"$backend" && D+=(operations-worker)
  fi
fi
frontend="$(grep '^frontend/' <<<"$CHANGED" || true)"
if [[ -n "$frontend" ]]; then
  shared="$(grep -v -E '^frontend/apps/(institution|member|platform)/' <<<"$frontend" || true)"
  if [[ -n "$shared" ]]; then F=("${FRONTEND[@]}"); else
    grep -q '^frontend/apps/institution/' <<<"$frontend" && F+=(institution)
    grep -q '^frontend/apps/member/' <<<"$frontend" && F+=(member)
    grep -q '^frontend/apps/platform/' <<<"$frontend" && F+=(platform)
  fi
fi
emit dotnet ${D[@]+"${D[@]}"}
emit frontend ${F[@]+"${F[@]}"}

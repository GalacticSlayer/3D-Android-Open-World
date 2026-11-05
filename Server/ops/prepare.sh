#!/bin/bash
set -euo pipefail
export GIT_TERMINAL_PROMPT=0
base=/opt/fishing-server
export npm_config_cache="$base/npm-cache"
repo="$base/source"
# This is the game repository that owns main and its deployed Home APIs.
# Do not follow the original upstream fork: those commits lag behind this project.
source_url="https://github.com/GalacticSlayer/3D-Android-Open-World.git"
if [[ ! -d "$repo/.git" ]]; then
  git clone --depth=1 --filter=blob:none --single-branch --branch main --no-checkout \
    "$source_url" "$repo" >&2
  git -C "$repo" sparse-checkout set --no-cone /Server/runtime/ >&2
else
  # Repair older installations whose origin still points at EthanL1245.
  current_url=$(git -C "$repo" remote get-url origin)
  if [[ "$current_url" != "$source_url" ]]; then
    git -C "$repo" remote set-url origin "$source_url"
    echo "Updated fishing server source to GalacticSlayer main" >&2
  fi
fi
git -C "$repo" fetch --depth=1 origin main >&2
revision=$(git -C "$repo" rev-parse FETCH_HEAD)
release=$(git -C "$repo" rev-parse "$revision:Server/runtime")
[[ "$release" =~ ^[0-9a-f]{40}$ ]]
target="$base/releases/$release"
if [[ ! -d "$target" ]]; then
  git -C "$repo" checkout --detach --force "$revision" >&2
  stage=$(mktemp -d "$base/releases/staging.XXXXXXXX")
  trap 'rm -rf -- "$stage"' EXIT
  cp -a "$repo/Server/runtime/." "$stage/"
  # Runtime releases must contain ordinary files/directories only.
  if [[ -n $(find "$stage" ! -type f ! -type d -print -quit) ]]; then
    echo 'Unsupported file type in runtime release' >&2; exit 1
  fi
  cd "$stage"
  if [[ -f package.json ]]; then
    test -f package-lock.json
    npm ci --omit=dev --ignore-scripts --no-audit --no-fund >&2
  fi
  node --check server.mjs >&2
  chmod -R a+rX "$stage"
  mv "$stage" "$target"
  trap - EXIT
fi
printf '%s\n' "$release"

#!/usr/bin/env bash
# Provisions the BoogaBooster dev container (design D4). Runs as the postCreateCommand.
# Every step is idempotent, so it is also safe to re-run by hand:
#
#   bash .devcontainer/post-create.sh
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
app_dir="$repo_root/src/FourDotnet.BoogaBooster.App"
apphost_csproj="$repo_root/src/Aspire/FourDotnet.BoogaBooster.Aspire.AppHost/FourDotnet.BoogaBooster.Aspire.AppHost.csproj"

step() { printf '\n==> %s\n' "$*"; }
warn() { printf 'WARNING: %s\n' "$*" >&2; }

# --- npm and the GitHub Copilot CLI -------------------------------------------------------
# npm is taken from the Angular app's packageManager field, so the version lives in one place.
npm_version="$(node -p "require('$app_dir/package.json').packageManager.split('@')[1]")"
step "npm $npm_version and the GitHub Copilot CLI"
if [ "$(npm --version)" != "$npm_version" ]; then
  npm install --global "npm@$npm_version"
fi
if ! command -v copilot > /dev/null; then
  npm install --global @github/copilot
fi


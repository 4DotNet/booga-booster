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


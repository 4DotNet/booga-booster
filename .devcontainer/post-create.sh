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

# --- 4dotnet-csharp-style-guide MCP server ------------------------------------------------
# .mcp.json launches it by bare name, so ~/.dotnet/tools must be on PATH in every shell, not
# only in VS Code terminals (devcontainer.json's remoteEnv covers those).
step "4dotnet-csharp-style-guide (MCP server)"
tools_path_line='export PATH="$PATH:$HOME/.dotnet/tools"'
for rc in "$HOME/.bashrc" "$HOME/.profile"; do
  grep -qxF "$tools_path_line" "$rc" 2> /dev/null || echo "$tools_path_line" >> "$rc"
done
export PATH="$PATH:$HOME/.dotnet/tools"
# A failed install must not stop the rest of provisioning: the solution builds without it.
if ! dotnet tool update --global 4dotnet-csharp-style-guide; then
  warn "could not install 4dotnet-csharp-style-guide - the MCP server stays unavailable until this script is re-run"
fi

# --- Dapr ---------------------------------------------------------------------------------
# Slim mode installs daprd without the default Redis/Zipkin/placement containers: BoogaBooster
# only uses pub/sub, declared by the AppHost (design D3). The runtime version is pinned.
dapr_runtime_version="1.17.4"
step "Dapr runtime $dapr_runtime_version (slim)"
installed_runtime="$("$HOME/.dapr/bin/daprd" --version 2> /dev/null || true)"
if [ "$installed_runtime" != "$dapr_runtime_version" ]; then
  if [ -n "$installed_runtime" ]; then
    dapr uninstall
  fi
  dapr init --slim --runtime-version "$dapr_runtime_version"
fi
dapr --version

# --- RabbitMQ development user-secrets ----------------------------------------------------
# The same user-secrets the README tells native users to set (design D5). Only set when
# missing, so values a developer chose survive a rebuild. Addressed by id, so no project
# evaluation (and therefore no restore) is needed first.
step "RabbitMQ development user-secrets"
secrets_id="$(sed -n 's|.*<UserSecretsId>\(.*\)</UserSecretsId>.*|\1|p' "$apphost_csproj")"
secrets="$(dotnet user-secrets list --id "$secrets_id")"
for name in username password; do
  key="Parameters:rabbitmq-$name"
  if grep -q "^$key = " <<< "$secrets"; then
    echo "$key is already set - leaving it"
  else
    dotnet user-secrets set "$key" guest --id "$secrets_id" > /dev/null
    echo "$key set to the development default"
  fi
done

# --- ASP.NET development certificate ------------------------------------------------------
step "ASP.NET development certificate"
# Prefer the host's certificate, exported by devcontainer.json's initializeCommand: the host
# browser already trusts it, so the forwarded Aspire dashboard opens without a warning.
# Without it (Codespaces, no dotnet on the host) the container generates its own.
host_cert="$repo_root/.devcontainer/devcert.pfx"
if [ -f "$host_cert" ]; then
  # --import is only accepted together with --clean, which replaces any existing certificate.
  dotnet dev-certs https --clean --import "$host_cert" --password boogabooster-devcontainer
  echo "Imported the host's development certificate"
else
  echo "No host certificate exported - generating one; the host browser will warn once"
fi
# Exit code 4 means "trusted by some clients but not others": on Linux, OpenSSL trust needs
# SSL_CERT_DIR, which devcontainer.json's containerEnv sets. Any other failure is fatal.
dev_certs_exit=0
dotnet dev-certs https --trust || dev_certs_exit=$?
if [ "$dev_certs_exit" -eq 4 ]; then
  warn "the dev certificate is only partially trusted - check that SSL_CERT_DIR includes ~/.aspnet/dev-certs/trust"
elif [ "$dev_certs_exit" -ne 0 ]; then
  exit "$dev_certs_exit"
fi

# --- Restore ------------------------------------------------------------------------------
step "Restore the .NET solution"
dotnet restore "$repo_root/src/BoogaBooster.slnx"

step "Install the Angular app's npm packages"
(cd "$app_dir" && npm ci)

step "Dev container ready"

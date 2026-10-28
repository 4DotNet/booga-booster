# Tasks

## 1. Pin the .NET SDK

- [x] 1.1 Add a root `global.json` pinning `sdk.version` to `10.0.401` with `rollForward: latestFeature` (design D7), and verify `dotnet --version` from both the repo root and `src/` reports a 10.0.4xx-or-newer SDK and that `dotnet build BoogaBooster.slnx` still succeeds natively
- [x] 1.2 Add the SDK pin to the README prerequisites table ("the SDK version in `global.json` or newer 10.0") and verify the README text matches the file

## 2. Dev container definition

- [x] 2.1 Create `.devcontainer/devcontainer.json` on `mcr.microsoft.com/devcontainers/dotnet:10.0` with the `node`, `docker-in-docker` (with a named volume for `/var/lib/docker`), `github-cli`, Dapr CLI and Claude Code features (design D1, D2), and verify with `devcontainer build --workspace-folder .` (or "Rebuild Container" in VS Code) that the image builds
- [x] 2.2 Add `customizations.vscode.extensions` (C# Dev Kit, Aspire, Angular Language Service, Prettier) and verify they are installed when the container opens
- [x] 2.3 Add `portsAttributes` labels for the dashboard launch-profile ports and RabbitMQ management (`15672`), with infrastructure ports set to `onAutoForward: "silent"` (design D6), and verify `devcontainer.json` passes the dev container JSON schema in VS Code without warnings

## 3. Provisioning

- [x] 3.1 Create `.devcontainer/post-create.sh` (`set -euo pipefail`, executable, LF line endings enforced through `.gitattributes`) and wire it as `postCreateCommand`; verify it runs on container creation and its log is visible in the creation output
- [x] 3.2 Install the npm version read from the Angular app's `packageManager` field plus `@github/copilot` globally, and verify `npm --version` equals `11.5.2` and `copilot --version` succeeds inside the container
- [ ] 3.3 Install or update the `4dotnet-csharp-style-guide` global tool, make sure `~/.dotnet/tools` is on `PATH` for login and non-login shells, and warn (without failing provisioning) if nuget.org is unreachable; verify `which 4dotnet-csharp-style-guide` resolves in a new terminal
- [ ] 3.4 Run `dapr init --slim` with the pinned runtime version (1.17.x), skipped when already initialised (design D3), and verify `dapr --version` reports both CLI and runtime versions
- [ ] 3.5 Set the AppHost's `Parameters:rabbitmq-username` / `rabbitmq-password` user-secrets to `guest` only when they are unset (design D5), and verify with `dotnet user-secrets list` both on a fresh container and after changing the password and re-running the script (the custom value must survive)
- [ ] 3.6 Trust the ASP.NET dev certificate (`dotnet dev-certs https --trust`), then `dotnet restore src/BoogaBooster.slnx` and `npm ci` in `src/FourDotnet.BoogaBooster.App`; verify `dotnet build` + `dotnet test BoogaBooster.slnx` and `npm test` all pass inside a freshly created container
- [ ] 3.7 Run `bash .devcontainer/post-create.sh` a second time in the same container and verify it exits 0 and the environment still builds and tests (idempotency scenario)
- [ ] 3.8 Verify no AI tokens or API keys appear in `devcontainer.json`, `post-create.sh`, the container's environment (`env`) or the image layers

## 4. Running the system in the container

- [ ] 4.1 Start the AppHost inside the container with the documented command and verify the Aspire dashboard (opened through the forwarded port in the host browser) shows the RabbitMQ, API, API sidecar and frontend resources running
- [ ] 4.2 Verify pub/sub end to end: a `weather-updated` event reaches the Queue module (visible in the dashboard traces/logs, and the queue arrival rate changes), as on a native setup
- [ ] 4.3 Open the Angular operator dashboard through its forwarded URL in the host browser and verify live telemetry streams over SSE; record any one-time certificate warning for the README
- [ ] 4.4 Add a "Dev container / Codespaces" quick start to `README.md`: open in container, the recommended "Clone Repository in Container Volume" path on Windows/macOS, the privileged docker-in-docker note, the dev-cert browser warning, signing in to Claude Code / Copilot CLI, and that the native setup stays supported. Verify each documented command runs as written inside the container

## 5. CI check for the container

- [ ] 5.1 Add `.github/workflows/devcontainer.yml` using `devcontainers/ci` that builds the container and runs `dotnet build src/BoogaBooster.slnx` and `npm run build` in the Angular app inside it, triggered on `pull_request` and `push` with `paths: [".devcontainer/**", "global.json", ".github/workflows/devcontainer.yml"]` and least-privilege `permissions: contents: read` (design D8); verify locally with `actionlint` (or by pushing the branch) that the workflow is valid
- [ ] 5.2 Verify the check on the pushed branch: the workflow runs green for this PR, and a deliberately broken `devcontainer.json` in a throwaway commit makes it fail (then revert)

## 6. Repo instructions and tooling sync

- [ ] 6.1 Mention the dev container in `CLAUDE.md` (Common commands / Status) and mirror it in condensed form in `.github/copilot-instructions.md`
- [ ] 6.2 Run `node .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.mjs` and verify it reports no errors

## 7. Integration check

- [ ] 7.1 Create a GitHub Codespace from the change branch and verify, without any manual setup, that `dotnet test BoogaBooster.slnx` passes, the AppHost starts, and the operator dashboard opens in the Codespaces browser with live telemetry
- [ ] 7.2 On a native (non-container) Windows setup, verify `dotnet build` / `dotnet test` and the AppHost run behave as before with `global.json` in place

# Proposal

## Why

Getting BoogaBooster running takes six separate manual steps: install the .NET 10 SDK, install Node with npm 11, install Docker, install and initialise the Dapr CLI, set two Aspire user-secrets, and put the `4dotnet-csharp-style-guide` tool on `PATH` for the MCP server. If any one is missing or the wrong version, something fails, sometimes only for the AI tooling. Nothing pins the .NET SDK either, so each machine builds with whatever SDK it happens to have. BoogaBooster is also a teaching project, so this setup cost lands on every workshop attendee. A dev container reduces all of it to "Reopen in Container", or opening a GitHub Codespace.

## What Changes

- A new `.devcontainer/devcontainer.json` defines one reproducible Linux development environment. It provides the .NET 10 SDK, Node with npm 11, a Docker engine, the Dapr CLI, the GitHub CLI, and the AI tooling CLIs (Claude Code and GitHub Copilot CLI).
- On creation, the container prepares itself without manual steps. It initialises Dapr, installs the `4dotnet-csharp-style-guide` global tool, sets the RabbitMQ development user-secrets to throwaway `guest`/`guest` values if they are not already set, trusts the ASP.NET development certificate, and restores the .NET solution and the Angular app's npm packages.
- The whole system starts inside the container with the existing command (`dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost`). The Aspire dashboard and the Angular app are reachable from the host browser through forwarded ports.
- The same definition works in **GitHub Codespaces**.
- The container ships VS Code extension recommendations for the stack: C# Dev Kit, Aspire, Angular Language Service and Prettier.
- A root `global.json` pins the .NET SDK feature band (rolling forward to the latest patch) for everyone, inside the container or not.
- A CI workflow builds the dev container and runs a smoke check (`dotnet build` and the Angular build) whenever `.devcontainer/` or `global.json` changes, so the container cannot silently break.
- `README.md` gains a "Dev container / Codespaces" quick-start. `CLAUDE.md` and `.github/copilot-instructions.md` mention it.
- The local (non-container) setup is unchanged and stays supported. Nothing here is **BREAKING**.

## Capabilities

### New Capabilities
- `dev-container`: the reproducible development environment. Covers which toolchain it provides, how it provisions itself on creation, running the full Aspire graph inside it with the endpoints reachable from the host, Codespaces support, the AI tooling it makes available, and the CI check that keeps it buildable.

### Modified Capabilities
<!-- None. No runtime behaviour of the ride, queue, weather or dashboard changes. -->

## Impact

- **New files.** `.devcontainer/devcontainer.json`, a `.devcontainer/post-create.sh` provisioning script, `global.json`, and `.github/workflows/devcontainer.yml`.
- **Docs.** `README.md` (prerequisites / quick start), `CLAUDE.md` and `.github/copilot-instructions.md` (kept in sync; the `ai-tooling-sync` check must pass).
- **CI.** One new workflow. The PR touches `.github/workflows/` and `CLAUDE.md`, so the code-quality review flags it as `major` by design.
- **Application code.** No backend or frontend source changes. `AppHost.cs`, `proxy.conf.js` and the Dapr component wiring stay as they are; the design explains why they work unchanged inside the container.
- **Dependencies.** The container uses Microsoft's `devcontainers/dotnet` base image and published dev container features. No NuGet or npm packages are added to the solution.

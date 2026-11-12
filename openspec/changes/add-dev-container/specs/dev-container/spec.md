# Spec Delta

## Purpose

Provides a reproducible, self-provisioning development environment so that anyone can build, test, run and use the AI tooling of BoogaBooster locally or in GitHub Codespaces, without installing the toolchain by hand.

## ADDED Requirements

### Requirement: Container provides the full toolchain
The repository SHALL contain a dev container definition. A container created from it MUST provide the .NET SDK selected by the repository's `global.json`, Node.js with the npm version named in the Angular app's `packageManager` field, a working Docker engine, the Dapr CLI with an initialised runtime, and the GitHub CLI.

#### Scenario: Toolchain versions match the repository pins
- **WHEN** a developer opens the repository in the dev container and runs `dotnet --version`, `npm --version`, `docker info`, `dapr --version` and `gh --version`
- **THEN** every command succeeds, the .NET SDK satisfies `global.json`, npm equals the `packageManager` version, and `dapr --version` reports both a CLI and a runtime version

### Requirement: SDK version is pinned for every environment
The repository SHALL pin the .NET SDK with a root `global.json` that accepts the pinned feature band and newer 10.0 feature bands, and rejects older SDKs, whether or not the developer uses the dev container.

#### Scenario: Older SDK is rejected
- **WHEN** a developer with only an SDK older than the pinned version runs `dotnet build` in `src/`
- **THEN** the command fails with an SDK-resolution error that names the required version

#### Scenario: Newer patch is accepted
- **WHEN** the installed SDK is a newer patch or feature band of .NET 10 than the pinned version
- **THEN** `dotnet build` in `src/` uses it without error

### Requirement: Container provisions itself on creation
Creating the container SHALL complete all one-time setup without manual steps: restoring the .NET solution and the Angular app's npm packages, trusting the ASP.NET development certificate inside the container, initialising Dapr, and installing the tools the MCP configuration needs.

#### Scenario: Build and tests pass right after creation
- **WHEN** container creation has finished and the developer runs `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` in `src/`, and `npm test` in the Angular app
- **THEN** all three succeed without the developer having installed or configured anything

#### Scenario: Provisioning is safe to repeat
- **WHEN** the provisioning runs a second time in the same container, for example after a rebuild that reuses volumes
- **THEN** it completes successfully and leaves the environment in the same working state

### Requirement: Development secrets are defaulted without overwriting
Provisioning SHALL set the AppHost's RabbitMQ username and password user-secrets to the throwaway development value `guest` only when they are not already set. It MUST NOT overwrite values the developer has set, and MUST NOT write any other credential.

#### Scenario: Secrets absent
- **WHEN** the container is provisioned and the AppHost has no RabbitMQ user-secrets
- **THEN** both are set to `guest` and the AppHost starts without prompting for parameters

#### Scenario: Secrets already present
- **WHEN** the developer has set a custom RabbitMQ password and the container is provisioned again
- **THEN** the custom password is kept unchanged

### Requirement: Full system runs inside the container
Running the AppHost inside the container with the documented command SHALL start RabbitMQ, the API with its Dapr sidecar, and the Angular app. Integration events MUST flow over pub/sub, as they do on a native setup.

#### Scenario: Aspire graph comes up healthy
- **WHEN** the developer runs `dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost` from `src/` inside the container
- **THEN** the Aspire dashboard lists the RabbitMQ, API, API sidecar and frontend resources as running

#### Scenario: Pub/sub works end to end
- **WHEN** the system is running inside the container and the weather simulation publishes a `weather-updated` event
- **THEN** the Queue module receives it through the Dapr sidecar, as it does on a native setup

### Requirement: System endpoints are reachable from the host
While the system runs inside the container, the Aspire dashboard and the Angular operator dashboard SHALL be reachable from a browser on the developer's machine (or in the Codespaces browser) through forwarded ports.

#### Scenario: Operator dashboard opens in the host browser
- **WHEN** the AppHost is running inside the container and the developer opens the frontend URL shown in the Aspire dashboard
- **THEN** the Angular operator dashboard loads in the host browser and shows live telemetry from the API

### Requirement: AI tooling is available in the container
The container SHALL provide the Claude Code CLI and the GitHub Copilot CLI. Every MCP server declared in the repository's `.mcp.json` MUST be launchable from the container. The container MUST NOT contain any AI service credentials; each developer signs in with their own account.

#### Scenario: Style-guide MCP server resolves
- **WHEN** an AI agent inside the container starts the MCP servers from `.mcp.json`
- **THEN** the `4dotnet-csharp-style-guide` executable is found on `PATH` and the server starts

#### Scenario: No credentials are baked in
- **WHEN** a freshly created container is inspected
- **THEN** no AI service token or API key is present in its image, environment or configuration

### Requirement: Same definition works in GitHub Codespaces
The dev container definition SHALL also work, unchanged, as a GitHub Codespaces configuration. A codespace created from the repository's default branch MUST satisfy the same toolchain and provisioning requirements.

#### Scenario: Codespace is ready to run
- **WHEN** a developer creates a codespace for the repository and waits for creation to finish
- **THEN** the AppHost can be started and the operator dashboard opened in the browser without any manual setup

### Requirement: Container definition is verified in CI
Every pull request or push that changes the dev container definition or `global.json` SHALL trigger an automated check. The check builds the container and, inside it, builds the .NET solution and the Angular app. A failure in any step MUST fail the check.

#### Scenario: Broken definition fails the check
- **WHEN** a pull request changes the dev container definition so that the container no longer builds, or the solution no longer builds inside it
- **THEN** the dev container check fails on that pull request

#### Scenario: Unrelated changes skip the check
- **WHEN** a pull request touches neither the dev container definition nor `global.json`
- **THEN** the dev container check does not run

### Requirement: Native setup stays supported
Adding the dev container SHALL NOT remove or change the documented native setup. The README MUST document both paths, with the dev container as the recommended quick start.

#### Scenario: Native developer is unaffected
- **WHEN** a developer who already has the prerequisites installed natively pulls this change and runs the existing commands
- **THEN** build, test and the AppHost run behave as before, provided their SDK satisfies `global.json`

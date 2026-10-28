# Design

## Context

See `proposal.md` for the motivation. These facts about the current system shape the approach:

- **The AppHost publishes RabbitMQ on a fixed host port** (`5672`, management on `15672`). It builds the Dapr pub/sub connection string from the endpoint's `HostAndPort`. That connection string is used by **`daprd`**, which the Community Toolkit launches as a **local process** next to the API, not as a container. The Dapr sidecar must therefore reach RabbitMQ at the port Docker publishes, from wherever the processes run.
- **The API speaks HTTPS** to its sidecar (`AppProtocol = "https"`). The Angular dev proxy accepts the ASP.NET development certificate (`secure: false`), but the API itself and Aspire's dashboard still need a usable dev cert.
- **The Angular app runs as an Aspire Vite resource** (`AddViteApp`). Aspire assigns its port and injects the API's service-discovery URL, which `proxy.conf.js` already reads.
- **The `4dotnet-csharp-style-guide` MCP server is a .NET global tool on nuget.org** (`4dotnet-csharp-style-guide`, from `github.com/4Dotnet/csharp-style-guide`). `.mcp.json` launches it by bare command name, so it only has to be on `PATH`.
- **Nothing pins the toolchain.** There is no `global.json`. The local reference machine runs SDK `10.0.401`, Node 24, and the Dapr CLI 1.18 with runtime 1.17. The Angular app pins `npm@11.5.2` through `packageManager`.
- **Aspire 13 detects dev containers and Codespaces.** It rewrites the dashboard and resource URLs to forwarded addresses and asks the editor to forward the ports it allocates.

## Goals / Non-Goals

**Goals:**
- One definition that works in local VS Code Dev Containers (Docker Desktop on Windows/macOS, plain Docker on Linux) and in GitHub Codespaces.
- After the container is created, `dotnet build`, `dotnet test`, `npm test` and the AppHost run work without any further manual setup.
- The AI tooling works inside the container: the MCP servers in `.mcp.json` resolve, and Claude Code / Copilot CLI are installed. Each user still signs in with their own account.

**Non-Goals:**
- Replacing or deprecating the local (non-container) setup.
- Shipping credentials. No API keys or tokens are baked in, and only throwaway RabbitMQ dev values are set.
- Production or deployment images. This is a development environment only.
- Support for editors other than VS Code / Codespaces. JetBrains Gateway and others may work through the spec, but nobody verifies them.
- Changing `AppHost.cs`, `proxy.conf.js` or any module code.

## Decisions

### D1 — Base image plus features, no custom Dockerfile

Use `mcr.microsoft.com/devcontainers/dotnet:10.0` and add tools through published dev container features: `node`, `docker-in-docker`, `github-cli`, the Dapr CLI feature, and the Claude Code feature. Copilot CLI is installed as a global npm package. npm itself is set to the version in the Angular app's `packageManager` field by `post-create.sh` (D4).

*Alternative: a custom Dockerfile.* It gives full control but has to be maintained and rebuilt for every patch. Features are versioned, cached and maintained upstream, and they are enough for everything here. If a feature proves unreliable, a Dockerfile can be introduced later without changing the specs.

### D2 — Docker-in-Docker, not Docker-outside-of-Docker

The container runs its own Docker engine (`docker-in-docker` feature).

*Why not reuse the host's Docker (docker-outside-of-docker)?* With the host's engine, RabbitMQ's port `5672` is published on the **host**, but `daprd` runs **inside the dev container** and connects to `localhost:5672`, which would not be RabbitMQ. Making that work would require changing the AppHost's endpoint wiring or adding container networking tricks, both outside this change's scope. With docker-in-docker, published ports appear on the container's own `localhost`, the same topology as running natively, so `AppHost.cs` works unchanged. Docker-in-docker is also the mode Codespaces supports best.

*Cost:* a privileged container and a separate image cache, so the first RabbitMQ pull happens inside the container. A named volume for `/var/lib/docker` keeps the cache across container rebuilds.

### D3 — `dapr init --slim`

Initialise Dapr in slim mode: it installs the `daprd` and `placement` binaries without starting the default Redis/Zipkin/placement containers.

*Rationale:* BoogaBooster only uses pub/sub, which is declared programmatically by the Community Toolkit. It uses no actors, no default state store and no Zipkin; tracing goes through OpenTelemetry to the Aspire dashboard. Full `dapr init` would start three extra containers at every container start and would assume a running Docker engine at creation time, which is fragile with docker-in-docker during `postCreateCommand`. Pin the Dapr runtime version to the one the reference machine uses (1.17), so the sidecar does not drift.

### D4 — A single idempotent `post-create.sh`

All provisioning lives in `.devcontainer/post-create.sh`, which `postCreateCommand` runs:

1. Install the npm version named in `packageManager`, and `@github/copilot` globally.
1. `dotnet tool update -g 4dotnet-csharp-style-guide` (install or update, idempotent) and make sure `~/.dotnet/tools` is on `PATH`.
2. `dapr init --slim --runtime-version <pinned>`, skipped if already initialised.
3. Set `Parameters:rabbitmq-username` / `rabbitmq-password` on the AppHost to `guest` **only when they are not set yet**, so a user's own values survive a rebuild.
4. `dotnet dev-certs https --trust`, which on Linux trusts the cert for OpenSSL/.NET clients inside the container.
5. `dotnet restore src/BoogaBooster.slnx` and `npm ci` in the Angular app.

*Why a script instead of inline JSON commands:* the steps can be read, linted and re-run by hand (`bash .devcontainer/post-create.sh`), and each step can be made idempotent. Every step must be safe to run twice.

### D5 — User-secrets, not environment variables, for RabbitMQ

The provisioning writes the same user-secrets the README tells native users to set, rather than injecting `Parameters__rabbitmq-username` as a container environment variable. This keeps a single documented mechanism. A developer who changes the values does so in exactly the way the README describes, and the AppHost code does not care where it runs.

### D6 — Ports: rely on Aspire's dev container support, label the fixed ones

Aspire allocates the dashboard, API and Vite ports itself and asks the editor to forward them, so they are not hard-coded. `devcontainer.json` adds `portsAttributes` labels only for the fixed ports (the dashboard `17071`/`15145` from the launch profile, RabbitMQ management `15672`), with `onAutoForward: "silent"` for the infrastructure ports so the user is not flooded with notifications.

### D7 — Pin the SDK with a root `global.json`

A `global.json` at the repo root pins `sdk.version` to `10.0.401` with `rollForward: latestFeature`. Any 10.0.4xx SDK or newer 10.0 feature band is accepted, but an older SDK fails fast with a clear message. It lives at the root, not in `src/`, so it also covers commands run from the repo root (for example `dotnet tool`), and the `devcontainers/dotnet:10.0` image satisfies it.

*Alternative: `rollForward: latestPatch`.* Stricter, but it would break every contributor the moment they install the next feature band. Rejected as too much friction for a teaching repo.

`global.json` also opts into **Microsoft.Testing.Platform** (`"test": { "runner": "Microsoft.Testing.Platform" }`). Implementation found that `dotnet test BoogaBooster.slnx` already failed on `development`: xunit.v3 on Microsoft.Testing.Platform 2.4 no longer runs under the VSTest target on the .NET 10 SDK. The opt-in belongs in `global.json`, and the spec requires the tests to pass in the container, so it is part of this change. It also fixes native runs.

### D8 — CI builds the container on change

A new `.github/workflows/devcontainer.yml` uses `devcontainers/ci` to build the container and, inside it, run `dotnet build src/BoogaBooster.slnx` and `npm run build` in the Angular app. It triggers on pull requests and pushes that touch `.devcontainer/**` or `global.json`. It runs no tests and does not start the AppHost; that stays a manual verification task. Starting RabbitMQ and Dapr inside CI's docker-in-docker would make the workflow slow and flaky, for little extra assurance.

## Risks / Trade-offs

- **[Slow I/O on Windows bind mounts]** If the repo is opened from `D:\` and bind-mounted into the container, `npm ci` and builds are much slower than native. → The README recommends *"Clone Repository in Container Volume"* on Windows/macOS and documents the bind-mount path as the slower option.
- **[Privileged container for docker-in-docker]** → Accepted for a dev-only environment. Documented in the README; no production use.
- **[Dev cert trust stops at the container]** The cert is trusted inside the container, but a host browser opening a forwarded HTTPS port sees an untrusted certificate. → The dashboard and the Angular app are reached over the forwarded URLs Aspire prints, and the README notes the one-time browser warning. The Angular→API hop already tolerates the dev cert.
- **[Dapr runtime drift]** → The runtime version is pinned in `post-create.sh` and bumped deliberately.
- **[Style-guide tool unavailable]** If nuget.org is unreachable, the tool install fails and the MCP server is missing. → The step logs a clear warning and the rest of provisioning continues, so the C# build still works.
- **[Duplicate toolchain truth]** Versions now live in `global.json`, `package.json` (`packageManager`) and the feature options. → `post-create.sh` reads the npm version from `packageManager` instead of repeating it, and the CI workflow catches drift that breaks the build.

## Migration Plan

This change is purely additive. Native setups are untouched, apart from `global.json` rejecting SDKs older than 10.0.401. To roll back, delete `.devcontainer/`, `global.json` and the workflow.

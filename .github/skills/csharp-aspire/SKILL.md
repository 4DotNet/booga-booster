---
name: csharp-aspire
description: >-
  Aspire orchestration and service-integration rules (ADR-0008). Use whenever
  touching the AppHost or ServiceDefaults projects, adding or configuring a
  backing service (database, cache, message broker, container), wiring a new
  project or frontend into the distributed application graph, adding a client
  library for an external service, or working with service discovery,
  connection strings or Aspire parameters. Always use the client library that
  ships with the corresponding Aspire integration.
---

# Aspire Orchestration

Aspire is the orchestrator and the integration layer for the whole solution. It
is also where observability is configured (ADR-0009), so almost every
infrastructure decision passes through these two projects.

## Non-negotiable

- **Use Aspire for orchestration and service integration in every solution.**
  (`adr-0008-r1`)
- **Aspire projects live in `src/Aspire/`.** (`adr-0008-r2`)
- **When integrating a service, use the client library that ships with the
  corresponding Aspire integration** — not a hand-rolled client, not the raw
  vendor SDK registered by hand. The Aspire client library brings configuration,
  health checks, resilience and OTEL instrumentation with it. (`adr-0008-r3`)
- **Cross-cutting configuration goes in `ServiceDefaults`**, and every service
  project calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`.
  Never reconfigure telemetry, health checks, service discovery or resilience per
  project. (`adr-0009-r2`)

## This repo's graph

`Aspire/FourDotnet.BoogaBooster.Aspire.AppHost/AppHost.cs` declares:

- a **RabbitMQ** container,
- a **Dapr `pubsub` component declared programmatically** via the Aspire
  Community Toolkit — there are **no** `components/*.yaml` files, so add or change
  components in `AppHost.cs`,
- the **API** with its Dapr sidecar (the only .NET project in the graph — it is
  the modular monolith's single host),
- the **Angular app** as a Vite resource (`AddViteApp`).

Consequences to keep in mind:

- Adding a module does **not** add a project to the graph; the module is hosted by
  the existing API. Wire it up with `AddXxxModule()` / `MapXxxEndpoints()` instead
  — see `csharp-minimal-api-endpoints`.
- RabbitMQ credentials are **secret Aspire parameters**, set through
  `dotnet user-secrets` on the AppHost (`Parameters:rabbitmq-username`,
  `Parameters:rabbitmq-password`). Never commit them or default them in code.
- Running the AppHost requires **Docker** and an initialised **Dapr CLI**
  (`dapr init`).
- The frontend dev proxy (`proxy.conf.js`) reads the Aspire-injected
  service-discovery environment variable, so renaming the API resource in
  `AppHost.cs` affects the Angular dev server.

## Adding a backing service

1. Find the Aspire integration for it (check the `microsoft-learn` MCP server or
   `microsoft-docs` skill for the current package name and API).
2. Add the **hosting** package to the AppHost and declare the resource in
   `AppHost.cs`.
3. Add the matching **client** integration package to the consuming project and
   register it with its `Add…` extension — that is the mandated client library.
4. Reference the resource from the consumer so Aspire injects the connection
   string / service-discovery values; never hard-code a host, port or connection
   string.
5. Confirm the integration's instrumentation flows into OTEL (ADR-0009) — if it
   needs an extra tracing source, add it in `ServiceDefaults`, not locally.

## Violations to catch

- An Aspire project outside `src/Aspire/`.
- A raw vendor SDK client registered by hand where an Aspire client integration
  exists.
- Hard-coded connection strings, hostnames or ports in a module or host.
- A Dapr component added as YAML instead of programmatically in `AppHost.cs`.
- Credentials or secrets committed to `appsettings*.json` rather than being
  Aspire parameters backed by user secrets.
- A service project that does not call `AddServiceDefaults()`.

## Read further

`get_document` with `adr-0008` on the `4dotnet-csharp-style-guide` MCP server;
`adr-0009` for how ServiceDefaults owns the telemetry configuration. For the
current API of a specific Aspire integration, query the `microsoft-learn` MCP
server rather than guessing package names.

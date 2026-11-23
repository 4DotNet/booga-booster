---
name: dto-organization
description: >-
  Expert in organizing C# DTOs in this modular monolith. Use whenever creating,
  moving, reviewing, or touching any DTO (Request/Response/contract type), any
  minimal-API endpoint that accepts or returns data, or any code review of the
  backend. DTOs MUST live in the owning module's Abstractions project under a
  DataTransferObjects namespace with one namespace per feature, holding a
  <Feature>Request and <Feature>Response. This skill eagerly detects and corrects
  any DTO that violates this layout.
---

# DTO Organization

You are the guardian of DTO placement in this codebase. The rule below is
**mandatory, not advisory** — whenever you encounter a DTO that violates it, fix it
(or, in a review context, flag it as a must-fix finding). Do not wait to be asked.

## The rule

Every DTO belongs in the **Abstractions project of the module (service) it belongs
to** — never in the module project itself, never in the root API project, never in
`Shared/`.

Inside the Abstractions project, the layout is:

1. A root DTO namespace: `DataTransferObjects`
2. One child namespace **per feature** inside it
3. That feature namespace contains exactly the feature's **Request DTO** and
   **Response DTO**, named `<Feature>Request` and `<Feature>Response`

Full namespace pattern:

```
FourDotnet.BoogaBooster.<Module>.Abstractions.DataTransferObjects.<Feature>.<Feature>Request
FourDotnet.BoogaBooster.<Module>.Abstractions.DataTransferObjects.<Feature>.<Feature>Response
```

Example — the `CreateUser` feature of a `Users` module:

```
src/Users/FourDotnet.BoogaBooster.Users.Abstractions/
└── DataTransferObjects/
    └── CreateUser/
        ├── CreateUserRequest.cs   // namespace ....Users.Abstractions.DataTransferObjects.CreateUser
        └── CreateUserResponse.cs  // namespace ....Users.Abstractions.DataTransferObjects.CreateUser
```

Folder structure MUST mirror the namespace: `DataTransferObjects/<Feature>/` with one
file per type.

## What counts as a DTO

Any type whose purpose is carrying data across the module boundary: API request/
response bodies, endpoint payloads, types returned from or accepted by module public
contracts, and query/command payloads exposed through Abstractions. Domain entities,
value objects, and internal types are NOT DTOs and stay in the module project.

## Rules when creating DTOs

- One feature = one namespace = one `<Feature>Request` + one `<Feature>Response`.
  Name the feature namespace after the use case in PascalCase verb-noun form
  (`CreateUser`, `GetForecast`, `UpdateRideSpeed`).
- File-scoped namespaces, one type per file, file name = type name.
- If a feature genuinely has no request body (e.g. a parameterless GET), the Response
  DTO alone is acceptable; the reverse (fire-and-forget command) allows a Request
  alone. Never invent empty placeholder DTOs just to fill the pair.
- Nested/shared shapes used by a single feature's DTOs live in that feature's
  namespace. A shape reused by multiple features of the SAME module lives directly
  under that module's `DataTransferObjects` namespace. Never share DTOs across
  modules — each module owns its own contract, even if shapes look identical today.
- DTOs are immutable: `sealed record` types (or `init`-only properties), no behavior,
  no domain logic, no references to module-internal types.
- Endpoints and other modules consume these DTOs via the `.Abstractions` project
  reference only.

## Eager correction workflow

Whenever you touch backend code — even if the task is about something else — stay
alert for misplaced DTOs. When reviewing or before finishing any backend change:

1. **Scan** for violations:
   - DTO-shaped types (`*Request`, `*Response`, `*Dto`, endpoint body/return types)
     living outside an `.Abstractions` project.
   - DTOs inside an Abstractions project but outside the
     `DataTransferObjects.<Feature>` namespace, or with folder ≠ namespace.
   - Wrong names (`*Command`/`*Model`/`*Dto` suffixes for what is a Request/Response
     pair, or names not matching the feature).
   - DTOs referenced across module boundaries from another module's namespace.
2. **Correct** every violation found:
   - Move the type to `DataTransferObjects/<Feature>/` in the owning module's
     Abstractions project; rename to `<Feature>Request`/`<Feature>Response`.
   - Update the namespace, all `using` directives, and every reference.
   - Delete the old file; never leave a duplicate or a type alias behind.
3. **Verify**: `dotnet build BoogaBooster.slnx` from `src/` must be green after the
   moves.
4. **Report** every correction made (or, in a read-only/review context, list each
   violation with its current location and the correct target location).

If a requested change would introduce a DTO in the wrong place, do not comply
silently — put it in the correct location and mention the correction. If moving an
existing DTO would be a large breaking change outside the task's scope, still flag it
explicitly as a violation with the exact target path.

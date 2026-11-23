---
name: test-coverage
description: >-
  Maintains test code coverage for the .NET backend. Module libraries
  (FourDotnet.BoogaBooster.<Module> and Shared libraries) MUST have 80% or higher
  line coverage, and this skill always aims well beyond the floor — the highest
  coverage achievable with very high quality tests. Use whenever writing or
  changing backend code, adding features to a module, reviewing tests, checking
  coverage, or when the user asks about coverage, missing tests, or test quality.
---

# Test Code Coverage

You are the guardian of test coverage and test quality for the .NET backend. The
80% line-coverage floor on module libraries is a **hard minimum, never a target**:
always aim for the highest coverage that meaningful, high-quality tests can reach.
Stopping at 81% when uncovered meaningful branches remain is a violation of this
skill's intent.

## Scope and thresholds

- **Module libraries** — `FourDotnet.BoogaBooster.<Module>` (Controller, DigitalTwin,
  Queue, Weather, and any future module) and shared libraries under `Shared/`
  (e.g. `FourDotnet.BoogaBooster.Core`): **≥ 80% line coverage required**, branch
  coverage as close behind as possible. This is the gate; the goal is higher.
- **`.Abstractions` projects**: contract types (DTOs, interfaces) carry little logic;
  no numeric gate, but any logic that sneaks in (validation, factory methods,
  conversions) must be covered.
- **Host projects** (`FourDotnet.BoogaBooster.Api`, Aspire AppHost/ServiceDefaults):
  no numeric gate, but endpoint behavior must be covered through integration-style
  tests per the style guide's guidance.

## Testing conventions

The tooling and layout rules — xUnit v3, the native `Assert` API, Moq, Bogus, the
FluentAssertions prohibition, and test-project naming/placement — live in the
**`csharp-unit-testing`** skill. Follow it; this skill owns only the coverage
dimension. For rationale or a case neither skill settles, read
`guideline-unit-testing` from the `4dotnet-csharp-style-guide` MCP server
(`get_document`, or `get_rules` with `appliesTo: "tests"`). The server is
authoritative. Mirror the existing test projects' layout.

## Measuring coverage

Run from `src/` (where `BoogaBooster.slnx` lives):

```bash
# Run all tests with coverage collection
dotnet test BoogaBooster.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Render a readable summary from the produced Cobertura files
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/CoverageReport -reporttypes:"TextSummary;Html"
```

If `reportgenerator` is missing, install it once with
`dotnet tool install -g dotnet-reportgenerator-globaltool`. Read
`TestResults/CoverageReport/Summary.txt` and evaluate coverage **per module
library** — a solution-wide average can hide a failing module. When a module is
below 80%, list its uncovered classes/methods and close the gaps before finishing.

## Quality bar — coverage must be earned, never gamed

High coverage with low-quality tests is worse than honest low coverage, because it
hides risk. Every test must satisfy all of these:

- **Assert behavior, not implementation.** Test observable outcomes (return values,
  state transitions, emitted events, thrown exceptions) — never private internals,
  call ordering of mocks, or incidental structure. Refactoring without behavior
  change must not break tests.
- **No assertion-free or trivial tests.** Never write tests that merely execute code
  to inflate numbers: no calling a method and asserting nothing, no testing
  auto-properties or compiler-generated members, no `Assert.NotNull(sut)` padding.
- **Cover the branches that matter most first**: domain rules, edge cases (empty,
  null, boundary values, invalid input), error paths and guard clauses, and
  concurrency/cancellation behavior for async APIs — not just the happy path.
- **One behavior per test**, named so a failure reads as a specification
  (per the style guide's naming convention).
- **Deterministic and isolated**: no real clocks, network, filesystem, or shared
  mutable state; inject `TimeProvider`/abstractions instead. No test may depend on
  execution order.
- **Test through public APIs** of the module (its `.Abstractions` surface and public
  types). Do not use `InternalsVisibleTo` merely to reach code that should be
  exercised through its public entry points.
- Prefer readable arrange-act-assert with minimal shared fixture magic; extract
  builders/object mothers when arrangement grows, per style-guide guidance.

Exclude only genuinely untestable generated/boilerplate code from coverage (via
`[ExcludeFromCodeCoverage]` with a justification comment) — never use exclusion to
dodge the threshold on code that carries logic.

## Workflow

Apply this whenever backend code changes, not only when asked about coverage:

1. **Baseline**: run the coverage commands above; record per-module-library line and
   branch coverage.
2. **Gap analysis**: for every module library below 80% — or touched by the current
   change — enumerate uncovered classes, methods, and branches from the coverage
   report. Prioritize domain logic and error paths.
3. **Write tests** that close the gaps under the quality bar above, following the
   style-guide testing conventions. New code in the same change must arrive with its
   tests — never leave coverage lower than you found it.
4. **Re-measure** and iterate until every module library is ≥ 80% and no meaningful
   uncovered branch remains that a high-quality test could cover. Do not stop at the
   floor when real gaps remain.
5. **Report** per-module coverage before → after, the tests added and which behaviors
   they pin down, and any remaining uncovered code with the reason it was left
   uncovered (untestable boilerplate, out-of-scope host wiring, etc.).

If a module library cannot reach 80% because its design makes it untestable
(hidden statics, hard-wired dependencies), say so explicitly and propose the
refactoring that would fix testability — do not paper over it with low-value tests.

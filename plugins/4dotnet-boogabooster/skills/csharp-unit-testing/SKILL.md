---
name: csharp-unit-testing
description: >-
  Backend unit-testing tooling and layout rules (guideline-unit-testing). Use
  whenever writing, changing or reviewing a .NET test, creating a test project,
  choosing an assertion or mocking approach, adding a test package reference, or
  generating fake data. xUnit v3 with the native Assert API, Moq for mocking,
  Bogus for fake data; FluentAssertions is PROHIBITED (commercial license). One
  test project per module library, named <ModuleLibrary>.Tests, in src/Tests/.
  Pair with the test-coverage skill, which owns the 80% coverage floor.
---

# Backend Unit Testing

This skill covers **how** backend tests are written and where they live. The
**≥ 80 % line-coverage floor** and the test-quality bar are owned by the
`test-coverage` skill — use both together.

## Non-negotiable

- **xUnit with the `xunit.v3` NuGet package.** Never use the older xUnit v2
  packages. (`guideline-unit-testing-r1`)
- **Assertions use the .NET-native xUnit `Assert` API.** Never add a separate
  fluent-assertion library. (`guideline-unit-testing-r6`)
- **FluentAssertions is prohibited** — it ships under a commercial licence. If you
  find it referenced, flag it and replace the assertions. (`guideline-unit-testing-r2`)
- **Moq for mocking, Bogus for fake data** (recommended defaults — use them unless
  there is a stated reason not to). (`guideline-unit-testing-r3`)
- **One dedicated test project per module library**, named after the library with a
  `.Tests` suffix (`FourDotnet.BoogaBooster.Weather` →
  `FourDotnet.BoogaBooster.Weather.Tests`). Never share one test project across
  modules. (`guideline-unit-testing-r4`)
- **Test projects live in `src/Tests/`**, not inside the module folder.
  (`guideline-unit-testing-r4`)
- **≥ 80 % line coverage of the module library**; `.Abstractions` projects are
  excluded from the threshold. (`guideline-unit-testing-r5` — see `test-coverage`)

## Determinism

Tests in this repo must be deterministic, which the physics makes non-negotiable:

- Time goes through **`TimeProvider`** — use
  `Microsoft.Extensions.TimeProvider.Testing`'s `FakeTimeProvider`, never
  `DateTime.Now` or real delays.
- Randomness goes through the injected samplers (`IRideEventSampler`,
  `IWeatherSampler`, `IPersonGenerator`) — seed them in tests.
- Seed Bogus (`new Faker<T>().UseSeed(...)`) so a failure reproduces.
- No real clocks, network, filesystem, or shared mutable state; no dependence on
  test execution order.

## The shape

```csharp
public sealed class SetNameTests
{
    [Fact]
    public void SetName_WithWhitespace_ThrowsDomainValidationException()
    {
        var sut = new Gondola("Gondola 1");

        var exception = Assert.Throws<DomainValidationException>(() => sut.SetName("  "));

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Gondola 2", DomainModelState.New)]
    public void SetName_OnNewModel_KeepsStateNew(string name, DomainModelState expected)
    {
        var sut = new Gondola("Gondola 1");

        sut.SetName(name);

        Assert.Equal(expected, sut.State);
    }
}
```

Name tests so a failure reads as a specification:
`Method_Scenario_ExpectedOutcome`.

## Violations to catch

- A `PackageReference` to `FluentAssertions`, or `.Should()` calls anywhere.
- `xunit` / `xunit.core` v2 packages instead of `xunit.v3`.
- A test project inside a module folder, or one test project covering several
  modules.
- A test project whose name does not match `<ModuleLibrary>.Tests`.
- `DateTime.Now` / `Task.Delay` / `Thread.Sleep` in a test, or an unseeded
  `Random` or `Faker`.
- A new module library with no corresponding test project.

## Commands

Run from `src/`:

```bash
dotnet test BoogaBooster.slnx
dotnet test BoogaBooster.slnx --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## Read further

`get_document` with `guideline-unit-testing` on the
`4dotnet-csharp-style-guide` MCP server, or `get_rules` with
`appliesTo: "tests"` for the checkable rule list.

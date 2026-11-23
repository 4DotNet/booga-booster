---
name: csharp-domain-model
description: >-
  Rich domain-model rules for the .NET backend (ADR-0003). Use whenever writing
  or changing an entity, aggregate, value object or domain service, adding a
  property or setter to a domain type, adding validation, working with entity
  lifecycle/change tracking, or reviewing anything under a module's Domain/
  folder. Properties are public-get / private-set, all mutation goes through
  intent-revealing SetX() methods that validate before assigning, multi-value
  changes go through a validated value object, and models derive from DomainModel
  for lifecycle state.
---

# Rich Domain Models

Entities own their data **and** their validation. An anemic model with public
setters and validation in a service is a violation of this standard.

## Non-negotiable

- **Public getter, private setter.** `public string Name { get; private set; }`
  State is never mutated from outside the model. (`adr-0003-r1`)
- **Every change goes through an intent-revealing `SetX()` method**, and **all**
  validation for that value lives inside it, running **before** the assignment.
  (`adr-0003-r2`)
- **Multi-value changes go through a value object** that is thoroughly validated
  in its constructor, so it cannot exist in an invalid state. The model then
  accepts the already-valid value object as a unit. (`adr-0003-r3`)
- **Derive from `FourDotnet.BoogaBooster.Core.DomainModel`** for the lifecycle
  plumbing. Never re-implement state tracking in a concrete model.
  (`adr-0003-r4`, `adr-0003`)
- **Initial state is only `New` or `Pristine`** — `New` when constructed in code,
  `Pristine` when rehydrated from a store. (`adr-0003`)
- **Validation failures throw `DomainValidationException`.** Endpoints translate
  it to a `400`; do not return error objects or booleans from `SetX()`.

## The base class

`DomainModel` gives you:

| Member | Use |
| --- | --- |
| `DomainModel(bool isNew)` | `true` → state `New`; `false` → state `Pristine` |
| `State` | current `DomainModelState` |
| `ApplyChange<T>(ref field, value, comparer?)` | single-value change; assigns only when different, moves state to `Modified` or `Touched`, returns whether it changed |
| `MarkChanged(bool changed = true)` | a change applied outside `ApplyChange` — a value object, or a collection the aggregate owns |
| `MarkDeleted()` | back this with a public `Delete()` on the model |

`DomainModelState`: `New`, `Pristine`, `Touched`, `Modified`, `Deleted`.
`Touched` means a `SetX()` ran but the value did not actually change; `Modified`
means it did. `New` and `Deleted` are terminal for change tracking.

## The shape

```csharp
public sealed class Gondola : DomainModel
{
    private string _name;

    // Created in code.
    public Gondola(string name) : base(isNew: true) => SetName(name);

    // Rehydrated from a store.
    private Gondola(string name, bool isNew) : base(isNew) => _name = name;

    public string Name => _name;

    public void SetName(string value)
    {
        // ALL validation for Name lives here, and runs before the assignment.
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Name is required.");
        }

        if (value.Length > 100)
        {
            throw new DomainValidationException("Name must be 100 characters or fewer.");
        }

        ApplyChange(ref _name, value);
    }
}
```

Value object for a multi-value change:

```csharp
public sealed record SeatAssignment
{
    public SeatAssignment(int row, int seat)
    {
        // Per-field and cross-field validation, before any model can accept it.
        if (row is < 1 or > 2) throw new DomainValidationException("Row must be 1 or 2.");
        // ...
        Row = row;
        Seat = seat;
    }

    public int Row { get; }
    public int Seat { get; }
}

public void SetSeatAssignment(SeatAssignment assignment)
{
    ArgumentNullException.ThrowIfNull(assignment);
    ApplyChange(ref _seatAssignment, assignment);
}
```

## Violations to catch

- A public or `internal` setter on a domain type.
- Validation for a domain value performed in a handler, endpoint, or service
  instead of inside the model.
- Two or more properties assigned together by separate `SetX()` calls where the
  invariant spans them — that needs a value object.
- A domain model that does not derive from `DomainModel`, or that carries its own
  `State` field / change-tracking logic.
- A constructor that starts a model in `Modified` or `Touched`.
- `SetX()` that assigns first and validates afterwards, or that returns an error
  instead of throwing `DomainValidationException`.

## In this repo specifically

Physics types under `DigitalTwin/Domain` carry two extra hard constraints from
`CLAUDE.md` and `docs/`: **no magic numbers** (every constant lives in
`RideParameters.cs` with its derivation in `docs/`) and **determinism** (the tick
is a pure function of state at a fixed 1/120 s step; randomness goes through an
injected sampler, time through `TimeProvider`). Read the relevant `docs/` file
before changing physics code — it is the specification the tests are written
against.

## Read further

`get_document` with `adr-0003` on the `4dotnet-csharp-style-guide` MCP server for
the full rationale, the state table, and the enforcement notes.

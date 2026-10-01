using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>Shared helpers for the physics tests.</summary>
internal static class TestHelpers
{
    public static readonly TimeSpan Dt = RideParameters.TimeStep;

    /// <summary>An anonymous boarding seed of the given weight with the default mood.</summary>
    public static PassengerSeed Seed(double kilograms = RideParameters.DefaultPassengerKg) =>
        PassengerSeed.Anonymous(new PassengerWeight(kilograms));

    /// <summary>A boarding seed for a queued guest with the given identity and mood.</summary>
    public static PassengerSeed Guest(
        long guestNumber,
        double happiness = 75d,
        double preferredG = 3d,
        double nausea = 0d,
        double kilograms = RideParameters.DefaultPassengerKg) =>
        new(guestNumber, new PassengerWeight(kilograms), happiness, preferredG, nausea);

    /// <summary>Boards both seats of every gondola on a hub with equal-weight passengers.</summary>
    public static void FillHub(Hub hub, double kilograms)
    {
        foreach (var gondola in hub.Gondolas)
        {
            gondola.Board(SeatPosition.Left, Passenger.OfWeight(kilograms), TimeSpan.Zero);
            gondola.Board(SeatPosition.Right, Passenger.OfWeight(kilograms), TimeSpan.Zero);
        }
    }

    /// <summary>Advances a mill's physics for the given number of whole steps.</summary>
    public static void StepMill(GreatMill mill, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            mill.AdvancePhysics(Dt.TotalSeconds);
        }
    }

    /// <summary>Asserts two angles are equal modulo 2π, within <paramref name="tolerance"/> radians.</summary>
    public static void AssertAngleClose(double expected, double actual, double tolerance = 0.05d)
    {
        var difference = Math.Atan2(Math.Sin(actual - expected), Math.Cos(actual - expected));
        Assert.True(
            Math.Abs(difference) < tolerance,
            $"Expected angle ≈ {expected} rad but was {actual} rad (Δ={difference}).");
    }
}

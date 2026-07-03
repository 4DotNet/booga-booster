using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class LoadBalanceTests
{
    [Fact]
    public void An_empty_mill_is_balanced()
    {
        var mill = new GreatMill();

        Assert.Equal(0d, mill.ImbalanceMillimeters, 6);
        Assert.True(mill.IsBalanced);
    }

    [Fact]
    public void Matching_loads_on_opposite_arms_stay_balanced()
    {
        var mill = new GreatMill();
        // Hubs 0 and 2 sit on opposite arms (0° and 180°); load them equally.
        TestHelpers.FillHub(mill.GetHub(0), RideParameters.MaxPassengerKg);
        TestHelpers.FillHub(mill.GetHub(2), RideParameters.MaxPassengerKg);

        Assert.True(mill.ImbalanceMillimeters < RideParameters.MaxImbalanceMillimeters);
        Assert.True(mill.IsBalanced);
    }

    [Fact]
    public void A_heavy_load_on_one_arm_makes_the_ride_unbalanced()
    {
        var mill = new GreatMill();
        TestHelpers.FillHub(mill.GetHub(0), RideParameters.MaxPassengerKg);

        Assert.True(mill.ImbalanceMillimeters > RideParameters.MaxImbalanceMillimeters);
        Assert.False(mill.IsBalanced);
    }

    [Fact]
    public void The_mill_reports_the_total_rotating_load()
    {
        var mill = new GreatMill();
        TestHelpers.FillHub(mill.GetHub(0), 100d); // 8 passengers × 100 kg

        // 16 empty gondolas plus the 800 kg of passengers just boarded.
        var expected = (16 * RideParameters.EmptyGondolaKg) + (8 * 100d);
        Assert.Equal(expected, mill.LoadKg, 6);
    }

    [Fact]
    public void An_empty_mill_is_not_overloaded()
    {
        var mill = new GreatMill();

        Assert.Equal(0d, mill.PassengerLoadKg, 6);
        Assert.False(mill.IsOverloaded);
    }

    [Fact]
    public void Passenger_weight_at_the_maximum_is_not_overloaded()
    {
        var mill = new GreatMill();
        // 32 seats × 100 kg = 3200 kg, exactly the maximum safe load.
        foreach (var hub in mill.Hubs)
        {
            TestHelpers.FillHub(hub, 100d);
        }

        Assert.Equal(RideParameters.MaxPassengerLoadKg, mill.PassengerLoadKg, 6);
        Assert.False(mill.IsOverloaded);
    }

    [Fact]
    public void Passenger_weight_above_the_maximum_overloads_the_mill()
    {
        var mill = new GreatMill();
        // 32 seats × 130 kg = 4160 kg, well over the 3200 kg maximum.
        foreach (var hub in mill.Hubs)
        {
            TestHelpers.FillHub(hub, RideParameters.MaxPassengerKg);
        }

        Assert.True(mill.PassengerLoadKg > RideParameters.MaxPassengerLoadKg);
        Assert.True(mill.IsOverloaded);
    }
}

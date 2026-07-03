namespace FourDotnet.BoogaBooster.Weather.Abstractions;

/// <summary>
/// The active weather regime driving the simulation toward a particular target.
/// </summary>
public enum WeatherRegime
{
    /// <summary>Autonomous moderate weather drifting around the defaults.</summary>
    Calm = 0,

    /// <summary>A user-triggered precipitation event is active.</summary>
    Precipitation = 1,

    /// <summary>A user-triggered strong-wind event is active.</summary>
    StrongWind = 2,
}

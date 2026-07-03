using FourDotnet.BoogaBooster.Core.Domain;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Air temperature in degrees Celsius, validated on construction (ADR-0003).
/// The value is quantized to one decimal so readings are stable and printable.
/// </summary>
public sealed record Temperature
{
    /// <summary>Lowest temperature the simulation will represent.</summary>
    public const double MinCelsius = -50d;

    /// <summary>Highest temperature the simulation will represent.</summary>
    public const double MaxCelsius = 60d;

    public Temperature(double celsius)
    {
        if (double.IsNaN(celsius) || double.IsInfinity(celsius))
        {
            throw new DomainValidationException("Temperature must be a real number.");
        }

        if (celsius < MinCelsius || celsius > MaxCelsius)
        {
            throw new DomainValidationException(
                $"Temperature must be between {MinCelsius} and {MaxCelsius} °C.");
        }

        Celsius = Math.Round(celsius, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>The temperature in degrees Celsius, rounded to one decimal.</summary>
    public double Celsius { get; }
}

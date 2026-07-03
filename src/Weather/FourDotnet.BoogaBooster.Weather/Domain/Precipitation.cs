using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Precipitation as a type and an intensity (0–100), validated on construction
/// (ADR-0003). Cross-field rule: <see cref="PrecipitationType.None"/> must carry
/// zero intensity, and any falling precipitation must carry a positive intensity.
/// </summary>
public sealed record Precipitation
{
    /// <summary>Lowest intensity.</summary>
    public const int MinIntensity = 0;

    /// <summary>Highest intensity.</summary>
    public const int MaxIntensity = 100;

    /// <summary>A shared "no precipitation" value.</summary>
    public static readonly Precipitation None = new(PrecipitationType.None, 0);

    public Precipitation(PrecipitationType type, int intensity)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException($"Unknown precipitation type '{type}'.");
        }

        if (intensity < MinIntensity || intensity > MaxIntensity)
        {
            throw new DomainValidationException(
                $"Precipitation intensity must be between {MinIntensity} and {MaxIntensity}.");
        }

        if (type == PrecipitationType.None && intensity != 0)
        {
            throw new DomainValidationException(
                "Precipitation of type None must have zero intensity.");
        }

        if (type != PrecipitationType.None && intensity == 0)
        {
            throw new DomainValidationException(
                "Falling precipitation must have a positive intensity.");
        }

        Type = type;
        Intensity = intensity;
    }

    /// <summary>The kind of precipitation currently falling.</summary>
    public PrecipitationType Type { get; }

    /// <summary>The intensity of the precipitation (0–100).</summary>
    public int Intensity { get; }
}

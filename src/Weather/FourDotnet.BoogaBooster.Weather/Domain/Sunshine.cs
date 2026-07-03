using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Sunshine intensity as a percentage (0–100), validated on construction
/// (ADR-0003). Higher means brighter, sunnier weather.
/// </summary>
public sealed record Sunshine
{
    /// <summary>No sunshine.</summary>
    public const int MinPercent = 0;

    /// <summary>Full sunshine.</summary>
    public const int MaxPercent = 100;

    public Sunshine(int percent)
    {
        if (percent < MinPercent || percent > MaxPercent)
        {
            throw new DomainValidationException(
                $"Sunshine must be between {MinPercent} and {MaxPercent} percent.");
        }

        Percent = percent;
    }

    /// <summary>The sunshine intensity as a percentage.</summary>
    public int Percent { get; }
}

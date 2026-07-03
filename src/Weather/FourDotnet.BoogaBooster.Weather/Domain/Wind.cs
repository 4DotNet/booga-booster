using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Wind speed on the Beaufort scale (0–12), validated on construction (ADR-0003).
/// </summary>
public sealed record Wind
{
    /// <summary>Beaufort 0 — calm.</summary>
    public const int MinBeaufort = 0;

    /// <summary>Beaufort 12 — hurricane force.</summary>
    public const int MaxBeaufort = 12;

    public Wind(int beaufort)
    {
        if (beaufort < MinBeaufort || beaufort > MaxBeaufort)
        {
            throw new DomainValidationException(
                $"Wind must be between {MinBeaufort} and {MaxBeaufort} Beaufort.");
        }

        Beaufort = beaufort;
    }

    /// <summary>The wind speed on the Beaufort scale.</summary>
    public int Beaufort { get; }
}

using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A motor power setting as a throttle percentage (0–100), validated on
/// construction (ADR-0003). The operator commands power, not speed: the resulting
/// rotation speed emerges from this setting, the friction, and the load.
/// </summary>
public sealed record EnginePower
{
    /// <summary>Lowest setting — the motor is off.</summary>
    public const double MinPercent = 0d;

    /// <summary>Highest setting — full commanded power.</summary>
    public const double MaxPercent = 100d;

    /// <summary>A shared "motor off" value.</summary>
    public static readonly EnginePower Off = new(0d);

    public EnginePower(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
        {
            throw new DomainValidationException("Engine power must be a finite percentage.");
        }

        if (percent < MinPercent || percent > MaxPercent)
        {
            throw new DomainValidationException(
                $"Engine power must be between {MinPercent}% and {MaxPercent}%.");
        }

        Percent = percent;
    }

    /// <summary>The throttle setting as a percentage (0–100).</summary>
    public double Percent { get; }

    /// <summary>The throttle as a fraction in [0, 1].</summary>
    public double Fraction => Percent / 100d;

    /// <summary>The commanded (consumed) power in watts for a motor of the given rating.</summary>
    public double ToWatts(double maxPowerWatts) => Fraction * maxPowerWatts;
}

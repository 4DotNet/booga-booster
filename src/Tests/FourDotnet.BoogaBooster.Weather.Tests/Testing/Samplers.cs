using FourDotnet.BoogaBooster.Weather.Domain;

namespace FourDotnet.BoogaBooster.Weather.Tests.Testing;

/// <summary>A sampler that always returns the same value — makes drift deterministic.</summary>
public sealed class ConstantSampler : IWeatherSampler
{
    private readonly double _value;

    public ConstantSampler(double value) => _value = value;

    /// <summary>A sampler that adds no noise (useful for isolating mean-reversion).</summary>
    public static ConstantSampler Zero => new(0d);

    public double Sample() => _value;
}

/// <summary>A sampler that replays a fixed sequence of samples, then repeats it.</summary>
public sealed class SequenceSampler : IWeatherSampler
{
    private readonly IReadOnlyList<double> _values;
    private int _index;

    public SequenceSampler(params double[] values)
    {
        if (values is null || values.Length == 0)
        {
            throw new ArgumentException("At least one sample value is required.", nameof(values));
        }

        _values = values;
    }

    public double Sample()
    {
        var value = _values[_index % _values.Count];
        _index++;
        return value;
    }
}

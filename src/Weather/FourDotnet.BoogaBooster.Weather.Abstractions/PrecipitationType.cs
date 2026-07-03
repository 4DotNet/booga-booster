namespace FourDotnet.BoogaBooster.Weather.Abstractions;

/// <summary>
/// The kind of precipitation currently falling.
/// </summary>
public enum PrecipitationType
{
    /// <summary>No precipitation.</summary>
    None = 0,

    /// <summary>Rain.</summary>
    Rain = 1,

    /// <summary>Snow.</summary>
    Snow = 2,

    /// <summary>Hail.</summary>
    Hail = 3,
}

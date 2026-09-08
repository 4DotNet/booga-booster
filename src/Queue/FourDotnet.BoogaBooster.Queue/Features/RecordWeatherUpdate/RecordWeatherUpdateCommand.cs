using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;

/// <summary>
/// Records the latest observed weather so the background filler can scale how
/// many guests arrive. Raised by the module's weather-update subscription.
/// </summary>
/// <param name="NiceWeather">
/// The observed <c>NiceWeather</c> indicator. Values outside <c>[0, 1]</c> are
/// clamped when stored, so an out-of-range reading is recorded rather than rejected.
/// </param>
public sealed record RecordWeatherUpdateCommand(float NiceWeather) : Command;

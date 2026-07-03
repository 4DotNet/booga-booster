using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;

/// <summary>Begins a precipitation event of the given type.</summary>
/// <param name="Type">The kind of precipitation to start (Rain, Snow, or Hail).</param>
public sealed record StartPrecipitationCommand(PrecipitationType Type) : Command;

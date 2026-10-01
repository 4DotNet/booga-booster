namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>A rider who left the ride in an offload, with the mood they left in.</summary>
/// <param name="GuestNumber">The rider's guest number from the queue; <c>null</c> for a rider boarded by hand.</param>
/// <param name="Happiness">The rider's happiness on leaving, in [0, 100].</param>
/// <param name="Nausea">The rider's nausea on leaving, in [0, 100].</param>
public sealed record OffloadedRiderTelemetry(long? GuestNumber, double Happiness, double Nausea);

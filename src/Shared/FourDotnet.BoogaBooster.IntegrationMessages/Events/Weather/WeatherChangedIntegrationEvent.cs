namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;

/// <summary>
/// Published by the Weather module whenever the observed weather changes.
/// </summary>
[TopicName("weather-changed")]
public sealed record WeatherChangedIntegrationEvent(
    string Location,
    int TemperatureC,
    string Summary,
    DateTimeOffset ObservedAt) : IIntegrationEvent;

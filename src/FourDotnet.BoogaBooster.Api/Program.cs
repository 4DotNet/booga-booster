using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.Queue;
using FourDotnet.BoogaBooster.Queue.Endpoints;
using FourDotnet.BoogaBooster.Weather;
using FourDotnet.BoogaBooster.Weather.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Integration messaging: registers the Dapr client and the integration-event publisher.
builder.AddBoogaBoosterIntegrationMessages();

// Compose modules (ADR-0007).
builder.AddWeatherModule();
builder.AddQueueModule();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Unwrap Dapr CloudEvents envelopes and expose the Dapr subscription endpoint so that
// endpoints annotated with WithTopic(...) are discovered by the sidecar.
app.UseCloudEvents();
app.MapSubscribeHandler();

app.UseHttpsRedirection();

// Map module endpoints (ADR-0007).
app.MapWeatherEndpoints();
app.MapQueueEndpoints();

app.Run();

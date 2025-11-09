using TelemetryWriter.Configuration;
using TelemetryWriter.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.Configure<MqttConfig>(
    builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<OutputConfig>(
    builder.Configuration.GetSection("Output"));

// Register MQTT subscriber background service
builder.Services.AddHostedService<MqttSubscriberService>(sp =>
{
    var mqttConfig = builder.Configuration.GetSection("Mqtt").Get<MqttConfig>() ?? new MqttConfig();
    var outputConfig = builder.Configuration.GetSection("Output").Get<OutputConfig>() ?? new OutputConfig();
    var logger = sp.GetRequiredService<ILogger<MqttSubscriberService>>();
    return new MqttSubscriberService(mqttConfig, outputConfig, logger);
});

var app = builder.Build();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("Health");

app.Run();

// Make Program class available for WebApplicationFactory
namespace TelemetryWriter
{
    public partial class Program { }
}

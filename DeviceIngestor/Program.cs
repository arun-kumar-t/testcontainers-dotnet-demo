using DeviceIngestor.Configuration;
using DeviceIngestor.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.Configure<MqttConfig>(
    builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<SseConfig>(
    builder.Configuration.GetSection("Sse"));

// Register MQTT service as singleton
builder.Services.AddSingleton<MqttService>(sp =>
{
    var config = builder.Configuration.GetSection("Mqtt").Get<MqttConfig>() ?? new MqttConfig();
    var logger = sp.GetRequiredService<ILogger<MqttService>>();
    return new MqttService(config, logger);
});

// Register SSE background service
builder.Services.AddHostedService<SseService>(sp =>
{
    var sseConfig = builder.Configuration.GetSection("Sse").Get<SseConfig>() ?? new SseConfig();
    var mqttService = sp.GetRequiredService<MqttService>();
    var logger = sp.GetRequiredService<ILogger<SseService>>();
    return new SseService(sseConfig, mqttService, logger);
});

// Add services to the container
builder.Services.AddControllers();

var app = builder.Build();

// Initialize MQTT connection (skip for testing if configured)
var connectOnStartup = builder.Configuration.GetValue<bool?>("Mqtt:ConnectOnStartup") ?? true;
if (connectOnStartup)
{
    var mqttService = app.Services.GetRequiredService<MqttService>();
    await mqttService.ConnectAsync();
}

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

app.Run();

// Make Program class available for WebApplicationFactory
namespace DeviceIngestor
{
    public partial class Program { }
}

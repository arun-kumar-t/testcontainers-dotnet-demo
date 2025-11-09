using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using System.Threading;
using DeviceIngestor.Models;
using DeviceIngestor.Services;
using DeviceIngestor.Configuration;
using System.Reflection;
using DIProgram = DeviceIngestor.Program;
using FluentAssertions;

namespace IntegrationTests;

// Custom WebApplicationFactory for DeviceIngestor
public class DeviceIngestorWebApplicationFactory : WebApplicationFactory<DIProgram>
{
    private readonly string _mqttHost;
    private readonly int _mqttPort;
    private readonly string _sseUrl;

    public DeviceIngestorWebApplicationFactory(string mqttHost, int mqttPort, string sseUrl)
    {
        _mqttHost = mqttHost;
        _mqttPort = mqttPort;
        _sseUrl = sseUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Mqtt:Host", _mqttHost },
                { "Mqtt:Port", _mqttPort.ToString() },
                { "Mqtt:Topic", "iot/telemetry" },
                { "Sse:Url", _sseUrl },
                { "Mqtt:ConnectOnStartup", "false" } // Don't connect to MQTT during startup for tests
            });
        });

        base.ConfigureWebHost(builder);
    }
}

public class DeviceIngestorTests : IntegrationTestBase
{
    private DeviceIngestorWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _factory = new DeviceIngestorWebApplicationFactory(MqttHost, MqttPort, $"{FakeDeviceUrl}/events");
        _client = _factory.CreateClient();

        // Initialize MQTT connection with retry logic
        var mqttService = _factory.Services.GetRequiredService<MqttService>();
        
        // Give MQTT broker time to fully start
        await Task.Delay(1000);
        
        // Retry connection a few times
        for (int i = 0; i < 3; i++)
        {
            try
            {
                await mqttService.ConnectAsync();
                Console.WriteLine($"[IntegrationTest] DeviceIngestor MQTT connection established");
                break;
            }
            catch (Exception ex)
            {
                if (i == 2) throw;
                Console.WriteLine($"[IntegrationTest] MQTT connection attempt {i + 1} failed: {ex.Message}, retrying...");
                await Task.Delay(1000);
            }
        }
    }

    public override async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await base.DisposeAsync();
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task IngestHttpPost_ShouldPublishToMqtt()
    {
        // Arrange
        var telemetryData = new
        {
            deviceId = "test-device-001",
            temperature = 25.5,
            humidity = 60.0
        };

        var json = JsonSerializer.Serialize(telemetryData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Subscribe to MQTT to receive the message
        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await mqttClient.ConnectAsync(options, cts.Token);
        
        TelemetryMessage? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            receivedMessage = JsonSerializer.Deserialize<TelemetryMessage>(payload);
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        };

        await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("iot/telemetry")
            .Build(), cts.Token);

        // Act
        var response = await _client.PostAsync("/ingest", content, cts.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        
        // Wait for message (with timeout)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
        Assert.True(completed == messageReceived.Task, "MQTT message not received within timeout");

        Assert.NotNull(receivedMessage);
        Assert.Equal("test-device-001", receivedMessage.DeviceId);
        Assert.Equal("http", receivedMessage.Source);
        Assert.NotEqual(default(DateTime), receivedMessage.Timestamp);
        Assert.NotNull(receivedMessage.Data);

        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task IngestSse_ShouldPublishToMqtt()
    {
        // Arrange
        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await mqttClient.ConnectAsync(options, cts.Token);

        var receivedMessages = new List<TelemetryMessage>();
        var messageReceived = new TaskCompletionSource<bool>();

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var message = JsonSerializer.Deserialize<TelemetryMessage>(payload);
            if (message != null)
            {
                receivedMessages.Add(message);
                if (receivedMessages.Count >= 2) // Wait for at least 2 messages
                {
                    messageReceived.SetResult(true);
                }
            }
            return Task.CompletedTask;
        };

        await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("iot/telemetry")
            .Build(), cts.Token);

        // Act - Wait for SSE messages to be processed (the background service should be running)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
        Assert.True(completed == messageReceived.Task, "SSE messages not received within timeout");

        // Assert
        Assert.True(receivedMessages.Count >= 2, "Expected at least 2 SSE messages");
        var sseMessage = receivedMessages.FirstOrDefault(m => m.Source == "sse");
        Assert.NotNull(sseMessage);
        Assert.Equal("sse", sseMessage.Source);
        Assert.NotEqual(default(DateTime), sseMessage.Timestamp);
        Assert.NotNull(sseMessage.Data);

        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();
    }

    [Fact(Timeout = 10000)] // 10 second timeout
    public async Task IngestHttpPost_ShouldReturn400OnInvalidJson()
    {
        // Arrange
        var invalidJson = "invalid json {";
        var content = new StringContent(invalidJson, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Act
        var response = await _client.PostAsync("/ingest", content, cts.Token);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact(Timeout = 10000)] // 10 second timeout
    public async Task IngestHttpPost_ShouldReturn400OnEmptyBody()
    {
        // Arrange
        var content = new StringContent("", Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        // Act
        var response = await _client.PostAsync("/ingest", content, cts.Token);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task IngestHttpPost_ShouldHandleMissingDeviceId()
    {
        // Arrange
        var telemetryData = new { temperature = 25.5, humidity = 60.0 };
        var json = JsonSerializer.Serialize(telemetryData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Subscribe to MQTT
        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await mqttClient.ConnectAsync(options, cts.Token);
        
        TelemetryMessage? receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            receivedMessage = JsonSerializer.Deserialize<TelemetryMessage>(payload);
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        };

        await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter("iot/telemetry")
            .Build(), cts.Token);

        // Act
        var response = await _client.PostAsync("/ingest", content, cts.Token);

        // Assert
        response.EnsureSuccessStatusCode();
        
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(messageReceived.Task, Task.Delay(Timeout.Infinite, timeoutCts.Token));
        Assert.True(completed == messageReceived.Task, "MQTT message not received within timeout");

        Assert.NotNull(receivedMessage);
        Assert.Null(receivedMessage.DeviceId);
        Assert.Equal("http", receivedMessage.Source);

        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();
    }

}


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using TelemetryWriter.Configuration;
using TelemetryWriter.Models;
using FluentAssertions;
using TelemetryWriter.Services;

namespace IntegrationTests;

public class TelemetryWriterTests : IntegrationTestBase
{
    private IHost _host = null!;
    private string _tempOutputDir = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Create temp directory for output
        _tempOutputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempOutputDir);

        // Create and start TelemetryWriter as hosted service
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Mqtt:Host", MqttHost },
                { "Mqtt:Port", MqttPort.ToString() },
                { "Mqtt:Topic", "iot/telemetry" },
                { "Output:Directory", _tempOutputDir }
            })
            .Build();

        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(builder => builder.AddConfiguration(configuration))
            .ConfigureServices((context, services) =>
            {
                services.Configure<TelemetryWriter.Configuration.MqttConfig>(context.Configuration.GetSection("Mqtt"));
                services.Configure<OutputConfig>(context.Configuration.GetSection("Output"));

                services.AddHostedService<MqttSubscriberService>(sp =>
                {
                    var mqttConfig = context.Configuration.GetSection("Mqtt").Get<TelemetryWriter.Configuration.MqttConfig>() ?? new TelemetryWriter.Configuration.MqttConfig();
                    var outputConfig = context.Configuration.GetSection("Output").Get<OutputConfig>() ?? new OutputConfig();
                    var logger = sp.GetRequiredService<ILogger<MqttSubscriberService>>();
                    return new MqttSubscriberService(mqttConfig, outputConfig, logger);
                });
            });

        _host = hostBuilder.Build();
        await _host.StartAsync();

        // Give the service time to connect to MQTT
        await Task.Delay(2000);
    }

    public override async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // Clean up temp directory
        if (Directory.Exists(_tempOutputDir))
        {
            Directory.Delete(_tempOutputDir, true);
        }

        await base.DisposeAsync();
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task SubscribeToMqtt_ShouldWriteFile()
    {
        // Arrange
        var telemetryMessage = new TelemetryMessage
        {
            DeviceId = "test-device-001",
            Data = new Dictionary<string, object>
            {
                { "temperature", 25.5 },
                { "humidity", 60.0 }
            },
            Timestamp = DateTime.UtcNow,
            Source = "test"
        };

        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await mqttClient.ConnectAsync(options, cts.Token);

        // Act - Publish message to MQTT
        var json = JsonSerializer.Serialize(telemetryMessage);
        var messagePayload = new MqttApplicationMessageBuilder()
            .WithTopic("iot/telemetry")
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqttClient.PublishAsync(messagePayload, cts.Token);
        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();

        // Wait for file to be written with timeout
        using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.Delay(2000, delayCts.Token);

        // Assert
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        Assert.NotEmpty(files);

        var file = files.FirstOrDefault(f => f.Contains("test-device-001"));
        Assert.NotNull(file);

        var fileContent = await File.ReadAllTextAsync(file);
        var writtenMessage = JsonSerializer.Deserialize<TelemetryMessage>(fileContent);

        Assert.NotNull(writtenMessage);
        Assert.Equal("test-device-001", writtenMessage.DeviceId);
        Assert.Equal("test", writtenMessage.Source);
        Assert.NotNull(writtenMessage.Data);
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task MultipleMessages_ShouldWriteMultipleFiles()
    {
        // Arrange
        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await mqttClient.ConnectAsync(options, cts.Token);

        // Act - Publish multiple messages
        for (int i = 1; i <= 3; i++)
        {
            var message = new TelemetryMessage
            {
                DeviceId = $"device-{i:D3}",
                Data = new Dictionary<string, object> { { "value", i } },
                Timestamp = DateTime.UtcNow,
                Source = "test"
            };

            var json = JsonSerializer.Serialize(message);
            var messagePayload = new MqttApplicationMessageBuilder()
                .WithTopic("iot/telemetry")
                .WithPayload(json)
                .Build();

            await mqttClient.PublishAsync(messagePayload, cts.Token);
            await Task.Delay(100, cts.Token); // Small delay between messages
        }

        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();

        // Wait for files to be written with timeout
        using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.Delay(3000, delayCts.Token);

        // Assert
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        Assert.True(files.Length >= 3, $"Expected at least 3 files, but found {files.Length}");

        // Verify file naming format: timestamp_deviceId.json
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            Assert.Matches(@"^\d+_device-\d{3}\.json$", fileName);
        }
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task SubscribeToMqtt_ShouldHandleMessageWithoutDeviceId()
    {
        // Arrange
        var factory = new MqttFactory();
        var mqttClient = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(MqttHost, MqttPort)
            .Build();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await mqttClient.ConnectAsync(options, cts.Token);

        var message = new TelemetryMessage
        {
            DeviceId = null,
            Data = new Dictionary<string, object> { { "value", 100 } },
            Timestamp = DateTime.UtcNow,
            Source = "test"
        };

        // Act
        var json = JsonSerializer.Serialize(message);
        var messagePayload = new MqttApplicationMessageBuilder()
            .WithTopic("iot/telemetry")
            .WithPayload(json)
            .Build();

        await mqttClient.PublishAsync(messagePayload, cts.Token);
        await mqttClient.DisconnectAsync();
        mqttClient.Dispose();

        using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.Delay(2000, delayCts.Token);

        // Assert
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        files.Should().NotBeEmpty();

        var file = files.FirstOrDefault(f => f.Contains("unknown"));
        file.Should().NotBeNull();
    }

}


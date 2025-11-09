using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using DeviceIngestor.Models;
using DeviceIngestor.Services;
using DeviceIngestor.Configuration;
using TelemetryWriter.Configuration;
using TelemetryWriter.Services;
using TelemetryWriter.Models;
using FluentAssertions;

namespace IntegrationTests;

public class EndToEndTests : IntegrationTestBase
{
    private DeviceIngestorWebApplicationFactory _deviceIngestorFactory = null!;
    private HttpClient _deviceIngestorClient = null!;
    private IHost _telemetryWriterHost = null!;
    private string _tempOutputDir = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Create temp directory for TelemetryWriter output
        _tempOutputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempOutputDir);

        // Start DeviceIngestor
        _deviceIngestorFactory = new DeviceIngestorWebApplicationFactory(MqttHost, MqttPort, $"{FakeDeviceUrl}/events");
        _deviceIngestorClient = _deviceIngestorFactory.CreateClient();

        var mqttService = _deviceIngestorFactory.Services.GetRequiredService<MqttService>();
        await mqttService.ConnectAsync();

        // Start TelemetryWriter
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

        _telemetryWriterHost = hostBuilder.Build();
        await _telemetryWriterHost.StartAsync();

        // Give services time to initialize
        await Task.Delay(3000);
    }

    public override async Task DisposeAsync()
    {
        _deviceIngestorClient?.Dispose();
        _deviceIngestorFactory?.Dispose();

        if (_telemetryWriterHost != null)
        {
            await _telemetryWriterHost.StopAsync();
            _telemetryWriterHost.Dispose();
        }

        // Clean up temp directory
        if (Directory.Exists(_tempOutputDir))
        {
            Directory.Delete(_tempOutputDir, true);
        }

        await base.DisposeAsync();
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task HttpPostToIngest_ShouldWriteFile()
    {
        // Arrange
        var telemetryData = new
        {
            deviceId = "e2e-device-001",
            temperature = 27.3,
            humidity = 65.5
        };

        var json = JsonSerializer.Serialize(telemetryData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // Act - Send HTTP POST to DeviceIngestor
        var response = await _deviceIngestorClient.PostAsync("/ingest", content, cts.Token);
        response.EnsureSuccessStatusCode();

        // Wait for message to flow through MQTT and be written to file
        // Use longer timeout for CI environments where processing may be slower
        using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.Delay(3000, delayCts.Token);

        // Assert - Verify file was written
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        Assert.NotEmpty(files);

        var file = files.FirstOrDefault(f => f.Contains("e2e-device-001"));
        Assert.NotNull(file);

        var fileContent = await File.ReadAllTextAsync(file);
        var writtenMessage = JsonSerializer.Deserialize<TelemetryWriter.Models.TelemetryMessage>(fileContent);

        Assert.NotNull(writtenMessage);
        Assert.Equal("e2e-device-001", writtenMessage.DeviceId);
        Assert.Equal("http", writtenMessage.Source);
        Assert.NotEqual(default(DateTime), writtenMessage.Timestamp);
        Assert.NotNull(writtenMessage.Data);
        Assert.True(writtenMessage.Data.ContainsKey("temperature"));
        Assert.True(writtenMessage.Data.ContainsKey("humidity"));
    }

    [Fact(Timeout = 30000)] // 30 second timeout
    public async Task SseEvents_ShouldWriteFiles()
    {
        // Act - Wait for SSE events to be processed
        // The fake device container emits SSE events every second
        // DeviceIngestor should pick them up and publish to MQTT
        // TelemetryWriter should write them to files
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.Delay(8000, cts.Token); // Wait for at least a few SSE events

        // Assert - Verify files were written from SSE
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        Assert.NotEmpty(files);

        // Find at least one file from SSE source
        var sseFiles = new List<TelemetryWriter.Models.TelemetryMessage>();
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var message = JsonSerializer.Deserialize<TelemetryWriter.Models.TelemetryMessage>(content);
            if (message != null && message.Source == "sse")
            {
                sseFiles.Add(message);
            }
        }

        Assert.NotEmpty(sseFiles);
        var sseMessage = sseFiles.First();
        Assert.Equal("sse", sseMessage.Source);
        Assert.NotNull(sseMessage.DeviceId);
        Assert.NotEqual(default(DateTime), sseMessage.Timestamp);
        Assert.NotNull(sseMessage.Data);
    }

    [Fact(Timeout = 45000)] // 45 second timeout - increased for CI environments
    public async Task FullFlow_HttpAndSse_ShouldWriteMultipleFiles()
    {
        // Arrange - Send HTTP POST
        var httpData = new
        {
            deviceId = "fullflow-http-001",
            temperature = 30.0,
            humidity = 70.0
        };

        var httpJson = JsonSerializer.Serialize(httpData);
        var httpContent = new StringContent(httpJson, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // Act
        var response = await _deviceIngestorClient.PostAsync("/ingest", httpContent, cts.Token);
        response.EnsureSuccessStatusCode();

        // Wait for both HTTP and SSE messages to be processed
        // Use longer timeout for CI environments where processing may be slower
        using var delayCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Task.Delay(10000, delayCts.Token);

        // Assert
        var files = Directory.GetFiles(_tempOutputDir, "*.json");
        Assert.True(files.Length >= 2, $"Expected at least 2 files (1 HTTP + 1+ SSE), but found {files.Length}");

        // Verify HTTP message file
        var httpFile = files.FirstOrDefault(f => f.Contains("fullflow-http-001"));
        Assert.NotNull(httpFile);

        var httpFileContent = await File.ReadAllTextAsync(httpFile);
        var httpMessage = JsonSerializer.Deserialize<TelemetryWriter.Models.TelemetryMessage>(httpFileContent);
        Assert.NotNull(httpMessage);
        Assert.Equal("http", httpMessage.Source);

        // Verify at least one SSE message file exists
        var hasSseFile = false;
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var msg = JsonSerializer.Deserialize<TelemetryWriter.Models.TelemetryMessage>(content);
            if (msg?.Source == "sse")
            {
                hasSseFile = true;
                break;
            }
        }
        Assert.True(hasSseFile, "Expected at least one SSE message file");
    }

}


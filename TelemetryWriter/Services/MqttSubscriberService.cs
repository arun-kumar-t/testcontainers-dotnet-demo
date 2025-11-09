using MQTTnet;
using MQTTnet.Client;
using TelemetryWriter.Configuration;
using System.Text;
using System.Text.Json;
using TelemetryWriter.Models;

namespace TelemetryWriter.Services;

public class MqttSubscriberService : BackgroundService
{
    private readonly MqttConfig _mqttConfig;
    private readonly OutputConfig _outputConfig;
    private readonly ILogger<MqttSubscriberService> _logger;
    private IMqttClient? _client;

    public MqttSubscriberService(
        MqttConfig mqttConfig,
        OutputConfig outputConfig,
        ILogger<MqttSubscriberService> logger)
    {
        _mqttConfig = mqttConfig;
        _outputConfig = outputConfig;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure output directory exists
        if (!Directory.Exists(_outputConfig.Directory))
        {
            Directory.CreateDirectory(_outputConfig.Directory);
            _logger.LogInformation("Created output directory: {Directory}", _outputConfig.Directory);
        }

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                var message = JsonSerializer.Deserialize<TelemetryMessage>(payload);

                if (message != null)
                {
                    await WriteMessageToFile(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message");
            }
        };

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(options, stoppingToken);
                    _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _mqttConfig.Host, _mqttConfig.Port);

                    var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(_mqttConfig.Topic)
                        .Build();

                    await _client.SubscribeAsync(subscribeOptions, stoppingToken);
                    _logger.LogInformation("Subscribed to topic: {Topic}", _mqttConfig.Topic);
                }

                // Keep the service running
                await Task.Delay(1000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MQTT subscriber, will retry in 5 seconds");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task WriteMessageToFile(TelemetryMessage message)
    {
        try
        {
            var deviceId = message.DeviceId ?? "unknown";
            var timestamp = message.Timestamp.ToString("yyyyMMddHHmmssfff");
            var filename = $"{timestamp}_{deviceId}.json";
            var filepath = Path.Combine(_outputConfig.Directory, filename);

            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filepath, json);

            _logger.LogInformation("Written message to file: {Filepath}", filepath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing message to file");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}


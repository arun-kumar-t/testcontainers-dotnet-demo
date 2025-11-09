using MQTTnet;
using MQTTnet.Client;
using DeviceIngestor.Configuration;
using System.Text;
using System.Text.Json;

namespace DeviceIngestor.Services;

public class MqttService : IDisposable
{
    private readonly MqttConfig _config;
    private readonly ILogger<MqttService> _logger;
    private IMqttClient? _client;

    public MqttService(MqttConfig config, ILogger<MqttService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ConnectAsync()
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_config.Host, _config.Port)
            .Build();

        try
        {
            await _client.ConnectAsync(options);
            _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _config.Host, _config.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            throw;
        }
    }

    public async Task PublishAsync<T>(T message)
    {
        if (_client == null || !_client.IsConnected)
        {
            _logger.LogWarning("MQTT client not connected, attempting to reconnect...");
            await ConnectAsync();
        }

        if (_client == null)
        {
            throw new InvalidOperationException("MQTT client is not initialized");
        }

        try
        {
            var json = JsonSerializer.Serialize(message);
            var messagePayload = new MqttApplicationMessageBuilder()
                .WithTopic(_config.Topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.PublishAsync(messagePayload);
            _logger.LogDebug("Published message to topic {Topic}", _config.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to MQTT");
            throw;
        }
    }

    public void Dispose()
    {
        _client?.DisconnectAsync().GetAwaiter().GetResult();
        _client?.Dispose();
    }
}


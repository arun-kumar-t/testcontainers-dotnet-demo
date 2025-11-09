using System.Text.Json;
using DeviceIngestor.Configuration;
using DeviceIngestor.Models;

namespace DeviceIngestor.Services;

public class SseService : BackgroundService
{
    private readonly SseConfig _config;
    private readonly MqttService _mqttService;
    private readonly ILogger<SseService> _logger;
    private readonly HttpClient _httpClient;

    public SseService(
        SseConfig config,
        MqttService mqttService,
        ILogger<SseService> logger)
    {
        _config = config;
        _mqttService = mqttService;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SSE reader for {Url}", _config.Url);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _config.Url);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stoppingToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(stoppingToken);
                using var reader = new StreamReader(stream);

                _logger.LogInformation("Connected to SSE stream");

                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !stoppingToken.IsCancellationRequested)
                {
                    if (line.StartsWith("data: "))
                    {
                        var jsonData = line.Substring(6); // Remove "data: " prefix
                        await ProcessSseEvent(jsonData, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SSE stream, will retry in 5 seconds");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessSseEvent(string jsonData, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData);
            if (message == null)
            {
                _logger.LogWarning("Received null or invalid JSON from SSE");
                return;
            }

            var enrichedMessage = new TelemetryMessage
            {
                Data = message,
                Timestamp = DateTime.UtcNow,
                Source = "sse"
            };

            // Try to extract deviceId if present
            if (message.TryGetValue("deviceId", out var deviceIdObj))
            {
                enrichedMessage.DeviceId = deviceIdObj?.ToString();
            }

            await _mqttService.PublishAsync(enrichedMessage);
            _logger.LogDebug("Processed and published SSE event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SSE event");
        }
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}


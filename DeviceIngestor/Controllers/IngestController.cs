using DeviceIngestor.Models;
using DeviceIngestor.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DeviceIngestor.Controllers;

[ApiController]
[Route("ingest")]
public class IngestController : ControllerBase
{
    private readonly MqttService _mqttService;
    private readonly ILogger<IngestController> _logger;

    public IngestController(MqttService mqttService, ILogger<IngestController> logger)
    {
        _mqttService = mqttService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return BadRequest(new { error = "Request body is required" });
            }

            Dictionary<string, object>? message;
            try
            {
                message = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBody);
            }
            catch (JsonException)
            {
                return BadRequest(new { error = "Invalid JSON" });
            }

            if (message == null)
            {
                return BadRequest(new { error = "Invalid JSON" });
            }

            var enrichedMessage = new TelemetryMessage
            {
                Data = message,
                Timestamp = DateTime.UtcNow,
                Source = "http"
            };

            // Try to extract deviceId if present
            if (message.TryGetValue("deviceId", out var deviceIdObj))
            {
                enrichedMessage.DeviceId = deviceIdObj?.ToString();
            }

            await _mqttService.PublishAsync(enrichedMessage);

            return Ok(new { message = "Message ingested and published", timestamp = enrichedMessage.Timestamp });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ingest request");
            return StatusCode(500);
        }
    }
}


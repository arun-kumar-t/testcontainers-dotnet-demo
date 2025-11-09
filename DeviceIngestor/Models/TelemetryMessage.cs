namespace DeviceIngestor.Models;

public class TelemetryMessage
{
    public string? DeviceId { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Source { get; set; }
}


namespace DeviceIngestor.Configuration;

public class MqttConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string Topic { get; set; } = "iot/telemetry";
}


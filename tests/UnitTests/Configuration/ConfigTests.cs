using FluentAssertions;
using TelemetryWriter.Configuration;
using Xunit;
using DI_MqttConfig = DeviceIngestor.Configuration.MqttConfig;
using DI_SseConfig = DeviceIngestor.Configuration.SseConfig;

namespace UnitTests.Configuration;

public class ConfigTests
{
    [Fact]
    public void MqttConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new DI_MqttConfig();

        // Assert
        config.Host.Should().Be("localhost");
        config.Port.Should().Be(1883);
        config.Topic.Should().Be("iot/telemetry");
    }

    [Fact]
    public void SseConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new DI_SseConfig();

        // Assert
        config.Url.Should().Be("http://localhost:5005/events");
    }

    [Fact]
    public void OutputConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new OutputConfig();

        // Assert
        config.Directory.Should().Be("data");
    }
}


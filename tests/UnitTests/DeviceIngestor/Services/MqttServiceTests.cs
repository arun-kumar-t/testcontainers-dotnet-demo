using DeviceIngestor.Configuration;
using DeviceIngestor.Models;
using DeviceIngestor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UnitTests.DeviceIngestor.Services;

public class MqttServiceTests : IDisposable
{
    private readonly Mock<ILogger<MqttService>> _loggerMock;
    private readonly MqttConfig _config;
    private MqttService? _service;

    public MqttServiceTests()
    {
        _loggerMock = new Mock<ILogger<MqttService>>();
        _config = new MqttConfig
        {
            Host = "localhost",
            Port = 1883,
            Topic = "test/topic"
        };
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        _service = new MqttService(_config, _loggerMock.Object);

        // Act & Assert - Should not throw even if client is null
        _service.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithConfig()
    {
        // Arrange & Act
        _service = new MqttService(_config, _loggerMock.Object);

        // Assert
        _service.Should().NotBeNull();
    }
}


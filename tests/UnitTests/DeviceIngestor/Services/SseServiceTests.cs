using DeviceIngestor.Services;
using MqttConfig = DeviceIngestor.Configuration.MqttConfig;
using SseConfig = DeviceIngestor.Configuration.SseConfig;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace UnitTests.DeviceIngestor.Services;

public class SseServiceTests : IDisposable
{
    private readonly Mock<ILogger<SseService>> _loggerMock;
    private readonly Mock<MqttService> _mqttServiceMock;
    private readonly SseConfig _config;
    private SseService? _service;

    public SseServiceTests()
    {
        _loggerMock = new Mock<ILogger<SseService>>();
        _mqttServiceMock = new Mock<MqttService>(
            new MqttConfig(),
            Mock.Of<ILogger<MqttService>>());
        _config = new SseConfig
        {
            Url = "http://localhost:5005/events"
        };
    }

    public void Dispose()
    {
        _service?.Dispose();
    }


    [Fact]
    public void Constructor_ShouldInitializeWithDependencies()
    {
        // Arrange & Act
        _service = new SseService(_config, _mqttServiceMock.Object, _loggerMock.Object);

        // Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldDisposeHttpClient()
    {
        // Arrange
        _service = new SseService(_config, _mqttServiceMock.Object, _loggerMock.Object);

        // Act
        _service.Invoking(s => s.Dispose()).Should().NotThrow();
    }
}


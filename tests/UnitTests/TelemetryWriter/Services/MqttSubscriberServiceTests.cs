using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Text.Json;
using TelemetryWriter.Configuration;
using TelemetryWriter.Models;
using TelemetryWriter.Services;
using Xunit;

namespace UnitTests.TelemetryWriter.Services;

public class MqttSubscriberServiceTests : IDisposable
{
    private readonly Mock<ILogger<MqttSubscriberService>> _loggerMock;
    private readonly MqttConfig _mqttConfig;
    private readonly OutputConfig _outputConfig;
    private MqttSubscriberService? _service;
    private string _tempDirectory = null!;

    public MqttSubscriberServiceTests()
    {
        _loggerMock = new Mock<ILogger<MqttSubscriberService>>();
        _mqttConfig = new MqttConfig
        {
            Host = "localhost",
            Port = 1883,
            Topic = "test/topic"
        };
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _outputConfig = new OutputConfig
        {
            Directory = _tempDirectory
        };
    }

    public void Dispose()
    {
        _service?.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task WriteMessageToFile_ShouldCreateFileWithCorrectName()
    {
        // Arrange
        _service = new MqttSubscriberService(_mqttConfig, _outputConfig, _loggerMock.Object);
        var message = new TelemetryMessage
        {
            DeviceId = "test-device",
            Data = new Dictionary<string, object> { { "temp", 25.5 } },
            Timestamp = new DateTime(2024, 1, 15, 10, 30, 0),
            Source = "test"
        };

        // Ensure directory exists
        Directory.CreateDirectory(_tempDirectory);

        // Use reflection to call private method
        var method = typeof(MqttSubscriberService).GetMethod("WriteMessageToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        // Act
        await (Task)method!.Invoke(_service, new object[] { message })!;

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "*.json");
        files.Should().HaveCount(1);
        var fileName = Path.GetFileName(files[0]);
        fileName.Should().StartWith("20240115103000");
        fileName.Should().Contain("test-device");
        fileName.Should().EndWith(".json");
    }

    [Fact]
    public async Task WriteMessageToFile_ShouldWriteValidJson()
    {
        // Arrange
        _service = new MqttSubscriberService(_mqttConfig, _outputConfig, _loggerMock.Object);
        var message = new TelemetryMessage
        {
            DeviceId = "test-device",
            Data = new Dictionary<string, object> { { "temperature", 25.5 } },
            Timestamp = DateTime.UtcNow,
            Source = "test"
        };

        // Ensure directory exists
        Directory.CreateDirectory(_tempDirectory);

        // Use reflection to call private method
        var method = typeof(MqttSubscriberService).GetMethod("WriteMessageToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        // Act
        await (Task)method!.Invoke(_service, new object[] { message })!;

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "*.json");
        files.Should().HaveCount(1);
        var fileContent = await File.ReadAllTextAsync(files[0]);
        var deserialized = JsonSerializer.Deserialize<TelemetryMessage>(fileContent);
        deserialized.Should().NotBeNull();
        deserialized!.DeviceId.Should().Be("test-device");
        deserialized.Source.Should().Be("test");
        deserialized.Data.Should().ContainKey("temperature");
    }

    [Fact]
    public async Task WriteMessageToFile_ShouldHandleUnknownDeviceId()
    {
        // Arrange
        _service = new MqttSubscriberService(_mqttConfig, _outputConfig, _loggerMock.Object);
        var message = new TelemetryMessage
        {
            DeviceId = null,
            Data = new Dictionary<string, object> { { "value", 100 } },
            Timestamp = DateTime.UtcNow,
            Source = "test"
        };

        // Ensure directory exists
        Directory.CreateDirectory(_tempDirectory);

        // Use reflection to call private method
        var method = typeof(MqttSubscriberService).GetMethod("WriteMessageToFile", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        // Act
        await (Task)method!.Invoke(_service, new object[] { message })!;

        // Assert
        var files = Directory.GetFiles(_tempDirectory, "*.json");
        files.Should().HaveCount(1);
        var fileName = Path.GetFileName(files[0]);
        fileName.Should().Contain("unknown");
    }


    [Fact]
    public void Constructor_ShouldInitializeWithConfig()
    {
        // Arrange & Act
        _service = new MqttSubscriberService(_mqttConfig, _outputConfig, _loggerMock.Object);

        // Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        _service = new MqttSubscriberService(_mqttConfig, _outputConfig, _loggerMock.Object);

        // Act & Assert
        _service.Invoking(s => s.Dispose()).Should().NotThrow();
    }

}


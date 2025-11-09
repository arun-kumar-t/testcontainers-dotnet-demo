using DeviceIngestor.Controllers;
using DeviceIngestor.Models;
using DeviceIngestor.Services;
using MqttConfig = DeviceIngestor.Configuration.MqttConfig;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UnitTests.DeviceIngestor.Controllers;

public class IngestControllerTests
{
    private readonly Mock<MqttService> _mqttServiceMock;
    private readonly Mock<ILogger<IngestController>> _loggerMock;
    private readonly IngestController _controller;
    private readonly DefaultHttpContext _httpContext;

    public IngestControllerTests()
    {
        _mqttServiceMock = new Mock<MqttService>(
            new MqttConfig(),
            Mock.Of<ILogger<MqttService>>());
        _loggerMock = new Mock<ILogger<IngestController>>();
        _controller = new IngestController(_mqttServiceMock.Object, _loggerMock.Object);
        _httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Fact]
    public async Task Post_ShouldReturn400OnEmptyBody()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));
        _httpContext.Request.Body = stream;

        // Act
        var result = await _controller.Post();

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
        // Note: Can't verify PublishAsync with Moq since it's not virtual
    }

    [Fact]
    public async Task Post_ShouldReturn400OnInvalidJson()
    {
        // Arrange
        var invalidJson = "invalid json {";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));
        _httpContext.Request.Body = stream;

        // Act
        var result = await _controller.Post();

        // Assert - Controller now catches JsonException and returns 400 BadRequest
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
    }

}


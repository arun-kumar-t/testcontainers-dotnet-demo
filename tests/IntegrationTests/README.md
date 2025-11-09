# Integration Tests

This directory contains Testcontainers-based integration and end-to-end tests for the DeviceIngestor and TelemetryWriter services.

## Status

✅ **All 11 integration tests passing**

## Overview

The test suite includes:

- **IntegrationTestBase**: Base class that manages MQTT and fake device containers
- **DeviceIngestorTests**: Tests for HTTP POST and SSE ingestion (5 tests)
- **TelemetryWriterTests**: Tests for MQTT subscription and file writing (3 tests)
- **EndToEndTests**: Full end-to-end flow tests (3 tests)

## How It Works

**Testcontainers automatically manages containers** - you don't need to manually start anything!

1. **Containers start automatically** when tests run:
   - MQTT broker (eclipse-mosquitto:2) with anonymous access
   - Fake device server (node:20-alpine) for SSE events

2. **Services run on your machine** (not in containers):
   - DeviceIngestor runs via `WebApplicationFactory` (in-memory web server)
   - TelemetryWriter runs as `HostedService` (background service)
   - Both connect to containers via `localhost:randomPort`

3. **Containers are cleaned up automatically** after tests complete

## Prerequisites

- .NET 8 SDK
- **Docker Desktop (or Docker Engine) - REQUIRED for Testcontainers**
- **Docker must be running before executing tests**

### ⚠️ Important: Docker Requirement

**All integration tests require Docker to be running.** If you see errors like:
```
Docker is either not running or misconfigured
```

**Solution:**
1. Start Docker Desktop (or Docker Engine)
2. Verify Docker is running: `docker ps`
3. Ensure Docker daemon is accessible (check Docker Desktop settings)
4. Re-run the tests: `dotnet test`

## Test Infrastructure

### Fake Device Container

The `FakeDevice/` directory contains a simple Node.js server that:
- Exposes `POST /mock` endpoint returning telemetry data
- Exposes `GET /events` SSE endpoint that emits JSON messages every second
- Exposes `GET /health` for health checks

This container is automatically built and started by Testcontainers during test execution.

### MQTT Container

Tests use Eclipse Mosquitto MQTT broker container with `mosquitto.conf` configured to allow anonymous connections. The configuration file is automatically mounted into the container.

## Running Tests

### Run All Tests

From the solution root:
```bash
dotnet test
```

Or from the test project directory:
```bash
cd tests/IntegrationTests/IntegrationTests
dotnet test
```

### Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~DeviceIngestorTests"
dotnet test --filter "FullyQualifiedName~TelemetryWriterTests"
dotnet test --filter "FullyQualifiedName~EndToEndTests"
```

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~IngestHttpPost_ShouldPublishToMqtt"
```

## Test Structure

### IntegrationTestBase

Base class that:
- Starts MQTT container (Eclipse Mosquitto)
- Starts fake device container
- Provides properties for MQTT host/port and fake device URL
- Cleans up containers after tests

### DeviceIngestorTests

**IngestHttpPost_ShouldPublishToMqtt**
- Sends HTTP POST to `/ingest` endpoint
- Subscribes to MQTT topic
- Verifies enriched message is published

**IngestSse_ShouldPublishToMqtt**
- Waits for SSE events from fake device
- Verifies SSE messages are published to MQTT with `source: "sse"`

### TelemetryWriterTests

**SubscribeToMqtt_ShouldWriteFile**
- Publishes test message to MQTT
- Verifies file is written with correct format and content

**MultipleMessages_ShouldWriteMultipleFiles**
- Publishes multiple messages
- Verifies multiple files are created with correct naming

### EndToEndTests

**HttpPostToIngest_ShouldWriteFile**
- Full flow: HTTP POST → DeviceIngestor → MQTT → TelemetryWriter → File
- Verifies file contains enriched message

**SseEvents_ShouldWriteFiles**
- Full flow: SSE → DeviceIngestor → MQTT → TelemetryWriter → File
- Verifies SSE messages are written to files

**FullFlow_HttpAndSse_ShouldWriteMultipleFiles**
- Tests both HTTP and SSE flows simultaneously
- Verifies multiple files are created from different sources

## Test Configuration

Tests use in-memory configuration to override:
- MQTT host and port (from container)
- SSE URL (from fake device container)
- Output directory (temp directory for TelemetryWriter)

## Troubleshooting

### Docker Not Running

If you see errors about Docker:
```
Error: Cannot connect to the Docker daemon
```

Ensure Docker Desktop (or Docker Engine) is running.

### Container Build Failures

If fake device container fails to build:
- Ensure Docker has access to build contexts
- Check that `FakeDevice/Dockerfile` and `FakeDevice/server.js` exist

### Port Conflicts

If you see port binding errors:
- Ensure no other services are using ports 1883 (MQTT) or 3000 (fake device)
- Testcontainers uses random ports by default, but conflicts can occur

### Test Timeouts

Some tests wait for async operations (SSE events, file writes). If tests timeout:
- Increase wait times in test code
- Check container logs for errors
- Verify MQTT broker is accessible

## File Naming

TelemetryWriter creates files with format: `<timestamp>_<deviceId>.json`

Example: `20240115103000123_device-001.json`

## Container Lifecycle

**Important:** Containers are created and destroyed **per test class**, not per test method.

### Why You See Containers Being Created/Removed Repeatedly

When you run all integration tests, you'll see this pattern in Docker Dashboard:

1. **Containers created** → `DeviceIngestorTests` (5 tests) runs → **Containers destroyed**
2. **Containers created** → `TelemetryWriterTests` (3 tests) runs → **Containers destroyed**
3. **Containers created** → `EndToEndTests` (3 tests) runs → **Containers destroyed**

This is **expected behavior** and provides:
- ✅ **Test isolation**: Each test class gets fresh containers
- ✅ **Clean state**: No data leakage between test classes
- ✅ **Reliability**: One failing test class doesn't affect others

### How It Works

Each test class inherits from `IntegrationTestBase` which implements `IAsyncLifetime`:
- `InitializeAsync()`: Called **once** before all tests in the class run → Creates containers
- `DisposeAsync()`: Called **once** after all tests in the class complete → Destroys containers

### Performance Impact

- **Container startup**: ~2-5 seconds per test class
- **Total overhead**: ~6-15 seconds for all 3 test classes
- **Trade-off**: Slightly slower execution for better isolation

If you want to optimize (share containers across all tests), you can use a shared fixture, but this reduces isolation.

## Notes

- Tests use temporary directories that are cleaned up after execution
- Containers are automatically stopped and removed after tests
- Tests run sequentially (via `[Collection]` attribute) to prevent resource contention
- All tests have timeouts to prevent hanging (10-60 seconds depending on test complexity)
- MQTT broker is configured with anonymous access for testing (see `mosquitto.conf`)


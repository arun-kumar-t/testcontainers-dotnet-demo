# Testing Guide

## Running Unit Tests Only

To run only unit tests (which are fast and don't require Docker):

```bash
dotnet test tests/UnitTests/UnitTests.csproj
```

### With Code Coverage

```bash
# Run unit tests with coverage
dotnet test tests/UnitTests/UnitTests.csproj --collect:"XPlat Code Coverage" --results-directory:"./TestResults"

# Generate HTML coverage report
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"Html;TextSummary"

# View summary
cat CoverageReport/Summary.txt

# Or use the helper script
./coverage.sh
```

## Unit Tests

- **Location**: `tests/UnitTests/`
- **No dependencies**: Don't require Docker or external services
- **Fast**: Complete in seconds
- **Coverage**: 14 passing tests covering services, controllers, and configuration

### Unit Test Structure

- `DeviceIngestor/Services/MqttServiceTests.cs` - MQTT service tests
- `DeviceIngestor/Services/SseServiceTests.cs` - SSE service tests
- `DeviceIngestor/Controllers/IngestControllerTests.cs` - Controller tests
- `TelemetryWriter/Services/MqttSubscriberServiceTests.cs` - Subscriber tests
- `Configuration/ConfigTests.cs` - Configuration tests

## Integration Tests

- **Location**: `tests/IntegrationTests/IntegrationTests/`
- **Requires Docker**: Must have Docker Desktop running
- **Status**: ✅ **All 11 tests passing**
- **Duration**: ~30-60 seconds (container startup + test execution)

### Integration Test Setup

The integration tests use Testcontainers to automatically:
- Start MQTT broker (eclipse-mosquitto:2) with anonymous access enabled
- Start fake device server (node:20-alpine) for SSE events
- Run DeviceIngestor and TelemetryWriter on the host machine
- Clean up containers after tests complete

### Running Integration Tests

To run integration tests:

```bash
# Ensure Docker is running
docker ps

# Run integration tests
dotnet test tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj

# Run specific test class
dotnet test tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~DeviceIngestorTests"

# Run specific test
dotnet test tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~IngestHttpPost_ShouldReturn400OnEmptyBody"
```

## Test Summary

| Test Type | Count | Status | Duration |
|-----------|-------|--------|----------|
| Unit Tests | 14 | ✅ Passing | ~1s |
| Integration Tests | 11 | ✅ Passing | ~30-60s |

## Quick Commands

```bash
# Unit tests only (recommended for development)
dotnet test tests/UnitTests/UnitTests.csproj

# Unit tests with coverage
./coverage.sh

# All tests
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~MqttServiceTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~ConnectAsync_ShouldConnectSuccessfully"
```

## Test Coverage

Current coverage (unit tests only):
- Line coverage: 24.6%
- Branch coverage: 10.9%
- Method coverage: 60.6%

## Recommendations

For development and CI/CD:
1. **Always run unit tests**: Fast and reliable (no Docker needed)
2. **Integration tests**: Run in CI/CD pipeline or before commits
3. **Manual testing**: Use docker-compose for full system testing

## Manual Testing with Docker Compose

For end-to-end testing without test containers:

```bash
# Start all services
docker-compose up --build

# Test HTTP endpoint
curl -X POST http://localhost:5000/ingest \
  -H "Content-Type: application/json" \
  -d '{"deviceId": "test-001", "temperature": 25.5}'

# Check output files
ls -la TelemetryWriter/data/

# Stop services
docker-compose down
```


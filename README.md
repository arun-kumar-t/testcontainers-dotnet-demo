# .NET 8 IoT Telemetry Processing with Testcontainers

[![CI](https://github.com/arun-kumar-t/testcontainers-dotnet-demo/actions/workflows/ci.yml/badge.svg)](https://github.com/arun-kumar-t/testcontainers-dotnet-demo/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-25%20passing-brightgreen)](https://github.com/arun-kumar-t/testcontainers-dotnet-demo/actions)
[![Coverage](https://img.shields.io/badge/coverage-24.6%25-yellow)](https://github.com/arun-kumar-t/testcontainers-dotnet-demo/actions)

> **A practical demonstration of integration testing with Testcontainers for .NET services that depend on external infrastructure (MQTT, devices, etc.)**

This repository showcases a complete IoT telemetry processing pipeline with **comprehensive integration tests** using [Testcontainers for .NET](https://dotnet.testcontainers.org/). It demonstrates how to test services that depend on external dependencies like MQTT brokers, device simulators, and other services without manual setup or shared test environments.

## Table of Contents

- [Use Case](#-use-case)
- [Key Features](#-key-features)
- [Architecture](#-architecture)
- [Prerequisites](#prerequisites)
- [Quick Start](#-quick-start)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [Running Locally](#running-locally)
- [Docker Deployment](#docker-deployment)
- [Testing](#-testing-with-testcontainers)
- [Troubleshooting](#troubleshooting)
- [Documentation](#-documentation)
- [License](#license)

## 🎯 Use Case

**Problem**: How do you test services that depend on external infrastructure (MQTT brokers, IoT devices, databases, message queues) without:
- Manual setup of test environments
- Shared test infrastructure that can cause flaky tests
- Complex mocking that doesn't reflect real-world behavior

**Solution**: This project demonstrates using **Testcontainers** to automatically spin up real dependencies (MQTT broker, fake devices) in isolated Docker containers during test execution.

## ✨ Key Features

- **Real Integration Testing**: Tests run against actual MQTT broker and device simulators in Docker containers
- **Zero Manual Setup**: Containers are automatically created, configured, and cleaned up
- **Isolated Test Environment**: Each test class gets fresh containers, ensuring test isolation
- **Complete Test Coverage**: 25 tests (14 unit + 11 integration)
- **Production-Ready Patterns**: Demonstrates real-world .NET 8 service architecture

## 🏗️ Architecture

This repository contains two independent .NET 8 services:

- **DeviceIngestor**: Ingests telemetry data via HTTP POST and SSE, enriches it, and publishes to MQTT
- **TelemetryWriter**: Subscribes to MQTT and writes messages to disk

### System Architecture Diagram

```
┌─────────────┐     HTTP POST       ┌──────────────-┐
│   Client    │ ─────────────────>  │ DeviceIngestor│
└─────────────┘                     │               │
                                    │               │
┌─────────────┐     SSE Events      │               │
│  SSE Source │ ───────────────────>│               │
└─────────────┘                     └──────┬───────-┘
                                           │
                                           │ MQTT Publish
                                           │
                                           ▼
                                    ┌──────────────┐
                                    │   MQTT       │
                                    │   Broker     │
                                    └──────┬───────┘
                                           │
                                           │ Subscribe
                                           │
                                           ▼
                                    ┌──────────────-┐
                                    │TelemetryWriter│
                                    └──────┬──────-─┘
                                           │
                                           │ Write
                                           ▼
                                    ┌────────────-──┐
                                    │  File System  │
                                    │  (data/*.json)│
                                    └───────────-───┘
```

**Data Flow:**
1. **Client** → HTTP POST → **DeviceIngestor** (IngestController)
2. **SSE Source** → SSE Events → **DeviceIngestor** (SseService reads stream)
3. **DeviceIngestor** → Enriches data (adds timestamp, source) → **MQTT Broker** (MqttService publishes)
4. **TelemetryWriter** → Subscribes to MQTT → Writes to **File System**

## Prerequisites

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Docker Desktop** (or Docker Engine) - Required for:
  - Integration tests (Testcontainers)
  - Running services with docker-compose
  - Manual local setup (MQTT broker, FakeDevice server)
- **Node.js** (optional) - Only needed if running FakeDevice server directly without Docker

> **Note**: Docker is required for integration tests. For manual local development, you can use Docker to run dependencies or install them locally.

## 🚀 Quick Start

### Option 1: Docker Compose (Recommended)

The fastest way to get everything running:

```bash
# Clone the repository
git clone https://github.com/arun-kumar-t/testcontainers-dotnet-demo.git
cd testcontainers-dotnet-demo

# Start all services
docker-compose up --build
```

This starts:
- MQTT Broker (Mosquitto)
- FakeDevice Server (SSE source)
- DeviceIngestor (port 5002)
- TelemetryWriter (port 5001)

### Option 2: Manual Setup

For local development:

```bash
# 1. Start MQTT Broker
docker run -d --name mosquitto -p 1883:1883 \
  -v $(pwd)/tests/IntegrationTests/mosquitto.conf:/mosquitto/config/mosquitto.conf \
  eclipse-mosquitto:2 \
  /usr/sbin/mosquitto -c /mosquitto/config/mosquitto.conf

# 2. Start FakeDevice Server
cd tests/IntegrationTests/FakeDevice
docker build -t fake-device . && docker run -d -p 5005:3000 --name fake-device fake-device
cd ../../..

# 3. Run services
dotnet run --project DeviceIngestor/DeviceIngestor.csproj  # Terminal 1
dotnet run --project TelemetryWriter/TelemetryWriter.csproj  # Terminal 2
```

### Run Tests

```bash
# All tests (requires Docker)
dotnet test

# Unit tests only (no Docker needed)
dotnet test tests/UnitTests/UnitTests.csproj
```

## Project Structure

```
.
├── DeviceIngestor/
│   ├── Configuration/
│   ├── Controllers/
│   │   └── IngestController.cs
│   ├── Models/
│   ├── Services/
│   ├── appsettings.json
│   ├── DeviceIngestor.csproj
│   ├── Dockerfile
│   └── Program.cs
├── TelemetryWriter/
│   ├── Configuration/
│   ├── Models/
│   ├── Services/
│   ├── appsettings.json
│   ├── TelemetryWriter.csproj
│   ├── Dockerfile
│   └── Program.cs
├── tests/
│   ├── UnitTests/          
│   └── IntegrationTests/    
├── TestContainer.sln
├── docker-compose.yml
├── TESTING.md              
└── README.md
```

## Configuration

### DeviceIngestor

Edit `DeviceIngestor/appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5002"
      }
    }
  },
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "Topic": "iot/telemetry"
  },
  "Sse": {
    "Url": "http://localhost:5005/events"
  }
}
```

> **Note**: Port configuration priority:
> 1. `ASPNETCORE_URLS` environment variable (used in Docker)
> 2. `Kestrel:Endpoints:Http:Url` in appsettings.json (for local runs)
> 3. Default port 5002 if neither is set

### TelemetryWriter

Edit `TelemetryWriter/appsettings.json`:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5001"
      }
    }
  },
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "Topic": "iot/telemetry"
  },
  "Output": {
    "Directory": "data"
  }
}
```

> **Note**: Port configuration follows the same priority as DeviceIngestor (environment variable > appsettings.json > default).

## Running Locally

### 1. Start MQTT Broker

**Option 1: Using Docker with configuration file (Recommended)**

Mount the configuration file that allows anonymous connections (matching the integration test setup):

```bash
docker run -d --name mosquitto -p 1883:1883 \
  -v $(pwd)/tests/IntegrationTests/mosquitto.conf:/mosquitto/config/mosquitto.conf \
  eclipse-mosquitto:2 \
  /usr/sbin/mosquitto -c /mosquitto/config/mosquitto.conf
```

> **Note**: Using `-d` (detached mode) runs the container in the background. Use `--name mosquitto` for easier cleanup later (see Cleanup section).

**Option 2: Using Docker with inline configuration**

Create a temporary configuration file and mount it:

```bash
# Create a temporary config file
echo -e "listener 1883\nallow_anonymous true" > /tmp/mosquitto-local.conf

# Run Mosquitto with the config (detached mode with name for easy cleanup)
docker run -d --name mosquitto -p 1883:1883 \
  -v /tmp/mosquitto-local.conf:/mosquitto/config/mosquitto.conf \
  eclipse-mosquitto:2
```

**Option 3: Install Mosquitto locally**

Install Mosquitto locally and start it with anonymous access:

```bash
# Create a simple config file
echo -e "listener 1883\nallow_anonymous true" > /tmp/mosquitto.conf

# Start Mosquitto
mosquitto -c /tmp/mosquitto.conf -p 1883
```

> **Note**: The default Eclipse Mosquitto image requires authentication. The configuration file at `tests/IntegrationTests/mosquitto.conf` explicitly allows anonymous connections, which is required for this demo to work without credentials.

### 2. Start FakeDevice Server (SSE Source)

The DeviceIngestor service reads from an SSE (Server-Sent Events) endpoint. Start the FakeDevice server that provides this endpoint:

**Option 1: Using Docker (Recommended)**

```bash
cd tests/IntegrationTests/FakeDevice
docker build -t fake-device .
docker run -d -p 5005:3000 --name fake-device fake-device
```

This maps the container's port 3000 to host port 5005, matching the default configuration in `appsettings.json`.

**Option 2: Using Node.js directly**

If you have Node.js installed:

```bash
cd tests/IntegrationTests/FakeDevice
node server.js
```

> **Note**: The server runs on port 3000 by default. If using this option, update `DeviceIngestor/appsettings.json` to set `"Sse": { "Url": "http://localhost:3000/events" }`.

### 3. Restore Dependencies

From the root directory:
```bash
dotnet restore
```

Or restore individual projects:
```bash
dotnet restore DeviceIngestor/DeviceIngestor.csproj
dotnet restore TelemetryWriter/TelemetryWriter.csproj
```

### 4. Run DeviceIngestor

```bash
cd DeviceIngestor
dotnet run
```

Or from the root using the solution:
```bash
dotnet run --project DeviceIngestor/DeviceIngestor.csproj
```

The service will start on `http://localhost:5002`.

### 5. Run TelemetryWriter

In a separate terminal:
```bash
cd TelemetryWriter
dotnet run
```

Or from the root using the solution:
```bash
dotnet run --project TelemetryWriter/TelemetryWriter.csproj
```

The service will start on `http://localhost:5001` and begin subscribing to MQTT messages.

> **Note**: TelemetryWriter is configured to use port 5001 to avoid conflicts with DeviceIngestor (port 5002). Both services can run simultaneously.

## Cleanup

When you're done testing, you can clean up the Docker containers and images:

### Docker Compose Cleanup

If you used `docker-compose`:

```bash
# Stop and remove all containers, networks (keeps volumes)
docker-compose down

# Stop and remove all containers, networks, and volumes
docker-compose down -v

# Remove all containers, networks, volumes, and images
docker-compose down -v --rmi all
```

### Manual Docker Cleanup

If you started containers manually:

#### Stop Running Containers

```bash
# Stop the FakeDevice container (if started with --name fake-device)
docker stop fake-device

# Stop Mosquitto container (if started with --name mosquitto)
docker stop mosquitto

# If Mosquitto was started without a name, find and stop it:
docker ps  # Find the container name/ID
docker stop <container-name-or-id>
```

Or stop all containers at once:
```bash
# Stop all running containers
docker stop $(docker ps -q)
```

#### Remove Containers

```bash
# Remove the FakeDevice container
docker rm fake-device

# Remove Mosquitto container (if started with --name mosquitto)
docker rm mosquitto

# If Mosquitto was started without a name, use the container name/ID from docker ps
docker rm <container-name-or-id>
```

Or remove all stopped containers:
```bash
# Remove all stopped containers
docker container prune -f
```

#### Remove Images (Optional)

If you want to remove the Docker images as well:

```bash
# Remove FakeDevice image
docker rmi fake-device

# Remove Mosquitto image (optional, as it's a standard image)
docker rmi eclipse-mosquitto:2
```

#### Complete Cleanup

To clean up everything (containers, images, volumes, networks):

```bash
# Remove all stopped containers, unused networks, and dangling images
docker system prune -f

# For more aggressive cleanup (removes all unused images, not just dangling ones)
docker system prune -a -f
```

> **Warning**: `docker system prune -a` will remove all unused images, not just the ones from this project. Use with caution.

#### Quick Reference

```bash
# List running containers
docker ps

# List all containers (including stopped)
docker ps -a

# Stop and remove a specific container by name
docker stop fake-device && docker rm fake-device

# Stop and remove Mosquitto (if started with --name mosquitto)
docker stop mosquitto && docker rm mosquitto

# If started without a name, find and remove:
docker ps | grep mosquitto  # Find container ID
docker stop <id> && docker rm <id>
```

### Manual Testing

### Test HTTP POST Endpoint

Send a POST request to DeviceIngestor (works for both manual and docker-compose setups):
```bash
curl -X POST http://localhost:5002/ingest \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId": "device-001",
    "temperature": 25.5,
    "humidity": 60
  }'
```

Expected response:
```json
{
  "message": "Message ingested and published",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Test SSE Integration

The FakeDevice server (started in step 2 above) automatically provides SSE events. The DeviceIngestor service will connect to it automatically when it starts.

Example SSE event format:
```
data: {"deviceId": "device-002", "temperature": 23.1, "humidity": 55}
```

The FakeDevice server emits events every second. You can verify it's working by checking the DeviceIngestor logs for "Connected to SSE stream" messages.

### Verify File Writing

After sending messages, check the `TelemetryWriter/data/` directory:

```bash
ls -la TelemetryWriter/data/
```

You should see files like:
```
20240115103000123_device-001.json
20240115103000456_device-002.json
```

Each file contains the enriched telemetry message:
```json
{
  "DeviceId": "device-001",
  "Data": {
    "deviceId": "device-001",
    "temperature": 25.5,
    "humidity": 60
  },
  "Timestamp": "2024-01-15T10:30:00Z",
  "Source": "http"
}
```

## Docker Deployment

### Run with Docker Compose (Recommended)

The `docker-compose.yml` file includes all services needed for the complete setup:

- **MQTT Broker** (Mosquitto) on port 1883
- **FakeDevice Server** (SSE source) on port 5005
- **DeviceIngestor** on port 5002
- **TelemetryWriter** on port 5001

Run:
```bash
docker-compose up --build
```

Or run in detached mode:
```bash
docker-compose up -d --build
```

View logs:
```bash
docker-compose logs -f
```

Stop all services:
```bash
docker-compose down
```

Stop and remove volumes:
```bash
docker-compose down -v
```

### Port Mapping

**When using `docker-compose`**, services are accessible on:
- **DeviceIngestor**: `http://localhost:5002` (mapped from container port 8080)
- **TelemetryWriter**: `http://localhost:5001` (mapped from container port 8080)
- **FakeDevice (SSE)**: `http://localhost:5005` (mapped from container port 3000)
- **MQTT Broker**: `localhost:1883`

**When running manually** (with `dotnet run`), services use:
- **DeviceIngestor**: `http://localhost:5002` (configured in `appsettings.json`)
- **TelemetryWriter**: `http://localhost:5001` (configured in `appsettings.json`)
- **FakeDevice (SSE)**: `http://localhost:5005` (when using Docker) or `http://localhost:3000` (when using Node.js directly)
- **MQTT Broker**: `localhost:1883`

> **Note**: Both manual and docker-compose setups use the same ports for consistency. Port configuration uses ASP.NET Core's standard approach: `ASPNETCORE_URLS` environment variable (Docker) > `Kestrel:Endpoints` in appsettings.json (local) > default.

### Build Individual Images

If you need to build images individually:

```bash
# Build DeviceIngestor
docker build -t device-ingestor -f DeviceIngestor/Dockerfile .

# Build TelemetryWriter
docker build -t telemetry-writer -f TelemetryWriter/Dockerfile .

# Build FakeDevice
docker build -t fake-device -f tests/IntegrationTests/FakeDevice/Dockerfile ./tests/IntegrationTests/FakeDevice
```

## End-to-End Flow

1. **HTTP POST Flow**:
   - Client sends JSON to `POST /ingest`
   - DeviceIngestor enriches with `timestamp` and `source: "http"`
   - Message published to MQTT topic `iot/telemetry`
   - TelemetryWriter receives message and writes to file

2. **SSE Flow**:
   - SSE source sends events to configured URL
   - DeviceIngestor reads SSE stream continuously
   - Each event is enriched with `timestamp` and `source: "sse"`
   - Message published to MQTT topic `iot/telemetry`
   - TelemetryWriter receives message and writes to file

## File Naming Convention

Files are written with the format: `<timestamp>_<deviceId>.json`

- `timestamp`: UTC timestamp in format `yyyyMMddHHmmssfff`
- `deviceId`: Extracted from message, or `"unknown"` if not present

Example: `20240115103000123_device-001.json`

## Logging

Both services use structured logging. Log levels can be configured in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Troubleshooting

### MQTT Connection Issues

- Verify MQTT broker is running: `docker ps` or check broker logs
- Check firewall settings for port 1883
- Verify host and port in `appsettings.json`

### SSE Connection Issues

- Ensure FakeDevice server is running (see step 2 in "Running Locally" section)
- Verify FakeDevice server is accessible: `curl http://localhost:5005/health` (should return `{"status":"healthy"}`)
- Check SSE URL in `appsettings.json` matches your FakeDevice server port
- If using Node.js directly (port 3000), update `appsettings.json` to use `http://localhost:3000/events`
- Verify SSE endpoint returns `text/event-stream` content type
- Connection errors every 5 seconds indicate the FakeDevice server is not running - start it using the instructions in step 2

### Port Conflict Issues

- **"Address already in use" error**: This means another service is using the same port
  - DeviceIngestor uses port **5002** (configured to avoid common conflicts)
  - TelemetryWriter uses port **5001** by default
  - If you see port conflicts, check what's running: `lsof -i :5002` or `lsof -i :5001`
  - Stop the conflicting service or change the port in the application configuration

### File Writing Issues

- Check write permissions for output directory
- Verify disk space availability
- Check TelemetryWriter logs for errors

## Development Notes

- DeviceIngestor uses ASP.NET Core Controllers (IngestController)
- TelemetryWriter uses HostedService pattern
- MQTTnet library handles MQTT communication
- Background services handle long-running tasks (SSE reading, MQTT subscription)
- Simple, straightforward code without complex architecture patterns

## 🧪 Testing with Testcontainers

### Why Testcontainers?

**Traditional Approach** (Problems):
- ❌ Manual setup of MQTT broker, test devices
- ❌ Shared test infrastructure causing flaky tests
- ❌ Complex mocking that doesn't test real behavior
- ❌ "Works on my machine" issues

**Testcontainers Approach** (Benefits):
- ✅ **Automatic Setup**: Containers start automatically before tests
- ✅ **Isolation**: Each test class gets fresh containers
- ✅ **Real Dependencies**: Tests run against actual MQTT broker, not mocks
- ✅ **Reproducible**: Same environment on every machine
- ✅ **Clean Teardown**: Containers automatically removed after tests

### Test Statistics

- **Unit Tests**: 14 passing (~1 second, no Docker needed)
- **Integration Tests**: 11 passing (~30-60 seconds, uses Testcontainers)
- **Total**: 25 tests passing
- **Code Coverage**: 24.6% line, 10.9% branch, 60.6% method

### Quick Test Run

```bash
# Run all tests (unit + integration)
dotnet test

# Run only unit tests (fast, no Docker needed)
dotnet test tests/UnitTests/UnitTests.csproj

# Run only integration tests (requires Docker)
dotnet test tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj

# Generate code coverage report
./coverage.sh
```

### What Gets Tested?

**Integration Tests Automatically:**
1. Start MQTT broker container (Eclipse Mosquitto)
2. Start fake device container (Node.js SSE server)
3. Run DeviceIngestor and TelemetryWriter against these containers
4. Verify end-to-end data flow
5. Clean up containers automatically

**No manual setup required!** Just ensure Docker is running.

See `TESTING.md` for detailed testing documentation.

## 🔄 Continuous Integration

This project includes GitHub Actions workflows for automated testing:

- **CI Workflow**: Runs all tests (unit + integration) on every push and PR
- **Unit Tests Workflow**: Fast unit tests (no Docker required)
- **Integration Tests Workflow**: Integration tests with Testcontainers (requires Docker)

### CI Features

- ✅ Automatic test execution on push/PR
- ✅ Code coverage reporting
- ✅ Test results published as artifacts
- ✅ Docker-in-Docker for Testcontainers support
- ✅ Test result annotations in PRs

View the workflows in `.github/workflows/` directory. See `.github/workflows/README.md` for detailed workflow documentation.

## 📖 Documentation

### For Users
- **[TESTING.md](TESTING.md)**: Comprehensive testing guide
- **[tests/IntegrationTests/README.md](tests/IntegrationTests/README.md)**: Integration test details
- **[.github/workflows/README.md](.github/workflows/README.md)**: CI/CD workflow documentation

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## Acknowledgments

- [Testcontainers for .NET](https://dotnet.testcontainers.org/) - For enabling easy integration testing
- [MQTTnet](https://github.com/dotnet/MQTTnet) - MQTT client library
- [Eclipse Mosquitto](https://mosquitto.org/) - MQTT broker


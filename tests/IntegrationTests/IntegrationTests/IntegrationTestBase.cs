using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Xunit;
using System.Net;

namespace IntegrationTests;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection
{
}

[Collection("IntegrationTests")]
public class IntegrationTestBase : IAsyncLifetime
{
    protected IContainer MqttContainer { get; private set; } = null!;
    protected IContainer FakeDeviceContainer { get; private set; } = null!;
    protected string MqttHost => "localhost";
    protected int MqttPort => MqttContainer.GetMappedPublicPort(1883);
    protected string FakeDeviceUrl => $"http://localhost:{FakeDeviceContainer.GetMappedPublicPort(3000)}";

    public virtual async Task InitializeAsync()
    {
        Console.WriteLine($"[IntegrationTest] Starting container initialization at {DateTime.Now:HH:mm:ss}");
        
        // Verify Docker is accessible
        try
        {
        Console.WriteLine("[IntegrationTest] Building MQTT container...");
        
        // Find mosquitto.conf file
        string mosquittoConfigPath = null!;
        
        // Strategy 1: From solution root (when running dotnet test from root)
        var mqttPath1 = Path.Combine(Directory.GetCurrentDirectory(), "tests", "IntegrationTests", "mosquitto.conf");
        if (File.Exists(mqttPath1))
        {
            mosquittoConfigPath = mqttPath1;
        }
        else
        {
            // Strategy 2: From test assembly location (bin/Debug/net8.0/)
            var mqttBaseDir = AppContext.BaseDirectory;
            var mqttPath2 = Path.GetFullPath(Path.Combine(mqttBaseDir, "..", "..", "..", "mosquitto.conf"));
            if (File.Exists(mqttPath2))
            {
                mosquittoConfigPath = mqttPath2;
            }
            else
            {
                // Strategy 3: From IntegrationTests directory
                var mqttPath3 = Path.GetFullPath(Path.Combine(mqttBaseDir, "..", "..", "..", "..", "mosquitto.conf"));
                if (File.Exists(mqttPath3))
                {
                    mosquittoConfigPath = mqttPath3;
                }
                else
                {
                    throw new FileNotFoundException(
                        $"mosquitto.conf not found. Tried:\n" +
                        $"1. {mqttPath1}\n" +
                        $"2. {mqttPath2}\n" +
                        $"3. {mqttPath3}\n" +
                        $"Current directory: {Directory.GetCurrentDirectory()}\n" +
                        $"Base directory: {mqttBaseDir}");
                }
            }
        }
        
        // Start MQTT container with configuration to allow anonymous connections
        // Map config file to directory - Testcontainers will preserve filename
        MqttContainer = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2")
            .WithPortBinding(1883, true)
            .WithResourceMapping(mosquittoConfigPath, "/mosquitto/config/")
            .WithCommand("/usr/sbin/mosquitto", "-c", "/mosquitto/config/mosquitto.conf")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(1883))
            .Build();

        Console.WriteLine("[IntegrationTest] Starting MQTT container...");
        using var mqttCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // Reduced timeout
        await MqttContainer.StartAsync(mqttCts.Token);
        Console.WriteLine($"[IntegrationTest] MQTT container started on port {MqttPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IntegrationTest] ERROR: Failed to start MQTT container: {ex.Message}");
            if (ex.Message.Contains("Docker") || ex.Message.Contains("docker") || ex.InnerException?.Message.Contains("Docker") == true)
            {
                throw new InvalidOperationException(
                    "Docker is required for integration tests. Please ensure:\n" +
                    "1. Docker Desktop (or Docker Engine) is installed and running\n" +
                    "2. Docker daemon is accessible (try: docker ps)\n" +
                    "3. Docker Desktop settings allow CLI access\n" +
                    "4. Docker socket has proper permissions\n" +
                    "For more info: https://dotnet.testcontainers.org/custom_configuration/", ex);
            }
            throw;
        }

        // Start fake device container
        // Find FakeDevice directory using multiple fallback strategies
        string fakeDeviceDir = null!;
        
        // Strategy 1: From solution root (when running dotnet test from root)
        var path1 = Path.Combine(Directory.GetCurrentDirectory(), "tests", "IntegrationTests", "FakeDevice");
        if (Directory.Exists(path1) && File.Exists(Path.Combine(path1, "server.js")))
        {
            fakeDeviceDir = path1;
        }
        else
        {
            // Strategy 2: From test assembly location (bin/Debug/net8.0/)
            var baseDir = AppContext.BaseDirectory;
            var path2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "FakeDevice"));
            if (Directory.Exists(path2) && File.Exists(Path.Combine(path2, "server.js")))
            {
                fakeDeviceDir = path2;
            }
            else
            {
                // Strategy 3: From IntegrationTests directory
                var path3 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "FakeDevice"));
                if (Directory.Exists(path3) && File.Exists(Path.Combine(path3, "server.js")))
                {
                    fakeDeviceDir = path3;
                }
                else
                {
                    throw new FileNotFoundException(
                        $"Fake device directory not found. Tried:\n" +
                        $"1. {path1}\n" +
                        $"2. {path2}\n" +
                        $"3. {path3}\n" +
                        $"Current directory: {Directory.GetCurrentDirectory()}\n" +
                        $"Base directory: {baseDir}");
                }
            }
        }
        
        // Use Dockerfile build approach - build image first, then use it
        var serverJsPath = Path.Combine(fakeDeviceDir, "server.js");
        var dockerfilePath = Path.Combine(fakeDeviceDir, "Dockerfile");
        
        if (!File.Exists(serverJsPath))
        {
            throw new FileNotFoundException($"server.js not found at: {serverJsPath}");
        }
        if (!File.Exists(dockerfilePath))
        {
            throw new FileNotFoundException($"Dockerfile not found at: {dockerfilePath}");
        }

        Console.WriteLine($"[IntegrationTest] Building fake device container from: {fakeDeviceDir}");
        // Use resource mapping with the server.js file directly
        // WithResourceMapping requires target as directory, not file
        FakeDeviceContainer = new ContainerBuilder()
            .WithImage("node:20-alpine")
            .WithResourceMapping(serverJsPath, "/app/") // Map to directory, not file
            .WithWorkingDirectory("/app")
            .WithCommand("node", "server.js")
            .WithPortBinding(3000, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(3000)) // Simplified wait strategy - just wait for port
            .Build();

        Console.WriteLine("[IntegrationTest] Starting fake device container...");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Further reduced timeout
        await FakeDeviceContainer.StartAsync(cts.Token);
        
        // Give the Node server a moment to start
        await Task.Delay(2000);
        
        // Verify health endpoint is accessible
        var fakeDevicePort = FakeDeviceContainer.GetMappedPublicPort(3000);
        Console.WriteLine($"[IntegrationTest] Fake device container started on port {fakeDevicePort}");
        
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var healthResponse = await httpClient.GetAsync($"http://localhost:{fakeDevicePort}/health");
            Console.WriteLine($"[IntegrationTest] Health check status: {healthResponse.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IntegrationTest] WARNING: Health check failed: {ex.Message}");
        }
        
        Console.WriteLine($"[IntegrationTest] Container initialization complete at {DateTime.Now:HH:mm:ss}");
    }

    public virtual async Task DisposeAsync()
    {
        if (FakeDeviceContainer != null)
        {
            await FakeDeviceContainer.DisposeAsync();
        }

        if (MqttContainer != null)
        {
            await MqttContainer.DisposeAsync();
        }
    }
}


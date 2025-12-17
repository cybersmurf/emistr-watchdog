using System.Diagnostics;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using StackExchange.Redis;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for Redis server.
/// </summary>
public sealed class RedisHealthChecker : HealthCheckerBase
{
    private readonly RedisOptions _options;
    private readonly string _serviceName;

    public RedisHealthChecker(
        string serviceName,
        RedisOptions options,
        ILogger<RedisHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? _serviceName;
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;
    protected override bool ConfigPrioritized => _options.Prioritized;
    public override ServiceRestartConfig? RestartConfig => _options.RestartConfig;

    protected override async Task<HealthCheckResult> PerformCheckAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return HealthCheckResult.Unhealthy(ServiceName, "Connection string is not configured");
        }

        var sw = Stopwatch.StartNew();

        var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
        configOptions.ConnectTimeout = _options.TimeoutSeconds * 1000;
        configOptions.SyncTimeout = _options.TimeoutSeconds * 1000;
        configOptions.AsyncTimeout = _options.TimeoutSeconds * 1000;
        configOptions.AbortOnConnectFail = false;

        await using var connection = await ConnectionMultiplexer.ConnectAsync(configOptions);
        
        if (!connection.IsConnected)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(ServiceName, "Failed to connect to Redis server");
        }

        var db = connection.GetDatabase(_options.Database);
        var server = connection.GetServer(connection.GetEndPoints().First());

        // Execute PING command
        var pingResult = await db.PingAsync();

        // Get server info
        var serverInfo = await GetServerInfoAsync(server, cancellationToken);

        // Optional: check if test key exists
        if (!string.IsNullOrEmpty(_options.TestKey))
        {
            var keyExists = await db.KeyExistsAsync(_options.TestKey);
            if (!keyExists)
            {
                Logger.LogWarning("Test key '{TestKey}' does not exist in Redis", _options.TestKey);
            }
        }

        sw.Stop();

        Logger.LogDebug(
            "{ServiceName} health check completed in {ElapsedMs}ms, ping: {PingMs}ms, version: {Version}",
            ServiceName,
            sw.ElapsedMilliseconds,
            pingResult.TotalMilliseconds,
            serverInfo?.Version ?? "unknown");

        // Check for degraded performance (ping > 100ms)
        if (pingResult.TotalMilliseconds > 100)
        {
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = ServiceStatus.Degraded,
                ErrorMessage = $"Slow ping response: {(int)pingResult.TotalMilliseconds}ms",
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ServerInfo = serverInfo,
                Details = new Dictionary<string, object>
                {
                    ["ping_ms"] = pingResult.TotalMilliseconds,
                    ["connected_clients"] = serverInfo?.AdditionalInfo?.GetValueOrDefault("connected_clients") ?? "unknown"
                }
            };
        }

        return new HealthCheckResult
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = ServiceStatus.Healthy,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            ServerInfo = serverInfo,
            Details = new Dictionary<string, object>
            {
                ["ping_ms"] = pingResult.TotalMilliseconds,
                ["database"] = _options.Database
            }
        };
    }

    private async Task<ServerInfo?> GetServerInfoAsync(IServer server, CancellationToken cancellationToken)
    {
        try
        {
            var info = await server.InfoAsync();
            var serverSection = info.FirstOrDefault(g => g.Key == "Server");
            var clientsSection = info.FirstOrDefault(g => g.Key == "Clients");
            var memorySection = info.FirstOrDefault(g => g.Key == "Memory");

            var version = serverSection?.FirstOrDefault(p => p.Key == "redis_version").Value ?? "unknown";
            var connectedClients = clientsSection?.FirstOrDefault(p => p.Key == "connected_clients").Value ?? "0";
            var usedMemory = memorySection?.FirstOrDefault(p => p.Key == "used_memory_human").Value ?? "unknown";

            return new ServerInfo
            {
                Version = version,
                ServerType = "Redis",
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["connected_clients"] = connectedClients,
                    ["used_memory"] = usedMemory
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get Redis server info");
            return null;
        }
    }
}

/// <summary>
/// Factory for creating Redis health checkers.
/// </summary>
public sealed class RedisHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public RedisHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public RedisHealthChecker Create(string serviceName, RedisOptions options)
    {
        return new RedisHealthChecker(
            serviceName,
            options,
            _loggerFactory.CreateLogger<RedisHealthChecker>(),
            _serviceController,
            _restartTracker);
    }
}

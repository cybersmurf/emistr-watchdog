using System.Diagnostics;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for MariaDB/MySQL database.
/// </summary>
public sealed class MariaDbHealthChecker : HealthCheckerBase
{
    private readonly MariaDbOptions _options;
    private readonly string _serviceName;

    public MariaDbHealthChecker(
        IOptions<WatchdogOptions> options,
        ILogger<MariaDbHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : this("MariaDB", options.Value.Services.MariaDB, logger, serviceController, restartTracker)
    {
    }

    public MariaDbHealthChecker(
        string serviceName,
        MariaDbOptions options,
        ILogger<MariaDbHealthChecker> logger,
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

        await using var connection = new MySqlConnection(_options.ConnectionString);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        await connection.OpenAsync(cts.Token);

        // Execute health check query or simple ping
        var query = _options.HealthCheckQuery ?? "SELECT 1";
        await using var cmd = new MySqlCommand(query, connection);
        await cmd.ExecuteScalarAsync(cts.Token);

        // Get server info
        var serverInfo = await GetServerInfoAsync(connection, cts.Token);

        sw.Stop();

        Logger.LogDebug(
            "{ServiceName} health check completed in {ElapsedMs}ms, version: {Version}",
            ServiceName,
            sw.ElapsedMilliseconds,
            serverInfo?.Version ?? "unknown");

        // Check for degraded performance (response time > 1 second)
        if (sw.ElapsedMilliseconds > 1000)
        {
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = ServiceStatus.Degraded,
                ErrorMessage = $"Slow response time: {sw.ElapsedMilliseconds}ms",
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ServerInfo = serverInfo
            };
        }

        return new HealthCheckResult
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = ServiceStatus.Healthy,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            ServerInfo = serverInfo
        };
    }

    private static async Task<ServerInfo?> GetServerInfoAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var additionalInfo = new Dictionary<string, string>();

            // Get version
            await using var versionCmd = new MySqlCommand("SELECT VERSION()", connection);
            var version = await versionCmd.ExecuteScalarAsync(cancellationToken) as string;

            // Determine server type (MariaDB or MySQL)
            var serverType = version?.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) == true
                ? "MariaDB"
                : "MySQL";

            // Get additional info
            await using var statusCmd = new MySqlCommand(
                "SHOW VARIABLES WHERE Variable_name IN ('version_compile_os', 'version_compile_machine', 'innodb_version', 'max_connections')",
                connection);

            await using var reader = await statusCmd.ExecuteReaderAsync(cancellationToken);
            string? platform = null;
            string? architecture = null;

            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                var value = reader.GetString(1);

                switch (name)
                {
                    case "version_compile_os":
                        platform = value;
                        break;
                    case "version_compile_machine":
                        architecture = value;
                        break;
                    case "innodb_version":
                        additionalInfo["InnoDB"] = value;
                        break;
                    case "max_connections":
                        additionalInfo["MaxConnections"] = value;
                        break;
                }
            }

            return new ServerInfo
            {
                Version = version,
                ServerType = serverType,
                Platform = platform,
                Architecture = architecture,
                AdditionalInfo = additionalInfo
            };
        }
        catch
        {
            // Server info is optional, don't fail the health check
            return null;
        }
    }
}

/// <summary>
/// Factory for creating MariaDB health checkers.
/// </summary>
public sealed class MariaDbHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public MariaDbHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public MariaDbHealthChecker Create(string serviceName, MariaDbOptions options)
    {
        var logger = _loggerFactory.CreateLogger<MariaDbHealthChecker>();
        return new MariaDbHealthChecker(serviceName, options, logger, _serviceController, _restartTracker);
    }
}

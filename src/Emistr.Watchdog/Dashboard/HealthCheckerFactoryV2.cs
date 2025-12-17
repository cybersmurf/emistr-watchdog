using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services;
using Emistr.Watchdog.Services.HealthCheckers;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// Factory for creating health checkers from V2 ServiceConfig.
/// </summary>
public static class HealthCheckerFactoryV2
{
    /// <summary>
    /// Creates all health checkers from V2 config services array.
    /// </summary>
    public static List<IHealthChecker> CreateFromConfig(
        List<ServiceConfig> services,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        var checkers = new List<IHealthChecker>();

        foreach (var config in services.Where(s => s.Enabled))
        {
            try
            {
                var checker = CreateChecker(config, httpClientFactory, loggerFactory, serviceController, restartTracker);
                if (checker != null)
                {
                    checkers.Add(checker);
                }
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger("HealthCheckerFactory");
                logger.LogWarning(ex, "Failed to create health checker for {Name} ({Type})", config.Name, config.Type);
            }
        }

        return checkers;
    }

    private static IHealthChecker? CreateChecker(
        ServiceConfig config,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IServiceController? serviceController,
        ServiceRestartTracker? restartTracker)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "http" => CreateHttpChecker(config, httpClientFactory, loggerFactory, serviceController, restartTracker),
            "mariadb" or "mysql" => CreateMariaDbChecker(config, loggerFactory, serviceController, restartTracker),
            "tcp" or "telnet" => CreateTcpChecker(config, loggerFactory, serviceController, restartTracker),
            "ping" or "icmp" => CreatePingChecker(config, loggerFactory),
            "script" => CreateScriptChecker(config, loggerFactory),
            "backgroundservice" or "background" => CreateBackgroundServiceChecker(config, loggerFactory),
            "process" => CreateProcessChecker(config, loggerFactory),
            _ => throw new ArgumentException($"Unknown service type: {config.Type}")
        };
    }

    private static IHealthChecker CreateHttpChecker(
        ServiceConfig config,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IServiceController? serviceController,
        ServiceRestartTracker? restartTracker)
    {
        var options = new HttpServiceOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            Url = config.Url ?? throw new ArgumentException("URL is required for HTTP service"),
            ExpectedStatusCodes = config.ExpectedStatusCodes,
            IgnoreSslErrors = !config.ValidateSsl,
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures,
            RestartConfig = config.RestartConfig
        };

        var httpClient = httpClientFactory.CreateClient(config.Name);
        var logger = loggerFactory.CreateLogger<HttpHealthChecker>();

        return new HttpHealthChecker(config.Name, options, httpClient, logger, serviceController, restartTracker);
    }

    private static IHealthChecker CreateMariaDbChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory,
        IServiceController? serviceController,
        ServiceRestartTracker? restartTracker)
    {
        var options = new MariaDbOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            ConnectionString = config.ConnectionString ?? throw new ArgumentException("ConnectionString is required for MariaDB service"),
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures,
            RestartConfig = config.RestartConfig
        };

        var logger = loggerFactory.CreateLogger<MariaDbHealthChecker>();
        return new MariaDbHealthChecker(config.Name, options, logger, serviceController, restartTracker);
    }

    private static IHealthChecker CreateTcpChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory,
        IServiceController? serviceController,
        ServiceRestartTracker? restartTracker)
    {
        var options = new TelnetServiceOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            Host = config.Host ?? throw new ArgumentException("Host is required for TCP service"),
            Port = config.Port > 0 ? config.Port : throw new ArgumentException("Port is required for TCP service"),
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures,
            RestartConfig = config.RestartConfig
        };

        var logger = loggerFactory.CreateLogger<TelnetHealthChecker>();
        return new TelnetHealthChecker(config.Name, options, logger, serviceController, restartTracker);
    }

    private static IHealthChecker CreatePingChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory)
    {
        var options = new PingOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            Host = config.Host ?? throw new ArgumentException("Host is required for Ping service"),
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures,
            PingCount = config.PingCount,
            PacketLossThresholdPercent = config.MaxPacketLossPercent
        };

        var logger = loggerFactory.CreateLogger<PingHealthChecker>();
        return new PingHealthChecker(config.Name, options, logger);
    }

    private static IHealthChecker CreateScriptChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory)
    {
        var options = new ScriptHealthCheckOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            ScriptPath = config.ScriptPath ?? throw new ArgumentException("ScriptPath is required for Script service"),
            Arguments = config.ScriptArguments ?? string.Empty,
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures
        };

        var logger = loggerFactory.CreateLogger<ScriptHealthChecker>();
        return new ScriptHealthChecker(config.Name, options, logger);
    }

    private static IHealthChecker CreateBackgroundServiceChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory)
    {
        var options = new BackgroundServiceOptions
        {
            Enabled = config.Enabled,
            Prioritized = config.Prioritized,
            DisplayName = config.DisplayName,
            ConnectionString = config.ConnectionString ?? throw new ArgumentException("ConnectionString is required for BackgroundService"),
            DatabaseName = config.DatabaseName ?? throw new ArgumentException("DatabaseName is required for BackgroundService"),
            TableName = config.TableName ?? throw new ArgumentException("TableName is required for BackgroundService"),
            ColumnName = config.ColumnName ?? throw new ArgumentException("ColumnName is required for BackgroundService"),
            MaxAgeMinutes = config.MaxAgeMinutes,
            TimeoutSeconds = config.TimeoutSeconds,
            CriticalAfterFailures = config.CriticalAfterFailures
        };

        var logger = loggerFactory.CreateLogger<BackgroundServiceHealthChecker>();
        return new BackgroundServiceHealthChecker(config.Name, options, logger);
    }

    private static IHealthChecker? CreateProcessChecker(
        ServiceConfig config,
        ILoggerFactory loggerFactory)
    {
        // TODO: Implement ProcessHealthChecker if needed
        var logger = loggerFactory.CreateLogger("HealthCheckerFactory");
        logger.LogWarning("Process health checker not yet implemented for {Name}", config.Name);
        return null;
    }
}


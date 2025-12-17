using System.Diagnostics;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for background services that write last run time to database.
/// Monitors the bgs_last_run column in the system table.
/// </summary>
public sealed class BackgroundServiceHealthChecker : HealthCheckerBase
{
    private readonly BackgroundServiceOptions _options;
    private readonly string _serviceName;

    public BackgroundServiceHealthChecker(
        IOptions<WatchdogOptions> options,
        ILogger<BackgroundServiceHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : this("BackgroundService", options.Value.Services.BackgroundService, logger, serviceController, restartTracker)
    {
    }

    public BackgroundServiceHealthChecker(
        string serviceName,
        BackgroundServiceOptions options,
        ILogger<BackgroundServiceHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? "Emistr Background Service";
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;
    protected override bool ConfigPrioritized => _options.Prioritized;

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

        // Query last run time
        var query = $"SELECT `{_options.ColumnName}` FROM `{_options.DatabaseName}`.`{_options.TableName}` WHERE id = @id";
        await using var cmd = new MySqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@id", _options.SystemRowId);

        var result = await cmd.ExecuteScalarAsync(cts.Token);

        sw.Stop();

        if (result == null || result == DBNull.Value)
        {
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Column '{_options.ColumnName}' is NULL or row not found");
        }

        DateTime lastRun;
        if (result is DateTime dt)
        {
            lastRun = dt;
        }
        else if (DateTime.TryParse(result.ToString(), out var parsed))
        {
            lastRun = parsed;
        }
        else
        {
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Invalid datetime format in '{_options.ColumnName}': {result}");
        }

        var age = DateTime.Now - lastRun;
        var maxAge = TimeSpan.FromMinutes(_options.MaxAgeMinutes);

        // Get server info
        var serverInfo = await GetServerInfoAsync(connection, lastRun, cts.Token);

        Logger.LogDebug(
            "{ServiceName} last run: {LastRun} ({Age} ago), max allowed: {MaxAge}",
            ServiceName,
            lastRun,
            age,
            maxAge);

        if (age > maxAge)
        {
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = false,
                Status = ServiceStatus.Unhealthy,
                ErrorMessage = $"Last run was {FormatTimeSpan(age)} ago (max: {_options.MaxAgeMinutes} min)",
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ServerInfo = serverInfo
            };
        }

        // Check for degraded (more than 50% of max age)
        if (age > TimeSpan.FromMinutes(_options.MaxAgeMinutes * 0.5))
        {
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = ServiceStatus.Degraded,
                ErrorMessage = $"Last run {FormatTimeSpan(age)} ago (approaching limit)",
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

    private async Task<ServerInfo?> GetServerInfoAsync(MySqlConnection connection, DateTime lastRun, CancellationToken cancellationToken)
    {
        try
        {
            var additionalInfo = new Dictionary<string, string>
            {
                ["LastRun"] = lastRun.ToString("yyyy-MM-dd HH:mm:ss"),
                ["MaxAgeMinutes"] = _options.MaxAgeMinutes.ToString()
            };

            // Get version and program info from system table
            var query = $"SELECT version, table_version, program_id FROM `{_options.DatabaseName}`.`{_options.TableName}` WHERE id = @id";
            await using var cmd = new MySqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@id", _options.SystemRowId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            string? version = null;
            string? programId = null;

            if (await reader.ReadAsync(cancellationToken))
            {
                version = reader.IsDBNull(0) ? null : reader.GetString(0);
                var tableVersion = reader.IsDBNull(1) ? null : reader.GetString(1);
                programId = reader.IsDBNull(2) ? null : reader.GetString(2);

                if (!string.IsNullOrEmpty(tableVersion))
                {
                    additionalInfo["TableVersion"] = tableVersion;
                }
                if (!string.IsNullOrEmpty(programId))
                {
                    additionalInfo["ProgramId"] = programId;
                }
            }

            return new ServerInfo
            {
                Version = version,
                ServerType = "BackgroundService",
                AdditionalInfo = additionalInfo
            };
        }
        catch
        {
            return null;
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}h {ts.Minutes}m";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }
}

/// <summary>
/// Factory for creating Background Service health checkers.
/// </summary>
public sealed class BackgroundServiceHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public BackgroundServiceHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public BackgroundServiceHealthChecker Create(string serviceName, BackgroundServiceOptions options)
    {
        var logger = _loggerFactory.CreateLogger<BackgroundServiceHealthChecker>();
        return new BackgroundServiceHealthChecker(serviceName, options, logger, _serviceController, _restartTracker);
    }
}

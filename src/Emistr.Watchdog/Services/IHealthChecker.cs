using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Interface for health check implementations.
/// </summary>
public interface IHealthChecker
{
    /// <summary>
    /// Unique name of the service being checked.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Display name for notifications and logging.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this health checker is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether this service is prioritized (shown at top of dashboard).
    /// </summary>
    bool IsPrioritized { get; }

    /// <summary>
    /// Number of consecutive failures before marking as critical.
    /// </summary>
    int CriticalThreshold { get; }

    /// <summary>
    /// Service restart configuration.
    /// </summary>
    ServiceRestartConfig? RestartConfig { get; }

    /// <summary>
    /// Performs the health check.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for health checkers with common functionality.
/// Thread-safe implementation with locking to prevent concurrent health checks.
/// </summary>
public abstract class HealthCheckerBase : IHealthChecker
{
    private int _consecutiveFailures;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private DateTime _lastCheckTime = DateTime.MinValue;
    
    protected readonly IServiceController? ServiceController;
    protected readonly ServiceRestartTracker? RestartTracker;
    protected readonly ILogger Logger;

    protected HealthCheckerBase(
        ILogger logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        Logger = logger;
        ServiceController = serviceController;
        RestartTracker = restartTracker;
    }

    public abstract string ServiceName { get; }
    public abstract string DisplayName { get; }
    
    /// <summary>
    /// Base enabled value from configuration.
    /// </summary>
    protected abstract bool ConfigEnabled { get; }
    
    /// <summary>
    /// Base prioritized value from configuration.
    /// </summary>
    protected virtual bool ConfigPrioritized => false;
    
    /// <summary>
    /// Whether this health checker is enabled.
    /// Uses runtime override if set, otherwise uses config value.
    /// </summary>
    public bool IsEnabled => RuntimeConfigurationService.Instance.GetEffectiveEnabled(ServiceName, ConfigEnabled);
    
    /// <summary>
    /// Whether this service is prioritized.
    /// Uses runtime override if set, otherwise uses config value.
    /// </summary>
    public bool IsPrioritized => RuntimeConfigurationService.Instance.GetEffectivePrioritized(ServiceName, ConfigPrioritized);
    
    public abstract int CriticalThreshold { get; }
    public virtual ServiceRestartConfig? RestartConfig => null;
    
    /// <summary>
    /// Gets the time of the last health check.
    /// </summary>
    public DateTime LastCheckTime => _lastCheckTime;
    
    /// <summary>
    /// Gets the current consecutive failure count.
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return HealthCheckResult.Healthy(ServiceName) with
            {
                Status = ServiceStatus.Unknown,
                Details = new Dictionary<string, object> { ["reason"] = "Check disabled" }
            };
        }

        // Try to acquire lock with timeout to prevent deadlocks
        if (!await _checkLock.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            Logger.LogWarning("Health check for {ServiceName} skipped - previous check still running", ServiceName);
            return HealthCheckResult.Healthy(ServiceName) with
            {
                Status = ServiceStatus.Unknown,
                Details = new Dictionary<string, object> { ["reason"] = "Previous check still running" }
            };
        }

        try
        {
            _lastCheckTime = DateTime.UtcNow;
            var result = await PerformCheckAsync(cancellationToken);

            if (result.IsHealthy)
            {
                _consecutiveFailures = 0;
                return result;
            }

            _consecutiveFailures++;
            var isCritical = _consecutiveFailures >= CriticalThreshold;

            if (isCritical && ShouldAttemptRestart())
            {
                await AttemptServiceRestartAsync(cancellationToken);
            }

            return result with
            {
                ConsecutiveFailures = _consecutiveFailures,
                IsCritical = isCritical,
                Status = isCritical ? ServiceStatus.Critical : ServiceStatus.Unhealthy
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (internal cancellation), treat as unhealthy
            _consecutiveFailures++;
            return HealthCheckResult.Unhealthy(
                ServiceName,
                "Health check timed out",
                ex,
                _consecutiveFailures,
                CriticalThreshold);
        }
        catch (OperationCanceledException)
        {
            // External cancellation requested, rethrow
            throw;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            return HealthCheckResult.Unhealthy(
                ServiceName,
                ex.Message,
                ex,
                _consecutiveFailures,
                CriticalThreshold);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private bool ShouldAttemptRestart()
    {
        if (RestartConfig is null || !RestartConfig.Enabled || !RestartConfig.RestartOnCritical)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(RestartConfig.WindowsServiceName))
        {
            return false;
        }

        if (ServiceController is null || !ServiceController.IsAvailable())
        {
            return false;
        }

        if (RestartTracker is null)
        {
            return false;
        }

        return RestartTracker.ShouldAttemptRestart(ServiceName, RestartConfig.MaxRestartAttempts);
    }

    private async Task AttemptServiceRestartAsync(CancellationToken cancellationToken)
    {
        if (RestartConfig is null || ServiceController is null || RestartTracker is null)
        {
            return;
        }

        try
        {
            Logger.LogWarning(
                "Service {ServiceName} is critical. Attempting to restart Windows service '{WindowsServiceName}'",
                ServiceName,
                RestartConfig.WindowsServiceName);

            var success = await ServiceController.RestartServiceAsync(
                RestartConfig.WindowsServiceName!,
                cancellationToken);

            RestartTracker.RecordRestartAttempt(ServiceName, success);

            if (success)
            {
                Logger.LogInformation(
                    "Successfully restarted Windows service '{WindowsServiceName}' for {ServiceName}",
                    RestartConfig.WindowsServiceName,
                    ServiceName);

                await Task.Delay(TimeSpan.FromSeconds(RestartConfig.RestartDelaySeconds), cancellationToken);
            }
            else
            {
                Logger.LogError(
                    "Failed to restart Windows service '{WindowsServiceName}' for {ServiceName}",
                    RestartConfig.WindowsServiceName,
                    ServiceName);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error while attempting to restart Windows service '{WindowsServiceName}' for {ServiceName}",
                RestartConfig?.WindowsServiceName,
                ServiceName);

            RestartTracker?.RecordRestartAttempt(ServiceName, false);
        }
    }

    /// <summary>
    /// Performs the actual health check. Implement in derived classes.
    /// </summary>
    protected abstract Task<HealthCheckResult> PerformCheckAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resets the consecutive failure counter.
    /// </summary>
    public void ResetFailureCount() => _consecutiveFailures = 0;
}

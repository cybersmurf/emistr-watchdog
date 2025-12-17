using System.Collections.Concurrent;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Dashboard;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Main background service that orchestrates health checks and notifications.
/// </summary>
public sealed class WatchdogService : BackgroundService
{
    private readonly IReadOnlyList<IHealthChecker> _healthCheckers;
    private readonly INotificationService _notificationService;
    private readonly WebhookNotificationService _webhookNotificationService;
    private readonly CriticalAlertService _criticalAlertService;
    private readonly IDashboardNotifier _dashboardNotifier;
    private readonly StatusTracker _statusTracker;
    private readonly WatchdogOptions _options;
    private readonly ILogger<WatchdogService> _logger;

    private readonly ConcurrentDictionary<string, ServiceState> _serviceStates = new();

    public WatchdogService(
        IEnumerable<IHealthChecker> healthCheckers,
        INotificationService notificationService,
        WebhookNotificationService webhookNotificationService,
        CriticalAlertService criticalAlertService,
        IDashboardNotifier dashboardNotifier,
        StatusTracker statusTracker,
        IOptions<WatchdogOptions> options,
        ILogger<WatchdogService> logger)
    {
        _healthCheckers = healthCheckers.ToList();
        _notificationService = notificationService;
        _webhookNotificationService = webhookNotificationService;
        _criticalAlertService = criticalAlertService;
        _dashboardNotifier = dashboardNotifier;
        _statusTracker = statusTracker;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Watchdog service started. Monitoring {ServiceCount} services with {IntervalSeconds}s interval",
            _healthCheckers.Count(h => h.IsEnabled),
            _options.CheckIntervalSeconds);

        // Log configured services
        foreach (var checker in _healthCheckers.Where(h => h.IsEnabled))
        {
            _logger.LogInformation("Monitoring service: {ServiceName} ({DisplayName})",
                checker.ServiceName,
                checker.DisplayName);

            _serviceStates[checker.ServiceName] = new ServiceState();
        }

        // Initial delay to let services start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunHealthChecksAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Watchdog service stopping");
    }

    private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Running health checks...");

        var tasks = _healthCheckers
            .Where(h => h.IsEnabled)
            .Select(checker => RunSingleCheckAsync(checker, cancellationToken));

        var results = await Task.WhenAll(tasks);

        // Summary log
        var healthy = results.Count(r => r.IsHealthy);
        var unhealthy = results.Length - healthy;
        var critical = results.Count(r => r.IsCritical);

        if (unhealthy > 0)
        {
            _logger.LogWarning(
                "Health check summary: {Healthy} healthy, {Unhealthy} unhealthy, {Critical} critical",
                healthy,
                unhealthy,
                critical);
        }
        else
        {
            _logger.LogDebug("All {Count} services healthy", healthy);
        }

        // Send dashboard update
        await SendDashboardUpdateAsync(results, cancellationToken);
    }

    private async Task SendDashboardUpdateAsync(HealthCheckResult[] results, CancellationToken cancellationToken)
    {
        var services = results.Select(r =>
        {
            var checker = _healthCheckers.FirstOrDefault(c => c.ServiceName == r.ServiceName);
            var isPrioritized = ConfigurationEndpoints.GetRuntimePriority(
                r.ServiceName, 
                checker?.IsPrioritized ?? false);
            return ServiceStatusDto.FromHealthCheckResult(r, checker?.DisplayName ?? r.ServiceName, isPrioritized);
        })
        .OrderByDescending(s => s.IsPrioritized)
        .ThenBy(s => s.DisplayName)
        .ToList();

        var healthyCount = services.Count(s => s.IsHealthy);
        var criticalCount = services.Count(s => s.IsCritical);
        var unhealthyCount = services.Count(s => !s.IsHealthy && !s.IsCritical);

        var overallStatus = criticalCount > 0
            ? "Critical"
            : unhealthyCount > 0
                ? "Degraded"
                : "Healthy";

        var status = new DashboardStatusResponse
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = overallStatus,
            HealthyCount = healthyCount,
            UnhealthyCount = unhealthyCount,
            CriticalCount = criticalCount,
            Services = services
        };

        await _dashboardNotifier.SendStatusUpdateAsync(status, cancellationToken);
    }

    private async Task<HealthCheckResult> RunSingleCheckAsync(
        IHealthChecker checker,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await checker.CheckHealthAsync(cancellationToken);

            // Record to status tracker for API
            _statusTracker.RecordResult(result);

                await ProcessResultAsync(result, checker.DisplayName, cancellationToken);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Only rethrow if the main cancellation token was cancelled (service stopping)
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Timeout or other cancellation - treat as unhealthy, don't crash
                _logger.LogWarning("Health check for {ServiceName} timed out", checker.ServiceName);

                var result = HealthCheckResult.Unhealthy(
                    checker.ServiceName,
                    $"Health check timed out: {ex.Message}",
                    ex);

                _statusTracker.RecordResult(result);
                await ProcessResultAsync(result, checker.DisplayName, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
            _logger.LogError(ex, "Error running health check for {ServiceName}", checker.ServiceName);

            var result = HealthCheckResult.Unhealthy(
                checker.ServiceName,
                $"Health check failed: {ex.Message}",
                ex);

            _statusTracker.RecordResult(result);
            await ProcessResultAsync(result, checker.DisplayName, cancellationToken);

            return result;
        }
    }

    private async Task ProcessResultAsync(HealthCheckResult result, string displayName, CancellationToken cancellationToken)
    {
        var state = _serviceStates.GetOrAdd(result.ServiceName, _ => new ServiceState());
        var wasHealthy = state.IsHealthy;
        var wasCritical = state.IsCritical;

        // Update state
        state.IsHealthy = result.IsHealthy;
        state.IsCritical = result.IsCritical;
        state.LastResult = result;
        state.LastCheckTime = DateTime.UtcNow;

        if (result.IsHealthy)
        {
            // Service recovered
            if (!wasHealthy)
            {
                _logger.LogInformation(
                    "✅ Service {ServiceName} recovered after {FailureCount} failures",
                    result.ServiceName,
                    state.ConsecutiveFailures);

                state.ConsecutiveFailures = 0;

                await _notificationService.NotifyRecoveryAsync(result.ServiceName, cancellationToken);
                await _webhookNotificationService.NotifyRecoveryAsync(result.ServiceName, cancellationToken);
            }

            // Log degraded status
            if (result.Status == ServiceStatus.Degraded)
            {
                _logger.LogWarning(
                    "⚠ Service {ServiceName} is degraded: {Message}",
                    result.ServiceName,
                    result.ErrorMessage);
            }
        }
        else
        {
            state.ConsecutiveFailures++;

            _logger.LogWarning(
                "❌ Service {ServiceName} is unhealthy ({Failures}/{Threshold}): {Error}",
                result.ServiceName,
                state.ConsecutiveFailures,
                result.ConsecutiveFailures,
                result.ErrorMessage);

            // Send notification (email)
            await _notificationService.NotifyAsync(result, cancellationToken);
            
            // Send webhook notifications (Teams, Slack, Discord, Generic)
            await _webhookNotificationService.NotifyAsync(result, cancellationToken);

            // Send dashboard alert
            var serviceDto = ServiceStatusDto.FromHealthCheckResult(result, displayName);
            await _dashboardNotifier.SendServiceAlertAsync(serviceDto, cancellationToken);

            // Critical alert - only once when crossing threshold
            if (result.IsCritical && !wasCritical)
            {
                _logger.LogCritical(
                    "🚨 Service {ServiceName} has reached CRITICAL status!",
                    result.ServiceName);

                await _notificationService.SendCriticalAlertAsync(result, cancellationToken);
                await _webhookNotificationService.SendCriticalAlertAsync(result, cancellationToken);
                await _criticalAlertService.RaiseAlertAsync(result, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gets the current status of all monitored services.
    /// </summary>
    public IReadOnlyDictionary<string, ServiceState> GetServiceStates()
        => _serviceStates;

    /// <summary>
    /// Gets the last health check result for a specific service.
    /// </summary>
    public HealthCheckResult? GetLastResult(string serviceName)
        => _serviceStates.TryGetValue(serviceName, out var state) ? state.LastResult : null;
}

/// <summary>
/// Tracks the state of a monitored service.
/// </summary>
public sealed class ServiceState
{
    public bool IsHealthy { get; set; } = true;
    public bool IsCritical { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastCheckTime { get; set; }
    public HealthCheckResult? LastResult { get; set; }
}

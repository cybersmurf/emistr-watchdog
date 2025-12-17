using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Interface for notification services.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification about a health check result.
    /// </summary>
    /// <param name="result">Health check result to notify about.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(HealthCheckResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification about service recovery.
    /// </summary>
    /// <param name="serviceName">Name of the recovered service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyRecoveryAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a critical alert that requires immediate attention.
    /// </summary>
    /// <param name="result">Health check result triggering the alert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendCriticalAlertAsync(HealthCheckResult result, CancellationToken cancellationToken = default);
}

namespace Emistr.Watchdog.Models;

/// <summary>
/// Result of a health check operation.
/// </summary>
public sealed record HealthCheckResult
{
    /// <summary>
    /// Name of the service that was checked.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Whether the service is healthy.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Current status of the service.
    /// </summary>
    public ServiceStatus Status { get; init; }

    /// <summary>
    /// Response time in milliseconds (if applicable).
    /// </summary>
    public long? ResponseTimeMs { get; init; }

    /// <summary>
    /// Error message if unhealthy.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception details if an error occurred.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Additional details about the check result.
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = [];

    /// <summary>
    /// Server information (version, architecture, etc.).
    /// </summary>
    public ServerInfo? ServerInfo { get; init; }

    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Whether this is a critical failure (after threshold reached).
    /// </summary>
    public bool IsCritical { get; init; }

    public static HealthCheckResult Healthy(string serviceName, long? responseTimeMs = null)
        => new()
        {
            ServiceName = serviceName,
            IsHealthy = true,
            Status = ServiceStatus.Healthy,
            ResponseTimeMs = responseTimeMs
        };

    public static HealthCheckResult Unhealthy(
        string serviceName,
        string errorMessage,
        Exception? exception = null,
        int consecutiveFailures = 1,
        int criticalThreshold = 3)
        => new()
        {
            ServiceName = serviceName,
            IsHealthy = false,
            Status = consecutiveFailures >= criticalThreshold
                ? ServiceStatus.Critical
                : ServiceStatus.Unhealthy,
            ErrorMessage = errorMessage,
            Exception = exception,
            ConsecutiveFailures = consecutiveFailures,
            IsCritical = consecutiveFailures >= criticalThreshold
        };

    public static HealthCheckResult Degraded(string serviceName, string message, long? responseTimeMs = null)
        => new()
        {
            ServiceName = serviceName,
            IsHealthy = true,
            Status = ServiceStatus.Degraded,
            ErrorMessage = message,
            ResponseTimeMs = responseTimeMs
        };
}

/// <summary>
/// Service health status.
/// </summary>
public enum ServiceStatus
{
    /// <summary>
    /// Service is fully operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is operational but with some issues.
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is not responding or failing.
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Service has been unhealthy for extended period - requires immediate attention.
    /// </summary>
    Critical,

        /// <summary>
        /// Service status is unknown (check not performed yet).
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Server information retrieved during health check.
    /// </summary>
    public sealed record ServerInfo
    {
        /// <summary>
        /// Server software version (e.g., "10.11.6-MariaDB", "Apache/2.4.58").
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Server type/product name (e.g., "MariaDB", "Apache", "PHP").
        /// </summary>
        public string? ServerType { get; init; }

        /// <summary>
        /// Operating system or platform.
        /// </summary>
        public string? Platform { get; init; }

        /// <summary>
        /// Architecture (e.g., "x64", "arm64").
        /// </summary>
        public string? Architecture { get; init; }

        /// <summary>
        /// Additional version info (e.g., PHP version for Apache).
        /// </summary>
        public Dictionary<string, string> AdditionalInfo { get; init; } = [];
    }

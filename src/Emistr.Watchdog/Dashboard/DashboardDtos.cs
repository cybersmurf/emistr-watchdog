using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// DTOs for the dashboard API.
/// </summary>
public sealed record DashboardStatusResponse
{
    public required DateTime Timestamp { get; init; }
    public required string OverallStatus { get; init; }
    public required int HealthyCount { get; init; }
    public required int UnhealthyCount { get; init; }
    public required int CriticalCount { get; init; }
    public required IReadOnlyList<ServiceStatusDto> Services { get; init; }
}

public sealed record ServiceStatusDto
{
    public required string ServiceName { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required bool IsHealthy { get; init; }
    public required bool IsCritical { get; init; }
    public bool IsPrioritized { get; init; }
    public long? ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? LastCheckAt { get; init; }
    public int ConsecutiveFailures { get; init; }
    public ServerInfoDto? ServerInfo { get; init; }
    public RestartInfoDto? RestartInfo { get; init; }

    public static ServiceStatusDto FromHealthCheckResult(HealthCheckResult result, string displayName, bool isPrioritized = false)
        => new()
        {
            ServiceName = result.ServiceName,
            DisplayName = displayName,
            Status = result.Status.ToString(),
            IsHealthy = result.IsHealthy,
            IsCritical = result.IsCritical,
            IsPrioritized = isPrioritized,
            ResponseTimeMs = result.ResponseTimeMs,
            ErrorMessage = result.ErrorMessage,
            LastCheckAt = result.CheckedAt,
            ConsecutiveFailures = result.ConsecutiveFailures,
            ServerInfo = result.ServerInfo != null
                ? ServerInfoDto.FromServerInfo(result.ServerInfo)
                : null
        };
}

public sealed record RestartInfoDto
{
    public int RestartCount { get; init; }
    public DateTime? LastRestartTime { get; init; }
    public bool? LastRestartSuccess { get; init; }
    public bool RestartEnabled { get; init; }
}

public sealed record ServerInfoDto
{
    public string? Version { get; init; }
    public string? ServerType { get; init; }
    public string? Platform { get; init; }
    public string? Architecture { get; init; }
    public Dictionary<string, string> AdditionalInfo { get; init; } = [];

    public static ServerInfoDto FromServerInfo(ServerInfo info)
        => new()
        {
            Version = info.Version,
            ServerType = info.ServerType,
            Platform = info.Platform,
            Architecture = info.Architecture,
            AdditionalInfo = new Dictionary<string, string>(info.AdditionalInfo)
        };
}

public sealed record ServiceHistoryDto
{
    public required string ServiceName { get; init; }
    public required IReadOnlyList<HealthCheckHistoryEntry> History { get; init; }
}

public sealed record HealthCheckHistoryEntry
{
    public required DateTime Timestamp { get; init; }
    public required bool IsHealthy { get; init; }
    public required string Status { get; init; }
    public long? ResponseTimeMs { get; init; }
}

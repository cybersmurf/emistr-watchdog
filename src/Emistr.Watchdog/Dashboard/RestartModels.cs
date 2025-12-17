namespace Emistr.Watchdog.Dashboard;

public record RestartRequest
{
    public string ServiceName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public record RestartResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record RestartStatsResponse
{
    public string ServiceName { get; init; } = string.Empty;
    public int RestartCount { get; init; }
    public DateTime? LastRestartTime { get; init; }
    public bool LastRestartSuccess { get; init; }
}

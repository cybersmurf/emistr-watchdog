namespace Emistr.Watchdog.Services;

public interface IServiceController
{
    Task<bool> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<WindowsServiceStatus> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default);
    bool IsAvailable();
}

public enum WindowsServiceStatus
{
    Unknown,
    Running,
    Stopped,
    Paused,
    StartPending,
    StopPending,
    NotFound
}

/// <summary>
/// No-op service controller for non-Windows platforms.
/// </summary>
public sealed class NullServiceController : IServiceController
{
    public bool IsAvailable() => false;

    public Task<bool> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<WindowsServiceStatus> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
        => Task.FromResult(WindowsServiceStatus.Unknown);
}


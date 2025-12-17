using Microsoft.AspNetCore.SignalR;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// </summary>
public sealed class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Dashboard client connected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Dashboard client disconnected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by clients to request immediate status update.
    /// </summary>
    public async Task RequestStatus()
    {
        _logger.LogDebug("Status requested by {ConnectionId}", Context.ConnectionId);
        // The WatchdogService will send updates via the hub context
        await Clients.Caller.SendAsync("StatusRequested");
    }
}

/// <summary>
/// Interface for sending dashboard updates.
/// </summary>
public interface IDashboardNotifier
{
    Task SendStatusUpdateAsync(DashboardStatusResponse status, CancellationToken cancellationToken = default);
    Task SendServiceAlertAsync(ServiceStatusDto service, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for sending real-time updates to dashboard clients.
/// </summary>
public sealed class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardNotifier> _logger;

    public DashboardNotifier(
        IHubContext<DashboardHub> hubContext,
        ILogger<DashboardNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendStatusUpdateAsync(DashboardStatusResponse status, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("StatusUpdate", status, cancellationToken);
            _logger.LogDebug("Status update sent to all dashboard clients");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send status update to dashboard clients");
        }
    }

    public async Task SendServiceAlertAsync(ServiceStatusDto service, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ServiceAlert", service, cancellationToken);
            _logger.LogDebug("Service alert sent for {ServiceName}", service.ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send service alert to dashboard clients");
        }
    }
}

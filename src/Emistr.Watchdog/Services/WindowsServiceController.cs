using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Emistr.Watchdog.Services;

[SupportedOSPlatform("windows")]
public class WindowsServiceController : IServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;

    public WindowsServiceController(ILogger<WindowsServiceController> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable()
    {
        return OperatingSystem.IsWindows();
    }

    [SupportedOSPlatform("windows")]
    public async Task<bool> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("Service restart is only supported on Windows");
            return false;
        }

        try
        {
            _logger.LogInformation("Attempting to restart service: {ServiceName}", serviceName);

            using var service = new ServiceController(serviceName);

            // Stop service
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("Stopping service {ServiceName}...", serviceName);
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }

            // Small delay before start
            await Task.Delay(2000, cancellationToken);

            // Start service
            _logger.LogInformation("Starting service {ServiceName}...", serviceName);
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            _logger.LogInformation("Service {ServiceName} restarted successfully", serviceName);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Service {ServiceName} not found", serviceName);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Insufficient permissions to restart service {ServiceName}. Watchdog must run as Administrator", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service {ServiceName}", serviceName);
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public async Task<WindowsServiceStatus> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            return WindowsServiceStatus.Unknown;
        }

        await Task.CompletedTask; // Make truly async to avoid CS1998

        try
        {
            using var service = new ServiceController(serviceName);
            return service.Status switch
            {
                ServiceControllerStatus.Running => WindowsServiceStatus.Running,
                ServiceControllerStatus.Stopped => WindowsServiceStatus.Stopped,
                ServiceControllerStatus.Paused => WindowsServiceStatus.Paused,
                ServiceControllerStatus.StartPending => WindowsServiceStatus.StartPending,
                ServiceControllerStatus.StopPending => WindowsServiceStatus.StopPending,
                _ => WindowsServiceStatus.Unknown
            };
        }
        catch (InvalidOperationException)
        {
            return WindowsServiceStatus.NotFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for service {ServiceName}", serviceName);
            return WindowsServiceStatus.Unknown;
        }
    }
}

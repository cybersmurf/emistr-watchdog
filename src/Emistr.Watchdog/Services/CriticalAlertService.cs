using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Service for handling critical event alerts (sound, desktop notification, event log).
/// </summary>
public sealed class CriticalAlertService
{
    private readonly CriticalEventOptions _options;
    private readonly ILogger<CriticalAlertService> _logger;

    public CriticalAlertService(
        IOptions<NotificationOptions> options,
        ILogger<CriticalAlertService> logger)
    {
        _options = options.Value.CriticalEvents;
        _logger = logger;
    }

    /// <summary>
    /// Raises a critical alert using all configured channels.
    /// </summary>
    public async Task RaiseAlertAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        _logger.LogCritical(
            "CRITICAL ALERT: Service {ServiceName} is down! Error: {ErrorMessage}",
            result.ServiceName,
            result.ErrorMessage);

        var tasks = new List<Task>();

        if (_options.LogToEventLog && OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // Platform compatibility - already checked with OperatingSystem.IsWindows()
            tasks.Add(Task.Run(() => LogToEventLog(result), cancellationToken));
#pragma warning restore CA1416
        }

        if (_options.EnableSound)
        {
            tasks.Add(PlayAlertSoundAsync(cancellationToken));
        }

        if (_options.EnableDesktopNotification)
        {
            tasks.Add(ShowDesktopNotificationAsync(result, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    [SupportedOSPlatform("windows")]
    private void LogToEventLog(HealthCheckResult result)
    {
        try
        {
            const string source = "Emistr.Watchdog";
            const string logName = "Application";

            // Try to check if source exists - this may fail without admin rights
            bool sourceExists;
            try
            {
                sourceExists = EventLog.SourceExists(source);
            }
            catch (SecurityException)
            {
                // Cannot check - try to write anyway (source might exist)
                sourceExists = true;
            }

            if (!sourceExists)
            {
                try
                {
                    EventLog.CreateEventSource(source, logName);
                }
                catch (SecurityException)
                {
                    // Cannot create source without admin rights
                    // Log instructions for manual creation
                    _logger.LogWarning(
                        "Cannot create Event Log source. Run as admin once or execute: " +
                        "New-EventLog -LogName Application -Source 'Emistr.Watchdog'");
                    return;
                }
            }

            var message = $"""
                Critical Service Failure
                
                Service: {result.ServiceName}
                Status: {result.Status}
                Error: {result.ErrorMessage}
                Consecutive Failures: {result.ConsecutiveFailures}
                Time: {result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC
                """;

            EventLog.WriteEntry(source, message, EventLogEntryType.Error, 1001);

            _logger.LogDebug("Critical alert logged to Windows Event Log");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to Windows Event Log");
        }
    }

    private async Task PlayAlertSoundAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                await PlayWindowsSoundAsync(cancellationToken);
            }
            else
            {
                _logger.LogDebug("Sound alerts not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play alert sound");
        }
    }

    [SupportedOSPlatform("windows")]
    private Task PlayWindowsSoundAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                // Use custom sound file if configured, otherwise system beep
                if (!string.IsNullOrWhiteSpace(_options.SoundFilePath) && File.Exists(_options.SoundFilePath))
                {
                    // Use Windows multimedia to play custom sound
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-c \"(New-Object Media.SoundPlayer '{_options.SoundFilePath}').PlaySync()\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using var process = Process.Start(startInfo);
                    process?.WaitForExit(5000);
                }
                else
                {
                    // System beep - multiple beeps for critical alert
                    for (var i = 0; i < 3 && !cancellationToken.IsCancellationRequested; i++)
                    {
                        Console.Beep(1000, 500);
                        Thread.Sleep(200);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to play Windows sound");
            }
        }, cancellationToken);
    }

    private Task ShowDesktopNotificationAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    ShowWindowsToast(result);
                }
                else if (OperatingSystem.IsLinux())
                {
                    ShowLinuxNotification(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to show desktop notification");
            }
        }, cancellationToken);
    }

            [SupportedOSPlatform("windows")]
            private void ShowWindowsToast(HealthCheckResult result)
            {
                // Use PowerShell to show Windows toast notification with custom XML for better appearance
                var title = $"Critical Alert: {result.ServiceName}";
                var message = result.ErrorMessage?.Replace("'", "''").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;") ?? "Service is down";
                var timestamp = result.CheckedAt.ToString("HH:mm:ss");

                // Try to find icon in application directory
                var appDir = AppContext.BaseDirectory;
                var iconPath = Path.Combine(appDir, "wwwroot", "alert-icon.png");
                var hasIcon = File.Exists(iconPath);

                // Build image element if icon exists
                var imageElement = hasIcon 
                    ? $"<image placement=\"appLogoOverride\" hint-crop=\"circle\" src=\"file:///{iconPath.Replace("\\", "/")}\"/>"
                    : "";

                // Custom XML template with better formatting
                var toastXml = $@"
        <toast duration='long'>
            <visual>
                <binding template='ToastGeneric'>
                    {imageElement}
                    <text hint-maxLines='1'>Emistr Watchdog</text>
                    <text>{title}</text>
                    <text>{message}</text>
                    <text placement='attribution'>Time: {timestamp}</text>
                </binding>
            </visual>
            <audio src='ms-winsoundevent:Notification.Default' />
        </toast>".Replace("'", "\"");

                var script = $@"
        [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
        [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
        $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
        $xml.LoadXml('{toastXml.Replace("\r\n", "").Replace("\n", "").Replace("'", "''")}')
        $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
        [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Emistr.Watchdog').Show($toast)
        ";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-ExecutionPolicy Bypass -Command \"{script.Replace("\r\n", "; ").Replace("\n", "; ")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);

                _logger.LogDebug("Windows toast notification shown for {ServiceName} (icon={HasIcon})", result.ServiceName, hasIcon);
            }

    [SupportedOSPlatform("linux")]
    private void ShowLinuxNotification(HealthCheckResult result)
    {
        // Use notify-send on Linux
        var startInfo = new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = $"-u critical \"?? Critical Alert: {result.ServiceName}\" \"{result.ErrorMessage}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);

        _logger.LogDebug("Linux notification shown for {ServiceName}", result.ServiceName);
    }
}

using System.Collections.Concurrent;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// API endpoints for the dashboard.
/// </summary>
public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/status", GetStatus)
            .WithName("GetStatus");

        group.MapGet("/status/{serviceName}", GetServiceStatus)
            .WithName("GetServiceStatus");

        group.MapGet("/health", GetHealth)
            .WithName("GetHealth");

        group.MapGet("/history/{serviceName}", GetServiceHistory)
            .WithName("GetServiceHistory");

        group.MapPost("/restart", RestartService)
            .WithName("RestartService");

        group.MapGet("/restart-stats", GetRestartStats)
            .WithName("GetRestartStats");

        group.MapGet("/restart-stats/{serviceName}", GetServiceRestartStats)
            .WithName("GetServiceRestartStats");

        // Webhook test endpoints
        group.MapPost("/test-webhook", NotificationTestEndpoints.TestWebhook)
            .WithName("TestWebhook");

        group.MapPost("/test-email", NotificationTestEndpoints.TestEmail)
            .WithName("TestEmail");

        group.MapGet("/notification-config", NotificationTestEndpoints.GetNotificationConfig)
            .WithName("GetNotificationConfig");

        // SLA and Maintenance endpoints
        group.MapGet("/sla", GetSlaSummary)
            .WithName("GetSlaSummary");

        group.MapGet("/sla/{serviceName}", GetServiceSla)
            .WithName("GetServiceSla");

        group.MapGet("/maintenance", GetMaintenanceStatus)
            .WithName("GetMaintenanceStatus");

        // Escalation endpoints
        group.MapGet("/escalations", GetEscalations)
            .WithName("GetEscalations");

        group.MapGet("/escalations/{serviceName}", GetServiceEscalation)
            .WithName("GetServiceEscalation");

        // Recovery endpoints
        group.MapGet("/recovery", GetRecoveryStatus)
            .WithName("GetRecoveryStatus");

        group.MapPost("/recovery/{serviceName}", TriggerRecovery)
            .WithName("TriggerRecovery");

        group.MapGet("/recovery/history", GetRecoveryHistory)
            .WithName("GetRecoveryHistory");

        // Performance endpoints
        group.MapGet("/performance", GetPerformanceMetrics)
            .WithName("GetPerformanceMetrics");

        group.MapPost("/performance/reset", ResetPerformanceMetrics)
            .WithName("ResetPerformanceMetrics");
    }

    private static IResult GetPerformanceMetrics()
    {
        var summary = Emistr.Common.Middleware.PerformanceMetrics.GetSummary();
        return Results.Ok(summary);
    }

    private static IResult ResetPerformanceMetrics()
    {
        Emistr.Common.Middleware.PerformanceMetrics.Reset();
        return Results.Ok(new { message = "Performance metrics reset" });
    }

    private static IResult GetRecoveryStatus(IRecoveryService recoveryService)
    {
        var history = recoveryService.GetAllHistory();
        return Results.Ok(new
        {
            TotalServices = history.Count,
            RecentAttempts = history.Values
                .SelectMany(h => h)
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
        });
    }

    private static async Task<IResult> TriggerRecovery(
        string serviceName,
        IRecoveryService recoveryService,
        CancellationToken ct)
    {
        if (!recoveryService.CanAttemptRecovery(serviceName))
        {
            return Results.BadRequest(new { error = "Recovery not allowed (cooldown or max attempts)" });
        }

        var result = await recoveryService.AttemptRecoveryAsync(serviceName, ct);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static IResult GetRecoveryHistory(
        IRecoveryService recoveryService,
        string? serviceName = null)
    {
        if (!string.IsNullOrEmpty(serviceName))
        {
            return Results.Ok(recoveryService.GetHistory(serviceName));
        }
        return Results.Ok(recoveryService.GetAllHistory());
    }

    private static IResult GetEscalations(IEscalationTracker escalationTracker)
    {
        var escalations = escalationTracker.GetAllActiveEscalations();
        return Results.Ok(new
        {
            ActiveCount = escalations.Count,
            Escalations = escalations.Values.OrderByDescending(e => e.CurrentLevel)
        });
    }

    private static IResult GetServiceEscalation(
        string serviceName,
        IEscalationTracker escalationTracker)
    {
        var state = escalationTracker.GetState(serviceName);
        if (state == null)
        {
            return Results.Ok(new { ServiceName = serviceName, IsEscalated = false, Message = "No active escalation" });
        }
        return Results.Ok(state);
    }

    private static IResult GetSlaSummary(IUptimeTracker uptimeTracker)
    {
        var summary = uptimeTracker.GetSlaSummary();
        return Results.Ok(summary);
    }

    private static IResult GetServiceSla(
        string serviceName,
        IUptimeTracker uptimeTracker,
        int days = 30)
    {
        var period = TimeSpan.FromDays(days);
        var stats = uptimeTracker.GetStats(serviceName, period);
        return Results.Ok(stats);
    }

    private static IResult GetMaintenanceStatus(IOptions<MaintenanceWindowOptions> options)
    {
        var config = options.Value;
        var now = DateTime.UtcNow;
        var isInMaintenance = config.IsInMaintenanceWindow(now);
        var nextMaintenance = config.GetNextMaintenanceStart(now);

        return Results.Ok(new
        {
            IsInMaintenance = isInMaintenance,
            NextMaintenanceStart = nextMaintenance,
            Windows = config.Windows.Select(w => new
            {
                w.Name,
                w.Enabled,
                w.StartTime,
                w.EndTime,
                w.DaysOfWeek
            })
        });
    }

    private static IResult GetStatus(
        IEnumerable<IHealthChecker> checkers,
        StatusTracker tracker,
        ServiceRestartTracker restartTracker)
    {
        var services = new List<ServiceStatusDto>();

        foreach (var checker in checkers.Where(c => c.IsEnabled))
        {
            var lastResult = tracker.GetLastResult(checker.ServiceName);
            var restartInfo = restartTracker.GetRestartInfo(checker.ServiceName);
            var isPrioritized = ConfigurationEndpoints.GetRuntimePriority(checker.ServiceName, checker.IsPrioritized);

            if (lastResult != null)
            {
                var dto = ServiceStatusDto.FromHealthCheckResult(lastResult, checker.DisplayName, isPrioritized);
                services.Add(dto with
                {
                    RestartInfo = checker.RestartConfig?.Enabled == true
                        ? new RestartInfoDto
                        {
                            RestartCount = restartInfo?.Count ?? 0,
                            LastRestartTime = restartInfo?.LastAttemptTime,
                            LastRestartSuccess = restartInfo?.LastSuccess,
                            RestartEnabled = true
                        }
                        : null
                });
            }
            else
            {
                services.Add(new ServiceStatusDto
                {
                    ServiceName = checker.ServiceName,
                    DisplayName = checker.DisplayName,
                    Status = ServiceStatus.Unknown.ToString(),
                    IsHealthy = false,
                    IsCritical = false,
                    IsPrioritized = isPrioritized,
                    ConsecutiveFailures = 0,
                    RestartInfo = checker.RestartConfig?.Enabled == true
                        ? new RestartInfoDto
                        {
                            RestartCount = restartInfo?.Count ?? 0,
                            LastRestartTime = restartInfo?.LastAttemptTime,
                            LastRestartSuccess = restartInfo?.LastSuccess,
                            RestartEnabled = true
                        }
                        : null
                });
            }
        }

        var healthyCount = services.Count(s => s.IsHealthy);
        var criticalCount = services.Count(s => s.IsCritical);
        var unhealthyCount = services.Count(s => !s.IsHealthy && !s.IsCritical);

        var overallStatus = criticalCount > 0
            ? "Critical"
            : unhealthyCount > 0
                ? "Degraded"
                : "Healthy";

        // Sort services: prioritized first, then by name
        var sortedServices = services
            .OrderByDescending(s => s.IsPrioritized)
            .ThenBy(s => s.DisplayName)
            .ToList();

        var response = new DashboardStatusResponse
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = overallStatus,
            HealthyCount = healthyCount,
            UnhealthyCount = unhealthyCount,
            CriticalCount = criticalCount,
            Services = sortedServices
        };

        return Results.Ok(response);
    }

    private static IResult GetServiceStatus(
        string serviceName,
        IEnumerable<IHealthChecker> checkers,
        StatusTracker tracker)
    {
        var checker = checkers.FirstOrDefault(c =>
            c.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        if (checker == null)
        {
            return Results.NotFound(new { error = $"Service '{serviceName}' not found" });
        }

        var lastResult = tracker.GetLastResult(checker.ServiceName);

        if (lastResult == null)
        {
            return Results.Ok(new ServiceStatusDto
            {
                ServiceName = checker.ServiceName,
                DisplayName = checker.DisplayName,
                Status = ServiceStatus.Unknown.ToString(),
                IsHealthy = false,
                IsCritical = false,
                ConsecutiveFailures = 0
            });
        }

        return Results.Ok(ServiceStatusDto.FromHealthCheckResult(lastResult, checker.DisplayName));
    }

    private static IResult GetHealth()
    {
        return Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "Emistr.Watchdog"
        });
    }

    private static IResult GetServiceHistory(
        string serviceName,
        StatusTracker tracker,
        int count = 50)
    {
        var history = tracker.GetHistory(serviceName, count);

        if (history.Count == 0)
        {
            return Results.NotFound(new { error = $"No history found for service '{serviceName}'" });
        }

        return Results.Ok(new ServiceHistoryDto
        {
            ServiceName = serviceName,
            History = history.Select(h => new HealthCheckHistoryEntry
            {
                Timestamp = h.CheckedAt,
                IsHealthy = h.IsHealthy,
                Status = h.Status.ToString(),
                ResponseTimeMs = h.ResponseTimeMs
                        }).ToList()
                    });
                }

                private static async Task<IResult> RestartService(
                    RestartRequest request,
                    IEnumerable<IHealthChecker> checkers,
                    IServiceController serviceController,
                    ILogger<StatusTracker> logger)
                {
                    const string PASSWORD = "9009";

                    if (request.Password != PASSWORD)
                    {
                        logger.LogWarning("Unauthorized restart attempt for service {ServiceName}", request.ServiceName);
                        return Results.Unauthorized();
                    }

                    var checker = checkers.FirstOrDefault(c =>
                        c.ServiceName.Equals(request.ServiceName, StringComparison.OrdinalIgnoreCase));

                    if (checker == null)
                    {
                        return Results.NotFound(new RestartResponse
                        {
                            Success = false,
                            Message = $"Service '{request.ServiceName}' not found"
                        });
                    }

                    if (checker.RestartConfig == null || !checker.RestartConfig.Enabled)
                    {
                        return Results.BadRequest(new RestartResponse
                        {
                            Success = false,
                            Message = $"Service '{request.ServiceName}' does not have restart configured"
                        });
                    }

                    if (!serviceController.IsAvailable())
                    {
                        return Results.BadRequest(new RestartResponse
                        {
                            Success = false,
                            Message = "Service restart is not available on this platform"
                        });
                    }

                    logger.LogInformation("Manual restart requested for service {ServiceName}", request.ServiceName);

                    try
                    {
                        var success = await serviceController.RestartServiceAsync(
                            checker.RestartConfig.WindowsServiceName,
                            CancellationToken.None);

                        return Results.Ok(new RestartResponse
                        {
                            Success = success,
                            Message = success
                                ? $"Service '{checker.RestartConfig.WindowsServiceName}' restarted successfully"
                                : $"Failed to restart service '{checker.RestartConfig.WindowsServiceName}'"
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error restarting service {ServiceName}", request.ServiceName);
                        return Results.Ok(new RestartResponse
                        {
                            Success = false,
                            Message = $"Error: {ex.Message}"
                        });
                    }
                }

                private static IResult GetRestartStats(
                    IEnumerable<IHealthChecker> checkers,
                    ServiceRestartTracker restartTracker)
                {
                    var stats = checkers
                        .Where(c => c.IsEnabled && c.RestartConfig?.Enabled == true)
                        .Select(c =>
                        {
                            var info = restartTracker.GetRestartInfo(c.ServiceName);
                            return new RestartStatsResponse
                            {
                                ServiceName = c.ServiceName,
                                RestartCount = info?.Count ?? 0,
                                LastRestartTime = info?.LastAttemptTime,
                                LastRestartSuccess = info?.LastSuccess ?? false
                            };
                        })
                        .ToList();

                    return Results.Ok(stats);
                }

                private static IResult GetServiceRestartStats(
                    string serviceName,
                    IEnumerable<IHealthChecker> checkers,
                    ServiceRestartTracker restartTracker)
                {
                    var checker = checkers.FirstOrDefault(c =>
                        c.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                    if (checker == null)
                    {
                        return Results.NotFound(new { error = $"Service '{serviceName}' not found" });
                    }

                    var info = restartTracker.GetRestartInfo(serviceName);

                    return Results.Ok(new RestartStatsResponse
                    {
                        ServiceName = serviceName,
                        RestartCount = info?.Count ?? 0,
                        LastRestartTime = info?.LastAttemptTime,
                        LastRestartSuccess = info?.LastSuccess ?? false
                    });
                }
            }

/// <summary>
/// Tracks health check results and maintains history.
/// </summary>
public sealed class StatusTracker
{
    private readonly ConcurrentDictionary<string, HealthCheckResult> _lastResults = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<HealthCheckResult>> _history = new();
    private readonly int _maxHistoryPerService;

    public StatusTracker(int maxHistoryPerService = 100)
    {
        _maxHistoryPerService = maxHistoryPerService;
    }

    public void RecordResult(HealthCheckResult result)
    {
        _lastResults[result.ServiceName] = result;

        var history = _history.GetOrAdd(result.ServiceName, _ => new ConcurrentQueue<HealthCheckResult>());
        history.Enqueue(result);

        // Trim history if needed
        while (history.Count > _maxHistoryPerService)
        {
            history.TryDequeue(out _);
        }
    }

    public HealthCheckResult? GetLastResult(string serviceName)
    {
        return _lastResults.TryGetValue(serviceName, out var result) ? result : null;
    }

    public IReadOnlyList<HealthCheckResult> GetHistory(string serviceName, int count)
    {
        if (!_history.TryGetValue(serviceName, out var history))
        {
            return [];
        }

        return history.TakeLast(count).Reverse().ToList();
    }

    public IReadOnlyDictionary<string, HealthCheckResult> GetAllLastResults()
    {
        return _lastResults;
    }
}

// Webhook/Email test endpoints implementation
public static class NotificationTestEndpoints
{
    public static async Task<IResult> TestWebhook(
        TestWebhookRequest request,
        WebhookNotificationService webhookService,
        ILogger<StatusTracker> logger)
    {
        try
        {
            var testResult = new HealthCheckResult
            {
                ServiceName = "Test Service",
                IsHealthy = false,
                Status = ServiceStatus.Unhealthy,
                IsCritical = false,
                ErrorMessage = "This is a test notification from Emistr Watchdog Dashboard",
                ConsecutiveFailures = 1,
                CheckedAt = DateTime.UtcNow
            };

            await webhookService.NotifyAsync(testResult);
            
            logger.LogInformation("Test webhook sent successfully for provider: {Provider}", request.Provider ?? "all");
            
            return Results.Ok(new TestNotificationResponse
            {
                Success = true,
                Message = "Test webhook sent successfully. Check your configured channels."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send test webhook");
            return Results.BadRequest(new TestNotificationResponse
            {
                Success = false,
                Message = $"Failed to send test webhook: {ex.Message}"
            });
        }
    }

    public static async Task<IResult> TestEmail(
        TestEmailRequest request,
        INotificationService emailService,
        ILogger<StatusTracker> logger)
    {
        try
        {
            var testResult = new HealthCheckResult
            {
                ServiceName = "Test Service",
                IsHealthy = false,
                Status = ServiceStatus.Unhealthy,
                IsCritical = false,
                ErrorMessage = "This is a test email notification from Emistr Watchdog Dashboard",
                ConsecutiveFailures = 1,
                CheckedAt = DateTime.UtcNow
            };

            await emailService.NotifyAsync(testResult);
            
            logger.LogInformation("Test email sent successfully");
            
            return Results.Ok(new TestNotificationResponse
            {
                Success = true,
                Message = "Test email sent successfully. Check your inbox."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send test email");
            return Results.BadRequest(new TestNotificationResponse
            {
                Success = false,
                Message = $"Failed to send test email: {ex.Message}"
            });
        }
    }

    public static IResult GetNotificationConfig(
        IConfiguration configuration)
    {
        var notificationSection = configuration.GetSection("Notifications");
        
        var config = new NotificationConfigResponse
        {
            Email = new EmailConfigStatus
            {
                Enabled = notificationSection.GetValue<bool>("Email:Enabled"),
                Provider = notificationSection.GetValue<string>("Email:Provider") ?? "Smtp",
                FromAddress = notificationSection.GetValue<string>("Email:FromAddress") ?? "",
                RecipientsCount = notificationSection.GetSection("Email:Recipients").GetChildren().Count()
            },
            Webhooks = new WebhookConfigStatus
            {
                TeamsEnabled = notificationSection.GetValue<bool>("Teams:Enabled"),
                SlackEnabled = notificationSection.GetValue<bool>("Slack:Enabled"),
                DiscordEnabled = notificationSection.GetValue<bool>("Discord:Enabled"),
                GenericEnabled = notificationSection.GetValue<bool>("GenericWebhook:Enabled")
            }
        };

        return Results.Ok(config);
    }
}

// DTOs for test endpoints
public record TestWebhookRequest
{
    public string? Provider { get; init; }
}

public record TestEmailRequest
{
    public string? RecipientOverride { get; init; }
}

public record TestNotificationResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record NotificationConfigResponse
{
    public EmailConfigStatus Email { get; init; } = new();
    public WebhookConfigStatus Webhooks { get; init; } = new();
}

public record EmailConfigStatus
{
    public bool Enabled { get; init; }
    public string Provider { get; init; } = "Smtp";
    public string FromAddress { get; init; } = "";
    public int RecipientsCount { get; init; }
}

public record WebhookConfigStatus
{
    public bool TeamsEnabled { get; init; }
    public bool SlackEnabled { get; init; }
    public bool DiscordEnabled { get; init; }
    public bool GenericEnabled { get; init; }
}


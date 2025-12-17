using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Service for sending webhook notifications to Teams, Slack, Discord, and custom endpoints.
/// </summary>
public sealed class WebhookNotificationService : INotificationService
{
    private readonly NotificationOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookNotificationService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastNotificationTimes = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WebhookNotificationService(
        IOptions<NotificationOptions> options,
        HttpClient httpClient,
        ILogger<WebhookNotificationService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        if (_options.Teams.Enabled && ShouldNotify("teams", result, _options.Teams.CooldownMinutes, _options.Teams.CriticalOnly))
        {
            tasks.Add(SendTeamsNotificationAsync(result, cancellationToken));
        }

        if (_options.Slack.Enabled && ShouldNotify("slack", result, _options.Slack.CooldownMinutes, _options.Slack.CriticalOnly))
        {
            tasks.Add(SendSlackNotificationAsync(result, cancellationToken));
        }

        if (_options.Discord.Enabled && ShouldNotify("discord", result, _options.Discord.CooldownMinutes, _options.Discord.CriticalOnly))
        {
            tasks.Add(SendDiscordNotificationAsync(result, cancellationToken));
        }

        if (_options.GenericWebhook.Enabled && ShouldNotify("generic", result, _options.GenericWebhook.CooldownMinutes, _options.GenericWebhook.CriticalOnly))
        {
            tasks.Add(SendGenericWebhookAsync(result, cancellationToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <inheritdoc />
    public async Task NotifyRecoveryAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var recoveryResult = new HealthCheckResult
        {
            ServiceName = serviceName,
            IsHealthy = true,
            Status = ServiceStatus.Healthy,
            CheckedAt = DateTime.UtcNow
        };

        var tasks = new List<Task>();

        if (_options.Teams.Enabled)
        {
            tasks.Add(SendTeamsRecoveryAsync(serviceName, cancellationToken));
        }

        if (_options.Slack.Enabled)
        {
            tasks.Add(SendSlackRecoveryAsync(serviceName, cancellationToken));
        }

        if (_options.Discord.Enabled)
        {
            tasks.Add(SendDiscordRecoveryAsync(serviceName, cancellationToken));
        }

        if (_options.GenericWebhook.Enabled)
        {
            tasks.Add(SendGenericWebhookRecoveryAsync(serviceName, cancellationToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        // Clear cooldown for the service
        ClearCooldown(serviceName);
    }

    /// <inheritdoc />
    public Task SendCriticalAlertAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        // For webhook service, critical alerts use the same mechanism as regular notifications
        // but with CriticalOnly check bypassed
        return NotifyAsync(result with { IsCritical = true }, cancellationToken);
    }

    #region Teams

    private async Task SendTeamsNotificationAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            var payload = CreateTeamsPayload(result);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(_options.Teams.WebhookUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams notification sent for {ServiceName}", result.ServiceName);
                UpdateCooldown("teams", result.ServiceName);
            }
            else
            {
                _logger.LogWarning("Failed to send Teams notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams notification for {ServiceName}", result.ServiceName);
        }
    }

    private async Task SendTeamsRecoveryAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                type = "MessageCard",
                context = "http://schema.org/extensions",
                themeColor = "00FF00",
                summary = $"Service Recovered: {serviceName}",
                sections = new[]
                {
                    new
                    {
                        activityTitle = $"✅ {serviceName} is back online",
                        activitySubtitle = $"Recovered at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                        facts = new[]
                        {
                            new { name = "Status", value = "Healthy" },
                            new { name = "Service", value = serviceName }
                        },
                        markdown = true
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync(_options.Teams.WebhookUrl, content, cancellationToken);
            _logger.LogInformation("Teams recovery notification sent for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams recovery notification for {ServiceName}", serviceName);
        }
    }

    private static object CreateTeamsPayload(HealthCheckResult result)
    {
        var themeColor = result.Status switch
        {
            ServiceStatus.Critical => "8B0000",
            ServiceStatus.Unhealthy => "FF0000",
            ServiceStatus.Degraded => "FFA500",
            _ => "00FF00"
        };

        var statusEmoji = result.Status switch
        {
            ServiceStatus.Critical => "🔴",
            ServiceStatus.Unhealthy => "🟠",
            ServiceStatus.Degraded => "🟡",
            _ => "🟢"
        };

        return new
        {
            type = "MessageCard",
            context = "http://schema.org/extensions",
            themeColor,
            summary = $"Service Alert: {result.ServiceName}",
            sections = new[]
            {
                new
                {
                    activityTitle = $"{statusEmoji} {result.ServiceName} - {result.Status}",
                    activitySubtitle = $"Checked at {result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC",
                    facts = new[]
                    {
                        new { name = "Status", value = result.Status.ToString() },
                        new { name = "Error", value = result.ErrorMessage ?? "N/A" },
                        new { name = "Consecutive Failures", value = result.ConsecutiveFailures.ToString() },
                        new { name = "Response Time", value = result.ResponseTimeMs.HasValue ? $"{result.ResponseTimeMs}ms" : "N/A" }
                    },
                    markdown = true
                }
            }
        };
    }

    #endregion

    #region Slack

    private async Task SendSlackNotificationAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            var payload = CreateSlackPayload(result);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(_options.Slack.WebhookUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Slack notification sent for {ServiceName}", result.ServiceName);
                UpdateCooldown("slack", result.ServiceName);
            }
            else
            {
                _logger.LogWarning("Failed to send Slack notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Slack notification for {ServiceName}", result.ServiceName);
        }
    }

    private async Task SendSlackRecoveryAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                channel = _options.Slack.Channel,
                username = _options.Slack.Username,
                icon_emoji = ":white_check_mark:",
                attachments = new[]
                {
                    new
                    {
                        color = "#00FF00",
                        blocks = new object[]
                        {
                            new
                            {
                                type = "header",
                                text = new { type = "plain_text", text = $"✅ Service Recovered: {serviceName}" }
                            },
                            new
                            {
                                type = "section",
                                text = new { type = "mrkdwn", text = $"*{serviceName}* is back online\n_Recovered at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_" }
                            }
                        }
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync(_options.Slack.WebhookUrl, content, cancellationToken);
            _logger.LogInformation("Slack recovery notification sent for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Slack recovery notification for {ServiceName}", serviceName);
        }
    }

    private object CreateSlackPayload(HealthCheckResult result)
    {
        var color = result.Status switch
        {
            ServiceStatus.Critical => "#8B0000",
            ServiceStatus.Unhealthy => "#FF0000",
            ServiceStatus.Degraded => "#FFA500",
            _ => "#00FF00"
        };

        var statusEmoji = result.Status switch
        {
            ServiceStatus.Critical => ":red_circle:",
            ServiceStatus.Unhealthy => ":large_orange_circle:",
            ServiceStatus.Degraded => ":large_yellow_circle:",
            _ => ":large_green_circle:"
        };

        return new
        {
            channel = _options.Slack.Channel,
            username = _options.Slack.Username,
            icon_emoji = ":warning:",
            attachments = new[]
            {
                new
                {
                    color,
                    blocks = new object[]
                    {
                        new
                        {
                            type = "header",
                            text = new { type = "plain_text", text = $"{statusEmoji} Service Alert: {result.ServiceName}" }
                        },
                        new
                        {
                            type = "section",
                            fields = new[]
                            {
                                new { type = "mrkdwn", text = $"*Status:*\n{result.Status}" },
                                new { type = "mrkdwn", text = $"*Failures:*\n{result.ConsecutiveFailures}" }
                            }
                        },
                        new
                        {
                            type = "section",
                            text = new { type = "mrkdwn", text = $"*Error:* {result.ErrorMessage ?? "N/A"}" }
                        },
                        new
                        {
                            type = "context",
                            elements = new[]
                            {
                                new { type = "mrkdwn", text = $"Checked at {result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC" }
                            }
                        }
                    }
                }
            }
        };
    }

    #endregion

    #region Discord

    private async Task SendDiscordNotificationAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            var payload = CreateDiscordPayload(result);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(_options.Discord.WebhookUrl, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Discord notification sent for {ServiceName}", result.ServiceName);
                UpdateCooldown("discord", result.ServiceName);
            }
            else
            {
                _logger.LogWarning("Failed to send Discord notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Discord notification for {ServiceName}", result.ServiceName);
        }
    }

    private async Task SendDiscordRecoveryAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                username = _options.Discord.Username,
                embeds = new[]
                {
                    new
                    {
                        title = $"✅ Service Recovered: {serviceName}",
                        color = 65280, // Green
                        fields = new[]
                        {
                            new { name = "Status", value = "Healthy", inline = true },
                            new { name = "Service", value = serviceName, inline = true }
                        },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            await _httpClient.PostAsync(_options.Discord.WebhookUrl, content, cancellationToken);
            _logger.LogInformation("Discord recovery notification sent for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Discord recovery notification for {ServiceName}", serviceName);
        }
    }

    private object CreateDiscordPayload(HealthCheckResult result)
    {
        var color = result.Status switch
        {
            ServiceStatus.Critical => 9109504,  // Dark red
            ServiceStatus.Unhealthy => 16711680, // Red
            ServiceStatus.Degraded => 16753920,  // Orange
            _ => 65280                            // Green
        };

        var statusEmoji = result.Status switch
        {
            ServiceStatus.Critical => "🔴",
            ServiceStatus.Unhealthy => "🟠",
            ServiceStatus.Degraded => "🟡",
            _ => "🟢"
        };

        return new
        {
            username = _options.Discord.Username,
            embeds = new[]
            {
                new
                {
                    title = $"{statusEmoji} Service Alert: {result.ServiceName}",
                    color,
                    fields = new[]
                    {
                        new { name = "Status", value = result.Status.ToString(), inline = true },
                        new { name = "Consecutive Failures", value = result.ConsecutiveFailures.ToString(), inline = true },
                        new { name = "Response Time", value = result.ResponseTimeMs.HasValue ? $"{result.ResponseTimeMs}ms" : "N/A", inline = true },
                        new { name = "Error", value = result.ErrorMessage ?? "N/A", inline = false }
                    },
                    timestamp = result.CheckedAt.ToString("o")
                }
            }
        };
    }

    #endregion

    #region Generic Webhook

    private async Task SendGenericWebhookAsync(HealthCheckResult result, CancellationToken cancellationToken)
    {
        try
        {
            var payload = CreateGenericPayload(result, "ServiceStatusChanged");
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(
                _options.GenericWebhook.Method.ToUpperInvariant() == "PUT" ? HttpMethod.Put : HttpMethod.Post,
                _options.GenericWebhook.Url);
            
            request.Content = content;

            // Add custom headers
            foreach (var header in _options.GenericWebhook.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Generic webhook notification sent for {ServiceName}", result.ServiceName);
                UpdateCooldown("generic", result.ServiceName);
            }
            else
            {
                _logger.LogWarning("Failed to send generic webhook notification: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending generic webhook notification for {ServiceName}", result.ServiceName);
        }
    }

    private async Task SendGenericWebhookRecoveryAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var recoveryResult = new HealthCheckResult
            {
                ServiceName = serviceName,
                IsHealthy = true,
                Status = ServiceStatus.Healthy,
                CheckedAt = DateTime.UtcNow
            };

            var payload = CreateGenericPayload(recoveryResult, "ServiceRecovered");
            var content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(
                _options.GenericWebhook.Method.ToUpperInvariant() == "PUT" ? HttpMethod.Put : HttpMethod.Post,
                _options.GenericWebhook.Url);
            
            request.Content = content;

            foreach (var header in _options.GenericWebhook.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            await _httpClient.SendAsync(request, cancellationToken);
            _logger.LogInformation("Generic webhook recovery notification sent for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending generic webhook recovery notification for {ServiceName}", serviceName);
        }
    }

    private static object CreateGenericPayload(HealthCheckResult result, string eventType)
    {
        return new
        {
            timestamp = result.CheckedAt.ToString("o"),
            eventType,
            service = new
            {
                name = result.ServiceName,
                status = result.Status.ToString(),
                isHealthy = result.IsHealthy,
                isCritical = result.IsCritical
            },
            details = new
            {
                errorMessage = result.ErrorMessage,
                responseTimeMs = result.ResponseTimeMs,
                consecutiveFailures = result.ConsecutiveFailures
            },
            serverInfo = result.ServerInfo != null ? new
            {
                version = result.ServerInfo.Version,
                serverType = result.ServerInfo.ServerType,
                additionalInfo = result.ServerInfo.AdditionalInfo
            } : null
        };
    }

    #endregion

    #region Cooldown Management

    private bool ShouldNotify(string provider, HealthCheckResult result, int cooldownMinutes, bool criticalOnly)
    {
        // Always notify for critical events if not filtered
        if (result.IsCritical)
        {
            return true;
        }

        // Skip non-critical events if criticalOnly is enabled
        if (criticalOnly)
        {
            return false;
        }

        // Check cooldown
        var key = $"{provider}:{result.ServiceName}";
        if (_lastNotificationTimes.TryGetValue(key, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < TimeSpan.FromMinutes(cooldownMinutes))
            {
                _logger.LogDebug("Notification for {ServiceName} on {Provider} skipped due to cooldown", result.ServiceName, provider);
                return false;
            }
        }

        return true;
    }

    private void UpdateCooldown(string provider, string serviceName)
    {
        var key = $"{provider}:{serviceName}";
        _lastNotificationTimes[key] = DateTime.UtcNow;
    }

    private void ClearCooldown(string serviceName)
    {
        // Remove all cooldowns for this service (across all providers)
        var keysToRemove = _lastNotificationTimes.Keys
            .Where(k => k.EndsWith($":{serviceName}"))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _lastNotificationTimes.TryRemove(key, out _);
        }
    }

    #endregion
}


using System.Collections.Concurrent;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Email notification service using MailKit.
/// </summary>
public sealed class EmailNotificationService : INotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastNotificationTimes = new();

    public EmailNotificationService(
        IOptions<NotificationOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value.Email;
        _logger = logger;
    }

    public async Task NotifyAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.Recipients.Count == 0)
        {
            _logger.LogDebug("Email notifications disabled or no recipients configured");
            return;
        }

        // Check cooldown to prevent spam
        if (IsInCooldown(result.ServiceName))
        {
            _logger.LogDebug(
                "Skipping notification for {ServiceName} - in cooldown period",
                result.ServiceName);
            return;
        }

        var subject = result.IsCritical
            ? $"?? CRITICAL: {result.ServiceName} is DOWN"
            : $"?? WARNING: {result.ServiceName} is unhealthy";

        var body = BuildEmailBody(result);

        await SendEmailAsync(subject, body, cancellationToken);

        _lastNotificationTimes[result.ServiceName] = DateTime.UtcNow;
    }

    public async Task NotifyRecoveryAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.Recipients.Count == 0)
        {
            return;
        }

        var subject = $"? RECOVERED: {serviceName} is back online";
        var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif;">
            <h2 style="color: #28a745;">Service Recovered</h2>
            <p><strong>Service:</strong> {serviceName}</p>
            <p><strong>Status:</strong> Healthy</p>
            <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <hr/>
            <p style="color: #666; font-size: 12px;">Emistr Watchdog</p>
            </body>
            </html>
            """;

        await SendEmailAsync(subject, body, cancellationToken);

        // Clear cooldown on recovery
        _lastNotificationTimes.TryRemove(serviceName, out _);
    }

    public async Task SendCriticalAlertAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || _options.Recipients.Count == 0)
        {
            return;
        }

        // Critical alerts bypass cooldown
        var subject = $"???? CRITICAL ALERT: {result.ServiceName} - IMMEDIATE ACTION REQUIRED";
        var body = BuildCriticalEmailBody(result);

        await SendEmailAsync(subject, body, cancellationToken);
    }

    private bool IsInCooldown(string serviceName)
    {
        if (_lastNotificationTimes.TryGetValue(serviceName, out var lastTime))
        {
            var cooldownPeriod = TimeSpan.FromMinutes(_options.CooldownMinutes);
            return DateTime.UtcNow - lastTime < cooldownPeriod;
        }

        return false;
    }

    private static string BuildEmailBody(HealthCheckResult result)
    {
        var statusColor = result.IsCritical ? "#dc3545" : "#ffc107";
        var statusText = result.IsCritical ? "CRITICAL" : "UNHEALTHY";

        return $"""
            <html>
            <body style="font-family: Arial, sans-serif;">
            <h2 style="color: {statusColor};">Service Alert: {statusText}</h2>
            <table style="border-collapse: collapse; width: 100%; max-width: 600px;">
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Service</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{result.ServiceName}</td>
                </tr>
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Status</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd; color: {statusColor};">{result.Status}</td>
                </tr>
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Error</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{result.ErrorMessage ?? "N/A"}</td>
                </tr>
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Consecutive Failures</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{result.ConsecutiveFailures}</td>
                </tr>
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Checked At</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                </tr>
            </table>
            <hr/>
            <p style="color: #666; font-size: 12px;">Emistr Watchdog</p>
            </body>
            </html>
            """;
    }

    private static string BuildCriticalEmailBody(HealthCheckResult result)
    {
        return $"""
            <html>
            <body style="font-family: Arial, sans-serif; background-color: #fff3cd; padding: 20px;">
            <div style="background-color: #dc3545; color: white; padding: 20px; text-align: center;">
                <h1>?? CRITICAL SYSTEM ALERT ??</h1>
            </div>
            <div style="padding: 20px; background-color: white; margin-top: 20px;">
                <h2>Immediate Action Required</h2>
                <p><strong>Service:</strong> {result.ServiceName}</p>
                <p><strong>Status:</strong> <span style="color: #dc3545; font-weight: bold;">CRITICAL</span></p>
                <p><strong>Error:</strong> {result.ErrorMessage ?? "Service not responding"}</p>
                <p><strong>Failed Checks:</strong> {result.ConsecutiveFailures}</p>
                <p><strong>Time:</strong> {result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
                
                {(result.Exception != null ? $"<p><strong>Exception:</strong> {result.Exception.GetType().Name}: {result.Exception.Message}</p>" : "")}
            </div>
            <div style="padding: 10px; background-color: #f8d7da; margin-top: 20px; border-radius: 5px;">
                <p style="margin: 0; font-weight: bold;">This alert indicates a critical service failure that may affect system operations.</p>
            </div>
            <hr/>
            <p style="color: #666; font-size: 12px;">Emistr Watchdog - Critical Alert System</p>
            </body>
            </html>
            """;
    }

    private async Task SendEmailAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

            foreach (var recipient in _options.Recipients)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.SmtpHost,
                _options.SmtpPort,
                _options.UseSsl,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Email notification sent: {Subject} to {Recipients}",
                subject,
                string.Join(", ", _options.Recipients));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification: {Subject}", subject);
        }
    }
}

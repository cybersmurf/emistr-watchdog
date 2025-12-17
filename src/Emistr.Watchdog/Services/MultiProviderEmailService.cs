using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Email service that supports multiple providers (SMTP, SendGrid, Microsoft365, etc.)
/// </summary>
public sealed class MultiProviderEmailService : INotificationService
{
    private readonly EmailOptions _options;
    private readonly IEmailSender _sender;
    private readonly ILogger<MultiProviderEmailService> _logger;
    private readonly Dictionary<string, DateTime> _lastNotificationTimes = new();

    public MultiProviderEmailService(
        IOptions<NotificationOptions> options,
        IEmailSenderFactory senderFactory,
        ILogger<MultiProviderEmailService> logger)
    {
        _options = options.Value.Email;
        _sender = senderFactory.Create(_options);
        _logger = logger;
    }

    public async Task NotifyAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;
        if (_options.CriticalOnly && !result.IsCritical) return;
        if (!ShouldNotify(result.ServiceName)) return;

        var subject = $"[{result.Status}] {result.ServiceName} - Health Alert";
        var body = BuildEmailBody(result);

        try
        {
            await _sender.SendAsync(
                _options.Recipients,
                subject,
                body,
                cancellationToken);

            UpdateLastNotificationTime(result.ServiceName);
            _logger.LogInformation("Email notification sent for {ServiceName} via {Provider}",
                result.ServiceName, _options.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email notification for {ServiceName}", result.ServiceName);
        }
    }

    public async Task NotifyRecoveryAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var subject = $"[RECOVERED] {serviceName} - Back Online";
        var body = $"""
            <html>
            <body style="font-family: Arial, sans-serif;">
            <h2 style="color: #28a745;">✅ Service Recovered</h2>
            <p><strong>{serviceName}</strong> is back online.</p>
            <p>Recovery time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <hr>
            <p style="color: #666; font-size: 12px;">Emistr Watchdog</p>
            </body>
            </html>
            """;

        try
        {
            await _sender.SendAsync(_options.Recipients, subject, body, cancellationToken);
            ClearLastNotificationTime(serviceName);
            _logger.LogInformation("Recovery email sent for {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send recovery email for {ServiceName}", serviceName);
        }
    }

    public Task SendCriticalAlertAsync(HealthCheckResult result, CancellationToken cancellationToken = default)
    {
        return NotifyAsync(result with { IsCritical = true }, cancellationToken);
    }

    private static string BuildEmailBody(HealthCheckResult result)
    {
        var statusColor = result.Status switch
        {
            ServiceStatus.Critical => "#dc3545",
            ServiceStatus.Unhealthy => "#fd7e14",
            ServiceStatus.Degraded => "#ffc107",
            _ => "#28a745"
        };

        return $"""
            <html>
            <body style="font-family: Arial, sans-serif;">
            <h2 style="color: {statusColor};">Service Alert: {result.ServiceName}</h2>
            <table style="border-collapse: collapse; width: 100%; max-width: 600px;">
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
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Response Time</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{(result.ResponseTimeMs.HasValue ? $"{result.ResponseTimeMs}ms" : "N/A")}</td>
                </tr>
                <tr>
                    <td style="padding: 8px; border: 1px solid #ddd;"><strong>Checked At</strong></td>
                    <td style="padding: 8px; border: 1px solid #ddd;">{result.CheckedAt:yyyy-MM-dd HH:mm:ss} UTC</td>
                </tr>
            </table>
            <hr>
            <p style="color: #666; font-size: 12px;">Emistr Watchdog</p>
            </body>
            </html>
            """;
    }

    private bool ShouldNotify(string serviceName)
    {
        if (_lastNotificationTimes.TryGetValue(serviceName, out var lastTime))
        {
            return DateTime.UtcNow - lastTime >= TimeSpan.FromMinutes(_options.CooldownMinutes);
        }
        return true;
    }

    private void UpdateLastNotificationTime(string serviceName)
    {
        _lastNotificationTimes[serviceName] = DateTime.UtcNow;
    }

    private void ClearLastNotificationTime(string serviceName)
    {
        _lastNotificationTimes.Remove(serviceName);
    }
}

/// <summary>
/// Interface for email senders.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating email senders based on configuration.
/// </summary>
public interface IEmailSenderFactory
{
    IEmailSender Create(EmailOptions options);
}

/// <summary>
/// Factory implementation that creates appropriate email sender based on provider.
/// </summary>
public sealed class EmailSenderFactory : IEmailSenderFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public EmailSenderFactory(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public IEmailSender Create(EmailOptions options)
    {
        return options.Provider switch
        {
            EmailProvider.SendGrid => new SendGridEmailSender(options, _httpClientFactory, _loggerFactory.CreateLogger<SendGridEmailSender>()),
            EmailProvider.Microsoft365 => new Microsoft365EmailSender(options, _httpClientFactory, _loggerFactory.CreateLogger<Microsoft365EmailSender>()),
            EmailProvider.Mailgun => new MailgunEmailSender(options, _httpClientFactory, _loggerFactory.CreateLogger<MailgunEmailSender>()),
            EmailProvider.Mailchimp => new MailchimpEmailSender(options, _httpClientFactory, _loggerFactory.CreateLogger<MailchimpEmailSender>()),
            _ => new SmtpEmailSender(options, _loggerFactory.CreateLogger<SmtpEmailSender>())
        };
    }
}

/// <summary>
/// SMTP email sender using MailKit.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(_options.FromName, _options.FromAddress));
        
        foreach (var recipient in recipients)
        {
            message.To.Add(MimeKit.MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;
        message.Body = new MimeKit.TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl, cancellationToken);
        
        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogDebug("Email sent via SMTP to {RecipientCount} recipients", recipients.Count());
    }
}

/// <summary>
/// SendGrid API email sender.
/// </summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(EmailOptions options, IHttpClientFactory httpClientFactory, ILogger<SendGridEmailSender> logger)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://api.sendgrid.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            personalizations = new[]
            {
                new { to = recipients.Select(r => new { email = r }).ToArray() }
            },
            from = new { email = _options.FromAddress, name = _options.FromName },
            subject,
            content = new[]
            {
                new { type = "text/html", value = htmlBody }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("v3/mail/send", payload, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid API error: {response.StatusCode} - {error}");
        }

        _logger.LogDebug("Email sent via SendGrid to {RecipientCount} recipients", recipients.Count());
    }
}

/// <summary>
/// Microsoft 365 / Graph API email sender.
/// </summary>
public sealed class Microsoft365EmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<Microsoft365EmailSender> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public Microsoft365EmailSender(EmailOptions options, IHttpClientFactory httpClientFactory, ILogger<Microsoft365EmailSender> logger)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var payload = new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = htmlBody },
                toRecipients = recipients.Select(r => new { emailAddress = new { address = r } }).ToArray()
            },
            saveToSentItems = "false"
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://graph.microsoft.com/v1.0/users/{_options.FromAddress}/sendMail",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Microsoft Graph API error: {response.StatusCode} - {error}");
        }

        _logger.LogDebug("Email sent via Microsoft 365 to {RecipientCount} recipients", recipients.Count());
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return;

        var tokenEndpoint = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId!,
            ["client_secret"] = _options.ClientSecret!,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        _accessToken = result.GetProperty("access_token").GetString();
        var expiresIn = result.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 1 minute buffer

        _logger.LogDebug("Microsoft 365 access token acquired, expires at {Expiry}", _tokenExpiry);
    }
}

/// <summary>
/// Mailgun API email sender.
/// </summary>
public sealed class MailgunEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MailgunEmailSender> _logger;

    public MailgunEmailSender(EmailOptions options, IHttpClientFactory httpClientFactory, ILogger<MailgunEmailSender> logger)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
        
        // Mailgun uses basic auth with api:key
        var authBytes = Encoding.ASCII.GetBytes($"api:{options.ApiKey}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        // Domain is extracted from FromAddress or should be configured separately
        var domain = _options.FromAddress.Split('@').LastOrDefault() ?? "example.com";
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = $"{_options.FromName} <{_options.FromAddress}>",
            ["to"] = string.Join(",", recipients),
            ["subject"] = subject,
            ["html"] = htmlBody
        });

        var response = await _httpClient.PostAsync(
            $"https://api.mailgun.net/v3/{domain}/messages",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mailgun API error: {response.StatusCode} - {error}");
        }

        _logger.LogDebug("Email sent via Mailgun to {RecipientCount} recipients", recipients.Count());
    }
}

/// <summary>
/// Mailchimp Transactional (Mandrill) API email sender.
/// </summary>
public sealed class MailchimpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MailchimpEmailSender> _logger;

    public MailchimpEmailSender(EmailOptions options, IHttpClientFactory httpClientFactory, ILogger<MailchimpEmailSender> logger)
    {
        _options = options;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri("https://mandrillapp.com/api/1.0/");
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> recipients, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            key = _options.ApiKey,
            message = new
            {
                html = htmlBody,
                subject,
                from_email = _options.FromAddress,
                from_name = _options.FromName,
                to = recipients.Select(r => new { email = r, type = "to" }).ToArray()
            }
        };

        var response = await _httpClient.PostAsJsonAsync("messages/send.json", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Mandrill API error: {response.StatusCode} - {error}");
        }

        _logger.LogDebug("Email sent via Mailchimp/Mandrill to {RecipientCount} recipients", recipients.Count());
    }
}


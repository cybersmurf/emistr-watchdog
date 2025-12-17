namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for notification services.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Email notification settings.
    /// </summary>
    public EmailOptions Email { get; set; } = new();

    /// <summary>
    /// Microsoft Teams webhook settings.
    /// </summary>
    public TeamsOptions Teams { get; set; } = new();

    /// <summary>
    /// Slack webhook settings.
    /// </summary>
    public SlackOptions Slack { get; set; } = new();

    /// <summary>
    /// Discord webhook settings.
    /// </summary>
    public DiscordOptions Discord { get; set; } = new();

    /// <summary>
    /// Generic webhook settings for custom integrations.
    /// </summary>
    public GenericWebhookOptions GenericWebhook { get; set; } = new();

    /// <summary>
    /// Critical event alerting settings.
    /// </summary>
    public CriticalEventOptions CriticalEvents { get; set; } = new();
}

/// <summary>
/// Email notification configuration.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>
    /// Whether email notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Email provider to use.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;

    /// <summary>
    /// SMTP server hostname (for SMTP provider).
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port (for SMTP provider).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API Key for cloud email providers (SendGrid, Mailchimp, etc.).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Tenant ID for Microsoft 365.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client ID for Microsoft 365.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client Secret for Microsoft 365.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Sender email address.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name.
    /// </summary>
    public string FromName { get; set; } = "Emistr Watchdog";

    /// <summary>
    /// List of recipient email addresses.
    /// </summary>
    public List<string> Recipients { get; set; } = [];

    /// <summary>
    /// Minimum time between notifications for the same service (in minutes).
    /// Prevents notification spam.
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Only send emails for critical events.
    /// </summary>
    public bool CriticalOnly { get; set; }
}

/// <summary>
/// Supported email providers.
/// </summary>
public enum EmailProvider
{
    /// <summary>
    /// Standard SMTP server.
    /// </summary>
    Smtp,

    /// <summary>
    /// Microsoft 365 / Outlook (via Graph API).
    /// </summary>
    Microsoft365,

    /// <summary>
    /// SendGrid API.
    /// </summary>
    SendGrid,

    /// <summary>
    /// Mailchimp Transactional (Mandrill).
    /// </summary>
    Mailchimp,

    /// <summary>
    /// Mailgun API.
    /// </summary>
    Mailgun
}

/// <summary>
/// Critical event alerting configuration.
/// </summary>
public sealed class CriticalEventOptions
{
    /// <summary>
    /// Play sound alert on critical events (Windows only).
    /// </summary>
    public bool EnableSound { get; set; }

    /// <summary>
    /// Show desktop notification on critical events.
    /// </summary>
    public bool EnableDesktopNotification { get; set; } = true;

    /// <summary>
    /// Log critical events to Windows Event Log.
    /// </summary>
    public bool LogToEventLog { get; set; } = true;

    /// <summary>
    /// Custom sound file path for alerts (optional).
    /// </summary>
    public string? SoundFilePath { get; set; }
}

/// <summary>
/// Microsoft Teams webhook configuration.
/// </summary>
public sealed class TeamsOptions
{
    /// <summary>
    /// Whether Teams notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Microsoft Teams Incoming Webhook URL.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Minimum time between notifications for the same service (in minutes).
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Only send notifications for critical events.
    /// </summary>
    public bool CriticalOnly { get; set; }
}

/// <summary>
/// Slack webhook configuration.
/// </summary>
public sealed class SlackOptions
{
    /// <summary>
    /// Whether Slack notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Slack Incoming Webhook URL.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Target channel (optional, uses webhook default if not specified).
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Bot username to display.
    /// </summary>
    public string Username { get; set; } = "Emistr Watchdog";

    /// <summary>
    /// Minimum time between notifications for the same service (in minutes).
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Only send notifications for critical events.
    /// </summary>
    public bool CriticalOnly { get; set; }
}

/// <summary>
/// Discord webhook configuration.
/// </summary>
public sealed class DiscordOptions
{
    /// <summary>
    /// Whether Discord notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Discord Webhook URL.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bot username to display.
    /// </summary>
    public string Username { get; set; } = "Emistr Watchdog";

    /// <summary>
    /// Minimum time between notifications for the same service (in minutes).
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Only send notifications for critical events.
    /// </summary>
    public bool CriticalOnly { get; set; }
}

/// <summary>
/// Generic webhook configuration for custom integrations.
/// </summary>
public sealed class GenericWebhookOptions
{
    /// <summary>
    /// Whether generic webhook is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Webhook URL to send notifications to.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method (POST, PUT).
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// Custom headers to include in the request.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];

    /// <summary>
    /// Minimum time between notifications for the same service (in minutes).
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;

    /// <summary>
    /// Only send notifications for critical events.
    /// </summary>
    public bool CriticalOnly { get; set; }
}


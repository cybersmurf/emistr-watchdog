namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Multi-tenant configuration for monitoring multiple environments.
/// </summary>
public sealed class MultiTenantOptions
{
    public const string SectionName = "MultiTenant";

    /// <summary>
    /// Whether multi-tenant mode is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Configuration for individual tenants/environments.
    /// </summary>
    public Dictionary<string, TenantOptions> Tenants { get; set; } = [];
}

/// <summary>
/// Configuration for a single tenant/environment.
/// </summary>
public sealed class TenantOptions
{
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public EnvironmentType Environment { get; set; } = EnvironmentType.Development;
    public TenantServicesOptions Services { get; set; } = new();
    public TenantNotificationOptions? Notifications { get; set; }
}

/// <summary>
/// Services configuration for a tenant.
/// </summary>
public sealed class TenantServicesOptions
{
    public Dictionary<string, MariaDbOptions> MariaDb { get; set; } = [];
    public Dictionary<string, HttpServiceOptions> Http { get; set; } = [];
    public Dictionary<string, TelnetServiceOptions> Telnet { get; set; } = [];
    public Dictionary<string, BackgroundServiceOptions> BackgroundServices { get; set; } = [];
    public Dictionary<string, RedisOptions> Redis { get; set; } = [];
    public Dictionary<string, RabbitMqOptions> RabbitMq { get; set; } = [];
    public Dictionary<string, ElasticsearchOptions> Elasticsearch { get; set; } = [];
}

/// <summary>
/// Notification settings for a tenant.
/// </summary>
public sealed class TenantNotificationOptions
{
    public List<string>? EmailRecipients { get; set; }
    public string? SlackChannel { get; set; }
    public string? TeamsWebhookUrl { get; set; }
    public NotificationSeverity MinimumSeverity { get; set; } = NotificationSeverity.Normal;
}

public enum EnvironmentType
{
    Development,
    Testing,
    Staging,
    Production
}

public enum NotificationSeverity
{
    Normal,
    Critical
}

namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Root configuration for the Watchdog service.
/// </summary>
public sealed class WatchdogOptions
{
    public const string SectionName = "Watchdog";

    /// <summary>
    /// Interval between health checks in seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Configuration for individual services to monitor.
    /// </summary>
    public ServicesOptions Services { get; set; } = new();
}

/// <summary>
/// Container for all service configurations.
/// </summary>
public sealed class ServicesOptions
{
    public MariaDbOptions MariaDB { get; set; } = new();
    public HttpServiceOptions LicenseManager { get; set; } = new();
    public HttpServiceOptions Apache { get; set; } = new();
    public TelnetServiceOptions PracantD { get; set; } = new();
    public BackgroundServiceOptions BackgroundService { get; set; } = new();
    public RedisOptions Redis { get; set; } = new();
    public RabbitMqOptions RabbitMQ { get; set; } = new();
    public ElasticsearchOptions Elasticsearch { get; set; } = new();

    /// <summary>
    /// Additional custom MariaDB/MySQL services to monitor.
    /// </summary>
    public Dictionary<string, MariaDbOptions> CustomMariaDbServices { get; set; } = [];

    /// <summary>
    /// Additional custom HTTP services to monitor.
    /// </summary>
    public Dictionary<string, HttpServiceOptions> CustomHttpServices { get; set; } = [];

    /// <summary>
    /// Additional custom Telnet services to monitor.
    /// </summary>
    public Dictionary<string, TelnetServiceOptions> CustomTelnetServices { get; set; } = [];

    /// <summary>
    /// Additional custom Background Service monitors.
    /// </summary>
    public Dictionary<string, BackgroundServiceOptions> CustomBackgroundServices { get; set; } = [];

    /// <summary>
    /// Additional custom Redis instances to monitor.
    /// </summary>
    public Dictionary<string, RedisOptions> CustomRedisServices { get; set; } = [];

    /// <summary>
    /// Additional custom RabbitMQ instances to monitor.
    /// </summary>
    public Dictionary<string, RabbitMqOptions> CustomRabbitMqServices { get; set; } = [];

    /// <summary>
    /// Additional custom Elasticsearch clusters to monitor.
    /// </summary>
    public Dictionary<string, ElasticsearchOptions> CustomElasticsearchServices { get; set; } = [];

    /// <summary>
    /// ICMP Ping checks for basic host availability.
    /// </summary>
    public Dictionary<string, PingOptions> PingServices { get; set; } = [];

    /// <summary>
    /// Custom script health checks (PowerShell, Bash, Python, etc.).
    /// </summary>
    public Dictionary<string, ScriptHealthCheckOptions> ScriptServices { get; set; } = [];
}

/// <summary>
/// Base configuration for any monitored service.
/// </summary>
public abstract class ServiceOptionsBase
{
    /// <summary>
    /// Whether this service check is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this service is prioritized (shown at the top of dashboard).
    /// </summary>
    public bool Prioritized { get; set; } = false;

    /// <summary>
    /// Timeout for the health check in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Number of consecutive failures before marking as critical.
    /// </summary>
    public int CriticalAfterFailures { get; set; } = 3;

    /// <summary>
    /// Display name for notifications.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Service restart configuration (Windows services only).
    /// </summary>
    public ServiceRestartConfig? RestartConfig { get; set; }
}

/// <summary>
/// MariaDB/MySQL specific configuration.
/// </summary>
public sealed class MariaDbOptions : ServiceOptionsBase
{
    /// <summary>
    /// Connection string for MariaDB.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Optional query to execute for health check. Default is simple ping.
    /// </summary>
    public string? HealthCheckQuery { get; set; }
}

/// <summary>
/// HTTP service configuration (for REST APIs, web servers).
/// </summary>
public sealed class HttpServiceOptions : ServiceOptionsBase
{
    /// <summary>
    /// URL to check for health status.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Expected HTTP status codes (default: 200).
    /// </summary>
    public int[] ExpectedStatusCodes { get; set; } = [200];

    /// <summary>
    /// Optional expected content in response body.
    /// </summary>
    public string? ExpectedContent { get; set; }

    /// <summary>
    /// Skip SSL certificate validation (for development/self-signed certificates).
    /// </summary>
    public bool IgnoreSslErrors { get; set; }
}

/// <summary>
/// Telnet-based service configuration (for PracantD and similar).
/// </summary>
public sealed class TelnetServiceOptions : ServiceOptionsBase
{
    /// <summary>
    /// Host address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Command to send after connection (optional).
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Expected response substring to validate service is healthy.
    /// </summary>
    public string? ExpectedResponse { get; set; }

    /// <summary>
    /// If true, only validates that connection can be established.
    /// </summary>
    public bool ConnectionOnly { get; set; }

    /// <summary>
    /// Send raw bytes instead of text command.
    /// </summary>
    public string? RawCommand { get; set; }

    /// <summary>
    /// Read timeout in milliseconds. Default is 2000ms.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// If true, appends newline to raw commands.
    /// </summary>
    public bool AppendNewlineToRawCommand { get; set; } = true;
}

/// <summary>
/// Background service configuration - monitors last run time in database.
/// </summary>
public sealed class BackgroundServiceOptions : ServiceOptionsBase
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "sud_utf8_aaa";
    public string TableName { get; set; } = "system";
    public string ColumnName { get; set; } = "bgs_last_run";
    public int MaxAgeMinutes { get; set; } = 5;
    public int SystemRowId { get; set; } = 1;
}

/// <summary>
/// Redis service configuration.
/// </summary>
public sealed class RedisOptions : ServiceOptionsBase
{
    /// <summary>
    /// Redis connection string (e.g., "localhost:6379" or "localhost:6379,password=secret").
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Database index to select (0-15).
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Optional key to check for existence as additional health validation.
    /// </summary>
    public string? TestKey { get; set; }
}

/// <summary>
/// RabbitMQ service configuration.
/// </summary>
public sealed class RabbitMqOptions : ServiceOptionsBase
{
    /// <summary>
    /// RabbitMQ host address.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port (default: 5672).
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ username.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Use SSL/TLS connection.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Optional queue name to check for existence.
    /// </summary>
    public string? TestQueue { get; set; }
}

/// <summary>
/// Elasticsearch service configuration.
/// </summary>
public sealed class ElasticsearchOptions : ServiceOptionsBase
{
    /// <summary>
    /// Elasticsearch URL (e.g., "http://localhost:9200").
    /// </summary>
    public string Url { get; set; } = "http://localhost:9200";

    /// <summary>
    /// Username for authentication (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for authentication (optional).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API key for authentication (optional, alternative to username/password).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Skip SSL certificate validation.
    /// </summary>
    public bool IgnoreSslErrors { get; set; } = false;

    /// <summary>
    /// Optional index name to check for existence.
    /// </summary>
    public string? TestIndex { get; set; }

    /// <summary>
    /// Minimum cluster health status: "green", "yellow", or "red".
    /// </summary>
    public string? MinimumHealthStatus { get; set; } = "yellow";
}

/// <summary>
/// ICMP Ping service configuration for basic host availability checks.
/// </summary>
public sealed class PingOptions : ServiceOptionsBase
{
    /// <summary>
    /// Host IP address or hostname to ping.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Number of ping requests to send per check.
    /// </summary>
    public int PingCount { get; set; } = 3;

    /// <summary>
    /// Packet loss threshold percentage for degraded status.
    /// </summary>
    public double PacketLossThresholdPercent { get; set; } = 20;

    /// <summary>
    /// Round-trip time threshold in milliseconds for degraded status.
    /// </summary>
    public int HighLatencyThresholdMs { get; set; } = 200;
}



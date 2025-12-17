using System.Text.Json.Serialization;

namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Root configuration for the Watchdog service.
/// V2 - simplified with single services array.
/// </summary>
public sealed class WatchdogOptionsV2
{
    public const string SectionName = "Watchdog";

    /// <summary>
    /// Interval between health checks in seconds.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// List of all services to monitor.
    /// </summary>
    public List<ServiceConfig> Services { get; set; } = [];
}

/// <summary>
/// Universal service configuration - one class for all service types.
/// </summary>
public sealed class ServiceConfig
{
    /// <summary>
    /// Unique name/identifier for the service.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in dashboard and notifications.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Type of service: Http, MariaDb, Tcp, Ping, Process, Script, BackgroundService
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Http";

    /// <summary>
    /// Whether this service check is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this service is prioritized (shown at top of dashboard).
    /// </summary>
    [JsonPropertyName("prioritized")]
    public bool Prioritized { get; set; } = false;

    /// <summary>
    /// Timeout for the health check in seconds.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Number of consecutive failures before marking as critical.
    /// </summary>
    [JsonPropertyName("criticalAfterFailures")]
    public int CriticalAfterFailures { get; set; } = 3;

    /// <summary>
    /// Tags for grouping/filtering.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    // ===== HTTP Options =====
    
    /// <summary>
    /// URL to check (for Http type).
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Expected HTTP status codes.
    /// </summary>
    [JsonPropertyName("expectedStatusCodes")]
    public int[] ExpectedStatusCodes { get; set; } = [200];

    /// <summary>
    /// Expected content in response body.
    /// </summary>
    [JsonPropertyName("expectedContent")]
    public string? ExpectedContent { get; set; }

    /// <summary>
    /// Whether to validate SSL certificate.
    /// </summary>
    [JsonPropertyName("validateSsl")]
    public bool ValidateSsl { get; set; } = true;

    /// <summary>
    /// Custom headers to send with request.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    // ===== TCP/Telnet Options =====
    
    /// <summary>
    /// Host to connect to (for Tcp, Ping, MariaDb types).
    /// </summary>
    [JsonPropertyName("host")]
    public string? Host { get; set; }

    /// <summary>
    /// Port to connect to (for Tcp type).
    /// </summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    // ===== MariaDB Options =====
    
    /// <summary>
    /// Connection string (for MariaDb type).
    /// </summary>
    [JsonPropertyName("connectionString")]
    public string? ConnectionString { get; set; }

    // ===== Script Options =====
    
    /// <summary>
    /// Path to script file (for Script type).
    /// </summary>
    [JsonPropertyName("scriptPath")]
    public string? ScriptPath { get; set; }

    /// <summary>
    /// Arguments to pass to script.
    /// </summary>
    [JsonPropertyName("scriptArguments")]
    public string? ScriptArguments { get; set; }

    /// <summary>
    /// Shell to use: bash, powershell, python, cmd
    /// </summary>
    [JsonPropertyName("shell")]
    public string? Shell { get; set; }

    // ===== Ping Options =====
    
    /// <summary>
    /// Number of ping packets to send.
    /// </summary>
    [JsonPropertyName("pingCount")]
    public int PingCount { get; set; } = 3;

    /// <summary>
    /// Maximum packet loss percentage allowed.
    /// </summary>
    [JsonPropertyName("maxPacketLossPercent")]
    public int MaxPacketLossPercent { get; set; } = 20;

    // ===== BackgroundService Options =====
    
    /// <summary>
    /// Database name for background service check.
    /// </summary>
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Table name containing last run timestamp.
    /// </summary>
    [JsonPropertyName("tableName")]
    public string? TableName { get; set; }

    /// <summary>
    /// Column name with last run timestamp.
    /// </summary>
    [JsonPropertyName("columnName")]
    public string? ColumnName { get; set; }

    /// <summary>
    /// Maximum age in minutes before considering stale.
    /// </summary>
    [JsonPropertyName("maxAgeMinutes")]
    public int MaxAgeMinutes { get; set; } = 5;

    // ===== Process Options =====
    
    /// <summary>
    /// Process name to check (for Process type).
    /// </summary>
    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }

    /// <summary>
    /// Minimum number of instances required.
    /// </summary>
    [JsonPropertyName("minInstances")]
    public int MinInstances { get; set; } = 1;

    /// <summary>
    /// Maximum number of instances allowed (0 = unlimited).
    /// </summary>
    [JsonPropertyName("maxInstances")]
    public int MaxInstances { get; set; } = 0;

    // ===== Restart Config =====
    
    /// <summary>
    /// Service restart configuration.
    /// </summary>
    [JsonPropertyName("restartConfig")]
    public ServiceRestartConfig? RestartConfig { get; set; }

    /// <summary>
    /// Get effective display name.
    /// </summary>
    public string GetDisplayName() => DisplayName ?? Name;
}


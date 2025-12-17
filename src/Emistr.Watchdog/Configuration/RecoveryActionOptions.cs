namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for automatic recovery actions.
/// </summary>
public sealed class RecoveryActionOptions
{
    /// <summary>
    /// Whether automatic recovery is enabled globally.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Default recovery actions for services without specific configuration.
    /// </summary>
    public RecoveryAction DefaultAction { get; set; } = new();

    /// <summary>
    /// Service-specific recovery configurations.
    /// </summary>
    public Dictionary<string, ServiceRecoveryConfig> Services { get; set; } = new();

    /// <summary>
    /// Global cooldown between recovery attempts in seconds.
    /// </summary>
    public int GlobalCooldownSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Maximum recovery attempts per hour globally.
    /// </summary>
    public int MaxAttemptsPerHour { get; set; } = 10;
}

/// <summary>
/// Recovery configuration for a specific service.
/// </summary>
public sealed class ServiceRecoveryConfig
{
    /// <summary>
    /// Whether recovery is enabled for this service.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before attempting recovery.
    /// </summary>
    public int FailuresBeforeRecovery { get; set; } = 3;

    /// <summary>
    /// Recovery actions to execute in order.
    /// </summary>
    public List<RecoveryAction> Actions { get; set; } = new();

    /// <summary>
    /// Cooldown between recovery attempts for this service in seconds.
    /// </summary>
    public int CooldownSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum recovery attempts before giving up.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Whether to notify on recovery attempt.
    /// </summary>
    public bool NotifyOnAttempt { get; set; } = true;

    /// <summary>
    /// Whether to notify on successful recovery.
    /// </summary>
    public bool NotifyOnSuccess { get; set; } = true;

    /// <summary>
    /// Whether to notify when max attempts reached.
    /// </summary>
    public bool NotifyOnMaxAttemptsReached { get; set; } = true;
}

/// <summary>
/// A single recovery action.
/// </summary>
public sealed class RecoveryAction
{
    /// <summary>
    /// Type of recovery action.
    /// </summary>
    public RecoveryActionType Type { get; set; } = RecoveryActionType.None;

    /// <summary>
    /// Name/description of the action.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// For RestartService: Windows service name to restart.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// For ExecuteScript: Path to script file.
    /// </summary>
    public string? ScriptPath { get; set; }

    /// <summary>
    /// For ExecuteScript: Arguments for the script.
    /// </summary>
    public string? ScriptArguments { get; set; }

    /// <summary>
    /// For ExecuteCommand: Command to execute.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// For ExecuteCommand: Arguments for the command.
    /// </summary>
    public string? CommandArguments { get; set; }

    /// <summary>
    /// For HttpRequest: URL to call.
    /// </summary>
    public string? HttpUrl { get; set; }

    /// <summary>
    /// For HttpRequest: HTTP method (GET, POST, etc.).
    /// </summary>
    public string HttpMethod { get; set; } = "POST";

    /// <summary>
    /// For HttpRequest: Request body.
    /// </summary>
    public string? HttpBody { get; set; }

    /// <summary>
    /// For HttpRequest: Headers to include.
    /// </summary>
    public Dictionary<string, string> HttpHeaders { get; set; } = new();

    /// <summary>
    /// Timeout for the action in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Delay before executing this action in seconds.
    /// </summary>
    public int DelayBeforeSeconds { get; set; } = 0;

    /// <summary>
    /// Whether to continue to next action if this one fails.
    /// </summary>
    public bool ContinueOnFailure { get; set; } = false;
}

/// <summary>
/// Type of recovery action.
/// </summary>
public enum RecoveryActionType
{
    /// <summary>
    /// No action.
    /// </summary>
    None,

    /// <summary>
    /// Restart a Windows/Linux service.
    /// </summary>
    RestartService,

    /// <summary>
    /// Execute a script (PowerShell, Bash, etc.).
    /// </summary>
    ExecuteScript,

    /// <summary>
    /// Execute a command directly.
    /// </summary>
    ExecuteCommand,

    /// <summary>
    /// Make an HTTP request (e.g., restart via API).
    /// </summary>
    HttpRequest,

    /// <summary>
    /// Send a notification only (no actual recovery).
    /// </summary>
    NotifyOnly
}


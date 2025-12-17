namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for custom script health checker.
/// </summary>
public sealed class ScriptHealthCheckOptions : ServiceOptionsBase
{
    /// <summary>
    /// Path to the script file (PowerShell, Bash, or executable).
    /// </summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>
    /// Arguments to pass to the script.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Working directory for script execution.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Script type - auto-detected from extension if not specified.
    /// </summary>
    public ScriptType Type { get; set; } = ScriptType.Auto;

    /// <summary>
    /// Expected exit code for healthy status. Default is 0.
    /// </summary>
    public int ExpectedExitCode { get; set; } = 0;

    /// <summary>
    /// Additional exit codes that indicate healthy status.
    /// </summary>
    public int[] AdditionalHealthyExitCodes { get; set; } = [];

    /// <summary>
    /// Exit codes that indicate warning/degraded status (not critical).
    /// </summary>
    public int[] WarningExitCodes { get; set; } = [];

    /// <summary>
    /// Whether to capture and include stdout in the result message.
    /// </summary>
    public bool CaptureOutput { get; set; } = true;

    /// <summary>
    /// Whether to capture and include stderr in error messages.
    /// </summary>
    public bool CaptureError { get; set; } = true;

    /// <summary>
    /// Maximum output length to capture (characters).
    /// </summary>
    public int MaxOutputLength { get; set; } = 1000;

    /// <summary>
    /// Environment variables to set for the script.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Whether to run the script with elevated privileges (Windows: RunAs, Linux: sudo).
    /// </summary>
    public bool RunElevated { get; set; } = false;

    /// <summary>
    /// Shell to use for script execution on Linux/macOS.
    /// </summary>
    public string Shell { get; set; } = "/bin/bash";
}

/// <summary>
/// Type of script to execute.
/// </summary>
public enum ScriptType
{
    /// <summary>
    /// Auto-detect from file extension.
    /// </summary>
    Auto,

    /// <summary>
    /// PowerShell script (.ps1).
    /// </summary>
    PowerShell,

    /// <summary>
    /// Bash script (.sh).
    /// </summary>
    Bash,

    /// <summary>
    /// Windows batch file (.bat, .cmd).
    /// </summary>
    Batch,

    /// <summary>
    /// Python script (.py).
    /// </summary>
    Python,

    /// <summary>
    /// Direct executable.
    /// </summary>
    Executable
}


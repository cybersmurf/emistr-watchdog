using System.Diagnostics;
using System.Text.RegularExpressions;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Security settings for script execution.
/// </summary>
public class ScriptSecurityOptions
{
    /// <summary>
    /// Whether script security is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// List of allowed base directories for scripts.
    /// Scripts must be located in one of these directories or their subdirectories.
    /// Empty list means allow all (not recommended for production).
    /// </summary>
    public List<string> AllowedDirectories { get; set; } = new()
    {
        "scripts",
        "healthchecks"
    };

    /// <summary>
    /// List of allowed script extensions.
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new()
    {
        ".ps1", ".sh", ".bat", ".cmd", ".py"
    };

    /// <summary>
    /// Maximum script execution time in seconds.
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Disallowed patterns in script arguments (security).
    /// </summary>
    public List<string> DisallowedArgumentPatterns { get; set; } = new()
    {
        @"[;&|`$]",           // Shell command chaining/substitution
        @"\.\.[/\\]",         // Path traversal
        @"rm\s+-rf",          // Dangerous commands
        @"del\s+/",           // Windows delete
        @"format\s+",         // Format command
        @">(>)?",             // Output redirection
        @"<"                  // Input redirection
    };

    /// <summary>
    /// Whether to allow environment variable expansion in arguments.
    /// </summary>
    public bool AllowEnvironmentExpansion { get; set; } = false;
}

/// <summary>
/// Health checker that executes custom scripts (PowerShell, Bash, Python, etc.).
/// Includes security features to prevent malicious script execution.
/// </summary>
public class ScriptHealthChecker : IHealthChecker
{
    private readonly ScriptHealthCheckOptions _options;
    private readonly ScriptSecurityOptions _securityOptions;
    private readonly ILogger<ScriptHealthChecker> _logger;
    private readonly string _serviceName;
    private readonly string _applicationBasePath;

    public ScriptHealthChecker(
        string serviceName,
        ScriptHealthCheckOptions options,
        ILogger<ScriptHealthChecker> logger,
        ScriptSecurityOptions? securityOptions = null)
    {
        _serviceName = serviceName;
        _options = options;
        _logger = logger;
        _securityOptions = securityOptions ?? new ScriptSecurityOptions();
        _applicationBasePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    public string ServiceName => _serviceName;
    public string DisplayName => _options.DisplayName ?? _serviceName;
    public bool IsEnabled => RuntimeConfigurationService.Instance.GetEffectiveEnabled(_serviceName, _options.Enabled);
    public int CriticalThreshold => _options.CriticalAfterFailures;
    public bool IsPrioritized => RuntimeConfigurationService.Instance.GetEffectivePrioritized(_serviceName, _options.Prioritized);
    public ServiceRestartConfig? RestartConfig => _options.RestartConfig;

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Security validation
            var securityResult = ValidateSecurity();
            if (securityResult != null)
            {
                _logger.LogWarning("Script security validation failed for {ServiceName}: {Reason}", 
                    _serviceName, securityResult);
                return CreateResult(
                    ServiceStatus.Unhealthy,
                    $"Security validation failed: {securityResult}",
                    stopwatch.Elapsed);
            }

            // Validate script exists
            if (!File.Exists(_options.ScriptPath))
            {
                return CreateResult(
                    ServiceStatus.Unhealthy,
                    $"Script not found: {_options.ScriptPath}",
                    stopwatch.Elapsed);
            }

            var scriptType = DetermineScriptType();
            var (fileName, arguments) = BuildProcessInfo(scriptType);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = _options.WorkingDirectory ?? Path.GetDirectoryName(_options.ScriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = _options.CaptureOutput,
                RedirectStandardError = _options.CaptureError,
                CreateNoWindow = true
            };

            // Set environment variables
            foreach (var (key, value) in _options.EnvironmentVariables)
            {
                process.StartInfo.EnvironmentVariables[key] = value;
            }

            // Capture output
            var stdout = new List<string>();
            var stderr = new List<string>();

            if (_options.CaptureOutput)
            {
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) stdout.Add(e.Data);
                };
            }

            if (_options.CaptureError)
            {
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) stderr.Add(e.Data);
                };
            }

            _logger.LogDebug("Executing script: {FileName} {Arguments}", fileName, arguments);

            process.Start();

            if (_options.CaptureOutput) process.BeginOutputReadLine();
            if (_options.CaptureError) process.BeginErrorReadLine();

            // Wait with timeout
            var timeoutMs = _options.TimeoutSeconds * 1000;
            var completed = await WaitForExitAsync(process, timeoutMs, cancellationToken);

            stopwatch.Stop();

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill errors
                }

                return CreateResult(
                    ServiceStatus.Unhealthy,
                    $"Script timed out after {_options.TimeoutSeconds}s",
                    stopwatch.Elapsed);
            }

            var exitCode = process.ExitCode;
            var output = TruncateOutput(string.Join(Environment.NewLine, stdout));
            var error = TruncateOutput(string.Join(Environment.NewLine, stderr));

            _logger.LogDebug(
                "Script completed with exit code {ExitCode}. Output: {Output}",
                exitCode,
                output.Length > 100 ? output[..100] + "..." : output);

            // Determine status based on exit code
            var status = DetermineStatus(exitCode);
            var message = BuildResultMessage(exitCode, output, error, status);

            return CreateResult(status, message, stopwatch.Elapsed, new Dictionary<string, object>
            {
                ["exitCode"] = exitCode,
                ["scriptPath"] = _options.ScriptPath,
                ["output"] = output,
                ["error"] = error
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Script execution failed for {ServiceName}", _serviceName);

            return CreateResult(
                ServiceStatus.Unhealthy,
                $"Script execution failed: {ex.Message}",
                stopwatch.Elapsed);
        }
    }

    private ScriptType DetermineScriptType()
    {
        if (_options.Type != ScriptType.Auto)
            return _options.Type;

        var extension = Path.GetExtension(_options.ScriptPath).ToLowerInvariant();
        return extension switch
        {
            ".ps1" => ScriptType.PowerShell,
            ".sh" => ScriptType.Bash,
            ".bat" or ".cmd" => ScriptType.Batch,
            ".py" => ScriptType.Python,
            ".exe" => ScriptType.Executable,
            _ => ScriptType.Executable
        };
    }

    private (string fileName, string arguments) BuildProcessInfo(ScriptType scriptType)
    {
        var scriptPath = Path.GetFullPath(_options.ScriptPath);
        var scriptArgs = _options.Arguments;

        return scriptType switch
        {
            ScriptType.PowerShell => OperatingSystem.IsWindows()
                ? ("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\" {scriptArgs}")
                : ("pwsh", $"-NoProfile -NonInteractive -File \"{scriptPath}\" {scriptArgs}"),

            ScriptType.Bash => (_options.Shell, $"\"{scriptPath}\" {scriptArgs}"),

            ScriptType.Batch => ("cmd.exe", $"/c \"{scriptPath}\" {scriptArgs}"),

            ScriptType.Python => OperatingSystem.IsWindows()
                ? ("python.exe", $"\"{scriptPath}\" {scriptArgs}")
                : ("python3", $"\"{scriptPath}\" {scriptArgs}"),

            ScriptType.Executable or _ => (scriptPath, scriptArgs)
        };
    }

    private ServiceStatus DetermineStatus(int exitCode)
    {
        // Check healthy exit codes
        if (exitCode == _options.ExpectedExitCode ||
            _options.AdditionalHealthyExitCodes.Contains(exitCode))
        {
            return ServiceStatus.Healthy;
        }

        // Check warning exit codes
        if (_options.WarningExitCodes.Contains(exitCode))
        {
            return ServiceStatus.Degraded;
        }

        return ServiceStatus.Unhealthy;
    }

    private string BuildResultMessage(int exitCode, string output, string error, ServiceStatus status)
    {
        var message = status switch
        {
            ServiceStatus.Healthy => "Script completed successfully",
            ServiceStatus.Degraded => $"Script returned warning (exit code: {exitCode})",
            _ => $"Script failed (exit code: {exitCode})"
        };

        if (!string.IsNullOrWhiteSpace(output) && _options.CaptureOutput)
        {
            message += $". Output: {output}";
        }

        if (!string.IsNullOrWhiteSpace(error) && _options.CaptureError && status != ServiceStatus.Healthy)
        {
            message += $". Error: {error}";
        }

        return message;
    }

    private string TruncateOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        var trimmed = output.Trim();
        if (trimmed.Length <= _options.MaxOutputLength)
            return trimmed;

        return trimmed[.._options.MaxOutputLength] + "...";
    }

    /// <summary>
    /// Validates script security settings.
    /// </summary>
    /// <returns>Error message if validation fails, null if OK.</returns>
    private string? ValidateSecurity()
    {
        if (!_securityOptions.Enabled)
            return null;

        var scriptPath = Path.GetFullPath(_options.ScriptPath);

        // 1. Validate allowed directories
        if (_securityOptions.AllowedDirectories.Count > 0)
        {
            var isInAllowedDirectory = false;
            foreach (var allowedDir in _securityOptions.AllowedDirectories)
            {
                var fullAllowedDir = Path.IsPathRooted(allowedDir) 
                    ? allowedDir 
                    : Path.GetFullPath(Path.Combine(_applicationBasePath, allowedDir));
                
                if (scriptPath.StartsWith(fullAllowedDir, StringComparison.OrdinalIgnoreCase))
                {
                    isInAllowedDirectory = true;
                    break;
                }
            }

            if (!isInAllowedDirectory)
            {
                return $"Script path not in allowed directories. Allowed: {string.Join(", ", _securityOptions.AllowedDirectories)}";
            }
        }

        // 2. Validate extension
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        if (_securityOptions.AllowedExtensions.Count > 0 && 
            !_securityOptions.AllowedExtensions.Contains(extension))
        {
            return $"Script extension '{extension}' not allowed. Allowed: {string.Join(", ", _securityOptions.AllowedExtensions)}";
        }

        // 3. Validate timeout
        if (_options.TimeoutSeconds > _securityOptions.MaxTimeoutSeconds)
        {
            return $"Timeout {_options.TimeoutSeconds}s exceeds maximum allowed {_securityOptions.MaxTimeoutSeconds}s";
        }

        // 4. Validate arguments for dangerous patterns
        if (!string.IsNullOrEmpty(_options.Arguments))
        {
            foreach (var pattern in _securityOptions.DisallowedArgumentPatterns)
            {
                if (Regex.IsMatch(_options.Arguments, pattern, RegexOptions.IgnoreCase))
                {
                    return $"Arguments contain disallowed pattern: {pattern}";
                }
            }

            // Check for environment variable expansion
            if (!_securityOptions.AllowEnvironmentExpansion)
            {
                if (_options.Arguments.Contains("$") || _options.Arguments.Contains("%"))
                {
                    return "Environment variable expansion in arguments is not allowed";
                }
            }
        }

        // 5. Validate path traversal in script path
        if (scriptPath.Contains(".."))
        {
            return "Path traversal (..) not allowed in script path";
        }

        return null;
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs, CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private HealthCheckResult CreateResult(
        ServiceStatus status,
        string message,
        TimeSpan responseTime,
        Dictionary<string, object>? additionalData = null)
    {
        return new HealthCheckResult
        {
            ServiceName = _serviceName,
            Status = status,
            IsHealthy = status == ServiceStatus.Healthy,
            ErrorMessage = status != ServiceStatus.Healthy ? message : null,
            ResponseTimeMs = (long)responseTime.TotalMilliseconds,
            CheckedAt = DateTime.UtcNow,
            Details = additionalData ?? new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Factory for creating ScriptHealthChecker instances.
/// </summary>
public class ScriptHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ScriptSecurityOptions _securityOptions;

    public ScriptHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        ScriptSecurityOptions? securityOptions = null)
    {
        _loggerFactory = loggerFactory;
        _securityOptions = securityOptions ?? new ScriptSecurityOptions();
    }

    public ScriptHealthChecker Create(string serviceName, ScriptHealthCheckOptions options)
    {
        var logger = _loggerFactory.CreateLogger<ScriptHealthChecker>();
        return new ScriptHealthChecker(serviceName, options, logger, _securityOptions);
    }
}


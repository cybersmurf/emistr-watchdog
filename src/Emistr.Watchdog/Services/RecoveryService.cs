using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using Emistr.Watchdog.Configuration;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Executes automatic recovery actions for failed services.
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Attempts recovery for a failed service.
    /// </summary>
    Task<RecoveryResult> AttemptRecoveryAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Gets recovery history for a service.
    /// </summary>
    IReadOnlyList<RecoveryAttempt> GetHistory(string serviceName);

    /// <summary>
    /// Gets all recovery attempts.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<RecoveryAttempt>> GetAllHistory();

    /// <summary>
    /// Checks if recovery is allowed (cooldown, max attempts).
    /// </summary>
    bool CanAttemptRecovery(string serviceName);
}

/// <summary>
/// Result of a recovery attempt.
/// </summary>
public record RecoveryResult
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public RecoveryActionType ActionType { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? ErrorDetails { get; init; }
    public int AttemptNumber { get; init; }
}

/// <summary>
/// A single recovery attempt record.
/// </summary>
public record RecoveryAttempt
{
    public DateTime Timestamp { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public RecoveryActionType ActionType { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Implementation of recovery service.
/// </summary>
public class RecoveryService : IRecoveryService
{
    private readonly RecoveryActionOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceController _serviceController;
    private readonly ILogger<RecoveryService> _logger;
    private readonly ConcurrentDictionary<string, List<RecoveryAttempt>> _history = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAttempt = new();
    private readonly ConcurrentDictionary<string, int> _attemptCount = new();

    public RecoveryService(
        IOptions<RecoveryActionOptions> options,
        IHttpClientFactory httpClientFactory,
        IServiceController serviceController,
        ILogger<RecoveryService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _serviceController = serviceController;
        _logger = logger;
    }

    public async Task<RecoveryResult> AttemptRecoveryAsync(string serviceName, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "Recovery is disabled globally"
            };
        }

        if (!CanAttemptRecovery(serviceName))
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "Recovery not allowed (cooldown or max attempts reached)"
            };
        }

        var config = GetServiceConfig(serviceName);
        if (!config.Enabled || config.Actions.Count == 0)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "No recovery actions configured for this service"
            };
        }

        var attemptNumber = _attemptCount.AddOrUpdate(serviceName, 1, (_, c) => c + 1);
        _lastAttempt[serviceName] = DateTime.UtcNow;

        _logger.LogWarning(
            "Starting recovery attempt {Attempt}/{Max} for {ServiceName}",
            attemptNumber, config.MaxAttempts, serviceName);

        RecoveryResult? lastResult = null;

        foreach (var action in config.Actions)
        {
            if (action.Type == RecoveryActionType.None)
                continue;

            try
            {
                if (action.DelayBeforeSeconds > 0)
                {
                    _logger.LogDebug("Waiting {Delay}s before action {Action}",
                        action.DelayBeforeSeconds, action.Name);
                    await Task.Delay(TimeSpan.FromSeconds(action.DelayBeforeSeconds), ct);
                }

                var stopwatch = Stopwatch.StartNew();
                var result = await ExecuteActionAsync(serviceName, action, attemptNumber, ct);
                stopwatch.Stop();

                result = result with { Duration = stopwatch.Elapsed, AttemptNumber = attemptNumber };
                lastResult = result;

                RecordAttempt(serviceName, action, result.Success, result.Message, stopwatch.Elapsed);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Recovery successful for {ServiceName} using {ActionType}: {Message}",
                        serviceName, action.Type, result.Message);
                    
                    // Reset attempt counter on success
                    _attemptCount[serviceName] = 0;
                    return result;
                }

                _logger.LogWarning(
                    "Recovery action {ActionType} failed for {ServiceName}: {Message}",
                    action.Type, serviceName, result.Message);

                if (!action.ContinueOnFailure)
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery action {ActionType} threw exception for {ServiceName}",
                    action.Type, serviceName);

                RecordAttempt(serviceName, action, false, ex.Message, TimeSpan.Zero);

                if (!action.ContinueOnFailure)
                {
                    return new RecoveryResult
                    {
                        ServiceName = serviceName,
                        Success = false,
                        Message = $"Recovery failed with exception: {ex.Message}",
                        ActionType = action.Type,
                        ActionName = action.Name,
                        ErrorDetails = ex.ToString(),
                        AttemptNumber = attemptNumber
                    };
                }
            }
        }

        return lastResult ?? new RecoveryResult
        {
            ServiceName = serviceName,
            Success = false,
            Message = "No recovery actions executed",
            AttemptNumber = attemptNumber
        };
    }

    public bool CanAttemptRecovery(string serviceName)
    {
        var config = GetServiceConfig(serviceName);
        
        // Check service-level max attempts
        if (_attemptCount.TryGetValue(serviceName, out var count) && count >= config.MaxAttempts)
        {
            return false;
        }

        // Check cooldown
        if (_lastAttempt.TryGetValue(serviceName, out var lastAttempt))
        {
            var cooldown = TimeSpan.FromSeconds(Math.Max(config.CooldownSeconds, _options.GlobalCooldownSeconds));
            if (DateTime.UtcNow - lastAttempt < cooldown)
            {
                return false;
            }
        }

        // Check global hourly limit
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var attemptsLastHour = _history.Values
            .SelectMany(h => h)
            .Count(a => a.Timestamp > hourAgo);

        if (attemptsLastHour >= _options.MaxAttemptsPerHour)
        {
            return false;
        }

        return true;
    }

    public IReadOnlyList<RecoveryAttempt> GetHistory(string serviceName)
    {
        return _history.TryGetValue(serviceName, out var history)
            ? history.AsReadOnly()
            : Array.Empty<RecoveryAttempt>();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<RecoveryAttempt>> GetAllHistory()
    {
        return _history.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<RecoveryAttempt>)kvp.Value.AsReadOnly());
    }

    private ServiceRecoveryConfig GetServiceConfig(string serviceName)
    {
        if (_options.Services.TryGetValue(serviceName, out var config))
            return config;

        // Return default config with default action
        return new ServiceRecoveryConfig
        {
            Enabled = _options.DefaultAction.Type != RecoveryActionType.None,
            Actions = _options.DefaultAction.Type != RecoveryActionType.None
                ? new List<RecoveryAction> { _options.DefaultAction }
                : new List<RecoveryAction>()
        };
    }

    private async Task<RecoveryResult> ExecuteActionAsync(
        string serviceName,
        RecoveryAction action,
        int attemptNumber,
        CancellationToken ct)
    {
        return action.Type switch
        {
            RecoveryActionType.RestartService => await RestartServiceAsync(serviceName, action, ct),
            RecoveryActionType.ExecuteScript => await ExecuteScriptAsync(serviceName, action, ct),
            RecoveryActionType.ExecuteCommand => await ExecuteCommandAsync(serviceName, action, ct),
            RecoveryActionType.HttpRequest => await ExecuteHttpRequestAsync(serviceName, action, ct),
            RecoveryActionType.NotifyOnly => new RecoveryResult
            {
                ServiceName = serviceName,
                Success = true,
                Message = "Notification sent (no actual recovery)",
                ActionType = action.Type,
                ActionName = action.Name
            },
            _ => new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"Unknown action type: {action.Type}",
                ActionType = action.Type,
                ActionName = action.Name
            }
        };
    }

    private async Task<RecoveryResult> RestartServiceAsync(
        string serviceName,
        RecoveryAction action,
        CancellationToken ct)
    {
        var targetService = action.ServiceName ?? serviceName;

        try
        {
            var success = await _serviceController.RestartServiceAsync(targetService, ct);
            
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = success,
                Message = success
                    ? $"Service '{targetService}' restarted successfully"
                    : $"Failed to restart service '{targetService}'",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }
        catch (Exception ex)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"Failed to restart service '{targetService}': {ex.Message}",
                ActionType = action.Type,
                ActionName = action.Name,
                ErrorDetails = ex.ToString()
            };
        }
    }

    private async Task<RecoveryResult> ExecuteScriptAsync(
        string serviceName,
        RecoveryAction action,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.ScriptPath))
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "Script path not configured",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }

        try
        {
            var (fileName, arguments) = GetScriptCommand(action.ScriptPath, action.ScriptArguments);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(action.TimeoutSeconds));

            await process.WaitForExitAsync(cts.Token);

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            var success = process.ExitCode == 0;

            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = success,
                Message = success
                    ? $"Script executed successfully: {output.Trim()}"
                    : $"Script failed (exit code {process.ExitCode}): {error.Trim()}",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }
        catch (OperationCanceledException)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"Script timed out after {action.TimeoutSeconds}s",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }
        catch (Exception ex)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"Script execution failed: {ex.Message}",
                ActionType = action.Type,
                ActionName = action.Name,
                ErrorDetails = ex.ToString()
            };
        }
    }

    private async Task<RecoveryResult> ExecuteCommandAsync(
        string serviceName,
        RecoveryAction action,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.Command))
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "Command not configured",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = action.Command,
                Arguments = action.CommandArguments ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(action.TimeoutSeconds));

            await process.WaitForExitAsync(cts.Token);

            var success = process.ExitCode == 0;
            var output = await process.StandardOutput.ReadToEndAsync(ct);

            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = success,
                Message = success
                    ? $"Command executed successfully"
                    : $"Command failed (exit code {process.ExitCode})",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }
        catch (Exception ex)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"Command execution failed: {ex.Message}",
                ActionType = action.Type,
                ActionName = action.Name,
                ErrorDetails = ex.ToString()
            };
        }
    }

    private async Task<RecoveryResult> ExecuteHttpRequestAsync(
        string serviceName,
        RecoveryAction action,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action.HttpUrl))
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = "HTTP URL not configured",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(action.TimeoutSeconds);

            var request = new HttpRequestMessage(
                new HttpMethod(action.HttpMethod),
                action.HttpUrl);

            foreach (var (key, value) in action.HttpHeaders)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }

            if (!string.IsNullOrEmpty(action.HttpBody))
            {
                request.Content = new StringContent(action.HttpBody, System.Text.Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, ct);
            var success = response.IsSuccessStatusCode;

            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = success,
                Message = success
                    ? $"HTTP request successful ({response.StatusCode})"
                    : $"HTTP request failed ({response.StatusCode})",
                ActionType = action.Type,
                ActionName = action.Name
            };
        }
        catch (Exception ex)
        {
            return new RecoveryResult
            {
                ServiceName = serviceName,
                Success = false,
                Message = $"HTTP request failed: {ex.Message}",
                ActionType = action.Type,
                ActionName = action.Name,
                ErrorDetails = ex.ToString()
            };
        }
    }

    private static (string fileName, string arguments) GetScriptCommand(string scriptPath, string? args)
    {
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        var fullPath = Path.GetFullPath(scriptPath);
        var scriptArgs = args ?? string.Empty;

        return extension switch
        {
            ".ps1" => OperatingSystem.IsWindows()
                ? ("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{fullPath}\" {scriptArgs}")
                : ("pwsh", $"-NoProfile -File \"{fullPath}\" {scriptArgs}"),
            ".sh" => ("/bin/bash", $"\"{fullPath}\" {scriptArgs}"),
            ".bat" or ".cmd" => ("cmd.exe", $"/c \"{fullPath}\" {scriptArgs}"),
            ".py" => OperatingSystem.IsWindows()
                ? ("python.exe", $"\"{fullPath}\" {scriptArgs}")
                : ("python3", $"\"{fullPath}\" {scriptArgs}"),
            _ => (fullPath, scriptArgs)
        };
    }

    private void RecordAttempt(
        string serviceName,
        RecoveryAction action,
        bool success,
        string message,
        TimeSpan duration)
    {
        var attempt = new RecoveryAttempt
        {
            Timestamp = DateTime.UtcNow,
            ServiceName = serviceName,
            ActionType = action.Type,
            ActionName = action.Name,
            Success = success,
            Message = message,
            Duration = duration
        };

        var history = _history.GetOrAdd(serviceName, _ => new List<RecoveryAttempt>());
        lock (history)
        {
            history.Add(attempt);
            
            // Keep only last 100 attempts per service
            if (history.Count > 100)
            {
                history.RemoveRange(0, history.Count - 100);
            }
        }
    }
}


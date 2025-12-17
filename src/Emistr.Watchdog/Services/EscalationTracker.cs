using System.Collections.Concurrent;
using Emistr.Watchdog.Configuration;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Tracks alert escalation state for services.
/// </summary>
public interface IEscalationTracker
{
    /// <summary>
    /// Records a failure and returns the current escalation level.
    /// </summary>
    EscalationState RecordFailure(string serviceName);

    /// <summary>
    /// Records recovery and resets escalation state.
    /// </summary>
    void RecordRecovery(string serviceName);

    /// <summary>
    /// Gets current escalation state for a service.
    /// </summary>
    EscalationState? GetState(string serviceName);

    /// <summary>
    /// Gets all active escalations.
    /// </summary>
    IReadOnlyDictionary<string, EscalationState> GetAllActiveEscalations();

    /// <summary>
    /// Checks if escalation should occur and returns the level to notify.
    /// </summary>
    EscalationLevel? CheckEscalation(string serviceName);
}

/// <summary>
/// State of escalation for a service.
/// </summary>
public record EscalationState
{
    public string ServiceName { get; init; } = string.Empty;
    public int CurrentLevel { get; init; }
    public string CurrentLevelName { get; init; } = string.Empty;
    public DateTime FirstFailureAt { get; init; }
    public DateTime LastFailureAt { get; init; }
    public DateTime? LastEscalationAt { get; init; }
    public int FailureCount { get; init; }
    public TimeSpan DurationSinceFirstFailure => DateTime.UtcNow - FirstFailureAt;
    public bool IsEscalated => CurrentLevel > 1;
}

/// <summary>
/// Implementation of escalation tracker.
/// </summary>
public class EscalationTracker : IEscalationTracker
{
    private readonly ConcurrentDictionary<string, EscalationStateInternal> _states = new();
    private readonly EscalationOptions _options;
    private readonly ILogger<EscalationTracker> _logger;

    public EscalationTracker(
        IOptions<EscalationOptions> options,
        ILogger<EscalationTracker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public EscalationState RecordFailure(string serviceName)
    {
        var now = DateTime.UtcNow;
        
        var state = _states.AddOrUpdate(
            serviceName,
            _ => new EscalationStateInternal
            {
                ServiceName = serviceName,
                FirstFailureAt = now,
                LastFailureAt = now,
                FailureCount = 1,
                CurrentLevel = 1,
                LastNotifiedLevel = 0
            },
            (_, existing) =>
            {
                existing.LastFailureAt = now;
                existing.FailureCount++;
                return existing;
            });

        return ToPublicState(state);
    }

    public void RecordRecovery(string serviceName)
    {
        if (_states.TryRemove(serviceName, out var state))
        {
            _logger.LogInformation(
                "Escalation reset for {ServiceName} after recovery. Was at level {Level}, duration {Duration}",
                serviceName,
                state.CurrentLevel,
                DateTime.UtcNow - state.FirstFailureAt);
        }
    }

    public EscalationState? GetState(string serviceName)
    {
        return _states.TryGetValue(serviceName, out var state) 
            ? ToPublicState(state) 
            : null;
    }

    public IReadOnlyDictionary<string, EscalationState> GetAllActiveEscalations()
    {
        return _states.ToDictionary(
            kvp => kvp.Key,
            kvp => ToPublicState(kvp.Value));
    }

    public EscalationLevel? CheckEscalation(string serviceName)
    {
        if (!_options.Enabled || _options.Levels.Count == 0)
            return null;

        if (!_states.TryGetValue(serviceName, out var state))
            return null;

        var now = DateTime.UtcNow;
        var minutesSinceFirstFailure = (now - state.FirstFailureAt).TotalMinutes;

        // Find the highest level we should be at
        var targetLevel = _options.Levels
            .Where(l => minutesSinceFirstFailure >= l.DelayMinutes)
            .OrderByDescending(l => l.Level)
            .FirstOrDefault();

        if (targetLevel == null)
            return null;

        // Check if we need to notify for this level
        if (targetLevel.Level > state.LastNotifiedLevel)
        {
            state.CurrentLevel = targetLevel.Level;
            state.LastNotifiedLevel = targetLevel.Level;
            state.LastEscalationAt = now;

            _logger.LogWarning(
                "Escalating {ServiceName} to {Level} ({LevelName}) after {Minutes} minutes",
                serviceName,
                targetLevel.Level,
                targetLevel.Name,
                (int)minutesSinceFirstFailure);

            return targetLevel;
        }

        return null;
    }

    private EscalationState ToPublicState(EscalationStateInternal state)
    {
        var levelConfig = _options.Levels.FirstOrDefault(l => l.Level == state.CurrentLevel);
        
        return new EscalationState
        {
            ServiceName = state.ServiceName,
            CurrentLevel = state.CurrentLevel,
            CurrentLevelName = levelConfig?.Name ?? $"L{state.CurrentLevel}",
            FirstFailureAt = state.FirstFailureAt,
            LastFailureAt = state.LastFailureAt,
            LastEscalationAt = state.LastEscalationAt,
            FailureCount = state.FailureCount
        };
    }

    private class EscalationStateInternal
    {
        public string ServiceName { get; set; } = string.Empty;
        public DateTime FirstFailureAt { get; set; }
        public DateTime LastFailureAt { get; set; }
        public DateTime? LastEscalationAt { get; set; }
        public int FailureCount { get; set; }
        public int CurrentLevel { get; set; }
        public int LastNotifiedLevel { get; set; }
    }
}


namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for alert escalation (L1 → L2 → L3).
/// </summary>
public sealed class EscalationOptions
{
    /// <summary>
    /// Whether escalation is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Escalation levels configuration.
    /// </summary>
    public List<EscalationLevel> Levels { get; set; } = new()
    {
        new EscalationLevel { Level = 1, Name = "L1", DelayMinutes = 0 },
        new EscalationLevel { Level = 2, Name = "L2", DelayMinutes = 15 },
        new EscalationLevel { Level = 3, Name = "L3", DelayMinutes = 60 }
    };

    /// <summary>
    /// Whether to reset escalation when service recovers.
    /// </summary>
    public bool ResetOnRecovery { get; set; } = true;

    /// <summary>
    /// Whether to notify all previous levels when escalating.
    /// </summary>
    public bool NotifyAllPreviousLevels { get; set; } = false;
}

/// <summary>
/// Configuration for a single escalation level.
/// </summary>
public sealed class EscalationLevel
{
    /// <summary>
    /// Level number (1, 2, 3, etc.).
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Display name for the level (L1, L2, L3, etc.).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Delay in minutes before escalating to this level.
    /// Level 1 should have 0 delay.
    /// </summary>
    public int DelayMinutes { get; set; }

    /// <summary>
    /// Email recipients for this level.
    /// </summary>
    public List<string> EmailRecipients { get; set; } = [];

    /// <summary>
    /// Webhook URL for this level (optional, uses default if empty).
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Whether to send SMS for this level.
    /// </summary>
    public bool SendSms { get; set; }

    /// <summary>
    /// SMS recipients for this level.
    /// </summary>
    public List<string> SmsRecipients { get; set; } = [];

    /// <summary>
    /// Custom message prefix for this level.
    /// </summary>
    public string? MessagePrefix { get; set; }
}


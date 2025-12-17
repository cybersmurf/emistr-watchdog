namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Configuration for maintenance windows when health checks are suppressed.
/// </summary>
public sealed class MaintenanceWindowOptions
{
    /// <summary>
    /// Whether maintenance windows are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// List of scheduled maintenance windows.
    /// </summary>
    public List<MaintenanceWindow> Windows { get; set; } = [];

    /// <summary>
    /// Check if currently in a maintenance window.
    /// </summary>
    public bool IsInMaintenanceWindow(DateTime utcNow)
    {
        if (!Enabled || Windows.Count == 0)
            return false;

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        var currentTime = localNow.TimeOfDay;
        var currentDay = localNow.DayOfWeek;

        foreach (var window in Windows)
        {
            if (!window.Enabled)
                continue;

            // Check day of week
            if (window.DaysOfWeek.Count > 0 && !window.DaysOfWeek.Contains(currentDay))
                continue;

            // Check time range
            var startTime = TimeSpan.Parse(window.StartTime);
            var endTime = TimeSpan.Parse(window.EndTime);

            // Handle overnight windows (e.g., 23:00 - 01:00)
            if (endTime < startTime)
            {
                if (currentTime >= startTime || currentTime <= endTime)
                    return true;
            }
            else
            {
                if (currentTime >= startTime && currentTime <= endTime)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get the next maintenance window start time.
    /// </summary>
    public DateTime? GetNextMaintenanceStart(DateTime utcNow)
    {
        if (!Enabled || Windows.Count == 0)
            return null;

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        DateTime? nearest = null;

        foreach (var window in Windows.Where(w => w.Enabled))
        {
            var startTime = TimeSpan.Parse(window.StartTime);
            
            // Check each day in the next week
            for (int i = 0; i < 7; i++)
            {
                var checkDate = localNow.Date.AddDays(i);
                var checkDay = checkDate.DayOfWeek;

                if (window.DaysOfWeek.Count > 0 && !window.DaysOfWeek.Contains(checkDay))
                    continue;

                var windowStart = checkDate.Add(startTime);
                
                if (windowStart > localNow)
                {
                    if (nearest == null || windowStart < nearest)
                        nearest = windowStart;
                    break;
                }
            }
        }

        return nearest.HasValue 
            ? TimeZoneInfo.ConvertTimeToUtc(nearest.Value, TimeZoneInfo.Local) 
            : null;
    }
}

/// <summary>
/// A single maintenance window definition.
/// </summary>
public sealed class MaintenanceWindow
{
    /// <summary>
    /// Name/description of the maintenance window.
    /// </summary>
    public string Name { get; set; } = "Maintenance";

    /// <summary>
    /// Whether this window is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Start time in HH:mm format (local time).
    /// </summary>
    public string StartTime { get; set; } = "02:00";

    /// <summary>
    /// End time in HH:mm format (local time).
    /// </summary>
    public string EndTime { get; set; } = "04:00";

    /// <summary>
    /// Days of week when this window applies. Empty = all days.
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];

    /// <summary>
    /// Services to suppress during this window. Empty = all services.
    /// </summary>
    public List<string> SuppressedServices { get; set; } = [];
}


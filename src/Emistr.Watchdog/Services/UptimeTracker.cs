using System.Collections.Concurrent;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Tracks uptime statistics for SLA reporting.
/// </summary>
public interface IUptimeTracker
{
    /// <summary>
    /// Records a health check result for uptime calculation.
    /// </summary>
    void RecordCheck(string serviceName, bool isHealthy, DateTime timestamp);

    /// <summary>
    /// Gets uptime statistics for a service.
    /// </summary>
    UptimeStats GetStats(string serviceName, TimeSpan period);

    /// <summary>
    /// Gets uptime statistics for all services.
    /// </summary>
    IReadOnlyDictionary<string, UptimeStats> GetAllStats(TimeSpan period);

    /// <summary>
    /// Gets SLA summary for dashboard.
    /// </summary>
    SlaSummary GetSlaSummary();
}

/// <summary>
/// Uptime statistics for a service.
/// </summary>
public record UptimeStats
{
    public string ServiceName { get; init; } = string.Empty;
    public double UptimePercent { get; init; }
    public int TotalChecks { get; init; }
    public int SuccessfulChecks { get; init; }
    public int FailedChecks { get; init; }
    public TimeSpan Period { get; init; }
    public DateTime? LastCheck { get; init; }
    public DateTime? LastFailure { get; init; }
    public TimeSpan? CurrentUptime { get; init; }
    public TimeSpan? LongestDowntime { get; init; }
}

/// <summary>
/// SLA summary for dashboard display.
/// </summary>
public record SlaSummary
{
    public double OverallUptimeToday { get; init; }
    public double OverallUptimeWeek { get; init; }
    public double OverallUptimeMonth { get; init; }
    public int TotalServices { get; init; }
    public int HealthyServices { get; init; }
    public int ServicesBelow99Percent { get; init; }
    public IReadOnlyList<ServiceUptime> ServiceUptimes { get; init; } = [];
}

public record ServiceUptime
{
    public string ServiceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public double UptimeToday { get; init; }
    public double UptimeWeek { get; init; }
    public double UptimeMonth { get; init; }
    public bool IsHealthy { get; init; }
}

/// <summary>
/// In-memory implementation of uptime tracker.
/// </summary>
public class UptimeTracker : IUptimeTracker
{
    private readonly ConcurrentDictionary<string, List<CheckRecord>> _records = new();
    private readonly ConcurrentDictionary<string, string> _displayNames = new();
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(31); // Keep 31 days
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.MinValue;
    
    /// <summary>
    /// Maximum number of records to keep per service.
    /// At 30s intervals, 1000 records = ~8.3 hours of data.
    /// With 31 day retention, we need more: 31*24*60*2 = 89,280 records max per service.
    /// We limit to 100,000 to prevent memory issues.
    /// </summary>
    private const int MaxRecordsPerService = 100_000;

    private record CheckRecord(DateTime Timestamp, bool IsHealthy);

    public void RecordCheck(string serviceName, bool isHealthy, DateTime timestamp)
    {
        var records = _records.GetOrAdd(serviceName, _ => new List<CheckRecord>());
        
        lock (records)
        {
            records.Add(new CheckRecord(timestamp, isHealthy));
        }

        // Periodic cleanup
        CleanupOldRecords();
    }

    public void SetDisplayName(string serviceName, string displayName)
    {
        _displayNames[serviceName] = displayName;
    }

    public UptimeStats GetStats(string serviceName, TimeSpan period)
    {
        if (!_records.TryGetValue(serviceName, out var records))
        {
            return new UptimeStats
            {
                ServiceName = serviceName,
                UptimePercent = 100,
                Period = period
            };
        }

        var cutoff = DateTime.UtcNow - period;
        List<CheckRecord> relevantRecords;
        
        lock (records)
        {
            relevantRecords = records.Where(r => r.Timestamp >= cutoff).ToList();
        }

        if (relevantRecords.Count == 0)
        {
            return new UptimeStats
            {
                ServiceName = serviceName,
                UptimePercent = 100,
                Period = period
            };
        }

        var successful = relevantRecords.Count(r => r.IsHealthy);
        var failed = relevantRecords.Count - successful;
        var uptimePercent = (double)successful / relevantRecords.Count * 100;

        // Calculate current uptime streak
        TimeSpan? currentUptime = null;
        var orderedRecords = relevantRecords.OrderByDescending(r => r.Timestamp).ToList();
        if (orderedRecords.First().IsHealthy)
        {
            var lastFailure = orderedRecords.FirstOrDefault(r => !r.IsHealthy);
            currentUptime = lastFailure != null 
                ? DateTime.UtcNow - lastFailure.Timestamp 
                : DateTime.UtcNow - orderedRecords.Last().Timestamp;
        }

        // Calculate longest downtime
        TimeSpan? longestDowntime = null;
        var chronological = relevantRecords.OrderBy(r => r.Timestamp).ToList();
        DateTime? downtimeStart = null;
        TimeSpan maxDowntime = TimeSpan.Zero;
        
        foreach (var record in chronological)
        {
            if (!record.IsHealthy && downtimeStart == null)
            {
                downtimeStart = record.Timestamp;
            }
            else if (record.IsHealthy && downtimeStart != null)
            {
                var duration = record.Timestamp - downtimeStart.Value;
                if (duration > maxDowntime)
                    maxDowntime = duration;
                downtimeStart = null;
            }
        }
        
        if (maxDowntime > TimeSpan.Zero)
            longestDowntime = maxDowntime;

        return new UptimeStats
        {
            ServiceName = serviceName,
            UptimePercent = Math.Round(uptimePercent, 2),
            TotalChecks = relevantRecords.Count,
            SuccessfulChecks = successful,
            FailedChecks = failed,
            Period = period,
            LastCheck = orderedRecords.FirstOrDefault()?.Timestamp,
            LastFailure = orderedRecords.FirstOrDefault(r => !r.IsHealthy)?.Timestamp,
            CurrentUptime = currentUptime,
            LongestDowntime = longestDowntime
        };
    }

    public IReadOnlyDictionary<string, UptimeStats> GetAllStats(TimeSpan period)
    {
        var result = new Dictionary<string, UptimeStats>();
        
        foreach (var serviceName in _records.Keys)
        {
            result[serviceName] = GetStats(serviceName, period);
        }

        return result;
    }

    public SlaSummary GetSlaSummary()
    {
        var now = DateTime.UtcNow;
        var dayAgo = TimeSpan.FromDays(1);
        var weekAgo = TimeSpan.FromDays(7);
        var monthAgo = TimeSpan.FromDays(30);

        var serviceUptimes = new List<ServiceUptime>();
        double totalUptimeToday = 0;
        double totalUptimeWeek = 0;
        double totalUptimeMonth = 0;
        int healthyCount = 0;
        int below99Count = 0;

        foreach (var serviceName in _records.Keys)
        {
            var statsDay = GetStats(serviceName, dayAgo);
            var statsWeek = GetStats(serviceName, weekAgo);
            var statsMonth = GetStats(serviceName, monthAgo);

            var isHealthy = statsDay.TotalChecks > 0 && 
                           _records.TryGetValue(serviceName, out var records) &&
                           records.LastOrDefault()?.IsHealthy == true;

            serviceUptimes.Add(new ServiceUptime
            {
                ServiceName = serviceName,
                DisplayName = _displayNames.GetValueOrDefault(serviceName, serviceName),
                UptimeToday = statsDay.UptimePercent,
                UptimeWeek = statsWeek.UptimePercent,
                UptimeMonth = statsMonth.UptimePercent,
                IsHealthy = isHealthy
            });

            totalUptimeToday += statsDay.UptimePercent;
            totalUptimeWeek += statsWeek.UptimePercent;
            totalUptimeMonth += statsMonth.UptimePercent;

            if (isHealthy) healthyCount++;
            if (statsMonth.UptimePercent < 99) below99Count++;
        }

        var serviceCount = _records.Count;
        
        return new SlaSummary
        {
            OverallUptimeToday = serviceCount > 0 ? Math.Round(totalUptimeToday / serviceCount, 2) : 100,
            OverallUptimeWeek = serviceCount > 0 ? Math.Round(totalUptimeWeek / serviceCount, 2) : 100,
            OverallUptimeMonth = serviceCount > 0 ? Math.Round(totalUptimeMonth / serviceCount, 2) : 100,
            TotalServices = serviceCount,
            HealthyServices = healthyCount,
            ServicesBelow99Percent = below99Count,
            ServiceUptimes = serviceUptimes.OrderBy(s => s.UptimeMonth).ToList()
        };
    }

    private void CleanupOldRecords()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromHours(1))
            return;

        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromHours(1))
                return;

            var cutoff = DateTime.UtcNow - _retentionPeriod;
            
            foreach (var kvp in _records)
            {
                lock (kvp.Value)
                {
                    // Remove old records
                    kvp.Value.RemoveAll(r => r.Timestamp < cutoff);
                    
                    // Enforce max size limit (keep newest)
                    if (kvp.Value.Count > MaxRecordsPerService)
                    {
                        var toRemove = kvp.Value.Count - MaxRecordsPerService;
                        kvp.Value.RemoveRange(0, toRemove);
                    }
                }
            }

            _lastCleanup = DateTime.UtcNow;
        }
    }
}


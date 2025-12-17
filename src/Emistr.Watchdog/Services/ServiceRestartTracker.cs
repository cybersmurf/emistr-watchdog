namespace Emistr.Watchdog.Services;

public class ServiceRestartTracker
{
    private readonly Dictionary<string, RestartInfoInternal> _restarts = new();
    private readonly object _lock = new();

    public bool ShouldAttemptRestart(string serviceName, int maxAttempts)
    {
        lock (_lock)
        {
            if (!_restarts.TryGetValue(serviceName, out var info))
            {
                return true;
            }

            if (info.Count >= maxAttempts)
            {
                var timeSinceLastAttempt = DateTime.UtcNow - info.LastAttemptTime;
                if (timeSinceLastAttempt < TimeSpan.FromMinutes(10))
                {
                    return false;
                }

                _restarts.Remove(serviceName);
                return true;
            }

            return true;
        }
    }

    public void RecordRestartAttempt(string serviceName, bool success)
    {
        lock (_lock)
        {
            if (!_restarts.TryGetValue(serviceName, out var info))
            {
                info = new RestartInfoInternal();
                _restarts[serviceName] = info;
            }

            info.Count++;
            info.LastAttemptTime = DateTime.UtcNow;
            info.LastSuccess = success;
        }
    }

    public RestartInfo? GetRestartInfo(string serviceName)
    {
        lock (_lock)
        {
            if (!_restarts.TryGetValue(serviceName, out var info))
            {
                return null;
            }

            return new RestartInfo
            {
                Count = info.Count,
                LastAttemptTime = info.LastAttemptTime,
                LastSuccess = info.LastSuccess
            };
        }
    }

    private class RestartInfoInternal
    {
        public int Count { get; set; }
        public DateTime LastAttemptTime { get; set; }
        public bool LastSuccess { get; set; }
    }
}

public record RestartInfo
{
    public int Count { get; init; }
    public DateTime LastAttemptTime { get; init; }
    public bool LastSuccess { get; init; }
}

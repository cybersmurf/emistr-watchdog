namespace Emistr.Watchdog.Services;

/// <summary>
/// Manages runtime configuration overrides that take effect without service restart.
/// Thread-safe singleton for managing enabled/prioritized states.
/// </summary>
public sealed class RuntimeConfigurationService
{
    private static readonly Lazy<RuntimeConfigurationService> _instance = 
        new(() => new RuntimeConfigurationService());
    
    public static RuntimeConfigurationService Instance => _instance.Value;
    
    private readonly Dictionary<string, ServiceRuntimeConfig> _overrides = 
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ReaderWriterLockSlim _lock = new();
    
    private RuntimeConfigurationService() { }
    
    /// <summary>
    /// Gets the effective enabled state for a service.
    /// Runtime override takes precedence over config.
    /// </summary>
    public bool GetEffectiveEnabled(string serviceName, bool configValue)
    {
        _lock.EnterReadLock();
        try
        {
            if (_overrides.TryGetValue(serviceName, out var config) && config.EnabledOverride.HasValue)
            {
                return config.EnabledOverride.Value;
            }
            return configValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    /// <summary>
    /// Gets the effective prioritized state for a service.
    /// Runtime override takes precedence over config.
    /// </summary>
    public bool GetEffectivePrioritized(string serviceName, bool configValue)
    {
        _lock.EnterReadLock();
        try
        {
            if (_overrides.TryGetValue(serviceName, out var config) && config.PrioritizedOverride.HasValue)
            {
                return config.PrioritizedOverride.Value;
            }
            return configValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    /// <summary>
    /// Sets runtime override for enabled state.
    /// </summary>
    public void SetEnabled(string serviceName, bool enabled)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_overrides.TryGetValue(serviceName, out var config))
            {
                config = new ServiceRuntimeConfig();
                _overrides[serviceName] = config;
            }
            config.EnabledOverride = enabled;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// Sets runtime override for prioritized state.
    /// </summary>
    public void SetPrioritized(string serviceName, bool prioritized)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_overrides.TryGetValue(serviceName, out var config))
            {
                config = new ServiceRuntimeConfig();
                _overrides[serviceName] = config;
            }
            config.PrioritizedOverride = prioritized;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// Clears all runtime overrides for a service.
    /// </summary>
    public void ClearOverrides(string serviceName)
    {
        _lock.EnterWriteLock();
        try
        {
            _overrides.Remove(serviceName);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// Gets all current overrides for debugging/display.
    /// </summary>
    public Dictionary<string, ServiceRuntimeConfig> GetAllOverrides()
    {
        _lock.EnterReadLock();
        try
        {
            return new Dictionary<string, ServiceRuntimeConfig>(_overrides);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    /// <summary>
    /// Runtime configuration for a single service.
    /// </summary>
    public sealed class ServiceRuntimeConfig
    {
        public bool? EnabledOverride { get; set; }
        public bool? PrioritizedOverride { get; set; }
    }
}


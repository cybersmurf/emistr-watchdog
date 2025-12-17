using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services.HealthCheckers;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Services;

/// <summary>
/// Service for managing multi-tenant health checking across multiple environments.
/// </summary>
public sealed class MultiTenantHealthService
{
    private readonly MultiTenantOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;
    private readonly ILogger<MultiTenantHealthService> _logger;
    private readonly Dictionary<string, List<IHealthChecker>> _tenantCheckers = [];

    public MultiTenantHealthService(
        IOptions<MultiTenantOptions> options,
        ILoggerFactory loggerFactory,
        ILogger<MultiTenantHealthService> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _serviceController = serviceController;
        _restartTracker = restartTracker;

        if (_options.Enabled)
        {
            InitializeTenantCheckers();
        }
    }

    public IEnumerable<string> GetTenantNames() => _tenantCheckers.Keys;

    public IEnumerable<IHealthChecker> GetCheckersForTenant(string tenantName)
    {
        return _tenantCheckers.TryGetValue(tenantName, out var checkers)
            ? checkers
            : Enumerable.Empty<IHealthChecker>();
    }

    public IEnumerable<(string TenantName, IHealthChecker Checker)> GetAllCheckers()
    {
        foreach (var (tenantName, checkers) in _tenantCheckers)
        {
            foreach (var checker in checkers)
            {
                yield return (tenantName, checker);
            }
        }
    }

    public async Task<TenantHealthReport> CheckTenantHealthAsync(
        string tenantName,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantCheckers.TryGetValue(tenantName, out var checkers))
        {
            throw new ArgumentException($"Tenant '{tenantName}' not found", nameof(tenantName));
        }

        var results = new List<HealthCheckResult>();
        var tasks = checkers
            .Where(c => c.IsEnabled)
            .Select(async checker =>
            {
                try
                {
                    return await checker.CheckHealthAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking health for {Service} in tenant {Tenant}",
                        checker.ServiceName, tenantName);
                    return HealthCheckResult.Unhealthy(checker.ServiceName, ex.Message, ex);
                }
            });

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        var tenant = _options.Tenants[tenantName];
        return new TenantHealthReport
        {
            TenantName = tenantName,
            DisplayName = tenant.DisplayName ?? tenantName,
            Environment = tenant.Environment,
            Tags = tenant.Tags,
            Results = results,
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<TenantHealthReport>> CheckAllTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        var reports = new List<TenantHealthReport>();
        foreach (var tenantName in _tenantCheckers.Keys)
        {
            var report = await CheckTenantHealthAsync(tenantName, cancellationToken);
            reports.Add(report);
        }
        return reports;
    }

    private void InitializeTenantCheckers()
    {
        foreach (var (tenantName, tenant) in _options.Tenants)
        {
            if (!tenant.Enabled) continue;

            var checkers = new List<IHealthChecker>();

            foreach (var (name, config) in tenant.Services.MariaDb.Where(x => x.Value.Enabled))
            {
                var factory = new MariaDbHealthCheckerFactory(_loggerFactory, _serviceController, _restartTracker);
                checkers.Add(factory.Create($"{tenantName}:{name}", config));
            }

            foreach (var (name, config) in tenant.Services.Http.Where(x => x.Value.Enabled))
            {
                var factory = new HttpHealthCheckerFactory(_loggerFactory, _serviceController, _restartTracker);
                checkers.Add(factory.Create($"{tenantName}:{name}", config));
            }

            foreach (var (name, config) in tenant.Services.Redis.Where(x => x.Value.Enabled))
            {
                var factory = new RedisHealthCheckerFactory(_loggerFactory, _serviceController, _restartTracker);
                checkers.Add(factory.Create($"{tenantName}:{name}", config));
            }

            foreach (var (name, config) in tenant.Services.RabbitMq.Where(x => x.Value.Enabled))
            {
                var factory = new RabbitMqHealthCheckerFactory(_loggerFactory, _serviceController, _restartTracker);
                checkers.Add(factory.Create($"{tenantName}:{name}", config));
            }

            foreach (var (name, config) in tenant.Services.Elasticsearch.Where(x => x.Value.Enabled))
            {
                var factory = new ElasticsearchHealthCheckerFactory(_loggerFactory, _serviceController, _restartTracker);
                checkers.Add(factory.Create($"{tenantName}:{name}", config));
            }

            _tenantCheckers[tenantName] = checkers;
            _logger.LogInformation("Initialized tenant {Tenant} with {Count} health checkers", tenantName, checkers.Count);
        }
    }
}

public sealed record TenantHealthReport
{
    public required string TenantName { get; init; }
    public required string DisplayName { get; init; }
    public EnvironmentType Environment { get; init; }
    public List<string> Tags { get; init; } = [];
    public required List<HealthCheckResult> Results { get; init; }
    public DateTime CheckedAt { get; init; }

    public bool IsHealthy => Results.All(r => r.IsHealthy);
    public bool HasCritical => Results.Any(r => r.IsCritical);
    public int HealthyCount => Results.Count(r => r.IsHealthy);
    public int UnhealthyCount => Results.Count(r => !r.IsHealthy);
}

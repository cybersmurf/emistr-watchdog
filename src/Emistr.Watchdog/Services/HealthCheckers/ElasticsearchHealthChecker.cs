using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for Elasticsearch cluster.
/// </summary>
public sealed class ElasticsearchHealthChecker : HealthCheckerBase
{
    private readonly ElasticsearchOptions _options;
    private readonly string _serviceName;

    public ElasticsearchHealthChecker(
        string serviceName,
        ElasticsearchOptions options,
        ILogger<ElasticsearchHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? _serviceName;
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;
    protected override bool ConfigPrioritized => _options.Prioritized;
    public override ServiceRestartConfig? RestartConfig => _options.RestartConfig;

    protected override async Task<HealthCheckResult> PerformCheckAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            return HealthCheckResult.Unhealthy(ServiceName, "Elasticsearch URL is not configured");
        }

        var sw = Stopwatch.StartNew();

        var settings = new ElasticsearchClientSettings(new Uri(_options.Url));
        
        // Configure authentication
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            settings.Authentication(new ApiKey(_options.ApiKey));
        }
        else if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
        {
            settings.Authentication(new BasicAuthentication(_options.Username, _options.Password));
        }

        // Configure SSL
        if (_options.IgnoreSslErrors)
        {
            settings.ServerCertificateValidationCallback((_, _, _, _) => true);
        }

        settings.RequestTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var client = new ElasticsearchClient(settings);

        // Check cluster health
        var healthResponse = await client.Cluster.HealthAsync(cancellationToken: cancellationToken);

        if (!healthResponse.IsValidResponse)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(ServiceName,
                $"Failed to get cluster health: {healthResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var clusterStatus = healthResponse.Status.ToString().ToLower();

        // Get server info
        var serverInfo = await GetServerInfoAsync(client, cancellationToken);

        // Optional: check if test index exists
        if (!string.IsNullOrEmpty(_options.TestIndex))
        {
            var indexExists = await client.Indices.ExistsAsync(_options.TestIndex, cancellationToken);
            if (!indexExists.Exists)
            {
                Logger.LogWarning("Test index '{TestIndex}' does not exist in Elasticsearch", _options.TestIndex);
            }
        }

        sw.Stop();

        Logger.LogDebug(
            "{ServiceName} health check completed in {ElapsedMs}ms, cluster status: {Status}, version: {Version}",
            ServiceName,
            sw.ElapsedMilliseconds,
            clusterStatus,
            serverInfo?.Version ?? "unknown");

        // Check cluster health status
        var isHealthy = true;
        var status = ServiceStatus.Healthy;
        string? errorMessage = null;

        if (!string.IsNullOrEmpty(_options.MinimumHealthStatus))
        {
            var minStatus = _options.MinimumHealthStatus.ToLower();
            
            if (clusterStatus == "red")
            {
                isHealthy = false;
                status = ServiceStatus.Unhealthy;
                errorMessage = "Cluster status is RED - some primary shards are not allocated";
            }
            else if (clusterStatus == "yellow" && minStatus == "green")
            {
                status = ServiceStatus.Degraded;
                errorMessage = "Cluster status is YELLOW - some replica shards are not allocated";
            }
        }

        // Check for slow response
        if (sw.ElapsedMilliseconds > 2000 && status == ServiceStatus.Healthy)
        {
            status = ServiceStatus.Degraded;
            errorMessage = $"Slow response time: {sw.ElapsedMilliseconds}ms";
        }

        return new HealthCheckResult
        {
            ServiceName = ServiceName,
            IsHealthy = isHealthy,
            Status = status,
            ErrorMessage = errorMessage,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            ServerInfo = serverInfo,
            Details = new Dictionary<string, object>
            {
                ["cluster_status"] = clusterStatus,
                ["cluster_name"] = healthResponse.ClusterName ?? "unknown",
                ["number_of_nodes"] = healthResponse.NumberOfNodes,
                ["active_shards"] = healthResponse.ActiveShards,
                ["relocating_shards"] = healthResponse.RelocatingShards,
                ["initializing_shards"] = healthResponse.InitializingShards,
                ["unassigned_shards"] = healthResponse.UnassignedShards
            }
        };
    }

    private async Task<ServerInfo?> GetServerInfoAsync(ElasticsearchClient client, CancellationToken cancellationToken)
    {
        try
        {
            var infoResponse = await client.InfoAsync(cancellationToken);
            
            if (!infoResponse.IsValidResponse)
            {
                return null;
            }

            return new ServerInfo
            {
                Version = infoResponse.Version?.Number ?? "unknown",
                ServerType = "Elasticsearch",
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["cluster_name"] = infoResponse.ClusterName ?? "unknown",
                    ["cluster_uuid"] = infoResponse.ClusterUuid ?? "unknown",
                    ["lucene_version"] = infoResponse.Version?.LuceneVersion ?? "unknown"
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get Elasticsearch server info");
            return null;
        }
    }
}

/// <summary>
/// Factory for creating Elasticsearch health checkers.
/// </summary>
public sealed class ElasticsearchHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public ElasticsearchHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public ElasticsearchHealthChecker Create(string serviceName, ElasticsearchOptions options)
    {
        return new ElasticsearchHealthChecker(
            serviceName,
            options,
            _loggerFactory.CreateLogger<ElasticsearchHealthChecker>(),
            _serviceController,
            _restartTracker);
    }
}

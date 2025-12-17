using System.Diagnostics;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using RabbitMQ.Client;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for RabbitMQ message broker.
/// </summary>
public sealed class RabbitMqHealthChecker : HealthCheckerBase
{
    private readonly RabbitMqOptions _options;
    private readonly string _serviceName;

    public RabbitMqHealthChecker(
        string serviceName,
        RabbitMqOptions options,
        ILogger<RabbitMqHealthChecker> logger,
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
        var sw = Stopwatch.StartNew();

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        if (_options.UseSsl)
        {
            factory.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = _options.Host
            };
        }

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Get server properties
        var serverInfo = GetServerInfo(connection);

        // Optional: check if test queue exists
        if (!string.IsNullOrEmpty(_options.TestQueue))
        {
            try
            {
                var queueDeclareOk = await channel.QueueDeclarePassiveAsync(_options.TestQueue, cancellationToken);
                Logger.LogDebug("Queue '{TestQueue}' exists with {MessageCount} messages",
                    _options.TestQueue, queueDeclareOk.MessageCount);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Test queue '{TestQueue}' does not exist or is not accessible", _options.TestQueue);
            }
        }

        sw.Stop();

        Logger.LogDebug(
            "{ServiceName} health check completed in {ElapsedMs}ms, version: {Version}",
            ServiceName,
            sw.ElapsedMilliseconds,
            serverInfo?.Version ?? "unknown");

        // Check for degraded performance (connection time > 1 second)
        if (sw.ElapsedMilliseconds > 1000)
        {
            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = ServiceStatus.Degraded,
                ErrorMessage = $"Slow connection time: {sw.ElapsedMilliseconds}ms",
                ResponseTimeMs = sw.ElapsedMilliseconds,
                ServerInfo = serverInfo
            };
        }

        return new HealthCheckResult
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = ServiceStatus.Healthy,
            ResponseTimeMs = sw.ElapsedMilliseconds,
            ServerInfo = serverInfo,
            Details = new Dictionary<string, object>
            {
                ["host"] = _options.Host,
                ["port"] = _options.Port,
                ["virtual_host"] = _options.VirtualHost
            }
        };
    }

    private ServerInfo? GetServerInfo(IConnection connection)
    {
        try
        {
            var serverProperties = connection.ServerProperties;
            if (serverProperties == null) return null;
            
            var version = "unknown";
            if (serverProperties.TryGetValue("version", out var versionBytes) && versionBytes is byte[] vb)
            {
                version = System.Text.Encoding.UTF8.GetString(vb);
            }

            var product = "RabbitMQ";
            if (serverProperties.TryGetValue("product", out var productBytes) && productBytes is byte[] pb)
            {
                product = System.Text.Encoding.UTF8.GetString(pb);
            }

            var cluster = "unknown";
            if (serverProperties.TryGetValue("cluster_name", out var clusterBytes) && clusterBytes is byte[] cb)
            {
                cluster = System.Text.Encoding.UTF8.GetString(cb);
            }

            return new ServerInfo
            {
                Version = version,
                ServerType = product,
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["product"] = product,
                    ["cluster_name"] = cluster
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get RabbitMQ server info");
            return null;
        }
    }
}

/// <summary>
/// Factory for creating RabbitMQ health checkers.
/// </summary>
public sealed class RabbitMqHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public RabbitMqHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public RabbitMqHealthChecker Create(string serviceName, RabbitMqOptions options)
    {
        return new RabbitMqHealthChecker(
            serviceName,
            options,
            _loggerFactory.CreateLogger<RabbitMqHealthChecker>(),
            _serviceController,
            _restartTracker);
    }
}

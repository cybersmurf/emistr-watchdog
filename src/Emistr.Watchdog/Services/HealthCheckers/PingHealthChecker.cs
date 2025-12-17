using System.Diagnostics;
using System.Net.NetworkInformation;
using Emistr.Watchdog.Models;
using PingServiceOptions = Emistr.Watchdog.Configuration.PingOptions;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker that performs ICMP ping to verify host availability.
/// Note: Some hosts may block ICMP, so use with caution.
/// </summary>
public sealed class PingHealthChecker : HealthCheckerBase
{
    private readonly PingServiceOptions _options;
    private readonly string _serviceName;

    public PingHealthChecker(
        string serviceName,
        PingServiceOptions options,
        ILogger<PingHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? $"Ping {_options.Host}";
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;
    protected override bool ConfigPrioritized => _options.Prioritized;

    protected override async Task<HealthCheckResult> PerformCheckAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            return HealthCheckResult.Unhealthy(ServiceName, "Host is not configured");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var ping = new Ping();
            var timeout = _options.TimeoutSeconds * 1000; // Convert to milliseconds
            
            // Perform multiple pings if configured
            var successCount = 0;
            var totalRoundtrip = 0L;
            var lastStatus = IPStatus.Unknown;
            
            for (var i = 0; i < _options.PingCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var reply = await ping.SendPingAsync(_options.Host, timeout);
                lastStatus = reply.Status;
                
                if (reply.Status == IPStatus.Success)
                {
                    successCount++;
                    totalRoundtrip += reply.RoundtripTime;
                }

                // Small delay between pings
                if (i < _options.PingCount - 1)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            sw.Stop();

            var serverInfo = new ServerInfo
            {
                ServerType = "ICMP Ping",
                AdditionalInfo = new Dictionary<string, string>
                {
                    ["Host"] = _options.Host,
                    ["PingCount"] = _options.PingCount.ToString(),
                    ["SuccessCount"] = successCount.ToString(),
                    ["LastStatus"] = lastStatus.ToString()
                }
            };

            // All pings failed
            if (successCount == 0)
            {
                return new HealthCheckResult
                {
                    ServiceName = ServiceName,
                    IsHealthy = false,
                    Status = ServiceStatus.Unhealthy,
                    ErrorMessage = $"Ping failed: {GetStatusDescription(lastStatus)}",
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    ServerInfo = serverInfo
                };
            }

            var avgRoundtrip = totalRoundtrip / successCount;
            serverInfo.AdditionalInfo["AvgRoundtripMs"] = avgRoundtrip.ToString();

            // Check for packet loss
            var packetLoss = (double)(_options.PingCount - successCount) / _options.PingCount * 100;
            serverInfo.AdditionalInfo["PacketLoss"] = $"{packetLoss:F1}%";

            // Degraded if packet loss > threshold or high latency
            if (packetLoss > _options.PacketLossThresholdPercent || avgRoundtrip > _options.HighLatencyThresholdMs)
            {
                return new HealthCheckResult
                {
                    ServiceName = ServiceName,
                    IsHealthy = true,
                    Status = ServiceStatus.Degraded,
                    ErrorMessage = packetLoss > 0 
                        ? $"Packet loss: {packetLoss:F1}% ({successCount}/{_options.PingCount})"
                        : $"High latency: {avgRoundtrip}ms",
                    ResponseTimeMs = avgRoundtrip,
                    ServerInfo = serverInfo
                };
            }

            return new HealthCheckResult
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = ServiceStatus.Healthy,
                ResponseTimeMs = avgRoundtrip,
                ServerInfo = serverInfo,
                Details = new Dictionary<string, object>
                {
                    ["avgRoundtripMs"] = avgRoundtrip,
                    ["packetLoss"] = packetLoss,
                    ["successCount"] = successCount,
                    ["pingCount"] = _options.PingCount
                }
            };
        }
        catch (PingException ex)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Ping error: {ex.Message}",
                ex);
        }
    }

    private static string GetStatusDescription(IPStatus status)
    {
        return status switch
        {
            IPStatus.Success => "Success",
            IPStatus.TimedOut => "Request timed out",
            IPStatus.DestinationHostUnreachable => "Destination host unreachable",
            IPStatus.DestinationNetworkUnreachable => "Destination network unreachable",
            IPStatus.DestinationUnreachable => "Destination unreachable",
            IPStatus.BadDestination => "Bad destination",
            IPStatus.BadRoute => "Bad route",
            IPStatus.TtlExpired => "TTL expired",
            IPStatus.PacketTooBig => "Packet too big",
            IPStatus.HardwareError => "Hardware error",
            _ => status.ToString()
        };
    }
}

/// <summary>
/// Factory for creating Ping health checkers.
/// </summary>
public sealed class PingHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public PingHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public PingHealthChecker Create(string serviceName, PingServiceOptions options)
    {
        var logger = _loggerFactory.CreateLogger<PingHealthChecker>();
        return new PingHealthChecker(serviceName, options, logger, _serviceController, _restartTracker);
    }
}


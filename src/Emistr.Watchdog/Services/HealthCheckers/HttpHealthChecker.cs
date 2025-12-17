using System.Diagnostics;
using System.Text.Json;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for HTTP-based services (REST APIs, web servers).
/// </summary>
public sealed class HttpHealthChecker : HealthCheckerBase
{
    private readonly HttpServiceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;

    public HttpHealthChecker(
        string serviceName,
        HttpServiceOptions options,
        HttpClient httpClient,
        ILogger<HttpHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
        _httpClient = httpClient;
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
            return HealthCheckResult.Unhealthy(ServiceName, "URL is not configured");
        }

        var sw = Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var response = await _httpClient.GetAsync(_options.Url, cts.Token);
            sw.Stop();

            var statusCode = (int)response.StatusCode;
            var isExpectedStatus = _options.ExpectedStatusCodes.Contains(statusCode);

            if (!isExpectedStatus)
            {
                return HealthCheckResult.Unhealthy(
                    ServiceName,
                    $"Unexpected status code: {statusCode} (expected: {string.Join(", ", _options.ExpectedStatusCodes)})");
            }

            // Read response content
            var content = await response.Content.ReadAsStringAsync(cts.Token);

            // Check expected content if configured
            if (!string.IsNullOrWhiteSpace(_options.ExpectedContent))
            {
                if (!content.Contains(_options.ExpectedContent, StringComparison.OrdinalIgnoreCase))
                {
                    return HealthCheckResult.Unhealthy(
                        ServiceName,
                        $"Response does not contain expected content: '{_options.ExpectedContent}'");
                }
            }

                    // Try to parse server info from JSON response
                    var serverInfo = TryParseServerInfo(content, response);

                    Logger.LogDebug(
                        "{ServiceName} health check completed in {ElapsedMs}ms with status {StatusCode}",
                        ServiceName,
                        sw.ElapsedMilliseconds,
                        statusCode);

                    // Check for degraded performance
                    if (sw.ElapsedMilliseconds > 2000)
                    {
                        return new HealthCheckResult
                        {
                            ServiceName = ServiceName,
                            IsHealthy = true,
                            Status = ServiceStatus.Degraded,
                            ErrorMessage = $"Slow response time: {sw.ElapsedMilliseconds}ms",
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
                        ServerInfo = serverInfo
                    };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // Service is stopping, propagate
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    return HealthCheckResult.Unhealthy(
                        ServiceName,
                        $"Request timeout after {_options.TimeoutSeconds}s to {_options.Url}");
                }
                catch (HttpRequestException ex)
                {
                    sw.Stop();
                    return HealthCheckResult.Unhealthy(
                        ServiceName,
                        $"HTTP request failed: {ex.Message}",
                        ex);
                }
            }

    private ServerInfo? TryParseServerInfo(string content, HttpResponseMessage response)
    {
        try
        {
            // Try JSON format first (extended health.php response)
            if (content.TrimStart().StartsWith('{'))
            {
                var json = JsonDocument.Parse(content);
                var root = json.RootElement;

                var additionalInfo = new Dictionary<string, string>();

                // Parse PHP version if present
                if (root.TryGetProperty("php_version", out var phpVersion))
                {
                    additionalInfo["PHP"] = phpVersion.GetString() ?? "";
                }

                // Parse other info
                if (root.TryGetProperty("server_software", out var serverSoftware))
                {
                    additionalInfo["ServerSoftware"] = serverSoftware.GetString() ?? "";
                }

                if (root.TryGetProperty("document_root", out var docRoot))
                {
                    additionalInfo["DocumentRoot"] = docRoot.GetString() ?? "";
                }

                return new ServerInfo
                {
                    Version = root.TryGetProperty("apache_version", out var apacheVer)
                        ? apacheVer.GetString()
                        : null,
                    ServerType = root.TryGetProperty("server_type", out var serverType)
                        ? serverType.GetString()
                        : "HTTP",
                    Platform = root.TryGetProperty("os", out var os)
                        ? os.GetString()
                        : null,
                    Architecture = root.TryGetProperty("architecture", out var arch)
                        ? arch.GetString()
                        : null,
                    AdditionalInfo = additionalInfo
                };
            }

            // Fallback: try to get info from HTTP headers
            var serverHeader = response.Headers.Server?.ToString();
            if (!string.IsNullOrEmpty(serverHeader))
            {
                return new ServerInfo
                {
                    Version = serverHeader,
                    ServerType = serverHeader.Contains("Apache", StringComparison.OrdinalIgnoreCase)
                        ? "Apache"
                        : serverHeader.Contains("nginx", StringComparison.OrdinalIgnoreCase)
                            ? "nginx"
                            : "HTTP"
                };
            }
        }
        catch
        {
            // Server info is optional
        }

        return null;
    }
}

/// <summary>
/// Factory for creating HTTP health checkers.
/// </summary>
public sealed class HttpHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<string, HttpClient> _httpClients = new();
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public HttpHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public HttpHealthChecker Create(string serviceName, HttpServiceOptions options)
    {
        if (!_httpClients.TryGetValue(serviceName, out var httpClient))
        {
            httpClient = CreateHttpClient(options);
            _httpClients[serviceName] = httpClient;
        }

        var logger = _loggerFactory.CreateLogger<HttpHealthChecker>();
        return new HttpHealthChecker(serviceName, options, httpClient, logger, _serviceController, _restartTracker);
    }

    private static HttpClient CreateHttpClient(HttpServiceOptions options)
    {
        if (options.IgnoreSslErrors)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler);
        }

        return new HttpClient();
    }
}

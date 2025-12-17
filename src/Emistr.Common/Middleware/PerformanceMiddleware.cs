using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Emistr.Common.Middleware;

/// <summary>
/// Middleware that measures and logs request performance metrics.
/// </summary>
public class PerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly PerformanceOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="PerformanceMiddleware"/>.
    /// </summary>
    public PerformanceMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMiddleware> logger,
        PerformanceOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new PerformanceOptions();
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || ShouldSkip(context))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.ToString();
        var requestMethod = context.Request.Method;

        // Register callback to add header before response starts
        context.Response.OnStarting(state =>
        {
            var (ctx, sw) = ((HttpContext, Stopwatch))state;
            try
            {
                // Only add header if response headers are still writable
                if (!ctx.Response.HasStarted && !ctx.Response.Headers.IsReadOnly)
                {
                    ctx.Response.Headers["X-Response-Time-Ms"] = sw.ElapsedMilliseconds.ToString();
                }
            }
            catch (InvalidOperationException)
            {
                // Headers already sent (WebSocket/SignalR), ignore
            }
            return Task.CompletedTask;
        }, (context, stopwatch));

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;


            // Log based on thresholds
            if (elapsed >= _options.CriticalThresholdMs)
            {
                _logger.LogError(
                    "CRITICAL: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                    requestMethod, requestPath, elapsed, _options.CriticalThresholdMs);
            }
            else if (elapsed >= _options.WarningThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                    requestMethod, requestPath, elapsed, _options.WarningThresholdMs);
            }
            else if (_options.LogAllRequests)
            {
                _logger.LogDebug(
                    "{Method} {Path} completed in {ElapsedMs}ms",
                    requestMethod, requestPath, elapsed);
            }

            // Record metrics
            PerformanceMetrics.RecordRequest(requestPath, requestMethod, elapsed, context.Response.StatusCode);
        }
    }

    private bool ShouldSkip(HttpContext context)
    {
        var path = context.Request.Path.ToString().ToLowerInvariant();
        
        // Skip static files and health checks by default
        foreach (var skipPath in _options.SkipPaths)
        {
            if (path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Configuration options for performance middleware.
/// </summary>
public class PerformanceOptions
{
    /// <summary>
    /// Whether performance monitoring is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold in milliseconds for warning log. Default: 500ms.
    /// </summary>
    public int WarningThresholdMs { get; set; } = 500;

    /// <summary>
    /// Threshold in milliseconds for critical log. Default: 2000ms.
    /// </summary>
    public int CriticalThresholdMs { get; set; } = 2000;

    /// <summary>
    /// Whether to log all requests (at Debug level). Default: false.
    /// </summary>
    public bool LogAllRequests { get; set; } = false;

    /// <summary>
    /// Paths to skip from performance monitoring.
    /// </summary>
    public List<string> SkipPaths { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/hub",
        "/favicon.ico",
        "/_framework",
        "/css",
        "/js",
        "/lib"
    };
}

/// <summary>
/// In-memory performance metrics collector.
/// </summary>
public static class PerformanceMetrics
{
    private static long _totalRequests;
    private static long _totalTimeMs;
    private static long _slowRequests;
    private static long _errorRequests;
    private static readonly object _lock = new();
    private static readonly Dictionary<string, EndpointMetrics> _endpointMetrics = new();

    /// <summary>
    /// Records a request completion.
    /// </summary>
    public static void RecordRequest(string path, string method, long elapsedMs, int statusCode)
    {
        lock (_lock)
        {
            _totalRequests++;
            _totalTimeMs += elapsedMs;

            if (elapsedMs > 500) _slowRequests++;
            if (statusCode >= 500) _errorRequests++;

            var key = $"{method}:{NormalizePath(path)}";
            if (!_endpointMetrics.TryGetValue(key, out var metrics))
            {
                metrics = new EndpointMetrics { Path = key };
                _endpointMetrics[key] = metrics;
            }

            metrics.RequestCount++;
            metrics.TotalTimeMs += elapsedMs;
            if (elapsedMs > metrics.MaxTimeMs) metrics.MaxTimeMs = elapsedMs;
            if (elapsedMs < metrics.MinTimeMs || metrics.MinTimeMs == 0) metrics.MinTimeMs = elapsedMs;
        }
    }

    /// <summary>
    /// Gets summary of performance metrics.
    /// </summary>
    public static PerformanceSummary GetSummary()
    {
        lock (_lock)
        {
            return new PerformanceSummary
            {
                TotalRequests = _totalRequests,
                AverageTimeMs = _totalRequests > 0 ? _totalTimeMs / _totalRequests : 0,
                SlowRequests = _slowRequests,
                ErrorRequests = _errorRequests,
                TopEndpoints = _endpointMetrics.Values
                    .OrderByDescending(m => m.RequestCount)
                    .Take(10)
                    .ToList()
            };
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _totalRequests = 0;
            _totalTimeMs = 0;
            _slowRequests = 0;
            _errorRequests = 0;
            _endpointMetrics.Clear();
        }
    }

    private static string NormalizePath(string path)
    {
        // Normalize paths with IDs like /api/users/123 -> /api/users/{id}
        var segments = path.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            if (Guid.TryParse(segments[i], out _) || 
                (int.TryParse(segments[i], out _) && segments[i].Length < 10))
            {
                segments[i] = "{id}";
            }
        }
        return string.Join("/", segments);
    }
}

/// <summary>
/// Metrics for a specific endpoint.
/// </summary>
public class EndpointMetrics
{
    public string Path { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public long TotalTimeMs { get; set; }
    public long MinTimeMs { get; set; }
    public long MaxTimeMs { get; set; }
    public double AverageTimeMs => RequestCount > 0 ? (double)TotalTimeMs / RequestCount : 0;
}

/// <summary>
/// Summary of performance metrics.
/// </summary>
public class PerformanceSummary
{
    public long TotalRequests { get; set; }
    public long AverageTimeMs { get; set; }
    public long SlowRequests { get; set; }
    public long ErrorRequests { get; set; }
    public List<EndpointMetrics> TopEndpoints { get; set; } = new();
}

/// <summary>
/// Extension methods for performance middleware.
/// </summary>
public static class PerformanceExtensions
{
    /// <summary>
    /// Adds performance monitoring middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(
        this IApplicationBuilder app, 
        PerformanceOptions? options = null)
    {
        return app.UseMiddleware<PerformanceMiddleware>(options ?? new PerformanceOptions());
    }
}


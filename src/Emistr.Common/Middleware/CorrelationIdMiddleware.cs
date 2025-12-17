using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emistr.Common.Middleware;

/// <summary>
/// Middleware that adds correlation ID to all HTTP requests for distributed tracing.
/// The correlation ID is propagated through the X-Correlation-ID header and added to the logging scope.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// // In Program.cs
/// builder.Services.AddCorrelationId();
/// // ...
/// app.UseCorrelationId();
/// </code>
/// </remarks>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// HTTP header name for correlation ID.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// Property name used to store correlation ID in HttpContext.Items and logging scope.
    /// </summary>
    public const string CorrelationIdProperty = "CorrelationId";

    /// <summary>
    /// Initializes a new instance of <see cref="CorrelationIdMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip SignalR/WebSocket endpoints
        var path = context.Request.Path.ToString();
        if (path.StartsWith("/hub", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var correlationId = GetOrCreateCorrelationId(context);

        // Add to response headers (only if response hasn't started)
        context.Response.OnStarting(state =>
        {
            var (ctx, corrId) = ((HttpContext, string))state;
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Headers.TryAdd(CorrelationIdHeader, corrId);
            }
            return Task.CompletedTask;
        }, (context, correlationId));

        // Add to logging scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdProperty] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString(),
            ["RequestMethod"] = context.Request.Method
        }))
        {
            // Set Activity for distributed tracing
            Activity.Current?.SetTag("correlation.id", correlationId);

            // Store in HttpContext for access in services
            context.Items[CorrelationIdProperty] = correlationId;

            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString("N")[..16];
    }
}

/// <summary>
/// Service interface for accessing the current correlation ID.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the correlation ID for the current request, or null if not available.
    /// </summary>
    string? CorrelationId { get; }
}

/// <summary>
/// HttpContext-based implementation of <see cref="ICorrelationIdAccessor"/>.
/// </summary>
public class HttpContextCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="HttpContextCorrelationIdAccessor"/>.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string? CorrelationId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdProperty, out var correlationId) == true)
            {
                return correlationId?.ToString();
            }
            return null;
        }
    }
}

/// <summary>
/// Extension methods for correlation ID middleware registration.
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Adds correlation ID services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
        return services;
    }

    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}


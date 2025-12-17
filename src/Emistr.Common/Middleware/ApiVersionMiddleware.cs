using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Emistr.Common.Middleware;

/// <summary>
/// API versioning constants and configuration.
/// </summary>
public static class ApiVersioning
{
    /// <summary>
    /// Current API version.
    /// </summary>
    public const string CurrentVersion = "1.0";

    /// <summary>
    /// Array of all supported API versions.
    /// </summary>
    public static readonly string[] SupportedVersions = { "1.0" };

    /// <summary>
    /// HTTP header name for API version.
    /// </summary>
    public const string VersionHeader = "X-API-Version";

    /// <summary>
    /// Query string parameter name for API version.
    /// </summary>
    public const string VersionQueryParam = "api-version";

    /// <summary>
    /// Route prefix for v1 API endpoints.
    /// </summary>
    public const string V1Prefix = "/api/v1";
}

/// <summary>
/// Middleware that handles API versioning through headers or query parameters.
/// </summary>
/// <remarks>
/// Clients can specify the API version using either:
/// <list type="bullet">
/// <item><description>X-API-Version HTTP header</description></item>
/// <item><description>api-version query parameter</description></item>
/// </list>
/// If no version is specified, the current version is assumed.
/// Unsupported versions result in HTTP 400 Bad Request.
/// </remarks>
public class ApiVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiVersionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiVersionMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public ApiVersionMiddleware(RequestDelegate next, ILogger<ApiVersionMiddleware> logger)
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
        // Add current API version to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(ApiVersioning.VersionHeader, ApiVersioning.CurrentVersion);
            context.Response.Headers.TryAdd("X-API-Supported-Versions",
                string.Join(", ", ApiVersioning.SupportedVersions));
            return Task.CompletedTask;
        });

        var requestedVersion = GetRequestedVersion(context);

        if (!string.IsNullOrEmpty(requestedVersion) &&
            !ApiVersioning.SupportedVersions.Contains(requestedVersion))
        {
            _logger.LogWarning("Unsupported API version requested: {Version}", requestedVersion);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "UNSUPPORTED_API_VERSION",
                message = $"API version '{requestedVersion}' is not supported. Supported versions: {string.Join(", ", ApiVersioning.SupportedVersions)}",
                supportedVersions = ApiVersioning.SupportedVersions
            });
            return;
        }

        context.Items["ApiVersion"] = requestedVersion ?? ApiVersioning.CurrentVersion;

        await _next(context);
    }

    private static string? GetRequestedVersion(HttpContext context)
    {
        // Header takes precedence
        if (context.Request.Headers.TryGetValue(ApiVersioning.VersionHeader, out var headerVersion) &&
            !string.IsNullOrWhiteSpace(headerVersion))
        {
            return headerVersion.ToString();
        }

        // Fall back to query parameter
        if (context.Request.Query.TryGetValue(ApiVersioning.VersionQueryParam, out var queryVersion) &&
            !string.IsNullOrWhiteSpace(queryVersion))
        {
            return queryVersion.ToString();
        }

        return null;
    }
}

/// <summary>
/// Extension methods for API versioning middleware.
/// </summary>
public static class ApiVersionExtensions
{
    /// <summary>
    /// Adds the API versioning middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiVersionMiddleware>();
    }

    /// <summary>
    /// Gets the API version for the current request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The API version string.</returns>
    public static string GetApiVersion(this HttpContext context)
    {
        return context.Items["ApiVersion"]?.ToString() ?? ApiVersioning.CurrentVersion;
    }
}


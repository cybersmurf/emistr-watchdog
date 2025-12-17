using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Configuration.Validation;

/// <summary>
/// Extension methods for registering configuration validators.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Adds configuration validation for all Watchdog options.
    /// Validation runs at startup and throws if configuration is invalid.
    /// </summary>
    public static IServiceCollection AddConfigurationValidation(this IServiceCollection services)
    {
        // Register validators
        services.AddSingleton<IValidateOptions<WatchdogOptions>, WatchdogOptionsValidator>();
        services.AddSingleton<IValidateOptions<MultiTenantOptions>, MultiTenantOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Validates all configuration options at startup.
    /// Call this after building the app to fail fast on configuration errors.
    /// </summary>
    public static WebApplication ValidateConfiguration(this WebApplication app)
    {
        // Force validation by resolving IOptions<T> which triggers validation
        using var scope = app.Services.CreateScope();
        
        var watchdogOptions = scope.ServiceProvider.GetService<IOptions<WatchdogOptions>>();
        _ = watchdogOptions?.Value; // This triggers validation

        var multiTenantOptions = scope.ServiceProvider.GetService<IOptions<MultiTenantOptions>>();
        _ = multiTenantOptions?.Value; // This triggers validation

        return app;
    }
}

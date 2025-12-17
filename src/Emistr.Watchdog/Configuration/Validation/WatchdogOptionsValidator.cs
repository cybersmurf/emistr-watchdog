using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Configuration.Validation;

/// <summary>
/// Validates WatchdogOptions configuration at startup.
/// </summary>
public sealed class WatchdogOptionsValidator : IValidateOptions<WatchdogOptions>
{
    public ValidateOptionsResult Validate(string? name, WatchdogOptions options)
    {
        var errors = new List<string>();

        if (options.CheckIntervalSeconds <= 0)
        {
            errors.Add("Watchdog.CheckIntervalSeconds must be positive.");
        }

        if (options.CheckIntervalSeconds < 5)
        {
            errors.Add("Watchdog.CheckIntervalSeconds should be at least 5 seconds to avoid overloading services.");
        }

        ValidateServices(options.Services, errors);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateServices(ServicesOptions services, List<string> errors)
    {
        // Validate built-in MariaDB
        if (services.MariaDB.Enabled && string.IsNullOrWhiteSpace(services.MariaDB.ConnectionString))
        {
            errors.Add("Watchdog.Services.MariaDB: ConnectionString is required when enabled.");
        }

        // Validate built-in HTTP services
        ValidateHttpService("LicenseManager", services.LicenseManager, errors);
        ValidateHttpService("Apache", services.Apache, errors);

        // Validate built-in Telnet
        if (services.PracantD.Enabled)
        {
            if (string.IsNullOrWhiteSpace(services.PracantD.Host))
            {
                errors.Add("Watchdog.Services.PracantD: Host is required when enabled.");
            }
            if (services.PracantD.Port <= 0 || services.PracantD.Port > 65535)
            {
                errors.Add("Watchdog.Services.PracantD: Port must be between 1 and 65535.");
            }
        }

        // Validate built-in Redis
        if (services.Redis.Enabled && string.IsNullOrWhiteSpace(services.Redis.ConnectionString))
        {
            errors.Add("Watchdog.Services.Redis: ConnectionString is required when enabled.");
        }

        // Validate built-in RabbitMQ
        if (services.RabbitMQ.Enabled)
        {
            if (string.IsNullOrWhiteSpace(services.RabbitMQ.Host))
            {
                errors.Add("Watchdog.Services.RabbitMQ: Host is required when enabled.");
            }
        }

        // Validate built-in Elasticsearch
        if (services.Elasticsearch.Enabled)
        {
            if (string.IsNullOrWhiteSpace(services.Elasticsearch.Url))
            {
                errors.Add("Watchdog.Services.Elasticsearch: Url is required when enabled.");
            }
        }

        // Validate custom services
        ValidateCustomMariaDbServices(services.CustomMariaDbServices, errors);
        ValidateCustomHttpServices(services.CustomHttpServices, errors);
        ValidateCustomTelnetServices(services.CustomTelnetServices, errors);
        ValidateCustomRedisServices(services.CustomRedisServices, errors);
        ValidateCustomRabbitMqServices(services.CustomRabbitMqServices, errors);
        ValidateCustomElasticsearchServices(services.CustomElasticsearchServices, errors);
    }

    private static void ValidateHttpService(string name, HttpServiceOptions options, List<string> errors)
    {
        if (!options.Enabled) return;

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            errors.Add($"Watchdog.Services.{name}: Url is required when enabled.");
        }
        else if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add($"Watchdog.Services.{name}: Invalid URL format '{options.Url}'.");
        }
    }

    private static void ValidateCustomMariaDbServices(Dictionary<string, MariaDbOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                errors.Add($"Watchdog.Services.CustomMariaDbServices.{serviceName}: ConnectionString is required.");
            }
        }
    }

    private static void ValidateCustomHttpServices(Dictionary<string, HttpServiceOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Url))
            {
                errors.Add($"Watchdog.Services.CustomHttpServices.{serviceName}: Url is required.");
            }
            else if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"Watchdog.Services.CustomHttpServices.{serviceName}: Invalid URL format '{options.Url}'.");
            }
        }
    }

    private static void ValidateCustomTelnetServices(Dictionary<string, TelnetServiceOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add($"Watchdog.Services.CustomTelnetServices.{serviceName}: Host is required.");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                errors.Add($"Watchdog.Services.CustomTelnetServices.{serviceName}: Port must be between 1 and 65535.");
            }
        }
    }

    private static void ValidateCustomRedisServices(Dictionary<string, RedisOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                errors.Add($"Watchdog.Services.CustomRedisServices.{serviceName}: ConnectionString is required.");
            }
        }
    }

    private static void ValidateCustomRabbitMqServices(Dictionary<string, RabbitMqOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add($"Watchdog.Services.CustomRabbitMqServices.{serviceName}: Host is required.");
            }
        }
    }

    private static void ValidateCustomElasticsearchServices(Dictionary<string, ElasticsearchOptions> services, List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Url))
            {
                errors.Add($"Watchdog.Services.CustomElasticsearchServices.{serviceName}: Url is required.");
            }
        }
    }
}

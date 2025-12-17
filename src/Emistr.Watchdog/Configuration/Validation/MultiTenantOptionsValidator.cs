using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Configuration.Validation;

/// <summary>
/// Validates MultiTenantOptions configuration at startup.
/// </summary>
public sealed class MultiTenantOptionsValidator : IValidateOptions<MultiTenantOptions>
{
    public ValidateOptionsResult Validate(string? name, MultiTenantOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (options.Tenants.Count == 0)
        {
            errors.Add("Multi-tenant mode is enabled but no tenants are configured.");
        }

        foreach (var (tenantName, tenant) in options.Tenants)
        {
            if (string.IsNullOrWhiteSpace(tenantName))
            {
                errors.Add("Tenant name cannot be empty.");
                continue;
            }

            if (!tenant.Enabled)
            {
                continue; // Skip validation for disabled tenants
            }

            ValidateTenant(tenantName, tenant, errors);
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateTenant(string tenantName, TenantOptions tenant, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(tenant.DisplayName))
        {
            errors.Add($"Tenant '{tenantName}': DisplayName is required.");
        }

        ValidateMariaDbServices(tenantName, tenant.Services.MariaDb, errors);
        ValidateHttpServices(tenantName, tenant.Services.Http, errors);
        ValidateTelnetServices(tenantName, tenant.Services.Telnet, errors);
        ValidateRedisServices(tenantName, tenant.Services.Redis, errors);
        ValidateRabbitMqServices(tenantName, tenant.Services.RabbitMq, errors);
        ValidateElasticsearchServices(tenantName, tenant.Services.Elasticsearch, errors);
    }

    private static void ValidateMariaDbServices(
        string tenantName,
        Dictionary<string, MariaDbOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                errors.Add($"Tenant '{tenantName}', MariaDb '{serviceName}': ConnectionString is required.");
            }

            if (options.TimeoutSeconds <= 0)
            {
                errors.Add($"Tenant '{tenantName}', MariaDb '{serviceName}': TimeoutSeconds must be positive.");
            }

            if (options.CriticalAfterFailures <= 0)
            {
                errors.Add($"Tenant '{tenantName}', MariaDb '{serviceName}': CriticalAfterFailures must be positive.");
            }
        }
    }

    private static void ValidateHttpServices(
        string tenantName,
        Dictionary<string, HttpServiceOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Url))
            {
                errors.Add($"Tenant '{tenantName}', Http '{serviceName}': Url is required.");
            }
            else if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"Tenant '{tenantName}', Http '{serviceName}': Invalid URL format '{options.Url}'.");
            }

            if (options.TimeoutSeconds <= 0)
            {
                errors.Add($"Tenant '{tenantName}', Http '{serviceName}': TimeoutSeconds must be positive.");
            }
        }
    }

    private static void ValidateTelnetServices(
        string tenantName,
        Dictionary<string, TelnetServiceOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add($"Tenant '{tenantName}', Telnet '{serviceName}': Host is required.");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                errors.Add($"Tenant '{tenantName}', Telnet '{serviceName}': Port must be between 1 and 65535.");
            }
        }
    }

    private static void ValidateRedisServices(
        string tenantName,
        Dictionary<string, RedisOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                errors.Add($"Tenant '{tenantName}', Redis '{serviceName}': ConnectionString is required.");
            }
        }
    }

    private static void ValidateRabbitMqServices(
        string tenantName,
        Dictionary<string, RabbitMqOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add($"Tenant '{tenantName}', RabbitMq '{serviceName}': Host is required.");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                errors.Add($"Tenant '{tenantName}', RabbitMq '{serviceName}': Port must be between 1 and 65535.");
            }
        }
    }

    private static void ValidateElasticsearchServices(
        string tenantName,
        Dictionary<string, ElasticsearchOptions> services,
        List<string> errors)
    {
        foreach (var (serviceName, options) in services)
        {
            if (!options.Enabled) continue;

            if (string.IsNullOrWhiteSpace(options.Url))
            {
                errors.Add($"Tenant '{tenantName}', Elasticsearch '{serviceName}': Url is required.");
            }
            else if (!Uri.TryCreate(options.Url, UriKind.Absolute, out _))
            {
                errors.Add($"Tenant '{tenantName}', Elasticsearch '{serviceName}': Invalid URL '{options.Url}'.");
            }
        }
    }
}

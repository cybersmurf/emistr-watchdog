using Emistr.Watchdog.Configuration;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Configuration;

/// <summary>
/// Validates Watchdog configuration at startup.
/// Prevents misconfigured services from starting.
/// </summary>
public class WatchdogOptionsValidator : IValidateOptions<WatchdogOptions>
{
    public ValidateOptionsResult Validate(string? name, WatchdogOptions options)
    {
        var errors = new List<string>();

        // Validate check interval
        if (options.CheckIntervalSeconds < 5)
        {
            errors.Add("CheckIntervalSeconds must be at least 5 seconds");
        }

        if (options.CheckIntervalSeconds > 3600)
        {
            errors.Add("CheckIntervalSeconds must not exceed 3600 seconds (1 hour)");
        }

        // Validate services
        if (options.Services != null)
        {
            ValidateServiceConfigurations(options.Services, errors);
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateServiceConfigurations(ServicesOptions services, List<string> errors)
    {
        // Validate MariaDB services
        ValidateMariaDb(services.MariaDB, "MariaDB", errors);

        // Validate HTTP services
        ValidateHttp(services.LicenseManager, "LicenseManager", errors);
        ValidateHttp(services.Apache, "Apache", errors);

        // Validate Telnet services
        ValidateTelnet(services.PracantD, "PracantD", errors);

        // Validate Background Service
        ValidateBackgroundService(services.BackgroundService, "BackgroundService", errors);

        // Validate additional services in dictionaries
        if (services.CustomMariaDbServices != null)
        {
            foreach (var (key, value) in services.CustomMariaDbServices)
            {
                ValidateMariaDb(value, $"CustomMariaDbServices.{key}", errors);
            }
        }

        if (services.CustomHttpServices != null)
        {
            foreach (var (key, value) in services.CustomHttpServices)
            {
                ValidateHttp(value, $"CustomHttpServices.{key}", errors);
            }
        }

        if (services.CustomTelnetServices != null)
        {
            foreach (var (key, value) in services.CustomTelnetServices)
            {
                ValidateTelnet(value, $"CustomTelnetServices.{key}", errors);
            }
        }
    }

    private static void ValidateMariaDb(MariaDbOptions? options, string name, List<string> errors)
    {
        if (options == null || !options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            errors.Add($"{name}: ConnectionString is required when enabled");
        }
        else if (!options.ConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{name}: ConnectionString must contain Server=");
        }

        if (options.TimeoutSeconds <= 0)
        {
            errors.Add($"{name}: TimeoutSeconds must be greater than 0");
        }

        if (options.TimeoutSeconds > 120)
        {
            errors.Add($"{name}: TimeoutSeconds should not exceed 120 seconds");
        }

        if (options.CriticalAfterFailures <= 0)
        {
            errors.Add($"{name}: CriticalAfterFailures must be greater than 0");
        }
    }

    private static void ValidateHttp(HttpServiceOptions? options, string name, List<string> errors)
    {
        if (options == null || !options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.Url))
        {
            errors.Add($"{name}: URL is required when enabled");
        }
        else if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri))
        {
            errors.Add($"{name}: URL '{options.Url}' is not a valid absolute URI");
        }
        else if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            errors.Add($"{name}: URL must use http or https scheme");
        }

        if (options.TimeoutSeconds <= 0)
        {
            errors.Add($"{name}: TimeoutSeconds must be greater than 0");
        }

        if (options.ExpectedStatusCodes == null || options.ExpectedStatusCodes.Length == 0)
        {
            errors.Add($"{name}: ExpectedStatusCodes must contain at least one status code");
        }
        else
        {
            foreach (var code in options.ExpectedStatusCodes)
            {
                if (code < 100 || code > 599)
                {
                    errors.Add($"{name}: Invalid HTTP status code {code}");
                }
            }
        }
    }

    private static void ValidateTelnet(TelnetServiceOptions? options, string name, List<string> errors)
    {
        if (options == null || !options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            errors.Add($"{name}: Host is required when enabled");
        }

        if (options.Port <= 0 || options.Port > 65535)
        {
            errors.Add($"{name}: Port must be between 1 and 65535");
        }

        if (options.TimeoutSeconds <= 0)
        {
            errors.Add($"{name}: TimeoutSeconds must be greater than 0");
        }
    }

    private static void ValidateBackgroundService(BackgroundServiceOptions? options, string name, List<string> errors)
    {
        if (options == null || !options.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            errors.Add($"{name}: DatabaseName is required when enabled");
        }

        if (options.MaxAgeMinutes <= 0)
        {
            errors.Add($"{name}: MaxAgeMinutes must be greater than 0");
        }

        if (options.MaxAgeMinutes > 1440) // 24 hours
        {
            errors.Add($"{name}: MaxAgeMinutes should not exceed 1440 (24 hours)");
        }
    }
}

/// <summary>
/// Validates Dashboard configuration at startup.
/// </summary>
public class DashboardOptionsValidator : IValidateOptions<DashboardOptions>
{
    public ValidateOptionsResult Validate(string? name, DashboardOptions options)
    {
        var errors = new List<string>();

        if (options.Port <= 0 || options.Port > 65535)
        {
            errors.Add("Dashboard Port must be between 1 and 65535");
        }

        if (options.UpdateIntervalSeconds < 1)
        {
            errors.Add("Dashboard UpdateIntervalSeconds must be at least 1 second");
        }

        if (options.UpdateIntervalSeconds > 300)
        {
            errors.Add("Dashboard UpdateIntervalSeconds should not exceed 300 seconds");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Validates Email notification configuration.
/// </summary>
public class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var errors = new List<string>();

        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            errors.Add("Email: FromAddress is required when enabled");
        }
        else if (!IsValidEmail(options.FromAddress))
        {
            errors.Add($"Email: FromAddress '{options.FromAddress}' is not a valid email address");
        }

        if (options.Recipients == null || options.Recipients.Count == 0)
        {
            errors.Add("Email: At least one recipient is required when enabled");
        }
        else
        {
            foreach (var recipient in options.Recipients)
            {
                if (!IsValidEmail(recipient))
                {
                    errors.Add($"Email: Recipient '{recipient}' is not a valid email address");
                }
            }
        }

        // Provider-specific validation based on enum
        switch (options.Provider)
        {
            case EmailProvider.Smtp:
                if (string.IsNullOrWhiteSpace(options.SmtpHost))
                    errors.Add("Email: SmtpHost is required for SMTP provider");
                if (options.SmtpPort <= 0 || options.SmtpPort > 65535)
                    errors.Add("Email: SmtpPort must be between 1 and 65535");
                break;

            case EmailProvider.SendGrid:
            case EmailProvider.Mailchimp:
            case EmailProvider.Mailgun:
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                    errors.Add("Email: ApiKey is required for this provider");
                break;

            case EmailProvider.Microsoft365:
                if (string.IsNullOrWhiteSpace(options.TenantId))
                    errors.Add("Email: TenantId is required for Microsoft 365 provider");
                if (string.IsNullOrWhiteSpace(options.ClientId))
                    errors.Add("Email: ClientId is required for Microsoft 365 provider");
                if (string.IsNullOrWhiteSpace(options.ClientSecret))
                    errors.Add("Email: ClientSecret is required for Microsoft 365 provider");
                break;
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}


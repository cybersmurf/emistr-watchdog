using System.Text.Json;
using Emistr.Watchdog.Configuration;
using Microsoft.Extensions.Options;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// API endpoints for configuration management.
/// Allows adding, editing, and removing services via web UI.
/// </summary>
public static class ConfigurationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Runtime priority cache (no restart needed)
    private static readonly Dictionary<string, bool> RuntimePriorityOverrides = new();

    public static void MapConfigurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/config");

        // Services
        group.MapGet("/services", GetServices);
        group.MapPost("/services", AddService);
        group.MapPut("/services/{serviceName}", UpdateService);
        group.MapDelete("/services/{serviceName}", DeleteService);
        
        // Priority (no restart needed)
        group.MapPut("/services/{serviceName}/priority", SetServicePriority);

        // Notifications
        group.MapGet("/notifications", GetNotifications);
        group.MapPut("/notifications", UpdateNotifications);

        // General settings
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);

        // Export/Import
        group.MapGet("/export", ExportConfig);
        group.MapPost("/import", ImportConfig);
    }

    /// <summary>
    /// Gets the runtime priority for a service (includes overrides).
    /// </summary>
    public static bool GetRuntimePriority(string serviceName, bool configPriority)
    {
        var normalizedName = serviceName.ToLowerInvariant();
        if (RuntimePriorityOverrides.TryGetValue(normalizedName, out var priority))
        {
            return priority;
        }
        return configPriority;
    }

    /// <summary>
    /// Sets the runtime priority for a service (called from V2 API).
    /// </summary>
    public static void SetRuntimePriority(string serviceName, bool prioritized)
    {
        var normalizedName = serviceName.ToLowerInvariant();
        RuntimePriorityOverrides[normalizedName] = prioritized;
    }

    private static async Task<IResult> SetServicePriority(
        string serviceName,
        PriorityUpdateDto dto,
        ILogger<Program> logger)
    {
        var normalizedName = serviceName.ToLowerInvariant();
        
        // Update runtime cache (immediate effect)
        RuntimePriorityOverrides[normalizedName] = dto.Prioritized;
        
        logger.LogInformation("Runtime priority cache updated: {ServiceName} = {Priority} (cache size: {Size})", 
            normalizedName, dto.Prioritized, RuntimePriorityOverrides.Count);
        
        // Also save to config file for persistence
        try
        {
            var configPath = GetConfigPath();
            logger.LogDebug("Config path: {Path}", configPath);
            
            if (configPath != null)
            {
                var json = await File.ReadAllTextAsync(configPath);
                using var doc = JsonDocument.Parse(json);
                var newConfig = UpdateServicePriorityInConfig(doc.RootElement, serviceName, dto.Prioritized);
                await File.WriteAllTextAsync(configPath, newConfig);
                logger.LogInformation("Priority persisted to config file for {ServiceName}", serviceName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist priority to config file");
        }

        return Results.Ok(new { 
            message = $"Priority updated for {serviceName}", 
            prioritized = dto.Prioritized,
            requiresRestart = false 
        });
    }

    private static string UpdateServicePriorityInConfig(JsonElement root, string serviceName, bool prioritized)
    {
        var json = root.GetRawText();
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        
        if (!dict.ContainsKey("Watchdog")) return json;

        var watchdog = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dict["Watchdog"].GetRawText())!;
        
        if (!watchdog.ContainsKey("Services")) return json;
        
        var services = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(watchdog["Services"].GetRawText())!;
        
        // Map service names to config keys
        var serviceNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mariadb"] = "MariaDB",
            ["license-manager"] = "LicenseManager",
            ["apache"] = "Apache",
            ["pracantd"] = "PracantD",
            ["background-service"] = "BackgroundService"
        };
        
        // Try to find and update the service
        string? configKey = null;
        
        // Check if it's a built-in service
        if (serviceNameMap.TryGetValue(serviceName, out var mapped))
        {
            configKey = mapped;
        }
        else
        {
            // Check custom services
            var customArrays = new[] { "CustomHttpServices", "CustomMariaDbServices", "CustomTelnetServices", "CustomBackgroundServices" };
            foreach (var arrayName in customArrays)
            {
                if (services.TryGetValue(arrayName, out var customServices))
                {
                    var customDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customServices.GetRawText());
                    if (customDict?.ContainsKey(serviceName) == true)
                    {
                        // Update custom service
                        var svcDict = JsonSerializer.Deserialize<Dictionary<string, object>>(customDict[serviceName].GetRawText())!;
                        svcDict["Prioritized"] = prioritized;
                        customDict[serviceName] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(svcDict));
                        services[arrayName] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(customDict));
                        
                        watchdog["Services"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(services));
                        dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));
                        return JsonSerializer.Serialize(dict, JsonOptions);
                    }
                }
            }
        }
        
        // Update built-in service
        if (configKey != null && services.TryGetValue(configKey, out var serviceElement))
        {
            var serviceDict = JsonSerializer.Deserialize<Dictionary<string, object>>(serviceElement.GetRawText())!;
            serviceDict["Prioritized"] = prioritized;
            services[configKey] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(serviceDict));
            
            watchdog["Services"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(services));
            dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));
            return JsonSerializer.Serialize(dict, JsonOptions);
        }

        return json;
    }

    #region Services

    private static IResult GetServices(IOptions<WatchdogOptions> options)
    {
        var services = new List<ServiceConfigDto>();
        var svc = options.Value.Services;

        // Built-in MariaDB
        if (svc.MariaDB is { } db)
        {
            services.Add(new ServiceConfigDto
            {
                Name = "mariadb",
                DisplayName = db.DisplayName ?? "MariaDB",
                Type = "MariaDB",
                Enabled = db.Enabled,
                Prioritized = GetRuntimePriority("mariadb", db.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["connectionString"] = MaskConnectionString(db.ConnectionString),
                    ["timeoutSeconds"] = db.TimeoutSeconds,
                    ["criticalAfterFailures"] = db.CriticalAfterFailures
                }
            });
        }

        // Custom MariaDB services
        foreach (var (name, db2) in svc.CustomMariaDbServices ?? [])
        {
            services.Add(new ServiceConfigDto
            {
                Name = name,
                DisplayName = db2.DisplayName ?? name,
                Type = "MariaDB",
                Enabled = db2.Enabled,
                Prioritized = GetRuntimePriority(name, db2.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["connectionString"] = MaskConnectionString(db2.ConnectionString),
                    ["timeoutSeconds"] = db2.TimeoutSeconds,
                    ["criticalAfterFailures"] = db2.CriticalAfterFailures
                }
            });
        }

        // Built-in HTTP services
        if (svc.LicenseManager is { } lm)
        {
            services.Add(new ServiceConfigDto
            {
                Name = "license-manager",
                DisplayName = lm.DisplayName ?? "License Manager",
                Type = "HTTP",
                Enabled = lm.Enabled,
                Prioritized = GetRuntimePriority("license-manager", lm.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["url"] = lm.Url,
                    ["expectedStatusCodes"] = lm.ExpectedStatusCodes,
                    ["timeoutSeconds"] = lm.TimeoutSeconds
                }
            });
        }

        if (svc.Apache is { } apache)
        {
            services.Add(new ServiceConfigDto
            {
                Name = "apache",
                DisplayName = apache.DisplayName ?? "Apache",
                Type = "HTTP",
                Enabled = apache.Enabled,
                Prioritized = GetRuntimePriority("apache", apache.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["url"] = apache.Url,
                    ["expectedStatusCodes"] = apache.ExpectedStatusCodes,
                    ["timeoutSeconds"] = apache.TimeoutSeconds
                }
            });
        }

        // Custom HTTP services
        foreach (var (name, http) in svc.CustomHttpServices ?? [])
        {
            services.Add(new ServiceConfigDto
            {
                Name = name,
                DisplayName = http.DisplayName ?? name,
                Type = "HTTP",
                Enabled = http.Enabled,
                Prioritized = GetRuntimePriority(name, http.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["url"] = http.Url,
                    ["expectedStatusCodes"] = http.ExpectedStatusCodes,
                    ["timeoutSeconds"] = http.TimeoutSeconds
                }
            });
        }

        // Built-in Telnet
        if (svc.PracantD is { } telnet)
        {
            services.Add(new ServiceConfigDto
            {
                Name = "pracantd",
                DisplayName = telnet.DisplayName ?? "PracantD",
                Type = "Telnet",
                Enabled = telnet.Enabled,
                Prioritized = GetRuntimePriority("pracantd", telnet.Prioritized),
                Config = new Dictionary<string, object?>
                {
                    ["host"] = telnet.Host,
                    ["port"] = telnet.Port,
                    ["timeoutSeconds"] = telnet.TimeoutSeconds
                }
            });
        }

        // Custom Telnet services
        foreach (var (name, tel) in svc.CustomTelnetServices ?? [])
        {
            services.Add(new ServiceConfigDto
            {
                Name = name,
                DisplayName = tel.DisplayName ?? name,
                Type = "Telnet",
                Enabled = tel.Enabled,
                Config = new Dictionary<string, object?>
                {
                    ["host"] = tel.Host,
                    ["port"] = tel.Port,
                    ["timeoutSeconds"] = tel.TimeoutSeconds
                }
            });
        }

        // Built-in Background Service
        if (svc.BackgroundService is { } bg)
        {
            services.Add(new ServiceConfigDto
            {
                Name = "background-service",
                DisplayName = bg.DisplayName ?? "Background Service",
                Type = "BackgroundService",
                Enabled = bg.Enabled,
                Config = new Dictionary<string, object?>
                {
                    ["databaseName"] = bg.DatabaseName,
                    ["timeoutSeconds"] = bg.TimeoutSeconds
                }
            });
        }

        // Custom Background services
        foreach (var (name, bgSvc) in svc.CustomBackgroundServices ?? [])
        {
            services.Add(new ServiceConfigDto
            {
                Name = name,
                DisplayName = bgSvc.DisplayName ?? name,
                Type = "BackgroundService",
                Enabled = bgSvc.Enabled,
                Config = new Dictionary<string, object?>
                {
                    ["databaseName"] = bgSvc.DatabaseName,
                    ["timeoutSeconds"] = bgSvc.TimeoutSeconds
                }
            });
        }

        // Ping services
        foreach (var (name, ping) in svc.PingServices ?? [])
        {
            services.Add(new ServiceConfigDto
            {
                Name = name,
                DisplayName = ping.DisplayName ?? name,
                Type = "Ping",
                Enabled = ping.Enabled,
                Config = new Dictionary<string, object?>
                {
                    ["host"] = ping.Host,
                    ["timeoutSeconds"] = ping.TimeoutSeconds,
                    ["pingCount"] = ping.PingCount
                }
            });
        }

        return Results.Ok(services);
    }

    private static async Task<IResult> AddService(
        ServiceConfigDto dto,
        IConfiguration configuration,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var newConfig = AddServiceToConfig(root, dto);
            await File.WriteAllTextAsync(configPath, newConfig);

            logger.LogInformation("Service {ServiceName} added via UI", dto.Name);

            return Results.Ok(new { message = "Service added. Restart required to apply changes.", requiresRestart = true });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateService(
        string serviceName,
        ServiceConfigDto dto,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var newConfig = UpdateServiceInConfig(root, serviceName, dto);
            await File.WriteAllTextAsync(configPath, newConfig);

            logger.LogInformation("Service {ServiceName} updated via UI", serviceName);

            return Results.Ok(new { message = "Service updated. Restart required to apply changes.", requiresRestart = true });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteService(
        string serviceName,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var newConfig = RemoveServiceFromConfig(root, serviceName);
            await File.WriteAllTextAsync(configPath, newConfig);

            logger.LogInformation("Service {ServiceName} deleted via UI", serviceName);

            return Results.Ok(new { message = "Service deleted. Restart required to apply changes.", requiresRestart = true });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Notifications

    private static IResult GetNotifications(IOptions<NotificationOptions> options)
    {
        var notif = options.Value;
        return Results.Ok(new NotificationConfigDto
        {
            EmailEnabled = notif.Email?.Enabled ?? false,
            EmailTo = string.Join(", ", notif.Email?.Recipients ?? []),
            EmailSubjectPrefix = "[Watchdog]",
            WebhookEnabled = notif.Teams?.Enabled ?? notif.Slack?.Enabled ?? notif.Discord?.Enabled ?? notif.GenericWebhook?.Enabled ?? false,
            WebhookUrl = notif.Teams?.WebhookUrl ?? notif.Slack?.WebhookUrl ?? notif.Discord?.WebhookUrl ?? notif.GenericWebhook?.Url,
            WebhookType = GetActiveWebhookType(notif)
        });
    }

    private static string GetActiveWebhookType(NotificationOptions notif)
    {
        if (notif.Teams?.Enabled == true) return "Teams";
        if (notif.Slack?.Enabled == true) return "Slack";
        if (notif.Discord?.Enabled == true) return "Discord";
        if (notif.GenericWebhook?.Enabled == true) return "Generic";
        return "Generic";
    }

    private static async Task<IResult> UpdateNotifications(
        NotificationConfigDto dto,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var newConfig = UpdateNotificationsInConfig(root, dto);
            await File.WriteAllTextAsync(configPath, newConfig);

            logger.LogInformation("Notifications updated via UI");

            return Results.Ok(new { message = "Notifications updated. Restart required to apply changes.", requiresRestart = true });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Settings

    private static IResult GetSettings(IOptions<WatchdogOptions> options)
    {
        return Results.Ok(new GeneralSettingsDto
        {
            DefaultCheckIntervalSeconds = options.Value.CheckIntervalSeconds,
            DefaultCriticalAfterFailures = 3, // Default value
            HistoryRetentionDays = 7 // Default value
        });
    }

    private static async Task<IResult> UpdateSettings(
        GeneralSettingsDto dto,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            var newConfig = UpdateGeneralSettingsInConfig(root, dto);
            await File.WriteAllTextAsync(configPath, newConfig);

            logger.LogInformation("General settings updated via UI");

            return Results.Ok(new { message = "Settings updated. Restart required to apply changes.", requiresRestart = true });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Export/Import

    private static async Task<IResult> ExportConfig()
    {
        var configPath = GetConfigPath();
        if (configPath == null)
        {
            return Results.BadRequest(new { error = "Configuration file not found" });
        }

        var json = await File.ReadAllTextAsync(configPath);
        return Results.Text(json, "application/json");
    }

    private static async Task<IResult> ImportConfig(
        HttpRequest request,
        ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Configuration file not found" });
            }

            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();

            // Validate JSON
            using var doc = JsonDocument.Parse(json);

            // Backup current config
            var backupPath = configPath + ".backup";
            File.Copy(configPath, backupPath, overwrite: true);

            await File.WriteAllTextAsync(configPath, json);

            logger.LogInformation("Configuration imported via UI");

            return Results.Ok(new { message = "Configuration imported. Restart required to apply changes.", requiresRestart = true });
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Helpers

    private static string? GetConfigPath()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var paths = new[]
        {
            $"appsettings.{env}.json",
            "appsettings.json"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        // Mask password in connection string
        var parts = connectionString.Split(';');
        var masked = parts.Select(p =>
        {
            if (p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase))
            {
                var key = p.Split('=')[0];
                return $"{key}=********";
            }
            return p;
        });
        return string.Join(";", masked);
    }

    private static string AddServiceToConfig(JsonElement root, ServiceConfigDto dto)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
        
        var watchdog = dict.ContainsKey("Watchdog") 
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(dict["Watchdog"].GetRawText())!
            : new Dictionary<string, object>();

        var serviceArrayName = dto.Type switch
        {
            "MariaDB" => "MariaDbServices",
            "HTTP" => "HttpServices",
            "Telnet" => "TelnetServices",
            "BackgroundService" => "BackgroundServices",
            "Ping" => "PingServices",
            _ => throw new ArgumentException($"Unknown service type: {dto.Type}")
        };

        var services = watchdog.ContainsKey(serviceArrayName)
            ? JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                JsonSerializer.Serialize(watchdog[serviceArrayName]))!
            : new List<Dictionary<string, object>>();

        var newService = new Dictionary<string, object>
        {
            ["Name"] = dto.Name,
            ["DisplayName"] = dto.DisplayName,
            ["Enabled"] = dto.Enabled,
            ["Prioritized"] = dto.Prioritized
        };

        foreach (var kvp in dto.Config)
        {
            if (kvp.Value != null)
            {
                newService[char.ToUpper(kvp.Key[0]) + kvp.Key[1..]] = kvp.Value;
            }
        }

        services.Add(newService);
        watchdog[serviceArrayName] = services;
        dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static string UpdateServiceInConfig(JsonElement root, string serviceName, ServiceConfigDto dto)
    {
        // For simplicity, remove old and add new
        var withoutOld = RemoveServiceFromConfig(root, serviceName);
        using var doc = JsonDocument.Parse(withoutOld);
        return AddServiceToConfig(doc.RootElement, dto);
    }

    private static string RemoveServiceFromConfig(JsonElement root, string serviceName)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
        
        if (!dict.ContainsKey("Watchdog"))
            return root.GetRawText();

        var watchdog = JsonSerializer.Deserialize<Dictionary<string, object>>(dict["Watchdog"].GetRawText())!;

        var serviceArrays = new[] { "MariaDbServices", "HttpServices", "TelnetServices", "BackgroundServices", "PingServices" };

        foreach (var arrayName in serviceArrays)
        {
            if (watchdog.ContainsKey(arrayName))
            {
                var services = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                    JsonSerializer.Serialize(watchdog[arrayName]))!;
                
                services.RemoveAll(s => 
                    s.ContainsKey("Name") && 
                    s["Name"]?.ToString()?.Equals(serviceName, StringComparison.OrdinalIgnoreCase) == true);
                
                watchdog[arrayName] = services;
            }
        }

        dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));
        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static string UpdateNotificationsInConfig(JsonElement root, NotificationConfigDto dto)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
        
        var watchdog = dict.ContainsKey("Watchdog") 
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(dict["Watchdog"].GetRawText())!
            : new Dictionary<string, object>();

        var notifications = new Dictionary<string, object>
        {
            ["Email"] = new Dictionary<string, object>
            {
                ["Enabled"] = dto.EmailEnabled,
                ["To"] = dto.EmailTo ?? "",
                ["SubjectPrefix"] = dto.EmailSubjectPrefix ?? "[Watchdog]"
            },
            ["Webhook"] = new Dictionary<string, object>
            {
                ["Enabled"] = dto.WebhookEnabled,
                ["Url"] = dto.WebhookUrl ?? "",
                ["WebhookType"] = dto.WebhookType ?? "Generic"
            }
        };

        watchdog["Notifications"] = notifications;
        dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    private static string UpdateGeneralSettingsInConfig(JsonElement root, GeneralSettingsDto dto)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
        
        var watchdog = dict.ContainsKey("Watchdog") 
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(dict["Watchdog"].GetRawText())!
            : new Dictionary<string, object>();

        watchdog["DefaultCheckIntervalSeconds"] = dto.DefaultCheckIntervalSeconds;
        watchdog["DefaultCriticalAfterFailures"] = dto.DefaultCriticalAfterFailures;
        watchdog["HistoryRetentionDays"] = dto.HistoryRetentionDays;

        dict["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(watchdog));

        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    #endregion
}

#region DTOs

public record ServiceConfigDto
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public bool Prioritized { get; init; } = false;
    public Dictionary<string, object?> Config { get; init; } = new();
}

public record NotificationConfigDto
{
    public bool EmailEnabled { get; init; }
    public string? EmailTo { get; init; }
    public string? EmailSubjectPrefix { get; init; }
    public bool WebhookEnabled { get; init; }
    public string? WebhookUrl { get; init; }
    public string? WebhookType { get; init; }
}

public record GeneralSettingsDto
{
    public int DefaultCheckIntervalSeconds { get; init; } = 30;
    public int DefaultCriticalAfterFailures { get; init; } = 3;
    public int HistoryRetentionDays { get; init; } = 7;
}

public record PriorityUpdateDto
{
    public bool Prioritized { get; init; }
}

#endregion


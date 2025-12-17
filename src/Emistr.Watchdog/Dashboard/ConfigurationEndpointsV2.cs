using System.Text.Json;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// Simplified configuration endpoints for V2 array-based config.
/// </summary>
public static class ConfigurationEndpointsV2
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Runtime overrides (no restart needed)
    private static readonly Dictionary<string, ServiceConfig> RuntimeOverrides = new(StringComparer.OrdinalIgnoreCase);

    public static void MapConfigurationEndpointsV2(this WebApplication app)
    {
        var group = app.MapGroup("/api/v2/config");

        group.MapGet("/services", GetServices);
        group.MapGet("/services/{name}", GetService);
        group.MapPost("/services", AddService);
        group.MapPut("/services/{name}", UpdateService);
        group.MapDelete("/services/{name}", DeleteService);
        
        // Quick actions (no restart)
        group.MapPut("/services/{name}/priority", SetPriority);
        group.MapPut("/services/{name}/enabled", SetEnabled);
    }

    /// <summary>
    /// Gets a service config with runtime overrides applied.
    /// </summary>
    public static ServiceConfig? GetServiceWithOverrides(string name, List<ServiceConfig> configServices)
    {
        // Runtime override has priority
        if (RuntimeOverrides.TryGetValue(name, out var runtimeConfig))
        {
            return runtimeConfig;
        }
        
        return configServices.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if service is prioritized (with runtime override).
    /// </summary>
    public static bool IsPrioritized(string name, bool configValue)
    {
        if (RuntimeOverrides.TryGetValue(name, out var config))
        {
            return config.Prioritized;
        }
        return configValue;
    }

    private static async Task<IResult> GetServices(ILogger<Program> logger)
    {
        var services = await LoadServicesFromConfig();
        
        // Apply runtime overrides
        for (int i = 0; i < services.Count; i++)
        {
            if (RuntimeOverrides.TryGetValue(services[i].Name, out var overrideConfig))
            {
                services[i] = overrideConfig;
            }
        }
        
        return Results.Ok(services);
    }

    private static async Task<IResult> GetService(string name, ILogger<Program> logger)
    {
        var services = await LoadServicesFromConfig();
        var service = GetServiceWithOverrides(name, services);
        
        if (service == null)
        {
            return Results.NotFound(new { error = $"Service '{name}' not found" });
        }
        
        return Results.Ok(service);
    }

    private static async Task<IResult> AddService(ServiceConfig service, ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Config file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
            var watchdog = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config["Watchdog"].GetRawText())!;
            
            var services = watchdog.ContainsKey("Services")
                ? JsonSerializer.Deserialize<List<ServiceConfig>>(watchdog["Services"].GetRawText())!
                : new List<ServiceConfig>();

            // Check if exists
            if (services.Any(s => s.Name.Equals(service.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = $"Service '{service.Name}' already exists" });
            }

            services.Add(service);
            
            await SaveServices(configPath, config, watchdog, services);
            
            logger.LogInformation("Service {Name} added", service.Name);
            return Results.Ok(new { message = $"Service '{service.Name}' added", requiresRestart = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add service");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdateService(string name, ServiceConfig service, ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Config file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
            var watchdog = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config["Watchdog"].GetRawText())!;
            
            var services = watchdog.ContainsKey("Services")
                ? JsonSerializer.Deserialize<List<ServiceConfig>>(watchdog["Services"].GetRawText())!
                : new List<ServiceConfig>();

            var index = services.FindIndex(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return Results.NotFound(new { error = $"Service '{name}' not found" });
            }

            service.Name = name; // Keep original name
            services[index] = service;
            
            // Update runtime override
            RuntimeOverrides[name] = service;
            
            await SaveServices(configPath, config, watchdog, services);
            
            logger.LogInformation("Service {Name} updated", name);
            return Results.Ok(new { message = $"Service '{name}' updated", requiresRestart = false });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update service");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> DeleteService(string name, ILogger<Program> logger)
    {
        try
        {
            var configPath = GetConfigPath();
            if (configPath == null)
            {
                return Results.BadRequest(new { error = "Config file not found" });
            }

            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.GetRawText())!;
            var watchdog = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config["Watchdog"].GetRawText())!;
            
            var services = watchdog.ContainsKey("Services")
                ? JsonSerializer.Deserialize<List<ServiceConfig>>(watchdog["Services"].GetRawText())!
                : new List<ServiceConfig>();

            var removed = services.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return Results.NotFound(new { error = $"Service '{name}' not found" });
            }

            // Remove runtime override
            RuntimeOverrides.Remove(name);
            
            await SaveServices(configPath, config, watchdog, services);
            
            logger.LogInformation("Service {Name} deleted", name);
            return Results.Ok(new { message = $"Service '{name}' deleted", requiresRestart = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete service");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SetPriority(string name, PriorityDto dto, ILogger<Program> logger)
    {
        try
        {
            var services = await LoadServicesFromConfig();
            var service = services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            if (service == null)
            {
                return Results.NotFound(new { error = $"Service '{name}' not found" });
            }

            // Apply to runtime immediately (no restart needed)
            RuntimeConfigurationService.Instance.SetPrioritized(name, dto.Prioritized);
            
            // Also update V2 runtime overrides
            service.Prioritized = dto.Prioritized;
            RuntimeOverrides[name] = service;
            
            // Also update V1 runtime priority cache for dashboard compatibility
            ConfigurationEndpoints.SetRuntimePriority(name, dto.Prioritized);
            
            // Save to config file (persists across restarts)
            await UpdateServiceProperty(name, "Prioritized", dto.Prioritized);
            
            logger.LogInformation("Service {Name} priority set to {Priority} (runtime + config)", name, dto.Prioritized);
            return Results.Ok(new { 
                message = $"Priority updated for '{name}'", 
                prioritized = dto.Prioritized,
                requiresRestart = false 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set priority");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SetEnabled(string name, EnabledDto dto, ILogger<Program> logger)
    {
        try
        {
            var services = await LoadServicesFromConfig();
            var service = services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            
            if (service == null)
            {
                return Results.NotFound(new { error = $"Service '{name}' not found" });
            }

            // Apply to runtime immediately (no restart needed)
            RuntimeConfigurationService.Instance.SetEnabled(name, dto.Enabled);
            
            // Also update V2 runtime overrides
            service.Enabled = dto.Enabled;
            RuntimeOverrides[name] = service;
            
            // Save to config file (persists across restarts)
            await UpdateServiceProperty(name, "Enabled", dto.Enabled);
            
            logger.LogInformation("Service {Name} enabled set to {Enabled} (runtime + config)", name, dto.Enabled);
            return Results.Ok(new { 
                message = $"Enabled updated for '{name}'", 
                enabled = dto.Enabled,
                requiresRestart = false 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set enabled");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    #region Helpers

    private static async Task<List<ServiceConfig>> LoadServicesFromConfig()
    {
        var configPath = GetConfigPath();
        if (configPath == null)
        {
            return new List<ServiceConfig>();
        }

        var json = await File.ReadAllTextAsync(configPath);
        using var doc = JsonDocument.Parse(json);
        
        if (!doc.RootElement.TryGetProperty("Watchdog", out var watchdog))
            return new List<ServiceConfig>();
            
        if (!watchdog.TryGetProperty("Services", out var services))
            return new List<ServiceConfig>();

        // Check if V2 format (array) or V1 format (object)
        if (services.ValueKind == JsonValueKind.Array)
        {
            // V2 format - direct array
            return JsonSerializer.Deserialize<List<ServiceConfig>>(services.GetRawText(), JsonOptions) ?? new List<ServiceConfig>();
        }
        else if (services.ValueKind == JsonValueKind.Object)
        {
            // V1 format - convert object to array
            return ConvertV1ToV2Services(services);
        }

        return new List<ServiceConfig>();
    }

    private static List<ServiceConfig> ConvertV1ToV2Services(JsonElement servicesObj)
    {
        var result = new List<ServiceConfig>();

        // Built-in MariaDB
        if (servicesObj.TryGetProperty("MariaDB", out var mariaDb))
        {
            result.Add(CreateServiceConfigFromV1(mariaDb, "MariaDB", "MariaDb"));
        }

        // Custom MariaDB services
        if (servicesObj.TryGetProperty("CustomMariaDbServices", out var customMariaDb) && 
            customMariaDb.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in customMariaDb.EnumerateObject())
            {
                result.Add(CreateServiceConfigFromV1(prop.Value, prop.Name, "MariaDb"));
            }
        }

        // Built-in LicenseManager
        if (servicesObj.TryGetProperty("LicenseManager", out var licenseManager))
        {
            result.Add(CreateServiceConfigFromV1(licenseManager, "LicenseManager", "Http"));
        }

        // Built-in Apache
        if (servicesObj.TryGetProperty("Apache", out var apache))
        {
            result.Add(CreateServiceConfigFromV1(apache, "Apache", "Http"));
        }

        // Custom HTTP services
        if (servicesObj.TryGetProperty("CustomHttpServices", out var customHttp) && 
            customHttp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in customHttp.EnumerateObject())
            {
                result.Add(CreateServiceConfigFromV1(prop.Value, prop.Name, "Http"));
            }
        }

        // Built-in PracantD (Telnet)
        if (servicesObj.TryGetProperty("PracantD", out var pracantD))
        {
            result.Add(CreateServiceConfigFromV1(pracantD, "PracantD", "Telnet"));
        }

        // Custom Telnet services
        if (servicesObj.TryGetProperty("CustomTelnetServices", out var customTelnet) && 
            customTelnet.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in customTelnet.EnumerateObject())
            {
                result.Add(CreateServiceConfigFromV1(prop.Value, prop.Name, "Telnet"));
            }
        }

        // Built-in BackgroundService
        if (servicesObj.TryGetProperty("BackgroundService", out var bgService))
        {
            result.Add(CreateServiceConfigFromV1(bgService, "BackgroundService", "BackgroundService"));
        }

        // Custom Background services
        if (servicesObj.TryGetProperty("CustomBackgroundServices", out var customBg) && 
            customBg.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in customBg.EnumerateObject())
            {
                result.Add(CreateServiceConfigFromV1(prop.Value, prop.Name, "BackgroundService"));
            }
        }

        // Ping services
        if (servicesObj.TryGetProperty("PingServices", out var pingServices) && 
            pingServices.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pingServices.EnumerateObject())
            {
                result.Add(CreateServiceConfigFromV1(prop.Value, prop.Name, "Ping"));
            }
        }

        return result;
    }

    private static ServiceConfig CreateServiceConfigFromV1(JsonElement v1Config, string name, string type)
    {
        return new ServiceConfig
        {
            Name = name,
            Type = type,
            DisplayName = GetStringProperty(v1Config, "DisplayName") ?? name,
            Enabled = GetBoolProperty(v1Config, "Enabled", true),
            Prioritized = GetBoolProperty(v1Config, "Prioritized", false),
            TimeoutSeconds = GetIntProperty(v1Config, "TimeoutSeconds", 10),
            CriticalAfterFailures = GetIntProperty(v1Config, "CriticalAfterFailures", 3),
            // Type-specific properties
            ConnectionString = GetStringProperty(v1Config, "ConnectionString"),
            Url = GetStringProperty(v1Config, "Url"),
            Host = GetStringProperty(v1Config, "Host"),
            Port = GetIntProperty(v1Config, "Port", 0),
            ExpectedStatusCodes = GetIntArrayProperty(v1Config, "ExpectedStatusCodes"),
            ValidateSsl = GetBoolProperty(v1Config, "ValidateSsl", true),
            DatabaseName = GetStringProperty(v1Config, "DatabaseName"),
            TableName = GetStringProperty(v1Config, "TableName"),
            ColumnName = GetStringProperty(v1Config, "ColumnName"),
            MaxAgeMinutes = GetIntProperty(v1Config, "MaxAgeMinutes", 5)
        };
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static bool GetBoolProperty(JsonElement element, string name, bool defaultValue)
    {
        if (element.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    private static int GetIntProperty(JsonElement element, string name, int defaultValue)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : defaultValue;
    }

    private static int[]? GetIntArrayProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            return prop.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        }
        return null;
    }

    private static async Task SaveServices(
        string configPath,
        Dictionary<string, JsonElement> config,
        Dictionary<string, JsonElement> watchdog,
        List<ServiceConfig> services)
    {
        watchdog["Services"] = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(services, JsonOptions));
        
        config["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(watchdog, JsonOptions));

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static async Task UpdateServiceProperty(string name, string property, object value)
    {
        var configPath = GetConfigPath();
        if (configPath == null) return;

        var json = await File.ReadAllTextAsync(configPath);
        using var doc = JsonDocument.Parse(json);

        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText())!;
        var watchdog = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config["Watchdog"].GetRawText())!;
        var services = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(watchdog["Services"].GetRawText())!;

        var service = services.FirstOrDefault(s => 
            s.TryGetValue("Name", out var n) && 
            n?.ToString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

        if (service != null)
        {
            service[property] = value;
            
            watchdog["Services"] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(services, JsonOptions));
            
            config["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(watchdog, JsonOptions));

            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonOptions));
        }
    }

    private static string? GetConfigPath()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var paths = new[] { $"appsettings.{env}.json", "appsettings.json" };

        foreach (var path in paths)
        {
            if (File.Exists(path)) return path;
        }
        return null;
    }

    #endregion
}

public record PriorityDto(bool Prioritized);
public record EnabledDto(bool Enabled);


using System.Text.Json;
using Emistr.Watchdog.Configuration;

namespace Emistr.Watchdog.Dashboard;

/// <summary>
/// Migration utility to convert V1 config to V2 array-based format.
/// </summary>
public static class ConfigMigration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Check if config is V1 (object-based) or V2 (array-based).
    /// </summary>
    public static bool IsV1Config(JsonElement watchdog)
    {
        if (!watchdog.TryGetProperty("Services", out var services))
            return false;

        // V1 has Services as object, V2 has Services as array
        return services.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// Migrate V1 config to V2 format.
    /// </summary>
    public static async Task<bool> MigrateIfNeeded(string configPath, ILogger logger)
    {
        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Watchdog", out var watchdog))
            {
                logger.LogDebug("No Watchdog section found, skipping migration");
                return false;
            }

            if (!IsV1Config(watchdog))
            {
                logger.LogDebug("Config is already V2 format");
                return false;
            }

            logger.LogInformation("Migrating config from V1 to V2 format...");

            // Backup original
            var backupPath = configPath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(configPath, backupPath);
            logger.LogInformation("Backup created: {BackupPath}", backupPath);

            // Convert
            var services = ExtractServicesFromV1(watchdog, logger);
            
            // Build new config
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.RootElement.GetRawText())!;
            var watchdogDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(watchdog.GetRawText())!;
            
            // Remove old V1 keys
            var v1Keys = new[] { "MariaDB", "LicenseManager", "Apache", "PracantD", "BackgroundService", 
                "Redis", "RabbitMQ", "Elasticsearch", "CustomMariaDbServices", "CustomHttpServices", 
                "CustomTelnetServices", "CustomBackgroundServices", "CustomRedisServices", 
                "CustomRabbitMqServices", "CustomElasticsearchServices", "PingServices", "ScriptServices" };
            
            foreach (var key in v1Keys)
            {
                watchdogDict.Remove(key);
            }
            
            // Add new Services array
            watchdogDict["Services"] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(services, JsonOptions));

            config["Watchdog"] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(watchdogDict, JsonOptions));

            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonOptions));
            
            logger.LogInformation("Migration complete. {Count} services converted", services.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to migrate config");
            return false;
        }
    }

    private static List<ServiceConfig> ExtractServicesFromV1(JsonElement watchdog, ILogger logger)
    {
        var services = new List<ServiceConfig>();

        if (!watchdog.TryGetProperty("Services", out var servicesElement))
            return services;

        // Built-in MariaDB
        if (TryGetService(servicesElement, "MariaDB", out var mariaDb))
        {
            services.Add(ConvertMariaDb("MariaDB", mariaDb, logger));
        }

        // Built-in LicenseManager
        if (TryGetService(servicesElement, "LicenseManager", out var lm))
        {
            services.Add(ConvertHttp("LicenseManager", lm, logger));
        }

        // Built-in Apache
        if (TryGetService(servicesElement, "Apache", out var apache))
        {
            services.Add(ConvertHttp("Apache", apache, logger));
        }

        // Built-in PracantD (Telnet)
        if (TryGetService(servicesElement, "PracantD", out var pracantd))
        {
            services.Add(ConvertTelnet("PracantD", pracantd, logger));
        }

        // Built-in BackgroundService
        if (TryGetService(servicesElement, "BackgroundService", out var bgSvc))
        {
            services.Add(ConvertBackgroundService("BackgroundService", bgSvc, logger));
        }

        // Custom MariaDB services
        if (TryGetService(servicesElement, "CustomMariaDbServices", out var customMaria))
        {
            foreach (var prop in customMaria.EnumerateObject())
            {
                services.Add(ConvertMariaDb(prop.Name, prop.Value, logger));
            }
        }

        // Custom HTTP services
        if (TryGetService(servicesElement, "CustomHttpServices", out var customHttp))
        {
            foreach (var prop in customHttp.EnumerateObject())
            {
                services.Add(ConvertHttp(prop.Name, prop.Value, logger));
            }
        }

        // Custom Telnet services
        if (TryGetService(servicesElement, "CustomTelnetServices", out var customTelnet))
        {
            foreach (var prop in customTelnet.EnumerateObject())
            {
                services.Add(ConvertTelnet(prop.Name, prop.Value, logger));
            }
        }

        // Custom Background services
        if (TryGetService(servicesElement, "CustomBackgroundServices", out var customBg))
        {
            foreach (var prop in customBg.EnumerateObject())
            {
                services.Add(ConvertBackgroundService(prop.Name, prop.Value, logger));
            }
        }

        // Ping services
        if (TryGetService(servicesElement, "PingServices", out var pingServices))
        {
            foreach (var prop in pingServices.EnumerateObject())
            {
                services.Add(ConvertPing(prop.Name, prop.Value, logger));
            }
        }

        // Script services
        if (TryGetService(servicesElement, "ScriptServices", out var scriptServices))
        {
            foreach (var prop in scriptServices.EnumerateObject())
            {
                services.Add(ConvertScript(prop.Name, prop.Value, logger));
            }
        }

        return services;
    }

    private static bool TryGetService(JsonElement services, string name, out JsonElement value)
    {
        if (services.TryGetProperty(name, out value))
        {
            return value.ValueKind != JsonValueKind.Null;
        }
        return false;
    }

    private static ServiceConfig ConvertMariaDb(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting MariaDB service: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "MariaDb",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 10),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            ConnectionString = GetString(el, "ConnectionString"),
            RestartConfig = GetRestartConfig(el)
        };
    }

    private static ServiceConfig ConvertHttp(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting HTTP service: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "Http",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 10),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            Url = GetString(el, "Url"),
            ExpectedStatusCodes = GetIntArray(el, "ExpectedStatusCodes", [200]),
            ValidateSsl = !GetBool(el, "IgnoreSslErrors", false),
            RestartConfig = GetRestartConfig(el)
        };
    }

    private static ServiceConfig ConvertTelnet(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting Telnet service: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "Tcp",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 5),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            Host = GetString(el, "Host"),
            Port = GetInt(el, "Port", 80),
            RestartConfig = GetRestartConfig(el)
        };
    }

    private static ServiceConfig ConvertBackgroundService(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting BackgroundService: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "BackgroundService",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 10),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            ConnectionString = GetString(el, "ConnectionString"),
            DatabaseName = GetString(el, "DatabaseName"),
            TableName = GetString(el, "TableName"),
            ColumnName = GetString(el, "ColumnName"),
            MaxAgeMinutes = GetInt(el, "MaxAgeMinutes", 5),
            RestartConfig = GetRestartConfig(el)
        };
    }

    private static ServiceConfig ConvertPing(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting Ping service: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "Ping",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 5),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            Host = GetString(el, "Host"),
            PingCount = GetInt(el, "PingCount", 3),
            MaxPacketLossPercent = GetInt(el, "MaxPacketLossPercent", 20)
        };
    }

    private static ServiceConfig ConvertScript(string name, JsonElement el, ILogger logger)
    {
        logger.LogDebug("Converting Script service: {Name}", name);
        return new ServiceConfig
        {
            Name = name,
            Type = "Script",
            DisplayName = GetString(el, "DisplayName"),
            Enabled = GetBool(el, "Enabled", true),
            Prioritized = GetBool(el, "Prioritized", false),
            TimeoutSeconds = GetInt(el, "TimeoutSeconds", 30),
            CriticalAfterFailures = GetInt(el, "CriticalAfterFailures", 3),
            ScriptPath = GetString(el, "ScriptPath") ?? GetString(el, "Path"),
            ScriptArguments = GetString(el, "Arguments"),
            Shell = GetString(el, "Shell")
        };
    }

    #region Helpers

    private static string? GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static bool GetBool(JsonElement el, string name, bool defaultValue)
    {
        if (el.TryGetProperty(name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }

    private static int GetInt(JsonElement el, string name, int defaultValue)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        return defaultValue;
    }

    private static int[] GetIntArray(JsonElement el, string name, int[] defaultValue)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var list = new List<int>();
            foreach (var item in prop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                    list.Add(item.GetInt32());
            }
            return list.Count > 0 ? list.ToArray() : defaultValue;
        }
        return defaultValue;
    }

    private static ServiceRestartConfig? GetRestartConfig(JsonElement el)
    {
        if (!el.TryGetProperty("RestartConfig", out var rc) || rc.ValueKind == JsonValueKind.Null)
            return null;

        return new ServiceRestartConfig
        {
            Enabled = GetBool(rc, "Enabled", false),
            WindowsServiceName = GetString(rc, "WindowsServiceName"),
            MaxRestartAttempts = GetInt(rc, "MaxRestartAttempts", 3),
            RestartDelaySeconds = GetInt(rc, "RestartDelaySeconds", 30),
            RestartOnCritical = GetBool(rc, "RestartOnCritical", true)
        };
    }

    #endregion
}



# Health Checkers

Emistr Watchdog provides 9 built-in health checkers for monitoring various services.

## Available Checkers

| Type | Description | Required Config |
|------|-------------|-----------------|
| `MariaDb` | MySQL/MariaDB database | `connectionString` |
| `Http` | HTTP/HTTPS endpoint | `url` |
| `Tcp` | TCP port connectivity | `host`, `port` |
| `Ping` | ICMP ping | `host` |
| `Redis` | Redis server | `connectionString` |
| `RabbitMq` | RabbitMQ broker | `host`, `username`, `password` |
| `Elasticsearch` | ES cluster health | `url` |
| `Script` | Custom PowerShell/Bash | `scriptPath` |
| `BackgroundService` | Emistr BG service | `connectionString` |

## Configuration Examples

### MariaDb

```json
{
  "name": "production-db",
  "type": "MariaDb",
  "displayName": "Production Database",
  "connectionString": "Server=localhost;Port=3306;Database=mydb;User=root;Password=xxx;",
  "enabled": true,
  "prioritized": true,
  "criticalAfterFailures": 3,
  "timeoutSeconds": 10
}
```

### Http

```json
{
  "name": "api-health",
  "type": "Http",
  "displayName": "API Health Check",
  "url": "https://api.example.com/health",
  "expectedStatusCodes": [200, 204],
  "validateSsl": true,
  "enabled": true,
  "timeoutSeconds": 30
}
```

### Redis

```json
{
  "name": "cache",
  "type": "Redis",
  "displayName": "Redis Cache",
  "connectionString": "localhost:6379,password=xxx",
  "enabled": true
}
```

### Script

```json
{
  "name": "disk-check",
  "type": "Script",
  "displayName": "Disk Space Check",
  "scriptPath": "scripts/check-disk.ps1",
  "arguments": "-Drive C:",
  "enabled": true,
  "timeoutSeconds": 60
}
```

**Exit codes:**
- `0` = Healthy
- `1` = Warning/Degraded
- `2+` = Unhealthy/Critical

## Common Properties

All checkers support these properties:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | string | required | Unique identifier |
| `type` | string | required | Checker type |
| `displayName` | string | name | Display name in dashboard |
| `enabled` | bool | true | Enable/disable checker |
| `prioritized` | bool | false | Show at top of dashboard |
| `criticalAfterFailures` | int | 3 | Failures before critical |
| `timeoutSeconds` | int | 30 | Check timeout |
| `tags` | string[] | [] | Tags for filtering |

## Creating Custom Checkers

```csharp
public class MyHealthChecker : HealthCheckerBase
{
    private readonly MyOptions _options;
    
    public MyHealthChecker(
        string serviceName,
        MyOptions options,
        ILogger<MyHealthChecker> logger)
        : base(logger)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? _serviceName;
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;

    protected override async Task<HealthCheckResult> PerformCheckAsync(
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        // Your check logic here
        var isHealthy = await CheckMyServiceAsync(cancellationToken);
        
        return isHealthy
            ? HealthCheckResult.Healthy(ServiceName, sw.ElapsedMilliseconds)
            : HealthCheckResult.Unhealthy(ServiceName, "Service not responding");
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddSingleton<IHealthChecker, MyHealthChecker>();
```

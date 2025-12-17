# Configuration

Complete configuration guide for Emistr Watchdog.

## Configuration File

The main configuration file is `appsettings.json`. Environment-specific overrides can be placed in `appsettings.{Environment}.json`.

## Full Example

```json
{
  "Dashboard": {
    "Port": 5080,
    "UseHttps": false,
    "CertificatePath": "",
    "CertificatePassword": "",
    "Authentication": {
      "Enabled": false,
      "Type": "ApiKey",
      "ApiKey": ""
    }
  },
  "Watchdog": {
    "CheckIntervalSeconds": 30,
    "Services": [
      {
        "name": "mariadb-main",
        "type": "MariaDb",
        "displayName": "MariaDB Production",
        "connectionString": "Server=localhost;Port=3306;Database=emistr;User=root;Password=xxx;",
        "enabled": true,
        "prioritized": true,
        "criticalAfterFailures": 3,
        "timeoutSeconds": 10
      },
      {
        "name": "api-health",
        "type": "Http",
        "displayName": "API Health",
        "url": "https://localhost:7115/healthz",
        "expectedStatusCodes": [200],
        "enabled": true
      },
      {
        "name": "redis-cache",
        "type": "Redis",
        "displayName": "Redis Cache",
        "connectionString": "localhost:6379",
        "enabled": true
      }
    ]
  },
  "Notifications": {
    "Email": {
      "Enabled": false,
      "Provider": "Smtp",
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "Username": "alerts@example.com",
      "Password": "",
      "FromAddress": "watchdog@example.com",
      "ToAddresses": ["admin@example.com"],
      "CooldownMinutes": 15
    },
    "Teams": {
      "Enabled": false,
      "WebhookUrl": "",
      "CooldownMinutes": 15
    },
    "Slack": {
      "Enabled": false,
      "WebhookUrl": "",
      "Channel": "#alerts",
      "CooldownMinutes": 15
    },
    "Discord": {
      "Enabled": false,
      "WebhookUrl": "",
      "CooldownMinutes": 15
    },
    "GenericWebhook": {
      "Enabled": false,
      "Url": "",
      "Method": "POST",
      "Headers": {}
    }
  },
  "Escalation": {
    "Enabled": false,
    "Levels": [
      { "Level": 1, "DelayMinutes": 5, "Recipients": ["oncall@example.com"] },
      { "Level": 2, "DelayMinutes": 15, "Recipients": ["manager@example.com"] },
      { "Level": 3, "DelayMinutes": 30, "Recipients": ["cto@example.com"] }
    ]
  },
  "MaintenanceWindows": [],
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/watchdog-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

## Dashboard Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Port` | int | 5080 | HTTP port |
| `UseHttps` | bool | false | Enable HTTPS |
| `CertificatePath` | string | - | Path to .pfx certificate |
| `CertificatePassword` | string | - | Certificate password |

### Authentication

| Property | Type | Description |
|----------|------|-------------|
| `Enabled` | bool | Enable authentication |
| `Type` | string | "ApiKey" or "Basic" |
| `ApiKey` | string | API key for ApiKey auth |

## Watchdog Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CheckIntervalSeconds` | int | 30 | Interval between health checks |
| `Services` | array | [] | List of services to monitor |

## Email Providers

| Provider | Required Config |
|----------|-----------------|
| `Smtp` | SmtpHost, SmtpPort, Username, Password |
| `SendGrid` | ApiKey |
| `Microsoft365` | TenantId, ClientId, ClientSecret |
| `Mailgun` | ApiKey, Domain |
| `Mailchimp` | ApiKey |

## Environment Variables

All settings can be overridden via environment variables:

```bash
# Dashboard
Dashboard__Port=5080
Dashboard__UseHttps=true

# Watchdog
Watchdog__CheckIntervalSeconds=60

# Notifications
Notifications__Email__Enabled=true
Notifications__Email__SmtpHost=smtp.gmail.com
```

## Runtime Configuration

Some settings can be changed at runtime via API without restart:

```bash
# Enable/disable service
curl -X PUT http://localhost:5080/api/v2/config/services/my-service/enabled \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'

# Change priority
curl -X PUT http://localhost:5080/api/v2/config/services/my-service/priority \
  -H "Content-Type: application/json" \
  -d '{"prioritized": true}'
```

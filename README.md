# Emistr Watchdog

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**Real-time system monitoring service with dashboard** for infrastructure health checking.

## Features

- 🏥 **9 Health Checkers** - MariaDB, HTTP, TCP, Ping, Redis, RabbitMQ, Elasticsearch, Script, Background Service
- 📊 **Real-time Dashboard** - SignalR-powered live updates
- 🔔 **Multi-channel Notifications** - Email (5 providers), Webhooks (Slack, Teams, Discord)
- 📈 **SLA/Uptime Tracking** - Daily, weekly, monthly statistics
- ⚡ **Alerting Escalation** - L1 → L2 → L3 automatic escalation
- 🛠️ **Maintenance Windows** - Scheduled downtime management
- 🔄 **Runtime Configuration** - Change settings without restart
- 📉 **Prometheus Metrics** - `/metrics` endpoint for Grafana

## Quick Start

### Prerequisites

- .NET 9 SDK
- (Optional) MariaDB, Redis, RabbitMQ for health checking

### Installation

```bash
# Clone
git clone https://github.com/cybersmurf/emistr-watchdog.git
cd emistr-watchdog

# Build
dotnet build

# Run
dotnet run --project src/Emistr.Watchdog
```

### Configuration

Edit `appsettings.json`:

```json
{
  "Dashboard": {
    "Port": 5080,
    "UseHttps": false
  },
  "Watchdog": {
    "CheckIntervalSeconds": 30,
    "Services": [
      {
        "name": "my-database",
        "type": "MariaDb",
        "displayName": "Production Database",
        "connectionString": "Server=localhost;Database=mydb;User=root;Password=xxx;",
        "enabled": true,
        "prioritized": true
      },
      {
        "name": "my-api",
        "type": "Http",
        "displayName": "API Health",
        "url": "https://api.example.com/health",
        "enabled": true
      }
    ]
  }
}
```

### Access Dashboard

Open: http://localhost:5080

## Health Checker Types

| Type | Description | Required Config |
|------|-------------|-----------------|
| `MariaDb` | MySQL/MariaDB database | `connectionString` |
| `Http` | HTTP/HTTPS endpoint | `url` |
| `Tcp` | TCP port connectivity | `host`, `port` |
| `Ping` | ICMP ping | `host` |
| `Redis` | Redis server | `connectionString` |
| `RabbitMq` | RabbitMQ broker | `host`, `username`, `password` |
| `Elasticsearch` | ES cluster health | `url` |
| `Script` | Custom PowerShell/Bash script | `scriptPath` |
| `BackgroundService` | Emistr background service | `connectionString` |

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /` | Dashboard |
| `GET /api/status` | Current status of all services |
| `GET /api/health/{service}` | Status of specific service |
| `GET /api/sla` | SLA statistics |
| `GET /api/escalations` | Escalation states |
| `GET /metrics` | Prometheus metrics |
| `PUT /api/v2/config/services/{name}/enabled` | Enable/disable service (no restart) |

## Notifications

### Email Providers

- SMTP
- SendGrid
- Microsoft 365
- Mailgun
- Mailchimp (Transactional)

### Webhooks

- Microsoft Teams
- Slack
- Discord
- Generic (custom URL)

## Deployment

### Windows Service

```powershell
cd deploy/windows
.\install-services.ps1
```

### Linux systemd

```bash
cd deploy/linux
sudo ./install.sh
```

### Docker

```bash
docker build -t emistr-watchdog:latest .
docker run -d -p 5080:5080 emistr-watchdog:latest
```

## Testing

```bash
dotnet test
```

**231 unit tests** covering health checkers, services, and configuration.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Configuration](docs/CONFIGURATION.md)
- [Health Checks](docs/HEALTH_CHECKS.md)
- [Email Providers](docs/EMAIL_PROVIDERS.md)
- [Webhooks](docs/WEBHOOKS.md)
- [Deployment](docs/DEPLOYMENT.md)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Author

**cybersmurf** - [GitHub](https://github.com/cybersmurf)


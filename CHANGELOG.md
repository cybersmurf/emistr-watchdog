# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.2.0] - 2025-12-17

### Added

- **Initial standalone release** - Split from emistr_nextgen monorepo
- **9 Health Checkers**
  - MariaDbHealthChecker - MySQL/MariaDB database monitoring
  - HttpHealthChecker - HTTP/HTTPS endpoint checks
  - TcpHealthChecker - TCP port connectivity
  - PingHealthChecker - ICMP ping checks
  - RedisHealthChecker - Redis server monitoring
  - RabbitMqHealthChecker - RabbitMQ broker monitoring
  - ElasticsearchHealthChecker - Elasticsearch cluster health
  - ScriptHealthChecker - Custom PowerShell/Bash scripts
  - BackgroundServiceHealthChecker - Emistr background service

- **Real-time Dashboard**
  - SignalR-powered live updates
  - Dark/Light/Auto theme support
  - Prioritized services display
  - Expandable service details

- **Multi-channel Notifications**
  - Email (SMTP, SendGrid, Microsoft 365, Mailgun, Mailchimp)
  - Webhooks (Slack, Microsoft Teams, Discord, Generic)
  - Configurable alert templates

- **SLA/Uptime Tracking**
  - Daily, weekly, monthly statistics
  - Per-service uptime records
  - API endpoints for SLA data

- **Alerting Escalation**
  - L1 → L2 → L3 automatic escalation
  - Configurable delays and recipients
  - Acknowledgment support

- **Maintenance Windows**
  - Scheduled downtime management
  - Service-specific windows
  - Automatic alert suppression

- **Runtime Configuration**
  - Enable/disable services without restart
  - Priority changes without restart
  - Config persisted to file

- **Prometheus Metrics**
  - `/metrics` endpoint
  - Grafana dashboard template

### Technical

- .NET 9, C# 13
- ASP.NET Core Minimal API
- SignalR for real-time updates
- Entity Framework Core (SQLite for history)
- Polly for HTTP resilience
- 231 unit tests

## [2.0.0] - 2025-12-16

### Added

- Emistr.Common shared library
- PerformanceMiddleware - request timing
- CorrelationIdMiddleware - distributed tracing
- ApiVersionMiddleware - API versioning
- Script security hardening

## [1.5.0] - 2025-12-16

### Added

- Alerting escalation (L1 → L2 → L3)
- Maintenance windows
- Recovery actions
- Multi-tenant support

## [1.0.0] - 2025-12-14

### Added

- Initial implementation
- Basic health checkers
- Email notifications
- Simple dashboard

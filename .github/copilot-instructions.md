# Copilot Instructions for Emistr Watchdog

> Tento soubor je automaticky načten GitHub Copilotem pro kontext projektu.
> **Aktuální verze: v2.2.0** (2025-12-17)

## Projekt

Emistr Watchdog je .NET 9 real-time monitorovací služba pro sledování stavu infrastruktury.

## Klíčové soubory

- Konfigurace: `appsettings.json`, `appsettings.Development.json`
- Entry point: `Program.cs`
- Health checkery: `src/Emistr.Watchdog/Services/HealthCheckers/`
- Dashboard: `src/Emistr.Watchdog/wwwroot/`
- Shared middleware: `src/Emistr.Common/Middleware/`
- Testy: `tests/Emistr.Watchdog.Tests/`

## Technologie

- .NET 9, C# 13
- ASP.NET Core Minimal API, SignalR
- Entity Framework Core 9 (SQLite)
- Polly (HTTP resilience)
- xUnit, FluentAssertions, NSubstitute

## Coding Conventions

- **Naming:** PascalCase pro public, `_camelCase` pro private fields
- **Async:** Vždy `Async` suffix, předávat `CancellationToken`
- **Nullable:** Enabled - explicitně značit `string?`
- **Records:** Pro DTO a immutable typy
- **Testy:** Arrange-Act-Assert pattern
- **231 testů celkem** - vždy spustit před commitem

## Commit Format

```
type(scope): description
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`

## Health Checker Types

- `MariaDb` - MySQL/MariaDB database
- `Http` - HTTP/HTTPS endpoints
- `Tcp` - TCP port check
- `Ping` - ICMP ping
- `Redis` - Redis server
- `RabbitMq` - RabbitMQ broker
- `Elasticsearch` - ES cluster
- `Script` - Custom scripts
- `BackgroundService` - Emistr BG service

## Důležité

- Terminálové příkazy s pagerem používat s `--no-pager` nebo `| cat`
- Runtime config změny (enabled/priority) fungují bez restartu
- Detailní dokumentace: `docs/`

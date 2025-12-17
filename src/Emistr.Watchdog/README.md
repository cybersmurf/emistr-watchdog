# Emistr.Watchdog

[![Version](https://img.shields.io/badge/version-0.5.1-blue.svg)]()
[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Monitorovaci sluzba pro sledovani zdravi systemovych komponent Emistr. Poskytuje real-time monitoring s notifikacemi a web dashboardem.

## Verze 0.5.1

### Nove funkce
- Custom ikona pro Windows toast notifikace
- Vylepseny format notifikaci (header, timestamp, attribution)
- Podpora obrazku v notifikacich (wwwroot/alert-icon.png)

## Verze 0.5.0

### Zmeny
- Vylepsene Windows toast notifikace s custom XML template
- Odstraneni emoji pro kompatibilitu

## Verze 0.4.x

### Funkce
- HTTPS podpora s self-signed certifikaty
- Konfigurace portu 5080
- Kompletni dokumentace (SSL, Troubleshooting, Webhooks)

## Verze 0.2.0

### Nove funkce
- Automaticke restartovani Windows sluzeb pri kritickem selhani
- Konfigurace restartu pro kazdy monitorovany service
- Sledovani pokusu o restart a jejich uspechu
- Omezeni max pokusu o restart

## Verze 0.1.1

### Zmeny
- Dokumentace v cestine bez emoji (kompatibilita)

## Verze 0.1.0

### Funkce
- MariaDB health checker se ziskanim verze serveru
- HTTP health checker s podporou JSON server info
- Telnet/TCP health checker (PracantD protokol)
- Background Service health checker (bgs_last_run)
- Real-time dashboard s SignalR
- Email notifikace s cooldown
- Windows Event Log integrace
- Multi-instance podpora pro vsechny typy sluzeb

## Hlavni funkce

- **Multi-service monitoring** - MariaDB, HTTP sluzby, Telnet sluzby (PracantD)
- **Real-time dashboard** - Web UI s live aktualizacemi pres SignalR
- **Email notifikace** - S cooldown periodou proti spamu
- **Critical alerty** - Windows Event Log, desktop notifikace, zvuk
- **Windows Service** - Beh jako systemova sluzba
- **Rozsiritelnost** - Snadne pridani vlastnich checkeru

## Architektura

```
Emistr.Watchdog/
??? Configuration/           # Konfiguracni modely
?   ??? WatchdogOptions.cs
?   ??? NotificationOptions.cs
?   ??? DashboardOptions.cs
??? Models/
?   ??? HealthCheckResult.cs # Vysledek health checku
??? Services/
?   ??? HealthCheckers/      # Implementace health checku
?   ?   ??? MariaDbHealthChecker.cs
?   ?   ??? HttpHealthChecker.cs
?   ?   ??? TelnetHealthChecker.cs
?   ?   ??? BackgroundServiceHealthChecker.cs
?   ??? WatchdogService.cs   # Hlavni background service
?   ??? EmailNotificationService.cs
?   ??? CriticalAlertService.cs
??? Dashboard/               # Web UI
?   ??? DashboardHub.cs      # SignalR hub
?   ??? DashboardEndpoints.cs
?   ??? DashboardDtos.cs
??? wwwroot/
?   ??? index.html           # Dashboard UI
??? Program.cs
??? appsettings.json
```

## Rychly start

### Prerekvizity

- .NET 9 SDK
- (Volitelne) SMTP server pro email notifikace

### Spusteni

```bash
cd src/Emistr.Watchdog
dotnet run
```

Dashboard bude dostupny na `http://localhost:5050`.

### Instalace jako Windows sluzba

```powershell
# Publikovani
dotnet publish -c Release -r win-x64 --self-contained

# Vytvoreni sluzby
sc.exe create "Emistr.Watchdog" binPath="C:\path\to\Emistr.Watchdog.exe"
sc.exe description "Emistr.Watchdog" "Monitoring sluzba pro Emistr system"

# Spusteni
sc.exe start "Emistr.Watchdog"
```

### Registrace Windows Event Log zdroje (jako admin)

```powershell
New-EventLog -LogName Application -Source 'Emistr.Watchdog'
```

## Konfigurace

### Monitorovane sluzby

```json
{
  "Watchdog": {
    "CheckIntervalSeconds": 30,
    "Services": {
      "MariaDB": {
        "Enabled": true,
        "DisplayName": "MariaDB Database",
        "ConnectionString": "Server=192.168.222.113;Port=5336;User=user;Password=pass;",
        "TimeoutSeconds": 10,
        "CriticalAfterFailures": 3
      },
      "LicenseManager": {
        "Enabled": true,
        "DisplayName": "License Manager API",
        "Url": "https://localhost:7115/healthz",
        "TimeoutSeconds": 10,
        "ExpectedStatusCodes": [200],
        "IgnoreSslErrors": true,
        "CriticalAfterFailures": 3
      },
      "Apache": {
        "Enabled": true,
        "DisplayName": "Apache Web Server",
        "Url": "http://192.168.222.144/health.php",
        "TimeoutSeconds": 10,
        "ExpectedStatusCodes": [200],
        "CriticalAfterFailures": 3
      },
      "PracantD": {
        "Enabled": true,
        "DisplayName": "PracantD Service",
        "Host": "192.168.225.221",
        "Port": 54321,
        "TimeoutSeconds": 5,
        "RawCommand": "01",
        "ExpectedResponse": "OK",
        "CriticalAfterFailures": 3
      }
    }
  }
}
```

### HTTP Service Options

| Vlastnost | Typ | Popis |
|-----------|-----|-------|
| `Enabled` | bool | Zapnout/vypnout monitoring |
| `DisplayName` | string | Nazev pro zobrazeni v UI |
| `Url` | string | URL pro health check |
| `TimeoutSeconds` | int | Timeout pripojeni |
| `ExpectedStatusCodes` | int[] | Ocekavane HTTP kody (default: [200]) |
| `ExpectedContent` | string? | Ocekavany obsah v odpovedi |
| `IgnoreSslErrors` | bool | Ignorovat SSL chyby (pro self-signed certs) |
| `CriticalAfterFailures` | int | Pocet selhani pred critical stavem |

### Telnet/TCP Service Options (PracantD)

| Vlastnost | Typ | Popis |
|-----------|-----|-------|
| `Enabled` | bool | Zapnout/vypnout monitoring |
| `DisplayName` | string | Nazev pro zobrazeni v UI |
| `Host` | string | IP adresa nebo hostname |
| `Port` | int | Port cislo |
| `TimeoutSeconds` | int | Timeout pripojeni |
| `Command` | string? | Textovy prikaz k odeslani |
| `RawCommand` | string? | Hex prikaz (napr. "01" = byte 0x01) |
| `ExpectedResponse` | string? | Ocekavana odpoved |
| `ConnectionOnly` | bool | Jen overit pripojeni (ignoruje Command/ExpectedResponse) |
| `CriticalAfterFailures` | int | Pocet selhani pred critical stavem |

### PracantD Protokol

PracantD pouziva binarni protokol:
- **Ping**: Posle byte `0x01` + `\n`
- **Odpoved**: Server odpovi `OK\n`

Konfigurace pro PracantD:
```json
"PracantD": {
  "Host": "192.168.225.221",
  "Port": 54321,
  "RawCommand": "01",
  "ExpectedResponse": "OK"
}
```

### Vlastni sluzby

Pro vice instanci stejneho typu sluzby pouzijte Custom slovniky:

```json
{
  "Watchdog": {
    "Services": {
      "CustomMariaDbServices": {
        "MariaDB_5336": {
          "Enabled": true,
          "DisplayName": "MariaDB Port 5336",
          "ConnectionString": "Server=192.168.222.113;Port=5336;User=user;Password=pass;",
          "TimeoutSeconds": 10,
          "CriticalAfterFailures": 3
        },
        "MariaDB_5337": {
          "Enabled": true,
          "DisplayName": "MariaDB Port 5337",
          "ConnectionString": "Server=192.168.222.113;Port=5337;User=user;Password=pass;",
          "TimeoutSeconds": 10,
          "CriticalAfterFailures": 3
        }
      },
      "CustomHttpServices": {
        "Apache81": {
          "Enabled": true,
          "DisplayName": "Apache Web Server 81",
          "Url": "http://192.168.222.114:81/health.php",
          "TimeoutSeconds": 10,
          "ExpectedStatusCodes": [200],
          "CriticalAfterFailures": 3
        },
        "Apache82": {
          "Enabled": true,
          "DisplayName": "Apache Web Server 82",
          "Url": "http://192.168.222.114:82/health.php",
          "TimeoutSeconds": 10,
          "ExpectedStatusCodes": [200],
          "CriticalAfterFailures": 3
        }
      },
            "CustomTelnetServices": {
              "PracantD_Server1": {
                "Enabled": true,
                "DisplayName": "PracantD Server 1",
                "Host": "192.168.1.100",
                "Port": 54321,
                "RawCommand": "01",
                "ExpectedResponse": "OK",
                "CriticalAfterFailures": 3
              }
            },
            "CustomBackgroundServices": {
              "BGS_Database2": {
                "Enabled": true,
                "DisplayName": "Background Service DB2",
                "ConnectionString": "Server=192.168.222.113;Port=5337;User=user;Password=pass;",
                "DatabaseName": "sud_utf8_bbb",
                "TableName": "system",
                "ColumnName": "bgs_last_run",
                "MaxAgeMinutes": 5,
                "CriticalAfterFailures": 3
              }
            }
          }
        }
      }
      ```

      ### Struktura sluzeb

      | Typ | Pevna instance | Custom instances (libovolny pocet) |
      |-----|---------------|-----------------------------------|
      | MariaDB | `MariaDB` | `CustomMariaDbServices` |
      | HTTP | `LicenseManager`, `Apache` | `CustomHttpServices` |
      | Telnet | `PracantD` | `CustomTelnetServices` |
      | Background | `BackgroundService` | `CustomBackgroundServices` |

      ### Background Service Options

      Monitoruje casovy razitko posledniho behu sluzby v databazi.

      | Vlastnost | Typ | Popis |
      |-----------|-----|-------|
      | `Enabled` | bool | Zapnout/vypnout monitoring |
      | `DisplayName` | string | Nazev pro zobrazeni v UI |
      | `ConnectionString` | string | Pripojeni k databazi |
      | `DatabaseName` | string | Nazev databaze (default: sud_utf8_aaa) |
      | `TableName` | string | Nazev tabulky (default: system) |
      | `ColumnName` | string | Nazev sloupce s casem (default: bgs_last_run) |
      | `MaxAgeMinutes` | int | Max stari v minutach (default: 5) |
      | `SystemRowId` | int | ID radku v tabulce (default: 1) |
      | `CriticalAfterFailures` | int | Pocet selhani pred critical stavem |

### Email notifikace

```json
{
  "Notifications": {
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "Username": "watchdog@example.com",
      "Password": "your-password",
      "FromAddress": "watchdog@example.com",
      "FromName": "Emistr Watchdog",
      "Recipients": [
        "admin@example.com",
        "ops@example.com"
      ],
      "CooldownMinutes": 15
    }
  }
}
```

### Critical alerty

```json
{
  "Notifications": {
    "CriticalEvents": {
      "EnableSound": true,
      "EnableDesktopNotification": true,
      "LogToEventLog": true,
      "SoundFilePath": "C:\\sounds\\alert.wav"
    }
  }
}
```

### Dashboard

```json
{
  "Dashboard": {
    "Enabled": true,
    "Port": 5050,
    "RequireAuthentication": false,
    "Title": "Emistr System Monitor"
  }
}
```

## Web Dashboard

Dashboard poskytuje real-time prehled vsech monitorovanych sluzeb:

- **Stav sluzeb** - Healthy/Degraded/Unhealthy/Critical
- **Response time** - Doba odezvy v ms
- **Pocet selhani** - Consecutive failures counter
- **Live updates** - Automaticka aktualizace pres SignalR

### Pristup

- **URL:** `http://localhost:5050`
- **API:** `http://localhost:5050/api/status`

## API Endpoints

| Endpoint | Metoda | Popis |
|----------|--------|-------|
| `/` | GET | Web dashboard |
| `/api/status` | GET | Aktualni stav vsech sluzeb |
| `/api/status/{serviceName}` | GET | Stav konkretni sluzby |
| `/api/health` | GET | Health check samotneho watchdogu |
| `/api/history/{serviceName}` | GET | Historie kontroly sluzby |
| `/hub/dashboard` | WebSocket | SignalR hub pro real-time updates |

### Priklad API odpovedi

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "overallStatus": "Healthy",
  "healthyCount": 3,
  "unhealthyCount": 1,
  "criticalCount": 0,
  "services": [
    {
      "serviceName": "MariaDB",
      "displayName": "MariaDB Database",
      "status": "Healthy",
      "isHealthy": true,
            "isCritical": false,
            "responseTimeMs": 12,
            "lastCheckAt": "2024-01-15T10:30:00Z",
            "consecutiveFailures": 0,
            "serverInfo": {
              "version": "10.11.6-MariaDB",
              "serverType": "MariaDB",
              "platform": "Linux",
              "architecture": "x86_64",
              "additionalInfo": {
                "InnoDB": "10.11.6",
                "MaxConnections": "151"
              }
            }
          }
        ]
      }
      ```

      ## Nastaveni monitorovanych sluzeb

      ### Apache - health.php (rozsirena verze)

      Pro ziskani informaci o serveru (verze Apache, PHP, architektura) pouzijte rozsirenou verzi:

      Soubor je k dispozici v `docs/health.php` - zkopirujte do document root Apache.

      ```php
      <?php
      /**
       * Extended Health Check Endpoint for Apache/PHP
       * Returns JSON with server information for Emistr Watchdog
       */

      header('Content-Type: application/json; charset=utf-8');
      header('Cache-Control: no-cache, no-store, must-revalidate');

      $healthy = true;

      // Get Apache version
      $apacheVersion = function_exists('apache_get_version') 
          ? apache_get_version() 
          : ($_SERVER['SERVER_SOFTWARE'] ?? null);

      $response = [
          'status' => $healthy ? 'OK' : 'UNHEALTHY',
          'timestamp' => date('c'),

          // Server information for Watchdog
          'server_type' => 'Apache',
          'apache_version' => $apacheVersion,
          'php_version' => PHP_VERSION,
          'os' => PHP_OS_FAMILY,
          'architecture' => php_uname('m'),
          'server_software' => $_SERVER['SERVER_SOFTWARE'] ?? null,
          'document_root' => $_SERVER['DOCUMENT_ROOT'] ?? null,

          // PHP configuration
          'php_config' => [
              'memory_limit' => ini_get('memory_limit'),
              'max_execution_time' => ini_get('max_execution_time'),
          ],
      ];

      http_response_code($healthy ? 200 : 503);
      echo json_encode($response, JSON_PRETTY_PRINT);
      ```

      **Umisteni:**
      - Linux: `/var/www/html/health.php`
      - Windows WAMP: `C:\wamp64\www\health.php`
      - Windows XAMPP: `C:\xampp\htdocs\health.php`

      ### Apache - health.php (jednoducha verze)

      Pro zakladni health check bez informaci o serveru:

      ```php
      <?php
      header('Content-Type: text/plain; charset=utf-8');
      header('Cache-Control: no-cache, no-store, must-revalidate');

      http_response_code(200);
      echo 'OK';
      ```

### ASP.NET Core - healthz endpoint

Pro .NET aplikace pridejte health check endpoint:

```csharp
// Program.cs
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/healthz");
```

### MariaDB - watchdog user

Vytvorte uzivatele pro health check (minimalni prava):

```sql
CREATE USER 'watchdog'@'%' IDENTIFIED BY 'watchdog_password';
GRANT USAGE ON *.* TO 'watchdog'@'%';
FLUSH PRIVILEGES;
```

## Notifikace

### Email

Emaily jsou odesilany pri:
- Zmene stavu sluzby na Unhealthy
- Dosazeni kritickeho stavu (po X po sobe jdoucich selhanich)
- Obnove sluzby (recovery)

### Windows Event Log

Critical eventy jsou logovany do Windows Event Log:
- Source: `Emistr.Watchdog`
- Log: `Application`
- Event ID: `1001`

Pro registraci zdroje (jako admin):
```powershell
New-EventLog -LogName Application -Source 'Emistr.Watchdog'
```

### Desktop notifikace

Na Windows se zobrazi toast notifikace pri kritickych udalostech.

## Testovani

```bash
cd src/Emistr.Watchdog.Tests
dotnet test
```

## Logovani

Watchdog pouziva Serilog s konfigurovatelnymi sinky:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/watchdog-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

## Rozsireni

### Vlastni Health Checker

```csharp
public class MyCustomHealthChecker : HealthCheckerBase
{
    public override string ServiceName => "MyService";
    public override string DisplayName => "My Custom Service";
    public override bool IsEnabled => true;
    public override int CriticalThreshold => 3;

    protected override async Task<HealthCheckResult> PerformCheckAsync(
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        // Vase logika kontroly
        var isHealthy = await CheckMyServiceAsync(cancellationToken);
        
        sw.Stop();
        
        return isHealthy
            ? HealthCheckResult.Healthy(ServiceName, sw.ElapsedMilliseconds)
            : HealthCheckResult.Unhealthy(ServiceName, "Service not responding");
    }
}
```

Registrace v `Program.cs`:

```csharp
builder.Services.AddSingleton<IHealthChecker, MyCustomHealthChecker>();
```

## Troubleshooting

### PracantD "Response does not contain expected content"

Zkontrolujte:
1. Spravnou IP adresu a port
2. Ze server odpovida na ping (byte 0x01)
3. Pouzijte `ConnectionOnly: true` pro test pripojeni

### SSL Certificate errors

Pro development prostredi s self-signed certifikaty:
```json
"IgnoreSslErrors": true
```

### Windows Event Log permission denied

Spustte jednou jako administrator:
```powershell
New-EventLog -LogName Application -Source 'Emistr.Watchdog'
```

## Licence

MIT License - viz [LICENSE](../../LICENSE)

## Podpora

Pro podporu kontaktujte vyvojovy tym Emistr.

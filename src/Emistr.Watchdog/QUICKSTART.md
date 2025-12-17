# Emistr.Watchdog v0.2.0 - Rychly start

## Nove ve verzi 0.2.0

- **Automaticky restart Windows sluzeb** pri kritickem selhani
- Konfigurace restartu pro kazdy monitorovany service
- Sledovani pokusu o restart

**POZOR:** Pro restart funkci musi Watchdog bezet s **Administrator pravy** (Windows).

---

## Nasazeni za 5 minut

### Windows (bez Dockeru)

```powershell
# 1. Stahni release (nebo publikuj sam)
dotnet publish -c Release -r win-x64 --self-contained -o C:\Emistr\Watchdog

# 2. Uprav konfiguraci
notepad C:\Emistr\Watchdog\appsettings.json

# 3. Spust (jako Administrator pro restart funkci)
cd C:\Emistr\Watchdog
.\Emistr.Watchdog.exe

# 4. Otevri dashboard
start http://localhost:5050
```

### Linux (bez Dockeru)

```bash
# 1. Stahni release (nebo publikuj sam)
dotnet publish -c Release -r linux-x64 --self-contained -o /opt/emistr/watchdog

# 2. Uprav konfiguraci
nano /opt/emistr/watchdog/appsettings.json

# 3. Spust
cd /opt/emistr/watchdog
./Emistr.Watchdog

# 4. Otevri dashboard
xdg-open http://localhost:5050
```

---

## Minimalni konfigurace

Uprav `appsettings.json` - povol jen sluzby ktere chces monitorovat:

```json
{
  "Watchdog": {
    "Services": {
      "MariaDB": {
        "Enabled": true,
        "ConnectionString": "Server=TVUJ_SERVER;Port=3306;User=watchdog;Password=HESLO;"
      }
    }
  }
}
```

---

## Docker nasazeni

```bash
# 1. Build image
docker build -t emistr-watchdog ./src/Emistr.Watchdog

# 2. Spust kontejner
docker run -d \
  --name watchdog \
  -p 5050:5050 \
  -v ./config/appsettings.json:/app/appsettings.json:ro \
  emistr-watchdog

# 3. Otevri dashboard
open http://localhost:5050
```

### Docker Compose

```yaml
version: '3.8'
services:
  watchdog:
    build: ./src/Emistr.Watchdog
    ports:
      - "5050:5050"
    volumes:
      - ./config/watchdog-appsettings.json:/app/appsettings.json:ro
      - watchdog_logs:/app/logs
    restart: unless-stopped

volumes:
  watchdog_logs:
```

```bash
docker-compose up -d
```

---

## Instalace jako sluzba

### Windows Service

```powershell
# Spustit jako Administrator
sc.exe create "Emistr.Watchdog" binPath="C:\Emistr\Watchdog\Emistr.Watchdog.exe" start=auto
sc.exe start "Emistr.Watchdog"
```

### Linux Systemd

```bash
# Zkopiruj unit soubor
sudo cp deploy/linux/emistr-watchdog.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable emistr-watchdog
sudo systemctl start emistr-watchdog
```

---

## Overeni instalace

```bash
# API status
curl http://localhost:5050/api/status

# Health check
curl http://localhost:5050/api/health
```

Dashboard: **http://localhost:5050**

---

## Priklady konfigurace sluzeb

### MariaDB s automatickym restartem
```json
"MariaDB": {
  "Enabled": true,
  "DisplayName": "MariaDB Database",
  "ConnectionString": "Server=192.168.1.100;Port=3306;User=watchdog;Password=xxx;",
  "CriticalAfterFailures": 3,
  "RestartConfig": {
    "Enabled": true,
    "WindowsServiceName": "MySQL",
    "MaxRestartAttempts": 3,
    "RestartDelaySeconds": 30,
    "RestartOnCritical": true
  }
}
```

### HTTP/Apache
```json
"CustomHttpServices": {
  "Apache": {
    "Enabled": true,
    "DisplayName": "Apache Server",
    "Url": "http://192.168.1.100/health.php",
    "ExpectedStatusCodes": [200],
    "RestartConfig": {
      "Enabled": true,
      "WindowsServiceName": "Apache2.4",
      "MaxRestartAttempts": 3,
      "RestartDelaySeconds": 30,
      "RestartOnCritical": true
    }
  }
}
```

### PracantD
```json
"PracantD": {
  "Enabled": true,
  "DisplayName": "PracantD",
  "Host": "192.168.1.100",
  "Port": 54321,
  "RawCommand": "01",
  "ExpectedResponse": "OK"
}
```

### Background Service (bgs_last_run)
```json
"BackgroundService": {
  "Enabled": true,
  "DisplayName": "Background Service",
  "ConnectionString": "Server=192.168.1.100;Port=3306;User=watchdog;Password=xxx;",
  "DatabaseName": "sud_utf8_aaa",
  "TableName": "system",
  "ColumnName": "bgs_last_run",
  "MaxAgeMinutes": 5
}
```

---

## Apache - health.php

Zkopiruj `docs/health.php` na Apache server do document root.

---

## Reseni problemu

| Problem | Reseni |
|---------|--------|
| Port 5050 je obsazeny | Zmen `Dashboard.Port` v appsettings.json |
| MariaDB connection failed | Zkontroluj connection string a firewall |
| Event Log chyba | Spust `New-EventLog -LogName Application -Source 'Emistr.Watchdog'` jako admin |

Podrobny navod: viz `DEPLOYMENT.md`

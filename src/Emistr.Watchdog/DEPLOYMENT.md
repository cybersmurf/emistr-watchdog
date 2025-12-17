# Emistr.Watchdog - Deployment Plan

Navod pro konzultanty k nasazeni monitorovaci sluzby Emistr.Watchdog.

---

## Obsah

1. [Pozadavky](#pozadavky)
2. [Priprava serveru](#priprava-serveru)
3. [Konfigurace sluzeb](#konfigurace-sluzeb)
4. [Instalace Watchdog](#instalace-watchdog)
5. [Overeni funkcnosti](#overeni-funkcnosti)
6. [Troubleshooting](#troubleshooting)

---

## Pozadavky

### Server
- Windows Server 2016+ nebo Windows 10/11
- .NET 9 Runtime (nebo self-contained deploy)
- Pristup k monitorovanym sluzbam (sitova konektivita)
- Port 5050 volny pro dashboard (nebo jiny dle konfigurace)

### Sitove pozadavky
| Sluzba | Port | Protokol | Smer |
|--------|------|----------|------|
| MariaDB | 3306, 5336, 4311... | TCP | Outbound |
| Apache/HTTP | 80, 81, 82... | HTTP/HTTPS | Outbound |
| PracantD | 54321 | TCP | Outbound |
| Dashboard | 5050 | HTTP | Inbound |
| SMTP (volitelne) | 587 | TCP/TLS | Outbound |

---

## Priprava serveru

### 1. Instalace .NET 9 Runtime (pokud neni self-contained)

```powershell
# Stazeni a instalace .NET 9 Runtime
winget install Microsoft.DotNet.Runtime.9
```

Nebo stahnete z: https://dotnet.microsoft.com/download/dotnet/9.0

### 2. Vytvoreni adresare

```powershell
# Vytvoreni adresare pro aplikaci
New-Item -ItemType Directory -Path "C:\Program Files\Emistr\Watchdog" -Force

# Vytvoreni adresare pro logy
New-Item -ItemType Directory -Path "C:\Program Files\Emistr\Watchdog\logs" -Force
```

### 3. Registrace Windows Event Log (jako Administrator)

```powershell
# Spustit jako Administrator!
New-EventLog -LogName Application -Source 'Emistr.Watchdog'
```

### 4. Firewall pravidlo pro dashboard

```powershell
# Povolit port 5050 pro dashboard
New-NetFirewallRule -DisplayName "Emistr Watchdog Dashboard" `
    -Direction Inbound -Protocol TCP -LocalPort 5050 -Action Allow
```

---

## Konfigurace sluzeb

### 1. Apache - Vytvoreni health.php

Na kazdem Apache serveru vytvorte soubor `health.php` v document root:

```php
<?php
header('Content-Type: text/plain; charset=utf-8');
header('Cache-Control: no-cache, no-store, must-revalidate');

// Volitelne: kontrola DB
$healthy = true;

if ($healthy) {
    http_response_code(200);
    echo 'OK';
} else {
    http_response_code(503);
    echo 'UNHEALTHY';
}
```

**Umisteni:**
- `/var/www/html/health.php` (Linux)
- `C:\Apache24\htdocs\health.php` (Windows)

**Overeni:**
```bash
curl http://192.168.222.114:81/health.php
# Ocekavana odpoved: OK
```

### 2. MariaDB - Vytvoreni watchdog uzivatele

```sql
-- Pripojte se k MariaDB jako root
mysql -u root -p

-- Vytvorte uzivatele s minimalni pravou
CREATE USER 'watchdog'@'%' IDENTIFIED BY 'watchdog_secure_password';
GRANT USAGE ON *.* TO 'watchdog'@'%';
FLUSH PRIVILEGES;
```

**Overeni:**
```bash
mysql -h 192.168.222.113 -P 5336 -u watchdog -p -e "SELECT 1"
```

### 3. PracantD

PracantD nepotrebuje specialni konfiguraci - Watchdog posila standardni ping (byte 0x01).

**Overeni pripojeni:**
```powershell
Test-NetConnection -ComputerName 192.168.225.221 -Port 54321
```

### 4. License Manager API

Overeni, ze `/healthz` endpoint existuje:

```powershell
Invoke-WebRequest -Uri "https://localhost:7115/healthz" -SkipCertificateCheck
```

---

## Instalace Watchdog

### Varianta A: Publikovani (doporuceno)

Na vyvojovem pocitaci:

```powershell
cd src\Emistr.Watchdog

# Self-contained (nevyzaduje .NET runtime na serveru)
dotnet publish -c Release -r win-x64 --self-contained -o publish

# Nebo framework-dependent (mensi velikost, vyzaduje .NET runtime)
dotnet publish -c Release -r win-x64 --no-self-contained -o publish
```

### Kopirovani na server

```powershell
# Kopirovani souboru na server
Copy-Item -Path "publish\*" -Destination "\\SERVER\C$\Program Files\Emistr\Watchdog\" -Recurse
```

### Konfigurace appsettings.json

Upravte `C:\Program Files\Emistr\Watchdog\appsettings.json`:

```json
{
  "Dashboard": {
    "Enabled": true,
    "Port": 5050,
    "Title": "Emistr System Monitor - PRODUKCE"
  },
  "Watchdog": {
    "CheckIntervalSeconds": 30,
    "Services": {
      "MariaDB": {
        "Enabled": true,
        "DisplayName": "MariaDB Primary",
        "ConnectionString": "Server=192.168.222.113;Port=5336;User=watchdog;Password=HESLO;Connection Timeout=5;",
        "TimeoutSeconds": 10,
        "CriticalAfterFailures": 3
      },
      "LicenseManager": {
        "Enabled": true,
        "DisplayName": "License Manager API",
        "Url": "https://HOSTNAME:7115/healthz",
        "IgnoreSslErrors": true,
        "CriticalAfterFailures": 3
      },
      "Apache": {
        "Enabled": false
      },
      "PracantD": {
        "Enabled": true,
        "DisplayName": "PracantD Server",
        "Host": "192.168.225.221",
        "Port": 54321,
        "RawCommand": "01",
        "ExpectedResponse": "OK",
        "CriticalAfterFailures": 3
      },
      "CustomHttpServices": {
        "Apache81": {
          "Enabled": true,
          "DisplayName": "Apache Port 81",
          "Url": "http://192.168.222.114:81/health.php",
          "CriticalAfterFailures": 3
        },
        "Apache82": {
          "Enabled": true,
          "DisplayName": "Apache Port 82",
          "Url": "http://192.168.222.114:82/health.php",
          "CriticalAfterFailures": 3
        }
      },
      "CustomMariaDbServices": {
        "MariaDB_4311": {
          "Enabled": true,
          "DisplayName": "MariaDB Port 4311",
          "ConnectionString": "Server=192.168.222.113;Port=4311;User=watchdog;Password=HESLO;",
          "CriticalAfterFailures": 3
        }
      },
      "CustomTelnetServices": {}
    }
  },
  "Notifications": {
    "Email": {
      "Enabled": false
    },
    "CriticalEvents": {
      "EnableSound": false,
      "EnableDesktopNotification": true,
      "LogToEventLog": true
    }
  }
}
```

### Instalace jako Windows Service

```powershell
# Jako Administrator!
$servicePath = "C:\Program Files\Emistr\Watchdog\Emistr.Watchdog.exe"

# Vytvoreni sluzby
sc.exe create "Emistr.Watchdog" binPath="$servicePath" start=auto
sc.exe description "Emistr.Watchdog" "Emistr System Monitor - sledovani zdravi systemu"

# Konfigurace restartu pri selhani
sc.exe failure "Emistr.Watchdog" reset=86400 actions=restart/60000/restart/60000/restart/60000

# Spusteni sluzby
sc.exe start "Emistr.Watchdog"

# Overeni stavu
sc.exe query "Emistr.Watchdog"
```

### Varianta B: Rucni spusteni (pro testovani)

```powershell
cd "C:\Program Files\Emistr\Watchdog"
.\Emistr.Watchdog.exe
```

---

## Overeni funkcnosti

### 1. Kontrola logu

```powershell
# Zobrazeni poslednich logu
Get-Content "C:\Program Files\Emistr\Watchdog\logs\watchdog-*.log" -Tail 50
```

### 2. Pristup na dashboard

Otevrete prohlizec a prejdete na:
```
http://SERVER_IP:5050
```

### 3. API test

```powershell
# Stav vsech sluzeb
Invoke-RestMethod -Uri "http://localhost:5050/api/status"

# Health check samotneho Watchdogu
Invoke-RestMethod -Uri "http://localhost:5050/api/health"
```

### 4. Kontrola Windows Event Log

```powershell
# Zobrazeni poslednich udalosti
Get-EventLog -LogName Application -Source "Emistr.Watchdog" -Newest 10
```

---

## Troubleshooting

### Problem: Sluzba se nespusti

**Reseni:**
```powershell
# Zkontrolujte Event Log
Get-EventLog -LogName Application -Source "Emistr.Watchdog" -Newest 5

# Spustte rucne pro zobrazeni chyb
cd "C:\Program Files\Emistr\Watchdog"
.\Emistr.Watchdog.exe
```

### Problem: Dashboard neni dostupny

**Reseni:**
1. Zkontrolujte firewall
2. Overite port v appsettings.json
3. Zkontrolujte, ze sluzba bezi

```powershell
netstat -an | findstr 5050
```

### Problem: MariaDB connection failed

**Reseni:**
1. Overite sitovou konektivitu
2. Zkontrolujte credentials
3. Overite, ze uzivatel ma pristup z dane IP

```powershell
Test-NetConnection -ComputerName 192.168.222.113 -Port 5336
mysql -h 192.168.222.113 -P 5336 -u watchdog -p -e "SELECT 1"
```

### Problem: PracantD neni healthy

**Reseni:**
1. Overite TCP pripojeni
2. Zkontrolujte, ze server odpovida na ping

```powershell
Test-NetConnection -ComputerName 192.168.225.221 -Port 54321

# Manual test (PowerShell)
$client = New-Object System.Net.Sockets.TcpClient
$client.Connect("192.168.225.221", 54321)
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream)
$reader = New-Object System.IO.StreamReader($stream)
$writer.WriteLine([char]1)  # Posle 0x01
$writer.Flush()
Start-Sleep -Milliseconds 500
$response = $reader.ReadLine()
Write-Host "Response: $response"  # Melo by byt 'OK'
$client.Close()
```

### Problem: Event Log permission denied

**Reseni:**
```powershell
# Jako Administrator
New-EventLog -LogName Application -Source 'Emistr.Watchdog'
```

### Problem: SSL certificate error

**Reseni:**
V appsettings.json pro danou HTTP sluzbu nastavte:
```json
"IgnoreSslErrors": true
```

---

## Checklist pro konzultanty

- [ ] Server splnuje pozadavky
- [ ] .NET 9 Runtime nainstalovan (nebo self-contained deploy)
- [ ] Adresar `C:\Program Files\Emistr\Watchdog` vytvoren
- [ ] Event Log source registrovan
- [ ] Firewall pravidlo pro port 5050
- [ ] health.php na Apache serverech
- [ ] watchdog uzivatel v MariaDB
- [ ] appsettings.json nakonfigurovano
- [ ] Windows Service nainstalovana a spustena
- [ ] Dashboard dostupny na http://SERVER:5050
- [ ] Vsechny sluzby zobrazuji spravny stav
- [ ] Logy se zapisuji do `logs/` adresare

---

## Kontakt

Pri problemech kontaktujte vyvojovy tym Emistr.

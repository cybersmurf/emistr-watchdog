# Emistr Services - Windows Installation Script
# Run as Administrator!

param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\Emistr",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipLicenseService,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipWatchdog
)

# Check for admin rights
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Emistr Services Installation Script  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create directories
Write-Host "[1/6] Creating directories..." -ForegroundColor Yellow
$directories = @(
    "$InstallPath\LicenseService",
    "$InstallPath\LicenseService\logs",
    "$InstallPath\Watchdog",
    "$InstallPath\Watchdog\logs"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $dir" -ForegroundColor Gray
    }
}

# Register Event Log sources
Write-Host ""
Write-Host "[2/6] Registering Event Log sources..." -ForegroundColor Yellow

$eventSources = @("Emistr.LicenseService", "Emistr.Watchdog")
foreach ($source in $eventSources) {
    try {
        if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
            New-EventLog -LogName Application -Source $source
            Write-Host "  Registered: $source" -ForegroundColor Green
        } else {
            Write-Host "  Already registered: $source" -ForegroundColor Gray
        }
    } catch {
        Write-Host "  Warning: Could not register $source - $_" -ForegroundColor Yellow
    }
}

# Configure firewall
Write-Host ""
Write-Host "[3/6] Configuring firewall rules..." -ForegroundColor Yellow

$firewallRules = @(
    @{Name="Emistr LicenseService HTTPS"; Port=7115; Description="Emistr License Service API"},
    @{Name="Emistr Watchdog Dashboard"; Port=5050; Description="Emistr Watchdog Dashboard"}
)

foreach ($rule in $firewallRules) {
    $existingRule = Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue
    if (-not $existingRule) {
        New-NetFirewallRule -DisplayName $rule.Name `
            -Direction Inbound `
            -Protocol TCP `
            -LocalPort $rule.Port `
            -Action Allow `
            -Description $rule.Description | Out-Null
        Write-Host "  Created: $($rule.Name) (Port $($rule.Port))" -ForegroundColor Green
    } else {
        Write-Host "  Exists: $($rule.Name)" -ForegroundColor Gray
    }
}

# Install LicenseService
if (-not $SkipLicenseService) {
    Write-Host ""
    Write-Host "[4/6] Installing Emistr.LicenseService..." -ForegroundColor Yellow
    
    $licenseServicePath = "$InstallPath\LicenseService\Emistr.LicenseService.exe"
    $serviceName = "Emistr.LicenseService"
    
    # Check if service exists
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    if ($existingService) {
        Write-Host "  Service already exists, stopping..." -ForegroundColor Yellow
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $serviceName | Out-Null
        Start-Sleep -Seconds 2
    }
    
    if (Test-Path $licenseServicePath) {
        sc.exe create $serviceName binPath="$licenseServicePath" start=auto DisplayName="Emistr License Service" | Out-Null
        sc.exe description $serviceName "Emistr License Management API - Manages software licenses" | Out-Null
        sc.exe failure $serviceName reset=86400 actions=restart/60000/restart/60000/restart/60000 | Out-Null
        Write-Host "  Service created: $serviceName" -ForegroundColor Green
    } else {
        Write-Host "  Warning: Executable not found at $licenseServicePath" -ForegroundColor Yellow
        Write-Host "  Please copy the published files to $InstallPath\LicenseService\" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "[4/6] Skipping LicenseService installation" -ForegroundColor Gray
}

# Install Watchdog
if (-not $SkipWatchdog) {
    Write-Host ""
    Write-Host "[5/6] Installing Emistr.Watchdog..." -ForegroundColor Yellow
    
    $watchdogPath = "$InstallPath\Watchdog\Emistr.Watchdog.exe"
    $serviceName = "Emistr.Watchdog"
    
    # Check if service exists
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    
    if ($existingService) {
        Write-Host "  Service already exists, stopping..." -ForegroundColor Yellow
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $serviceName | Out-Null
        Start-Sleep -Seconds 2
    }
    
    if (Test-Path $watchdogPath) {
        sc.exe create $serviceName binPath="$watchdogPath" start=auto DisplayName="Emistr Watchdog" | Out-Null
        sc.exe description $serviceName "Emistr System Monitor - Monitors system health and sends alerts" | Out-Null
        sc.exe failure $serviceName reset=86400 actions=restart/60000/restart/60000/restart/60000 | Out-Null
        Write-Host "  Service created: $serviceName" -ForegroundColor Green
    } else {
        Write-Host "  Warning: Executable not found at $watchdogPath" -ForegroundColor Yellow
        Write-Host "  Please copy the published files to $InstallPath\Watchdog\" -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "[5/6] Skipping Watchdog installation" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "[6/6] Installation Summary" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$services = Get-Service -Name "Emistr.*" -ErrorAction SilentlyContinue
if ($services) {
    Write-Host ""
    Write-Host "Installed Services:" -ForegroundColor Green
    $services | Format-Table Name, Status, StartType -AutoSize
} else {
    Write-Host "No Emistr services found." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Copy published files to installation directories" -ForegroundColor White
Write-Host "  2. Configure appsettings.json for each service" -ForegroundColor White
Write-Host "  3. Start services: Start-Service Emistr.*" -ForegroundColor White
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Cyan
Write-Host "  Start-Service Emistr.LicenseService" -ForegroundColor Gray
Write-Host "  Start-Service Emistr.Watchdog" -ForegroundColor Gray
Write-Host "  Get-Service Emistr.*" -ForegroundColor Gray
Write-Host ""

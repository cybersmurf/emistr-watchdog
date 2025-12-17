# Emistr Services - Windows Uninstall Script
# Run as Administrator!

param(
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\Emistr",
    
    [Parameter(Mandatory=$false)]
    [switch]$RemoveFiles,
    
    [Parameter(Mandatory=$false)]
    [switch]$RemoveLogs
)

# Check for admin rights
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Emistr Services Uninstall Script     " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Stop and remove services
Write-Host "[1/4] Stopping and removing services..." -ForegroundColor Yellow

$serviceNames = @("Emistr.LicenseService", "Emistr.Watchdog")

foreach ($serviceName in $serviceNames) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "  Stopping: $serviceName" -ForegroundColor Yellow
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        
        Write-Host "  Removing: $serviceName" -ForegroundColor Yellow
        sc.exe delete $serviceName | Out-Null
        Write-Host "  Removed: $serviceName" -ForegroundColor Green
    } else {
        Write-Host "  Not found: $serviceName" -ForegroundColor Gray
    }
}

# Remove firewall rules
Write-Host ""
Write-Host "[2/4] Removing firewall rules..." -ForegroundColor Yellow

$firewallRules = @("Emistr LicenseService HTTPS", "Emistr Watchdog Dashboard")

foreach ($ruleName in $firewallRules) {
    $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($rule) {
        Remove-NetFirewallRule -DisplayName $ruleName
        Write-Host "  Removed: $ruleName" -ForegroundColor Green
    } else {
        Write-Host "  Not found: $ruleName" -ForegroundColor Gray
    }
}

# Remove Event Log sources (optional - may fail if logs exist)
Write-Host ""
Write-Host "[3/4] Event Log sources..." -ForegroundColor Yellow
Write-Host "  Note: Event Log sources are not removed to preserve logs" -ForegroundColor Gray
Write-Host "  To remove manually: Remove-EventLog -Source 'Emistr.LicenseService'" -ForegroundColor Gray

# Remove files
if ($RemoveFiles) {
    Write-Host ""
    Write-Host "[4/4] Removing installation files..." -ForegroundColor Yellow
    
    if ($RemoveLogs) {
        # Remove everything
        if (Test-Path $InstallPath) {
            Remove-Item -Path $InstallPath -Recurse -Force
            Write-Host "  Removed: $InstallPath (including logs)" -ForegroundColor Green
        }
    } else {
        # Keep logs, remove executables
        $dirsToClean = @(
            "$InstallPath\LicenseService",
            "$InstallPath\Watchdog"
        )
        
        foreach ($dir in $dirsToClean) {
            if (Test-Path $dir) {
                Get-ChildItem -Path $dir -Exclude "logs" | Remove-Item -Recurse -Force
                Write-Host "  Cleaned: $dir (logs preserved)" -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host ""
    Write-Host "[4/4] Keeping installation files" -ForegroundColor Gray
    Write-Host "  Use -RemoveFiles to delete files" -ForegroundColor Gray
    Write-Host "  Use -RemoveFiles -RemoveLogs to delete everything" -ForegroundColor Gray
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Uninstallation complete!" -ForegroundColor Green
Write-Host ""

$remainingServices = Get-Service -Name "Emistr.*" -ErrorAction SilentlyContinue
if ($remainingServices) {
    Write-Host "Remaining services:" -ForegroundColor Yellow
    $remainingServices | Format-Table Name, Status -AutoSize
} else {
    Write-Host "All Emistr services have been removed." -ForegroundColor Green
}

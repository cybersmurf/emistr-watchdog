# check-service.ps1 - Windows Service Health Check
# Usage: .\check-service.ps1 -ServiceName "MyService"
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceName
)

try {
    $service = Get-Service -Name $ServiceName -ErrorAction Stop

    switch ($service.Status) {
        "Running" {
            Write-Host "OK: Service '$ServiceName' is running"
            exit 0
        }
        "Stopped" {
            Write-Host "CRITICAL: Service '$ServiceName' is stopped"
            exit 2
        }
        "Paused" {
            Write-Host "WARNING: Service '$ServiceName' is paused"
            exit 1
        }
        "StartPending" {
            Write-Host "WARNING: Service '$ServiceName' is starting"
            exit 1
        }
        "StopPending" {
            Write-Host "WARNING: Service '$ServiceName' is stopping"
            exit 1
        }
        default {
            Write-Host "WARNING: Service '$ServiceName' status: $($service.Status)"
            exit 1
        }
    }
}
catch {
    Write-Host "CRITICAL: Service '$ServiceName' not found or error: $($_.Exception.Message)"
    exit 2
}


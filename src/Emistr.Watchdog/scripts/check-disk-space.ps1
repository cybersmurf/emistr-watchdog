# Check Disk Space - PowerShell Health Check Script
# Returns 0 if OK, 1 if warning, 2 if critical
# Output: JSON with disk usage info

param(
    [int]$Threshold = 80,
    [int]$CriticalThreshold = 95,
    [string]$Drive = "C:"
)

$ErrorActionPreference = "Stop"

try {
    $disk = Get-PSDrive -Name ($Drive -replace ':','') -PSProvider FileSystem
    
    $usedPercent = [math]::Round(($disk.Used / ($disk.Used + $disk.Free)) * 100, 1)
    $freeGB = [math]::Round($disk.Free / 1GB, 2)
    $totalGB = [math]::Round(($disk.Used + $disk.Free) / 1GB, 2)
    
    $result = @{
        drive = $Drive
        usedPercent = $usedPercent
        freeGB = $freeGB
        totalGB = $totalGB
        status = "OK"
    }
    
    if ($usedPercent -ge $CriticalThreshold) {
        $result.status = "CRITICAL"
        Write-Output ($result | ConvertTo-Json -Compress)
        exit 2
    }
    elseif ($usedPercent -ge $Threshold) {
        $result.status = "WARNING"
        Write-Output ($result | ConvertTo-Json -Compress)
        exit 1
    }
    else {
        Write-Output ($result | ConvertTo-Json -Compress)
        exit 0
    }
}
catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    exit 3
}


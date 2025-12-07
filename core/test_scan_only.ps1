# Simplified test script - ONLY scans applications (doesn't open anything)
# Usage: .\test_scan_only.ps1

$baseUrl = "http://localhost:5055"

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  APPLICATION SCANNER - SCAN ONLY" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

function Send-JarvisCommand {
    param (
        [string]$Action,
        [hashtable]$Params = @{}
    )

    $body = @{
        action = $Action
        params = $Params
        uuid = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json -Depth 10

    Write-Host "> Executing: $Action" -ForegroundColor Cyan

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/action/execute" -Method Post -Body $body -ContentType "application/json"

        if ($response.status -eq "ok") {
            Write-Host "[OK] SUCCESS" -ForegroundColor Green
            return $response
        } else {
            Write-Host "[FAIL] ERROR: $($response.error)" -ForegroundColor Red
            return $null
        }
    } catch {
        Write-Host "[FAIL] FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

# Check server status
Write-Host "[0] CHECKING SERVER STATUS" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
try {
    $status = Invoke-RestMethod -Uri "$baseUrl/" -Method Get
    Write-Host "[OK] Server is running: $($status.message)" -ForegroundColor Green
    Write-Host "    Version: $($status.version)" -ForegroundColor Gray
} catch {
    Write-Host "[FAIL] Server is not running! Start it first with 'dotnet run'" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 1: Scan applications
Write-Host "`n[1] SCANNING APPLICATIONS" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  This will scan your system for installed applications..." -ForegroundColor Gray
Write-Host "  Expected time: 5-15 seconds" -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date
$scanResult = Send-JarvisCommand -Action "scan_applications"
$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

if ($scanResult) {
    Write-Host "`nScan completed in $([math]::Round($duration, 2)) seconds!" -ForegroundColor Green
    Write-Host ""

    $stats = $scanResult.result.statistics
    Write-Host "SCAN RESULTS:" -ForegroundColor Cyan
    Write-Host "  Total Applications: $($stats.totalApplications)" -ForegroundColor White
    Write-Host "  System Applications: $($stats.systemApplications)" -ForegroundColor White
    Write-Host "  User Applications: $($stats.userApplications)" -ForegroundColor White
    Write-Host "  Last Updated: $($stats.lastUpdated)" -ForegroundColor Gray

    Write-Host "`n  Categories:" -ForegroundColor Cyan
    foreach ($cat in $stats.categories) {
        Write-Host "    - $($cat.category): $($cat.count)" -ForegroundColor Gray
    }

    Write-Host "`n  Registry saved to: core/Data/applications.json" -ForegroundColor Yellow
} else {
    Write-Host "`nScan failed!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 2: List all applications
Write-Host "`n[2] LISTING ALL APPLICATIONS" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$listResult = Send-JarvisCommand -Action "list_applications"

if ($listResult) {
    Write-Host "`nFound $($listResult.result.count) applications:" -ForegroundColor Green
    Write-Host ""

    # Group by category
    $appsByCategory = $listResult.result.applications | Group-Object -Property category | Sort-Object Name

    foreach ($categoryGroup in $appsByCategory) {
        Write-Host "  [$($categoryGroup.Name)]" -ForegroundColor Cyan
        foreach ($app in $categoryGroup.Group | Sort-Object Name) {
            Write-Host "    - $($app.name)" -ForegroundColor White
            if ($app.aliases.Count -gt 0) {
                Write-Host "      Aliases: $($app.aliases -join ', ')" -ForegroundColor DarkGray
            }
        }
        Write-Host ""
    }
}
Write-Host ""

# Summary
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  SUMMARY" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue

if ($scanResult -and $listResult) {
    Write-Host "  Status: SUCCESS!" -ForegroundColor Green
    Write-Host "  Total apps scanned: $($stats.totalApplications)" -ForegroundColor White
    Write-Host "  Scan duration: $([math]::Round($duration, 2)) seconds" -ForegroundColor White
    Write-Host "  Registry file: core/Data/applications.json" -ForegroundColor White

    Write-Host "`n  Next steps:" -ForegroundColor Cyan
    Write-Host "    - Open applications with: Send-JarvisCommand -Action 'open_app' -Params @{ application = 'steam' }" -ForegroundColor Gray
    Write-Host "    - Server will load these apps instantly on next restart!" -ForegroundColor Gray
} else {
    Write-Host "  Status: FAILED" -ForegroundColor Red
}

Write-Host "========================================`n" -ForegroundColor Blue

# Test script for popular applications
# Usage: Run this script after starting the C# server

$baseUrl = "http://localhost:5055"

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  POPULAR APPS TEST" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

function Test-App {
    param (
        [string]$AppName,
        [string]$DisplayName
    )

    $body = @{
        action = "open_app"
        params = @{ application = $AppName }
        uuid = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json

    Write-Host "> Testing: $DisplayName ($AppName)" -ForegroundColor Cyan

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/action/execute" -Method Post -Body $body -ContentType "application/json"

        if ($response.status -eq "ok") {
            Write-Host "[OK] $DisplayName opened successfully" -ForegroundColor Green
            return $true
        } else {
            Write-Host "[FAIL] $($response.error)" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "[FAIL] $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Test apps
$apps = @(
    @{ Name = "calculator"; Display = "Calculator" },
    @{ Name = "notepad"; Display = "Notepad" },
    @{ Name = "discord"; Display = "Discord" },
    @{ Name = "telegram"; Display = "Telegram" },
    @{ Name = "vscode"; Display = "VS Code" },
    @{ Name = "spotify"; Display = "Spotify" },
    @{ Name = "steam"; Display = "Steam" }
)

$results = @()

foreach ($app in $apps) {
    $result = Test-App -AppName $app.Name -DisplayName $app.Display
    $results += @{ App = $app.Display; Success = $result }
    Write-Host ""
    Start-Sleep -Milliseconds 500
}

# Summary
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  TEST SUMMARY" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

$passed = ($results | Where-Object { $_.Success }).Count
$total = $results.Count

foreach ($result in $results) {
    $status = if ($result.Success) { "[PASS]" } else { "[FAIL]" }
    $color = if ($result.Success) { "Green" } else { "Red" }
    Write-Host "  $($result.App.PadRight(20)) $status" -ForegroundColor $color
}

Write-Host "`n========================================" -ForegroundColor Blue
$summaryColor = if ($passed -eq $total) { "Green" } else { "Yellow" }
Write-Host "  Total: $total | Passed: $passed | Failed: $($total - $passed)" -ForegroundColor $summaryColor
Write-Host "========================================`n" -ForegroundColor Blue

if ($passed -eq $total) {
    Write-Host "SUCCESS! All apps tested!" -ForegroundColor Green
} else {
    Write-Host "NOTE: Some apps may not be installed on this system" -ForegroundColor Yellow
}

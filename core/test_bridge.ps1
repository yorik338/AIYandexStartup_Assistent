# Test script for C# bridge connectivity
# PowerShell version

Write-Host "`n============================================================" -ForegroundColor Blue
Write-Host "  JARVIS C# Bridge Connection Test" -ForegroundColor Blue
Write-Host "============================================================`n" -ForegroundColor Blue

$baseUrl = "http://localhost:5055"
$passed = 0
$total = 0

# Test 1: Root endpoint
Write-Host "> Testing GET / ..." -ForegroundColor Cyan
$total++
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/" -Method GET -TimeoutSec 5
    if ($response.service -eq "JarvisCore") {
        Write-Host "[OK] Root endpoint: $($response.service) v$($response.version)" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "[FAIL] Root endpoint returned unexpected data" -ForegroundColor Red
    }
} catch {
    Write-Host "[FAIL] Root endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: System status
Write-Host "`n> Testing GET /system/status ..." -ForegroundColor Cyan
$total++
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/system/status" -Method GET -TimeoutSec 5
    if ($response.status -eq "ok") {
        Write-Host "[OK] System status: uptime=$($response.result.uptimeMs)ms" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "[FAIL] System status returned error: $($response.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "[FAIL] System status failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Action execute
Write-Host "`n> Testing POST /action/execute ..." -ForegroundColor Cyan
$total++
try {
    $body = @{
        action = "system_status"
        params = @{}
        uuid = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/action/execute" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 10

    if ($response.status -eq "ok") {
        Write-Host "[OK] Action execute: Command executed successfully" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "[FAIL] Action execute returned error: $($response.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "[FAIL] Action execute failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "`n============================================================" -ForegroundColor Blue
Write-Host "  Test Results" -ForegroundColor Blue
Write-Host "============================================================`n" -ForegroundColor Blue

if ($passed -eq $total) {
    Write-Host "  [SUCCESS] All $total tests passed!" -ForegroundColor Green
    Write-Host "`n============================================================`n" -ForegroundColor Blue
    exit 0
} else {
    Write-Host "  [FAILED] $($total - $passed) of $total tests failed" -ForegroundColor Red
    Write-Host "`n============================================================`n" -ForegroundColor Blue
    Write-Host "Make sure C# server is running:" -ForegroundColor Yellow
    Write-Host "  cd core" -ForegroundColor White
    Write-Host "  dotnet run`n" -ForegroundColor White
    exit 1
}

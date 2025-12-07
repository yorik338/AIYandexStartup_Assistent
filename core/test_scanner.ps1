# Test script for application scanner
# Usage: Run this script after starting the C# server

$baseUrl = "http://localhost:5055"

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  APPLICATION SCANNER TEST" -ForegroundColor Blue
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

    Write-Host "> Testing: $Action" -ForegroundColor Cyan

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

# Test 1: Scan applications
Write-Host "`n[1] SCAN_APPLICATIONS TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  This may take 30-60 seconds..." -ForegroundColor Gray
$scanResult = Send-JarvisCommand -Action "scan_applications"

if ($scanResult) {
    Write-Host "`nScan Results:" -ForegroundColor Green
    $stats = $scanResult.result.statistics
    Write-Host "  Total Applications: $($stats.totalApplications)" -ForegroundColor White
    Write-Host "  System Applications: $($stats.systemApplications)" -ForegroundColor White
    Write-Host "  User Applications: $($stats.userApplications)" -ForegroundColor White

    Write-Host "`n  Categories:" -ForegroundColor Cyan
    foreach ($cat in $stats.categories) {
        Write-Host "    - $($cat.category): $($cat.count)" -ForegroundColor Gray
    }
}
Write-Host ""

# Test 2: List all applications
Write-Host "`n[2] LIST_APPLICATIONS TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$listResult = Send-JarvisCommand -Action "list_applications"

if ($listResult) {
    Write-Host "`nFound $($listResult.result.count) applications:" -ForegroundColor Green

    # Show first 10 applications
    $apps = $listResult.result.applications | Select-Object -First 10
    foreach ($app in $apps) {
        Write-Host "  [$($app.category)] $($app.name)" -ForegroundColor White
        Write-Host "    Aliases: $($app.aliases -join ', ')" -ForegroundColor Gray
        Write-Host "    Path: $($app.path)" -ForegroundColor DarkGray
    }

    if ($listResult.result.count -gt 10) {
        Write-Host "`n  ... and $($listResult.result.count - 10) more" -ForegroundColor Gray
    }
}
Write-Host ""

# Test 3: List applications by category
Write-Host "`n[3] LIST_APPLICATIONS TEST (Communication category)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$categoryResult = Send-JarvisCommand -Action "list_applications" -Params @{ category = "Communication" }

if ($categoryResult) {
    Write-Host "`nCommunication apps ($($categoryResult.result.count)):" -ForegroundColor Green
    foreach ($app in $categoryResult.result.applications) {
        Write-Host "  - $($app.name)" -ForegroundColor White
        Write-Host "    Aliases: $($app.aliases -join ', ')" -ForegroundColor Gray
    }
}
Write-Host ""

# Test 4: Try to open an application
Write-Host "`n[4] OPEN_APP TEST (Calculator)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$openResult = Send-JarvisCommand -Action "open_app" -Params @{ application = "calculator" }

if ($openResult) {
    Write-Host "`nOpened: $($openResult.result.application)" -ForegroundColor Green
    Write-Host "  Category: $($openResult.result.category)" -ForegroundColor Gray
    Write-Host "  Process ID: $($openResult.result.processId)" -ForegroundColor Gray
    Write-Host "  Path: $($openResult.result.path)" -ForegroundColor DarkGray
}
Write-Host ""

# Test 5: Try to open with alias
Write-Host "`n[5] OPEN_APP TEST (calc - using alias)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$aliasResult = Send-JarvisCommand -Action "open_app" -Params @{ application = "calc" }

if ($aliasResult) {
    Write-Host "`nOpened: $($aliasResult.result.application)" -ForegroundColor Green
    Write-Host "  Process ID: $($aliasResult.result.processId)" -ForegroundColor Gray
}
Write-Host ""

# Summary
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  TEST SUMMARY" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue

$tests = @(
    @{ Name = "Scan applications"; Result = $scanResult },
    @{ Name = "List all applications"; Result = $listResult },
    @{ Name = "List by category"; Result = $categoryResult },
    @{ Name = "Open application"; Result = $openResult },
    @{ Name = "Open with alias"; Result = $aliasResult }
)

$passed = 0
$failed = 0

foreach ($test in $tests) {
    if ($test.Result -and $test.Result.status -eq "ok") {
        $passed++
        Write-Host "  $($test.Name.PadRight(30)) [PASS]" -ForegroundColor Green
    } else {
        $failed++
        Write-Host "  $($test.Name.PadRight(30)) [FAIL]" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Blue
$summaryColor = if ($failed -eq 0) { "Green" } else { "Yellow" }
Write-Host "  Total: $($tests.Count) | Passed: $passed | Failed: $failed" -ForegroundColor $summaryColor
Write-Host "========================================`n" -ForegroundColor Blue

if ($failed -eq 0) {
    Write-Host "SUCCESS! All tests passed!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "  - Check the Data/applications.json file" -ForegroundColor White
    Write-Host "  - Try opening different applications by name or alias" -ForegroundColor White
    Write-Host "  - Applications are automatically discovered!" -ForegroundColor White
} else {
    Write-Host "WARNING: Some tests failed. Check the logs!" -ForegroundColor Yellow
}

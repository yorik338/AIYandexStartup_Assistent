# Test script for new file system commands
# Usage: Run this script after starting the C# server

$baseUrl = "http://localhost:5055"
$testFolder = "$env:USERPROFILE\Desktop\JarvisTest"

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  FILE SYSTEM COMMANDS TEST" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

function Send-JarvisCommand {
    param (
        [string]$Action,
        [hashtable]$Params
    )

    $body = @{
        action = $Action
        params = $Params
        uuid = [guid]::NewGuid().ToString()
        timestamp = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json -Depth 10

    Write-Host "→ Testing: $Action" -ForegroundColor Cyan
    Write-Host "  Params: $($Params | ConvertTo-Json -Compress)" -ForegroundColor Gray

    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/action/execute" -Method Post -Body $body -ContentType "application/json"

        if ($response.status -eq "ok") {
            Write-Host "✓ SUCCESS" -ForegroundColor Green
            Write-Host "  Result: $($response.result | ConvertTo-Json -Compress)" -ForegroundColor Gray
        } else {
            Write-Host "✗ ERROR: $($response.error)" -ForegroundColor Red
        }

        return $response
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }

    Write-Host ""
}

# Test 1: Create folder
Write-Host "`n[1] CREATE_FOLDER TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result1 = Send-JarvisCommand -Action "create_folder" -Params @{ path = $testFolder }
Write-Host ""

# Test 2: Create nested folder
Write-Host "`n[2] CREATE_FOLDER TEST (nested)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result2 = Send-JarvisCommand -Action "create_folder" -Params @{ path = "$testFolder\SubFolder\NestedFolder" }
Write-Host ""

# Test 3: Copy file (create test file first)
Write-Host "`n[3] COPY_FILE TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$testFile = "$testFolder\test.txt"
$copiedFile = "$testFolder\test_copy.txt"
"Test content for JARVIS" | Out-File -FilePath $testFile -Encoding utf8
Write-Host "  Created test file: $testFile" -ForegroundColor Gray
$result3 = Send-JarvisCommand -Action "copy_file" -Params @{ source = $testFile; destination = $copiedFile }
Write-Host ""

# Test 4: Move file
Write-Host "`n[4] MOVE_FILE TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$movedFile = "$testFolder\SubFolder\test_moved.txt"
$result4 = Send-JarvisCommand -Action "move_file" -Params @{ source = $copiedFile; destination = $movedFile }
Write-Host ""

# Test 5: Try to create folder that already exists
Write-Host "`n[5] CREATE_FOLDER TEST (already exists)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result5 = Send-JarvisCommand -Action "create_folder" -Params @{ path = $testFolder }
Write-Host ""

# Test 6: Try to access forbidden path (should fail)
Write-Host "`n[6] CREATE_FOLDER TEST (forbidden path - should fail)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result6 = Send-JarvisCommand -Action "create_folder" -Params @{ path = "C:\Windows\System32\JarvisTest" }
Write-Host ""

# Test 7: Delete nested folder
Write-Host "`n[7] DELETE_FOLDER TEST (nested)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result7 = Send-JarvisCommand -Action "delete_folder" -Params @{ path = "$testFolder\SubFolder" }
Write-Host ""

# Test 8: Delete main test folder
Write-Host "`n[8] DELETE_FOLDER TEST (cleanup)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$result8 = Send-JarvisCommand -Action "delete_folder" -Params @{ path = $testFolder }
Write-Host ""

# Summary
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  TEST SUMMARY" -ForegroundColor Blue
Write-Host "========================================" -ForegroundColor Blue

$tests = @(
    @{ Name = "Create folder"; Result = $result1 },
    @{ Name = "Create nested folder"; Result = $result2 },
    @{ Name = "Copy file"; Result = $result3 },
    @{ Name = "Move file"; Result = $result4 },
    @{ Name = "Create existing folder"; Result = $result5 },
    @{ Name = "Forbidden path (should fail)"; Result = $result6 },
    @{ Name = "Delete nested folder"; Result = $result7 },
    @{ Name = "Delete main folder"; Result = $result8 }
)

$passed = 0
$failed = 0

foreach ($test in $tests) {
    $status = if ($test.Result -and $test.Result.status -eq "ok") {
        $passed++
        "✓ PASS"
    } elseif ($test.Name -like "*should fail*" -and $test.Result.status -eq "error") {
        $passed++
        "✓ PASS (expected error)"
    } else {
        $failed++
        "✗ FAIL"
    }

    $color = if ($status -like "*PASS*") { "Green" } else { "Red" }
    Write-Host "  $($test.Name.PadRight(30)) [$status]" -ForegroundColor $color
}

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  Total: $($tests.Count) | Passed: $passed | Failed: $failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "========================================`n" -ForegroundColor Blue

if ($failed -eq 0) {
    Write-Host "🎉 ВСЕ ЧИКИ-ПУКИ! Все тесты прошли!" -ForegroundColor Green
} else {
    Write-Host "⚠ Некоторые тесты провалились. Проверь логи!" -ForegroundColor Yellow
}

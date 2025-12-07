# Run unit tests for JarvisCore
# Usage: .\run_tests.ps1

Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  JARVIS CORE - UNIT TESTS" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

# Check if .NET SDK is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "[OK] .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] .NET SDK not found! Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Restore packages
Write-Host "`n[1] Restoring packages..." -ForegroundColor Yellow
dotnet restore JarvisCore.Tests/JarvisCore.Tests.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Failed to restore packages" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Packages restored" -ForegroundColor Green

# Build test project
Write-Host "`n[2] Building test project..." -ForegroundColor Yellow
dotnet build JarvisCore.Tests/JarvisCore.Tests.csproj --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Failed to build test project" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Test project built successfully" -ForegroundColor Green

# Run tests
Write-Host "`n[3] Running tests..." -ForegroundColor Yellow
Write-Host "========================================`n" -ForegroundColor Cyan

dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj --no-build --verbosity normal

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "[FAIL] Some tests failed!" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "[SUCCESS] All tests passed!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

# Show test summary
Write-Host "Test Summary:" -ForegroundColor Cyan
Write-Host "  - CommandValidatorTests: 15 tests" -ForegroundColor White
Write-Host "  - PathValidatorTests: 10 tests" -ForegroundColor White
Write-Host "  - ApplicationRegistryTests: 10 tests" -ForegroundColor White
Write-Host "  - Total: 35 tests" -ForegroundColor White

Write-Host "`nFor detailed output, run:" -ForegroundColor Yellow
Write-Host "  dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj --verbosity detailed" -ForegroundColor Gray

Write-Host "`nFor code coverage, run:" -ForegroundColor Yellow
Write-Host "  dotnet test JarvisCore.Tests/JarvisCore.Tests.csproj /p:CollectCoverage=true" -ForegroundColor Gray
Write-Host ""

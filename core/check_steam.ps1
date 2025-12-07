# Check Steam installation and game libraries
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "  STEAM LIBRARY CHECKER" -ForegroundColor Blue
Write-Host "========================================`n" -ForegroundColor Blue

# Check standard Steam installation path
$steamPath = "C:\Program Files (x86)\Steam\steam.exe"
if (Test-Path $steamPath) {
    Write-Host "[OK] Steam found at: $steamPath" -ForegroundColor Green
} else {
    Write-Host "[!] Steam NOT found at standard location" -ForegroundColor Yellow
    Write-Host "    Checking alternative locations..." -ForegroundColor Gray

    # Check other possible locations
    $altPaths = @(
        "C:\Program Files\Steam\steam.exe",
        "D:\Steam\steam.exe",
        "E:\Steam\steam.exe",
        "D:\Program Files (x86)\Steam\steam.exe"
    )

    foreach ($path in $altPaths) {
        if (Test-Path $path) {
            Write-Host "[OK] Steam found at: $path" -ForegroundColor Green
            $steamPath = $path
            break
        }
    }
}

# Check Steam library folders
$libraryConfigPath = "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf"
if (Test-Path $libraryConfigPath) {
    Write-Host "`n[OK] Library config found!" -ForegroundColor Green
    Write-Host "Reading Steam library folders..." -ForegroundColor Cyan

    $content = Get-Content $libraryConfigPath -Raw

    # Extract paths from VDF file
    $pathPattern = '"path"\s+"([^"]+)"'
    $matches = [regex]::Matches($content, $pathPattern)

    Write-Host "`nSteam Library Folders:" -ForegroundColor Yellow
    foreach ($match in $matches) {
        $libraryPath = $match.Groups[1].Value
        $commonPath = Join-Path $libraryPath "steamapps\common"

        Write-Host "  - $commonPath" -ForegroundColor White

        if (Test-Path $commonPath) {
            $games = Get-ChildItem $commonPath -Directory | Select-Object -First 5
            Write-Host "    Games found: $($games.Count)" -ForegroundColor Gray
            foreach ($game in $games) {
                Write-Host "      * $($game.Name)" -ForegroundColor DarkGray
            }
        }
    }
} else {
    Write-Host "`n[!] Library config not found at: $libraryConfigPath" -ForegroundColor Yellow
}

# Check common game installation directories
Write-Host "`n========================================" -ForegroundColor Blue
Write-Host "Checking common game directories:" -ForegroundColor Yellow

$gameDirs = @(
    "C:\Program Files (x86)\Steam\steamapps\common",
    "C:\Program Files\Steam\steamapps\common",
    "D:\SteamLibrary\steamapps\common",
    "D:\Steam\steamapps\common",
    "E:\SteamLibrary\steamapps\common",
    "C:\Program Files\Epic Games",
    "D:\Epic Games",
    "C:\GOG Games",
    "D:\GOG Games"
)

foreach ($dir in $gameDirs) {
    if (Test-Path $dir) {
        $gameCount = (Get-ChildItem $dir -Directory -ErrorAction SilentlyContinue).Count
        Write-Host "  [OK] $dir ($gameCount games)" -ForegroundColor Green
    } else {
        Write-Host "  [ ] $dir (not found)" -ForegroundColor DarkGray
    }
}

Write-Host "`n========================================`n" -ForegroundColor Blue

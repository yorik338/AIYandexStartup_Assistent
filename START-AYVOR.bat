@echo off
title AYVOR Assistant Launcher
color 0A

REM Remove ELECTRON_RUN_AS_NODE
set ELECTRON_RUN_AS_NODE=

echo.
echo  ============================================
echo    AYVOR AI Assistant - Starting...
echo  ============================================
echo.

echo [1/2] Starting C# Core Backend...
start "AYVOR Core" cmd /k "cd core && dotnet run"

echo [2/2] Waiting 5 seconds for core to start...
timeout /t 5 /nobreak > nul

echo [2/2] Starting Electron GUI...
start "AYVOR GUI" cmd /k "cd jarvis-gui && npm start"

echo.
echo ============================================
echo  AYVOR is starting!
echo  - Core: http://localhost:5055
echo  - GUI: Opening in new window...
echo ============================================
echo.
echo Closing launcher in 2 seconds...
timeout /t 2 /nobreak > nul
exit

@echo off
echo ========================================
echo Starting AYVOR GUI (Electron)
echo ========================================
cd jarvis-gui

REM Remove ELECTRON_RUN_AS_NODE if it exists
set ELECTRON_RUN_AS_NODE=

echo Starting Electron...
npm start
pause

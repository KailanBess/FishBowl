@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install FishBowl.ps1"
if errorlevel 1 (
    echo FishBowl could not be installed.
    pause
)

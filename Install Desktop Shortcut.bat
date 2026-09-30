@echo off
setlocal
set "FISHBOWL_APP=%~dp0FishBowl.exe"

if not exist "%FISHBOWL_APP%" (
    echo FishBowl.exe was not found beside this installer.
    pause
    exit /b 1
)

powershell -NoProfile -Command "$shell = New-Object -ComObject WScript.Shell; $link = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'FishBowl.lnk')); $link.TargetPath = $env:FISHBOWL_APP; $link.WorkingDirectory = Split-Path $env:FISHBOWL_APP; $link.IconLocation = (Join-Path (Split-Path $env:FISHBOWL_APP) 'FishBowl.ico') + ',0'; $link.Description = 'FishBowl Emulator Hub'; $link.Save()"

if errorlevel 1 (
    echo FishBowl could not create the desktop shortcut.
    pause
    exit /b 1
)

echo A FishBowl shortcut was added to your desktop.
pause

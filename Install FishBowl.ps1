$ErrorActionPreference = 'Stop'

$source = Split-Path -Parent $PSCommandPath
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\FishBowl'
$programsFolder = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$startMenuFolder = Join-Path $programsFolder 'FishBowl'

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Get-ChildItem -LiteralPath $source -Force |
    Where-Object { $_.Name -ne 'FishBowl - Shortcut.lnk' -and $_.Name -ne 'portable.flag' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $installRoot -Recurse -Force }

New-Item -ItemType Directory -Path $startMenuFolder -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenuFolder 'FishBowl.lnk'))
$shortcut.TargetPath = Join-Path $installRoot 'FishBowl.exe'
$shortcut.WorkingDirectory = $installRoot
$shortcut.IconLocation = (Join-Path $installRoot 'FishBowl.ico') + ',0'
$shortcut.Description = 'FishBowl Emulator Hub'
$shortcut.Save()

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FishBowl'
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value 'FishBowl' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value '1.0' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value 'FishBowl' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'DisplayIcon' -Value (Join-Path $installRoot 'FishBowl.ico') -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value ('"' + (Join-Path $installRoot 'Uninstall FishBowl.bat') + '"') -PropertyType String -Force | Out-Null

Write-Host 'FishBowl is installed. Find it in Start, or search for FishBowl.'
Start-Sleep -Seconds 2

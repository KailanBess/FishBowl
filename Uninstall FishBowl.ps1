$ErrorActionPreference = 'Stop'

$installRoot = Split-Path -Parent $PSCommandPath
$programsFolder = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
$startMenuFolder = Join-Path $programsFolder 'FishBowl'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FishBowl'
$appData = Join-Path $env:LOCALAPPDATA 'FishBowl'
$portableData = Join-Path $installRoot 'FishBowlData'

Add-Type -AssemblyName System.Windows.Forms
$choice = [System.Windows.Forms.MessageBox]::Show('Keep your FishBowl library, settings, and backups?', 'Uninstall FishBowl', [System.Windows.Forms.MessageBoxButtons]::YesNoCancel, [System.Windows.Forms.MessageBoxIcon]::Question)
if ($choice -eq [System.Windows.Forms.DialogResult]::Cancel) { exit 0 }
if ($choice -eq [System.Windows.Forms.DialogResult]::Yes -and (Test-Path -LiteralPath $portableData)) {
    $saved = Join-Path $appData ('UninstalledLibrary-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $appData -Force | Out-Null
    Copy-Item -LiteralPath $portableData -Destination $saved -Recurse -Force
}
if ($choice -eq [System.Windows.Forms.DialogResult]::No -and (Test-Path -LiteralPath $appData)) {
    Remove-Item -LiteralPath $appData -Recurse -Force
}

if (Test-Path -LiteralPath $startMenuFolder) {
    Remove-Item -LiteralPath $startMenuFolder -Recurse -Force
}
if (Test-Path -LiteralPath $uninstallKey) {
    Remove-Item -LiteralPath $uninstallKey -Recurse -Force
}

$cleanup = 'timeout /t 2 /nobreak > nul & rmdir /s /q "' + $installRoot + '"'
Start-Process -FilePath $env:ComSpec -ArgumentList '/c', $cleanup -WindowStyle Hidden

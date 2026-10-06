param([switch]$SkipSetup)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location $PSScriptRoot
try {
 & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:FishBowl.exe FishBowl.cs FishBowl.InputPainting.cs FishBowl.TextFit.cs FishBowl.UiPolish.cs FishBowl.GameTools.cs FishBowl.Integrations.cs ..\FishBowl.GameRecognition.cs ..\FishBowl.Model.cs ..\FishBowl.Library.cs ..\FishBowl.GameTools.cs ..\FishBowl.Integrations.cs ..\FishBowl.SaveTools.cs
 if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
 if (-not $SkipSetup) {
  & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico '/out:FishBowl Setup.exe' 'FishBowl.Setup.cs'
  if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
 }
 Write-Output 'FishBowl 1.26.0 built.'
} finally { Pop-Location }

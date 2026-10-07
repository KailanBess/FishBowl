param([switch]$SkipSetup)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location $PSScriptRoot
try {
 & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico /resource:..\remote-play\client\index.html,RemotePlay.index.html /resource:..\remote-play\client\client.js,RemotePlay.client.js /resource:..\remote-play\client\controls.js,RemotePlay.controls.js /resource:..\remote-play\client\style.css,RemotePlay.style.css /resource:..\remote-play\client\livekit.js,RemotePlay.livekit.js /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:FishBowl.exe FishBowl.cs FishBowl.InputPainting.cs FishBowl.TextFit.cs FishBowl.UiPolish.cs FishBowl.Navigation.cs Play.cs FishBowl.Sessions.cs FishBowl.RemotePlay.cs ..\FishBowl.Sessions.cs FishBowl.ColorHarmony.cs FishBowl.GameTools.cs FishBowl.Integrations.cs ..\FishBowl.GameRecognition.cs ..\FishBowl.Model.cs ..\FishBowl.Library.cs ..\FishBowl.GameTools.cs ..\FishBowl.Integrations.cs ..\FishBowl.SaveTools.cs
 if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
 if (-not $SkipSetup) {
  & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico '/out:FishBowl Setup.exe' 'FishBowl.Setup.cs'
  if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
 }
 Write-Output 'FishBowl 1.28.0 built.'
} finally { Pop-Location }

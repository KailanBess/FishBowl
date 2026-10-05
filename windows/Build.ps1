$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico /r:System.Xml.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:FishBowl.exe FishBowl.cs FishBowl.LibraryAdditions.cs FishBowl.CompactMenus.cs FishBowl.Cosmetics.cs FishBowl.UserTools.cs FishBowl.Polish.cs FishBowl.Hub.cs FishBowl.HubRuntime.cs FishBowl.ReleaseWatch.cs FishBowl.Immersion.cs FishBowl.Fluid.cs
    if ($LASTEXITCODE -ne 0) { throw 'FishBowl build failed' }
    & $compiler /nologo /target:winexe /win32icon:FishBowl.ico /resource:FishBowl.png,FishBowl.png /resource:FishBowl.ico,FishBowl.ico '/out:FishBowl Setup.exe' FishBowl.Setup.cs
    if ($LASTEXITCODE -ne 0) { throw 'FishBowl Setup build failed' }
} finally { Pop-Location }

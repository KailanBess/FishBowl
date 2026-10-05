# Opens every FishBowl window at 100%, 150% and 200% text size and reports text that doesn't fit.
# Output: overflow-audit\overflow-report.md and screenshots (red outlines mark clipped text). Run after Build.ps1.
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'overflow-audit'
$fixture = Join-Path $env:TEMP ('FishBowl-Overflow-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'FishBowl.exe'),(Join-Path $PSScriptRoot 'OverflowAudit.cs') $fixture
Set-Content (Join-Path $fixture 'portable.flag') ''
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $fixture
try {
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:OverflowAudit.exe OverflowAudit.cs
    if ($LASTEXITCODE -ne 0) { throw 'Overflow audit compilation failed' }
    & '.\OverflowAudit.exe' $output
    if ($LASTEXITCODE -ne 0) { throw 'Overflow audit failed to run' }
} finally { Pop-Location; Remove-Item $fixture -Recurse -Force -ErrorAction SilentlyContinue }

$ErrorActionPreference = 'Stop'
$fixture = Join-Path $env:TEMP ('FishBowl-1.24-Tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'FishBowl.exe') $fixture
Copy-Item (Join-Path $PSScriptRoot 'IntegrationTests.cs'),(Join-Path $PSScriptRoot 'AdditionTests.cs') $fixture
Copy-Item (Join-Path $PSScriptRoot 'CompactTests.cs') $fixture
Copy-Item (Join-Path $PSScriptRoot 'CosmeticTests.cs') $fixture
Copy-Item (Join-Path $PSScriptRoot 'UserToolsTests.cs') $fixture
Copy-Item (Join-Path $PSScriptRoot 'PolishTests.cs'),(Join-Path $PSScriptRoot 'AuditTests.cs'),(Join-Path $PSScriptRoot 'FishBowl Setup.exe') $fixture
Copy-Item (Join-Path $PSScriptRoot 'HubTests.cs'),(Join-Path $PSScriptRoot 'ImmersionTests.cs'),(Join-Path $PSScriptRoot 'FluidTests.cs') $fixture
Copy-Item (Join-Path $PSScriptRoot 'TextFieldTests.cs'),(Join-Path $PSScriptRoot 'RowPaintTests.cs'),(Join-Path $PSScriptRoot 'PauseAnimationTests.cs') $fixture
Set-Content (Join-Path $fixture 'portable.flag') ''
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $fixture
try {
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Tests.exe IntegrationTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed' }
    & '.\Tests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:AdditionTests.exe AdditionTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Addition test compilation failed' }
    & '.\AdditionTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Addition tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:CompactTests.exe CompactTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Compact test compilation failed' }
    & '.\CompactTests.exe' current-menus.txt
    if ($LASTEXITCODE -ne 0) { throw 'Compact tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:CosmeticTests.exe CosmeticTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Cosmetic test compilation failed' }
    & '.\CosmeticTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Cosmetic tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:UserToolsTests.exe UserToolsTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'User tool test compilation failed' }
    & '.\UserToolsTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'User tool tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:PolishTests.exe PolishTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Polish test compilation failed' }
    & '.\PolishTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Polish tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:AuditTests.exe AuditTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Audit test compilation failed' }
    & '.\AuditTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Audit tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:HubTests.exe HubTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Hub extension test compilation failed' }
    & '.\HubTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Hub extension tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:ImmersionTests.exe ImmersionTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Immersion test compilation failed' }
    & '.\ImmersionTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Immersion tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:FluidTests.exe FluidTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Fluid test compilation failed' }
    & '.\FluidTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Fluid tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:TextFieldTests.exe TextFieldTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Text field test compilation failed' }
    & '.\TextFieldTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Text field tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:RowPaintTests.exe RowPaintTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Row paint test compilation failed' }
    & '.\RowPaintTests.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Row paint tests failed' }
    & $compiler /nologo /r:FishBowl.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /out:PauseAnimationTests.exe PauseAnimationTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Animation pause test compilation failed' }
    & '.\PauseAnimationTests.exe' --pause-ui-animation
    if ($LASTEXITCODE -ne 0) { throw 'Animation pause tests failed' }
    Write-Host "Test fixtures and previews: $fixture"
} finally { Pop-Location }

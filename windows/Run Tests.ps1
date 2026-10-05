param([switch]$FullVisual)
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $env:TEMP ('FB-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FishBowl.exe'), (Join-Path $PSScriptRoot 'FishBowl Setup.exe') -Destination $fixture
Set-Content -LiteralPath (Join-Path $fixture 'portable.flag') -Value ''
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
Push-Location $fixture
try {
 foreach ($testName in @('IntegrationTests','NextRegressionTests','RecognitionTests','AzaharStorageTests','VisualRegressionTests','VisualMatrixTests','EmulatorEditorTests','SmoothUiTests','PopupCloseTests','PopupPositionTests','PopupAnimationTests')) {
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Tests\' + $testName + '.cs')) -Destination $fixture
  & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$testName.exe" "$testName.cs"
  if ($LASTEXITCODE -ne 0) { throw "$testName compilation failed." }
  $testArgs = @()
  if ($testName -eq 'VisualMatrixTests' -and $FullVisual) { $testArgs += '--full' }
  & (Join-Path $fixture ($testName + '.exe')) @testArgs
  if ($LASTEXITCODE -ne 0) { throw "$testName failed." }
 }
 foreach ($legacyTest in @('AdditionTests','CompactTests','CosmeticTests','UserToolsTests','PolishTests','AuditTests','HubTests','ImmersionTests','FluidTests','TextFieldTests','RowPaintTests','PauseAnimationTests')) {
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ($legacyTest + '.cs')) -Destination $fixture
  & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$legacyTest.exe" "$legacyTest.cs"
  if ($LASTEXITCODE -ne 0) { throw "$legacyTest compilation failed." }
  $legacyArgs = @()
  if ($legacyTest -eq 'CompactTests') { $legacyArgs += 'current-menus.txt' }
  if ($legacyTest -eq 'PauseAnimationTests') { $legacyArgs += '--pause-ui-animation' }
  & (Join-Path $fixture ($legacyTest + '.exe')) @legacyArgs
  if ($LASTEXITCODE -ne 0) { throw "$legacyTest failed." }
 }
 Write-Output "Test fixtures and previews: $fixture"
} finally { Pop-Location }

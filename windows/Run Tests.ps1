param([switch]$FullVisual)
$ErrorActionPreference = 'Stop'
$fixture = Join-Path $env:TEMP ('FB-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FishBowl.exe'), (Join-Path $PSScriptRoot 'FishBowl Setup.exe') -Destination $fixture
Set-Content -LiteralPath (Join-Path $fixture 'portable.flag') -Value ''
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
function Invoke-FixtureTest([string]$Executable, [string[]]$TestArguments) {
 $stdout = $Executable + '.stdout.txt'
 $stderr = $Executable + '.stderr.txt'
 $options = @{ FilePath=$Executable; WorkingDirectory=(Split-Path -Parent $Executable); PassThru=$true; WindowStyle='Hidden'; RedirectStandardOutput=$stdout; RedirectStandardError=$stderr }
 if ($TestArguments.Count -gt 0) { $options.ArgumentList = $TestArguments }
 $process = Start-Process @options
 $timeout = $(if ($FullVisual) { 600000 } else { 180000 })
 $finished = $process.WaitForExit($timeout)
 if (-not $finished) { $process.Kill(); $process.WaitForExit() }
 foreach ($log in @($stdout,$stderr)) { if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log | Write-Output } }
 if (-not $finished) { throw ((Split-Path -Leaf $Executable) + ' timed out; see the fixture logs.') }
 if ($process.ExitCode -ne 0) { throw ((Split-Path -Leaf $Executable) + ' failed with exit code ' + $process.ExitCode) }
}
Push-Location $fixture
try {
 foreach ($testName in @('LibraryRecoveryTests','LibraryEditTests','RemovalTests','IntegrationTests','NextRegressionTests','RecognitionTests','AzaharStorageTests','VisualRegressionTests','VisualMatrixTests','EmulatorEditorTests','SmoothUiTests','PopupCloseTests','PopupPositionTests','PopupAnimationTests')) {
  Write-Output "Running $testName"
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Tests\' + $testName + '.cs')) -Destination $fixture
  & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$testName.exe" "$testName.cs"
  if ($LASTEXITCODE -ne 0) { throw "$testName compilation failed." }
  $testArgs = @()
  if ($testName -eq 'VisualMatrixTests' -and $FullVisual) { $testArgs += '--full' }
  Invoke-FixtureTest (Join-Path $fixture ($testName + '.exe')) $testArgs
 }
 foreach ($legacyTest in @('AdditionTests','CompactTests','CosmeticTests','UserToolsTests','PolishTests','AuditTests','HubTests','ImmersionTests','FluidTests','TextFieldTests','RowPaintTests','PauseAnimationTests')) {
  Write-Output "Running $legacyTest"
  $legacyFixture = $fixture
  if ($legacyTest -eq 'CosmeticTests') {
   # Appearance fixtures must not inherit settings changed by earlier test executables.
   $legacyFixture = Join-Path $fixture 'cosmetic-isolated'
   New-Item -ItemType Directory -Path $legacyFixture -Force | Out-Null
   Copy-Item -LiteralPath (Join-Path $fixture 'FishBowl.exe') -Destination $legacyFixture
   Set-Content -LiteralPath (Join-Path $legacyFixture 'portable.flag') -Value ''
  }
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('Tests\' + $legacyTest + '.cs')) -Destination $legacyFixture
  Push-Location $legacyFixture
  try {
  & $compiler /nologo /r:FishBowl.exe /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/out:$legacyTest.exe" "$legacyTest.cs"
  if ($LASTEXITCODE -ne 0) { throw "$legacyTest compilation failed." }
  $legacyArgs = @()
  if ($legacyTest -eq 'CompactTests') { $legacyArgs += 'current-menus.txt' }
  if ($legacyTest -eq 'PauseAnimationTests') { $legacyArgs += '--pause-ui-animation' }
  Invoke-FixtureTest (Join-Path $legacyFixture ($legacyTest + '.exe')) $legacyArgs
  } finally { Pop-Location }
 }
 Write-Output "Test fixtures and previews: $fixture"
} finally { Pop-Location }

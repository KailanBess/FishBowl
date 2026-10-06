param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/windows'), [string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'FishBowl.exe'
$setup = Join-Path $PSScriptRoot 'FishBowl Setup.exe'
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($app).ProductVersion
$version = ($version -split '\+')[0] -replace '\.0$',''
if ($ExpectedVersion -and $version -ne $ExpectedVersion) { throw "App version $version differs from requested version $ExpectedVersion." }
$setupVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup).ProductVersion -replace '\.0$',''
if ($setupVersion -ne $version) { throw 'App and installer versions differ.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$bundle = Join-Path $output ('FishBowl-' + $version)
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($name in @('FishBowl.exe','FishBowl Setup.exe','FishBowl.ico','Uninstall FishBowl.bat','Uninstall FishBowl.ps1')) {
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $bundle $name) -Force
}
@"
# FishBowl $version

Run FishBowl.exe, or keep these six files together and run FishBowl Setup.exe to install.

Home and appearance settings offer theme samples, restrained accents, cover proportions, card ordering and accessibility presets. Game tools includes native imports, Steam discovery, launch profiles, mod backups and rollback, media links, save history and play insights. Integrations includes reviewed metadata catalogs, declarative extensions, RetroAchievements progress and a paired read-only browser companion. API keys and pairing addresses stay private; online progress needs your own RetroAchievements account.

Game titles and artwork are stored in the library without renaming game files. Removing games/emulators supports Undo. Review mod changes and save restore destinations before applying them. Game files and emulator programs are supplied separately.

Source and platform build instructions are in the accompanying Source archive and the repository README.
"@ | Set-Content -LiteralPath (Join-Path $bundle 'README.md') -Encoding UTF8
$zip = Join-Path $output ('FishBowl-' + $version + '.zip')
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
$source = Join-Path $output ('FishBowl-' + $version + '-Source.zip')
& git -C (Join-Path $PSScriptRoot '..') archive --format=zip "--output=$source" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source packaging failed.' }
$hashes = @($zip,$source) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Complete app and source bundles: $output"

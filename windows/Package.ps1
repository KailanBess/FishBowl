param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/windows'), [string]$ExpectedVersion)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$changes = & git -C $repository status --porcelain
if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit source changes before packaging so the app and source archive match.' }
& (Join-Path $PSScriptRoot 'Build.ps1')
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
$expectedNames = @('FishBowl.exe','FishBowl Setup.exe','FishBowl.ico','Uninstall FishBowl.bat','Uninstall FishBowl.ps1','README.md')
if (Test-Path -LiteralPath $bundle) {
 $extra = Get-ChildItem -LiteralPath $bundle -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin $expectedNames }
 if ($extra) { throw 'Existing bundle contains unrelated files; choose a fresh output directory.' }
}
New-Item -ItemType Directory -Path $bundle -Force | Out-Null
foreach ($name in @('FishBowl.exe','FishBowl Setup.exe','FishBowl.ico','Uninstall FishBowl.bat','Uninstall FishBowl.ps1')) {
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $bundle $name) -Force
}
@"
# FishBowl $version

Run FishBowl.exe, or keep these six files together and run FishBowl Setup.exe to install.

Home and appearance settings offer Compact/Standard/Roomy control spacing, Harmonized/Original color blending, theme samples, restrained accents, cover proportions, card ordering and accessibility presets. Compact spacing keeps your selected font and text size. Game tools includes native imports, Steam discovery, launch profiles, mod backups and rollback, media links, save history and play insights. Integrations includes reviewed metadata catalogs, declarative extensions, RetroAchievements progress and a paired read-only browser companion. API keys and pairing addresses stay private; online progress needs your own RetroAchievements account.

Game titles and artwork are stored in the library without renaming game files. Removing games/emulators supports Undo. Review mod changes and save restore destinations before applying them. Game files and emulator programs are supplied separately.

Source and platform build instructions are in the accompanying Source archive and the repository README.

Session checkpoints recover observed playtime after restart and follow verified launcher descendants without stopping games on app close. Appearance and accessibility settings share one hub, with separate General preferences. Controller navigation covers current menus, overflow actions, dialogs and controls.

Remote couch play includes a browser media client and host-approved keyboard controls. It requires a LiveKit deployment and the private token service described in remote-play/README.md in the Source archive. This package does not deploy the service or include credentials. Live streaming and emulator key mappings require a two-device test; Linux can join as a browser guest.
"@ | Set-Content -LiteralPath (Join-Path $bundle 'README.md') -Encoding UTF8
$zip = Join-Path $output ('FishBowl-' + $version + '.zip')
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
$source = Join-Path $output ('FishBowl-' + $version + '-Source.zip')
& git -C (Join-Path $PSScriptRoot '..') archive --format=zip "--output=$source" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source packaging failed.' }
$hashes = @($zip,$source) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Complete app and source bundles: $output"

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

Play opens compatible emulator windows inside FishBowl. Switching Home, Library or Emulators leaves sessions running. Full screen expands Play; Exit full screen returns to the normal window. Open in window restores the emulator window. Closing FishBowl asks before closing running games. Close games and exit requests normal emulator shutdown; Cancel keeps playing and Keep games running restores their windows. Save/exit prompts are honored without force termination. Game view settings in More hides standard menus and lets you save toolbar/status edge sizes per emulator. Some elevated or renderer-managed windows require external mode. Share session selects the running game, opens its own window for sharing and returns it to Play when sharing closes. More opens session details and recovery guidance.

Web uses an installed browser: Firefox by default, with Microsoft Edge or Google Chrome available in Browser settings. No browser engine executable is bundled. Enter an address or search and press Go in Web. A separate FishBowl browser profile holds cookies and logins; personal browser windows are not attached or closed. See docs/BROWSER.md in the Source archive for navigation and compatibility details.

Session checkpoints recover observed playtime after restart and follow verified launcher descendants with confirmed graceful game shutdown on app close. Appearance and accessibility settings share one hub, with separate General preferences. Controller navigation covers current menus, overflow actions, dialogs and controls.

Remote couch play includes a browser media client and host-approved keyboard controls. It requires a LiveKit deployment and the private token service described in remote-play/README.md in the Source archive. This package does not deploy the service or include credentials. Live streaming and emulator key mappings require a two-device test; Linux can join as a browser guest.
"@ | Set-Content -LiteralPath (Join-Path $bundle 'README.md') -Encoding UTF8
$commit = & git -C $repository rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the packaged source commit.' }
@{ version=$version; sourceCommit=$commit; appFile='FishBowl.exe'; installerFile='FishBowl Setup.exe'; appFiles=$expectedNames; sourceOnly=$false } |
 ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'BUILD-INFO.json') -Encoding UTF8
@"
# FishBowl $version

Download **FishBowl-$version.zip** for the Windows app. Extract it, keep the six files together, and run FishBowl.exe or FishBowl Setup.exe.

**FishBowl-$version-Source.zip** contains source code for contributors. Build instructions are in its README; it does not include compiled executables. The Linux archive is a separate executable build.

This version adds a Web workspace using installed Firefox by default, selectable Edge/Chrome adapters and separate browser profiles. It adds running-game exit confirmation and graceful shutdown, plus saved game-only view adjustments. The existing UI style remains in place. Browser and renderer compatibility vary; external-window fallback is available.

Before upgrading, use Library > Backup and transfer > Export FishBowl settings and retain the previous app folder. Close FishBowl, extract the new app into a separate folder, and keep your existing data location. For portable mode, copy portable.flag and FishBowlData while FishBowl is closed. The installer updates the app without intentionally replacing library data. Emulator programs and game files remain separate.

To roll back, close FishBowl and reopen the previous app version. If data recovery is required, use the exported settings or Library restore points and review the restore preview. Do not overwrite an active portable data folder.

Physical controllers, audio and two-device remote streaming require live tests. Renderer and monitor compatibility vary; use Open in window when needed. See docs/PLAY-VALIDATION.md in the Source archive.

Source commit: $commit. BUILD-INFO.json records the matching version and source commit. SHA256SUMS.txt covers both Windows archives and the release metadata.
"@ | Set-Content -LiteralPath (Join-Path $output 'RELEASE-NOTES.md') -Encoding UTF8
$zip = Join-Path $output ('FishBowl-' + $version + '.zip')
Compress-Archive -LiteralPath $bundle -DestinationPath $zip -Force
$source = Join-Path $output ('FishBowl-' + $version + '-Source.zip')
& git -C (Join-Path $PSScriptRoot '..') archive --format=zip "--output=$source" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source packaging failed.' }
$hashes = @($zip,$source,(Join-Path $output 'BUILD-INFO.json'),(Join-Path $output 'RELEASE-NOTES.md')) | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Complete app and source bundles: $output"

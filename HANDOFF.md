# Handoff

Current development state for both maintainers. Update this file with every change; older implementation notes are in [Development history](docs/DEVELOPMENT-HISTORY.md).

_Last updated: 2026-11-05, coordinated release preparation._

## Current state

| Area | State |
| --- | --- |
| Windows | Source **1.29.4**, browser fitting on branch `fishbowl-browser-auto-fit`. PR #25 is merged; source includes main through `5b96312`. |
| Linux | Round 1 (#28–#30) and round 2 integrations/QA (#37) merged; round 2 profiles in PR #38 (see Linux work). |
| Shared | C# 5 game, library, session, save and living-room helpers. Optional Browser/PlayView fields survive Linux saves; Linux browsing remains external. |
| Issues | [Issue #34](https://github.com/KailanBess/FishBowl/issues/34) tracks automatic browser fitting. Issues #23/#27/#31/#32 are implemented through merged PR #25. |

## Open pull requests

- [#46: FishBowl 1.29.5 Aquarium family release](https://github.com/KailanBess/FishBowl/pull/46), branch `release/aquarium-family-1.29.5`, targets `main`. Prepared Windows/Linux release packages; normal source checks and approvals must pass before merge/publication.

- [#38: Linux profiles, launch options and settings](https://github.com/KailanBess/FishBowl/pull/38), branch `parity/profiles-2`, targets `main` (see Linux work). #36 and #37 are merged.

Required checks must pass before merging. Distribute executables through Releases or passing workflow artifacts.
## Linux work

**Round 2:** PR #37 merged integrations (Flatpak permissions and updates, AppImage updates, Add to Steam, EmuDeck/RetroDECK, AUR `linux/PKGBUILD`, Living Room desktop action) and QA (every Linux screen and dialog checked; interface scale applies to dialogs; single-instance guard: a second launch forwards its arguments to the running copy over a socket in `XDG_RUNTIME_DIR`). PR #38 (`parity/profiles-2`) adds shared `FishBowl.Profiles.cs` (user profiles, guest mode, profile transfer/reviewed sync, multiplayer readiness, ROM integrity, accessibility presets; Windows keeps its dialogs and calls it), Linux launch options per emulator or launch profile (Proton/Wine, GameMode, MangoHud, Gamescope; stored as the extra `LinuxLaunch` field, which Windows ignores), `fishbowl --launch/--launch-last/--list/--help`, and Linux User tools, controller profiles, multiplayer, preferences, appearance/accessibility and keyboard shortcut dialogs. **Windows-side agents: please avoid changing `FishBowl.Avalonia/` until #38 merges.**

**Round 1 (#28–#30):** shared `FishBowl.Games.cs` (library queries, collections, lists, import, launch/sessions via `GamePlay`), `FishBowl.SaveTools.cs` (save history; Windows keeps its emulator rules through partial-method hooks; both apps use `Game Saves/<id>/<kind>/History`) and `FishBowl.LivingRoom.cs` (Immersion logic and living-room navigation; Windows `Immersion` calls it). Linux controllers use evdev, falling back to `/dev/input/js*`.

**Round 3 (`parity/details-3`, after #38):** the rest of the Windows game details dialog: personal rating (Edit game), disc list and disc to launch, emulator core and title ID (Game setup); the Library details panel shows them plus controller and compatibility notes, and links Save history and Launch preview (the exact command with Linux launch options, or why the game can't start). Home quick actions gain Save history and Readiness. `FishBowl.Avalonia/Aquarium.cs` adds the Windows bubble rails beside the workspace (the Bubbles immersion setting; they animate only while the window is active, motion is allowed and no game runs) and main-window ambient artwork (the Ambient setting and idle minutes; full screen, closes on any input).

**Still missing on Linux:** embedded Play/Web (Linux launches emulators and browsers as separate windows). Possible follow-up: Windows MainForm could use the shared `SaveMonitor` rules.

## Validation and compatibility

Windows regression suites, browser/process ownership fixtures and normal/compact text audits cover the automated behavior. Run `windows/Build.ps1`, `windows/Run Tests.ps1` and both modes of `windows/Run Overflow Audit.ps1` after shared-source changes. CI also compiles all retained shared sources with C# 5 and runs the Linux build, core tests and schema checks.

Windows 1.29.4 targeted checks pass: browser routing/geometry (40), game exit (196), hosting (90), launch (20), smooth UI (667) and framework/large-library audit (46). Normal and compact text audits report zero findings across five workspaces and 68 dialogs at 100/150/200%. Live browser and monitor compatibility checks remain open.

1.29.3 automated checks pass: the complete Windows regression run, final exit/prompt fixtures (196), native hosting (90), launch routing (20), framework/large-library audit (46), five workspaces plus 68 dialogs with zero normal/compact overflow at 100/150/200%, shared C# 5 compilation, schema preservation (229) and Linux semantic compilation. PR #25 is merged and 1.29.3 is released. Native emulator performance and Qt accessibility-provider behavior still need live checks.

Installed Firefox has rendered inside Web, survived section changes and rendered fullscreen. 1.29.2 address-bar keyboard navigation, browser forms/downloads/permissions, normal live shutdown, Edge/Chrome versions and mixed-monitor setups still need native checks. Browser fixtures use a fixture-only navigation transport because the test desktop cannot activate a foreign window; those passes establish routing/window lifetime, not live keyboard delivery. Browser executables are installed separately; none is bundled. See [Browser setup](docs/BROWSER.md) and [Play validation](docs/PLAY-VALIDATION.md).

Actual emulator rendering/audio, physical controllers and two-device remote streaming remain native compatibility checks. The remote token service is a reference implementation and is not deployed. Linux desktop/controller testing remains required; headless or fixture passes do not establish desktop compatibility.

## Repository housekeeping

Source, scripts, tests, artwork and documentation belong in Git. Generated executables, packages, temporary output, personal libraries and game files are ignored. See [Source layout](SOURCE-LAYOUT.md).

Merged/closed branches should be deleted after checking their pull request and ancestry. Preserve active `fishbowl-browser-auto-fit` work; merged parity branch refs have been removed on GitHub. The owner can enable **Settings → General → Pull Requests → Automatically delete head branches** to prevent future clutter.

## Log

- **2026-11-05 (release integrity):** generate cross-platform release notes before hashing the Windows metadata. Publication no longer edits RELEASE-NOTES.md after SHA256SUMS.txt was generated/verified. Windows archive/source and metadata digests now remain unchanged through upload. No runtime behavior changed.

- **2026-11-05 (release audit):** the Windows audit contained hardcoded 1.29.4 app and installer expectations. Updated both to the paired 1.29.5 release contract and run that audit first for quicker feedback, followed by the unchanged full regression suite. No product behavior or shared schema changed; CI and publication remain gated.

- **2026-11-05 (release gate):** PR #46 was created by the maintainer. Linux build/core/schema and Windows overflow checks pass, but the complete Windows regression job failed. Publication remains blocked. Added bounded, inert-fixture stdout/stderr annotations to identify the failing test without weakening or skipping tests; Windows app code and Linux behavior are unchanged.

- **2026-11-05 (both packages):** prepared 1.29.5 on `release/aquarium-family-1.29.5`, with paired Windows installer/app and Linux package metadata. No Linux behavior or shared schema changes. Release automation opens a pull request, waits for the existing Build and text-audit workflows, merges only if normal repository rules permit, then dispatches the release workflow from main. Windows and Linux release jobs must both pass before publication. Linux Release build, shared/core checks and 229 schema checks pass. The build reports the existing unawaited SaveMonitor call warning (CS4014); native browser/emulator/controller acceptance remains open. No game, firmware or emulator binaries are packaged. The initial release-preparation Actions run failed before merge; GitHub reports that Actions cannot create pull requests; the maintainer created release PR #46 and preparation is being retried against its checked head. Preparation now accepts push/PR check runs, cancels stale preparation runs and requires an exact tested head when merging. Public job summaries/error annotations retain failure details.
- **2026-11-04 (Windows hosting readiness correction):** defer the Aquarium workspace marker until after the first Shown cycle, preventing Aquarium from reparenting a form while WinForms is still initializing its top-level state. Handle recreation republishes readiness after initialization. No shared/Linux data or code changes.

- **2026-11-04 (Windows Aquarium hosting):** opt-in AQUARIUM_HOSTED workspace registration marks only MainForm, selects compatible DPI mode before UI startup and permits a smaller embedded workspace. Existing standalone startup and saved data are unchanged. Aquarium hosts only the marked main workspace; startup/sign-in dialogs are not adopted. No Linux or shared-schema changes. Parent lifecycle/DPI and physical controller compatibility need native checks.

- **2026-11-04 (Windows library essentials):** surfaced per-game launch/controller setup, local cover selection, save/configuration backups and a controller-navigation guide in the existing library menus. Existing path repair, profile, backup and XInput navigation implementations are reused. No shared schema or Linux files changed. Physical controller/emulator compatibility remains a native check.

- **2026-11-04 (Windows reference-layout pass):** added a Platforms entry in the Windows Game Library that opens a searchable rounded-tile platform gallery. Counts come from existing game entries; choosing a tile filters the existing library and switches to artwork view. Existing launch, saves, controller and library logic remain intact. No Linux files or shared schema changes. The gallery is the first layout pass; the complete shell/sidebar migration remains follow-up work.

- **2026-11-04 (Windows):** added an opt-in Aquarium family preset in Appearance and accessibility, with Ocean/Midnight/Slate themes and Mint/Blue/Coral accents. The preset uses rounded, harmonized surfaces and retains text size, density, motion, library data and existing themes. Windows UI only; Linux sources and shared schema are unchanged. Original conversation images were search references, so this matches the Aquarium family palette rather than claiming pixel-identical mockups. Build and theme regression checks are recorded in the pull request; native visual review remains required.

- **2026-10-13:** README refreshed for the 1.29.4 release: version table, a short "What's new" in place of the long source-version paragraph, Windows install steps (the release ships one ZIP with `FishBowl Setup.exe` and `FishBowl.exe`), the Linux feature list (launch options, integrations, command line), the PKGBUILD, the release process and the repository layout.

- **2026-10-13 (Linux):** restored the "no games, firmware or emulator files" rule that missed PR #24's merge: `AGENTS.md` code rule, a "Bring your own games and emulators" README section and `.gitignore` entries for more game, firmware and key file types (merged with the existing list). Deleted merged branches on GitHub.

- **2026-10-13 (Linux):** README gains Screenshots and Tutorials sections (eight short walkthroughs) with images in `docs/images/`. They are Linux screenshots taken headlessly from a demo library of made-up games and generated covers; Windows screenshots could be added beside them. Snapshot rows on Linux no longer say "Not reported by this program" when the emulator reports no version, and the Living room tab's button now shows the real shortcut, Ctrl+B. Fixed the README's stale version table and controller note.

- **2026-10-13 (Linux):** branch `parity/details-3` (stacked on #38) fills the remaining game-details gaps and Home quick actions, and adds bubble rails and main-window ambient artwork (see Linux work). Linux build and core tests pass; checked headlessly with a fake library.

- **2026-10-13 (Linux):** merged main (through #36/#37) into PR #38 and resolved conflicts (menus keep both the profile and Linux integration items; the desktop file has both Living Room and Reopen last game actions; `--launch`/`--list` run before the single-instance check; interface scale times text size). The PR's earlier Windows CI failure was the stale 1.29.2 package version that #33 fixed. Linux build, core tests, schema (229) and C# 5 shared/Windows-source compiles pass locally.

- **2026-10-08:** reviewed all open GitHub issues. Issue #34 has an implemented browser fitting fix with successful Build and Text overflow audit checks. Resolved the PR #36 handoff conflict against merged Linux QA/AppImage work, preserving notes from both maintainers. Fresh integration checks must pass before merging the fix and closing #34. PR #38 remains separate Linux feature work.

- **2026-10-08:** Windows 1.29.4 addresses issue #34. Browser viewports verify native geometry and restore browser-native movement, resizing and maximization to the Web area. Open in window starts fitted over Web, then respects user adjustments; Return to Web resumes fitting. Unsupported attachment receives an initial external fit. Browser fixtures cover automatic fit, native geometry reset, app move/resize, idle placement stability and external positioning. Updated the Linux version only to satisfy the paired CI package contract; Linux parity implementations are untouched. Based on merged main through #25/#33.

- **2026-10-08 (Linux):** summarized Linux parity round 1 and started round 2 (see Linux work). The Build workflow now takes the expected package version from `FishBowl.Avalonia.csproj` instead of a hardcoded number (it failed on main because it still expected 1.29.2). Releases now include a Linux AppImage (`linux/build-appimage.sh`, run by the release workflow) with `SHA256SUMS-linux.txt`; v1.29.3 was released with one.

- **2026-10-08:** Windows 1.29.3 addresses issue #32. Play defaults to owned renderer viewports so tab layout does not propagate through a foreign child window. Process ownership retains a verified lifetime handle, unchanged viewport clips are reused, renderer discovery stops rescanning stable windows after startup, and decorative motion pauses on every section while a game runs. Close games and exit waits in a responsive FishBowl dialog, acknowledges strictly recognized English plain exit confirmations, presents save/unknown prompts inside the dialog, and completes FishBowl closure when the tracked processes exit. Veto/cancel retains the game; no ordinary game process is force terminated. Added native shutdown and active-navigation fixtures. Linux has matching version metadata only; saved data is unchanged. Native emulator rendering and Qt accessibility remain compatibility checks.

- **2026-10-08:** fixed issue #31 routing and return actions, retained embedded games during multiplayer setup, added ownership/session-return fixtures and bumped paired package metadata to 1.29.2. Preserved main through PR #28 and completed the prior documentation cleanup. Native navigation/capture checks remain required.

- **2026-10-07:** audited GitHub issues, pull requests and workflow status; confirmed zero open issues and two distinct pending pull requests. Refreshed release/shutdown/browser and shared-source documentation, archived historical notes and expanded ignore rules. Copilot workflow approval and native compatibility checks remain explicit.
- **2026-10-07:** synchronized PR #25 with latest main shared game/living-room extraction, retaining `FishBowl.Browser.cs`, `FishBowl.Games.cs` and `FishBowl.LivingRoom.cs` in the Windows build.
- **2026-10-07:** PR #29 merged Library/Home/living-room work; PR #28 retains additional save/backup changes. Windows 1.29.1 browser rendering fix is tracked by closed issues #23/#27 and pending PR #25.




Tracked hosting readiness defect: #45. Windows navigation and reference-layout fixtures pass; Linux and shared data are unchanged.

2026-11-04: Follow-up to #45: the WinForms workspace now handles cooperative visibility messages using Form.Show/Hide so child controls are restored after native parenting. Aquarium opts in through a window property. Windows-only adapter change; shared schema and Linux are unchanged.
2026-11-05 (Windows): Game Library > Library tools > Platform setup combines launch validation, firmware folder availability, controller profile and XInput detection with an explicit test launch. Physical controller and emulator verification remain manual. Shared library schema and Linux files were not edited.


2026-11-05 (Windows, #47): Play offers More > Focus game. Owned renderers receive a native foreground activation request and child renderers use the correct input queue. Hidden and detached viewports refuse focus. Eden keyboard and controller acceptance remains manual.

- **2026-11-06 (Windows library layout):** moved workspace navigation to a left rail and added a wide-window Library summary with live game, filter and emulator counts and the existing folder scan action. The Library toolbar exposes the existing list/artwork switch. Saved views, launch routing, customization and the shared library format are preserved. Native Eden input remains a separate verification item.

- **2026-11-06 (Windows toolbar):** keep the list/artwork switch in Library tools so the removal button fits at standard text size. The library toolbar wraps on narrow Windows displays and grows to fit the extra row. Platform browsing is available under Library tools.

- **2026-11-06 (Windows session fixture):** hold the generated child process until the test verifies tracking beyond the launcher grace period. Release it explicitly and check that the session finishes. Session tracking code and shared schema are unchanged.

- **2026-11-06 (1.29.6 release):** prepare matching Windows app/installer and Linux package versions for the reviewed library redesign. Linux behavior and the shared library format are preserved. Release packaging runs the existing Windows and Linux checks before publishing.

- **2026-11-06 (Windows artwork shell):** added shared Search and Settings shortcuts and icon navigation to the Windows workspaces. The Library artwork view now uses responsive landscape cards with a title overlay and the existing collection and selection panel. Gallery Night is available in the theme picker. Saved views, artwork size, density, navigation and emulation actions are retained. The right panel uses local collection data. Linux sources and the shared library schema are unchanged.

- **2026-11-06 (Gallery Night shell):** the Windows Gallery Night preset now uses a separate workspace rail and command strip. My Games opens the existing library. The Menu command reveals the full menu bar. Other themes retain their familiar shell, and the workspace controls and saved data are shared.

- **2026-11-06 (1.29.7 release):** prepare the tested Windows Gallery Night workspace shell with matching installer and Linux package versions. Linux behavior and shared library data are preserved.

- 2026-11-06: Rebuilt the Windows workspace chrome for every theme. The sidebar links to the existing Home, Emulators, Library, Play, Web and Settings features. The Library uses compact game cards with status badges, a search and view strip, and a summary based on the local collection. Existing view and appearance settings are retained. Linux shell remains separate.

- 2026-11-06: Replaced the full-width promotional banner with a compact optional brand strip and moved Library search, scope, list/grid and filters into the Library heading. Other workspaces use a single shared section heading. The Library and native Play/Web controls retain their existing behavior.

- 2026-11-06: Added a Windows Settings workspace with categorized cards, moved Home summary actions into a right column at wider sizes, and gave the Emulators workspace a narrower details inspector. Settings editors, emulator actions, and saved preferences continue to use the existing implementations.
- Play and Web use the dashboard command strip spacing and a right-hand status inspector on wide windows, with a bottom status line on narrow or fullscreen layouts.
- Shared Windows detail and settings dialogs based on NextDialog now use a compact title strip and matching workspace surfaces.

- 2026-11-06: Prepared 1.29.8 packaging for the Windows workspace redesign. The compact-density fixture opens Filters before testing the action bar and verifies the My Games heading at each supported text size.

- **2026-11-09 (iOS library preview):** Added a separate native SwiftUI app for iPhone and iPad, with local Files import, search, favorites, and appearance settings. Windows and Linux builds are unaffected. iOS simulator compilation runs on macOS CI. Gameplay, desktop sync, on-device testing and signing remain open.

# Handoff

The current state of FishBowl development, for both maintainers and their coding agents. **Update this file with every change** (see [AGENTS.md](AGENTS.md)).

_Last updated: 2026-10-07, main merged into PR #25 and conflicts resolved._

## Current state

| | State |
| --- | --- |
| Windows | Source **1.29.1** on `fishbowl-web-game-view`, pending review in PR #25. Public release remains [v1.25.8](../../releases/tag/v1.25.8). |
| Linux | Preview with Home/Library/profiles/game setup and expansion screens; native Linux desktop smoke testing remains required. |
| Shared | Optional expansion settings in the shared model, C# 5 tools for mods/imports/catalogs/companion/file save recovery. Unknown JSON fields preserved on Linux. |

## Open pull requests

PR #20 is merged at main `11a6f935a98491d7fd0118ed0ce3fcb986f8f0de`. PR #21 is merged (Windows 1.28.1). New 1.29.0 work on `fishbowl-web-game-view` targets main and issue #23; Linux testing status from PR #24 is preserved.

## Windows browser hosting 1.29.1

Issue #27 follows up on browsers opening outside Web. Web now requests borderless owned-window hosting even at matching DPI, preserving the browser's GPU context instead of calling SetParent. Firefox starts with new-window rather than desktop kiosk mode. Actual installed Firefox rendered a page inside Web, hid on Home, restored the same page on return, and rendered in fullscreen with FishBowl's exit control visible. Dedicated profiles and strict process/window identities remain in use. Normal emulator hosting defaults are unchanged. Browser fixtures pass 30 checks, game-exit fixtures pass 35 and retained native hosting passes 88. Final regressions, overflow audits and CI validate the final commit; downloads/permissions, other browser versions and mixed monitors remain live checks.

## Windows Web and shutdown 1.29.0

The 1.29.0 branch implements issue #23: an actual installed Firefox window in a fifth Web workspace, selectable Edge/Chrome adapters, dedicated browser profiles, URI/search entry, native navigation commands, fullscreen and external fallback. Browser preferences are optional shared fields; Linux preserves them and keeps its existing external-launch UI. Firefox Go opens a tab in its dedicated profile. Edge/Chrome Go closes the previous app window normally before opening the next address; the address box is an entry field, not a synchronized current-page URL. See docs/BROWSER.md for requirements and compatibility.

Play hides standard native menus and saves optional per-emulator PlayView edge settings for renderer-drawn controls. Zero edges preserve the full game image; unmatched native games use session-only settings. Native menus/styles/placement are restored in external mode. Renderer/title-aware selection prefers the game over a larger manager; verified matching sibling frontend windows are hidden during game-only Play and restored on external mode, shutdown or explicit controls. Closing FishBowl asks before requesting normal game shutdown, offers Cancel and explicit keep-running behavior, and honors emulator exit/save vetoes without force termination. A slow browser offers staying open or explicit external continuation on exit.

Native browser fixtures pass 27 checks; exit/game-view fixtures pass 35 checks; retained native hosting/launch checks pass 88/19. Shared schema checks pass 229 assertions and Linux semantic compilation reports no errors. Full Windows regressions passed over 31,000 checks before the final frontend-selection and dialog refinements; targeted native suites validate those refinements. Normal/compact audits report zero findings across five workspaces and 67 dialogs at 100/150/200% text size. Required GitHub checks validate the final source commit. Installed Firefox testing reached window attachment, navigation routing and workspace persistence, but graphics/compositor failures and incomplete shutdown prevented a complete rendering/shutdown smoke pass. Rendering, actual navigation handling, permissions/download dialogs, monitors and physical input remain live checks; fixture passes do not establish browser compatibility. No browser is bundled or installed and no release is published before review.
## Windows polish 1.28.1

Play adds explicit fullscreen exit, disabled unavailable controls, clearer empty/waiting/error states, session details and confirmed native detach feedback. A guided Share session action restores and preselects the exact selected window for remote hosting, then returns previously embedded sessions to Play when sharing closes. No token service or credentials are deployed. The token endpoint preference persists; changing a target stops guest input.

Dropdown choice surfaces follow themes/high contrast, including empty/selected/disabled states; editable text fields keep native editing. The footer shows filters only in Emulators. Toolbar layout can shrink a row previously expanded during initial layout. Home expansions persist in optional `ThemeSettings.ExpandedHomeCards`; Linux preserves this field but does not apply Windows Home expansion behavior. Linux's package version changes only to keep paired release archives aligned.

Native Play placement skips unchanged bounds, settled/hidden polling slows and decorative motion pauses during active Play. Executable lookup can use limited process-query rights; exact PID/start/window ownership checks remain mandatory. Unsupported or unverifiable windows keep external fallback. Native session guidance is covered at enlarged text sizes. The full Windows regression pass covered 31,385 checks before the final toolbar/overflow refinements; final targeted native Play coverage passes 88 checks. Schema preservation passes 202 checks; Linux semantic compilation passes. GitHub checks validate the final source commit before review.

App/source packages include source commit metadata, checksums, explicit download labels and upgrade/rollback instructions. Draft tagged releases require source on main plus normal/compact overflow checks. Source-only changes remain in PR #21; no public release is published before review. See docs/PLAY-VALIDATION.md for live emulator, input, monitor and remote checks. Actual embedded Azahar rendering, audio, physical controllers, mixed-DPI monitors and two-device remote streaming remain unverified.

## Windows Play workspace 1.28.0

Windows adds a persistent fourth Play section. Game and emulator launch paths route to exact-process native window hosts, keep sessions alive across section changes, and expose fullscreen and detach/reattach actions. Original native parent/styles/placement are restored before host destruction; emulators remain running when FishBowl closes. Matching-DPI windows use native child hosting; incompatible per-monitor renderers use a borderless owned window aligned and clipped to the Play viewport without resetting renderer DPI. Native ownership is restored before fullscreen handle changes or shutdown. Elevated/unsupported windows retain external-window fallback. The foreign process controller guard remains strict. Remote media input currently requires detaching the game window. No saved model changes are introduced; Linux embedding remains a separate task.

Validation: all existing Windows regressions passed 31,089 checks. Actual emulator, embedded Library and modal game launch routing passed 19 checks. Native child/owned-window lifetime tests passed 58 checks and include exact PID/start ownership, resize, multiple sessions, DPI preservation and fullscreen. Normal and compact 100/150/200% audits report zero findings across all four sections and 64 dialog types; 199 schema-preservation checks and Linux semantic compilation pass. Required GitHub Actions build, packaging, remote-service and text-audit checks run on PR #21. Actual emulator rendering/input and multi-monitor DPI combinations require native smoke testing.

## Session, navigation and remote play 1.27.0

Shared session journals store observed seconds, exact PID/start identities and profile ownership. Recovery applies only missing deltas, preserves other profiles and excludes app downtime. Windows background polling follows verified launcher children; Linux uses procfs identities. Unverifiable instant Linux launchers remain uncertain rather than attaching unrelated processes. Journals commit only after library saves; games are not terminated on shutdown.

Windows Appearance and accessibility unifies existing panels; General preferences keep startup/backup settings. Controller navigation handles menu/submenu/overflow actions, choices, lists, checks, numeric fields and tabs, with active-process, reconnect and repeat guards. Linux reads existing joystick devices and reuses the current settings style. Physical controllers and Linux display sessions need native smoke testing.

Remote media client and reference Node token service are under `remote-play/`. Windows explicitly chooses a single game window, approves one guest and sends only supported keyboard keys to that exact foreground game. Linux provides browser joining; native hosting is unsupported. SDKs are pinned; no service is deployed and no credentials are bundled. Actual two-device video/audio/input, account/provider calls and physical emulator input remain live-test requirements. See remote-play/README.md. Normal/compact 100/150/200% audits check all main sections and 64 dialog types and report zero text overflow. Validation details accompany the PR.

## Compact controls and color harmony 1.26.2

Windows compact density reduces control/card spacing without reducing user-selected fonts. Optional `ThemeSettings.ControlDensity` and `ColorHarmony` preserve the choice across both platforms; default values use Compact and Harmonized. Standard/Roomy density, Original color behavior, custom palettes and accessibility settings remain available. Controller presets use Roomy. Theme switching updates previous themed backgrounds, avoiding mixed palettes. Embedded Library fonts scale once, filter rows use actual native control heights, and smaller viewports scroll to retain game actions and list rows. Returning to normal text size restores input heights. Windows build, 54 density checks, 17,129 color/selection checks and 199 schema preservation checks pass. Full regressions and fresh normal/compact text audits are required in PR #20. Linux UI density/color styling is unchanged; the new optional choices survive Linux settings and profile saves.

## Home layout 1.26.1

Windows Home now uses wider cards in up to two columns, more padding and a single row for primary/More actions. Game/emulator cards preview three entries; information cards preview one summary line. Show all/Show less reveals every remaining item and preserves expansion across Home refreshes. No data or commands are removed. Existing style, card preferences and page scrolling remain in use. Home interaction validation passed 751 checks at 100/150/200% on normal/compact desktops, including all Quick actions, game/emulator/detail expansion, collapse and refresh preservation. Full regression/audit and clean-runner checks are recorded in PR #20.

## Expansion 1.26.0

Home page scrolling, primary/More actions, 40 theme/28 accent previews, card ordering/visibility, cover proportions, restrained accents and accessibility presets use current controls. Added unified game setup, Steam manifests and reviewed native executable imports, verified mod overlay journals and rollback, IPS patch copies, media links/gallery, monthly/session history, reviewed save timeline/transfers, RetroAchievements completion cache, metadata catalogs, declarative extensions and a paired read-only companion.

Linux adds library/profiles/collections/removal Undo/reassignment/path repair/cover editing and shared expansion tools. File save timelines use SaveTools; folder saves use EmulatorBackups. Living-room input is keyboard/buttons; new gamepad polling is not included. Existing Linux-maintainer launch wrappers and in-progress native integrations below remain separate.

Validation: Windows build and 2,310 expansion layout checks passed across 132 captures at three text sizes on normal/compact screens; UI settings tests passed 184 checks; game tools passed 36 checks. Full Windows run passed 13,141 checks; targeted final integration/game/UI reruns passed 46/36/184 checks. Normal and compact audits report zero findings. Clean-runner Linux build, core tests, 193 schema preservation checks and self-contained publish passed on PR #20. Windows build, all 13,144 regression checks, packaging and the zero-finding text audit passed for 1.26.0. Linux metadata selection/importers/retained covers and paired companion lifecycle are integrated. Closing records observed playtime once without stopping games; crash recovery and launcher handoff tracking remain limited. Local semantic Avalonia compilation passed; this is not a native Linux desktop smoke test. Account-dependent achievement/network metadata calls need real credentials/providers for live verification.

Release packaging now produces a complete six-file app folder, app ZIP, source ZIP and SHA256 checksums. Tagged reviewed versions produce draft releases; no binary build output is committed. App/installer/source versions must match.

## Recently merged: Windows 1.25.11 (#15)

Library recovery and layout fixes: normalized multi-disc grouping, large-text layouts, persisted Undo removal (last 20 batches), portable/moved-folder path recovery, explicit emulator reassignment after removal, and reviewed artwork cleanup. Shared parts: optional `GameEntry.RequiresEmulatorAssignment`, `LibraryData.RemovalHistory`, `SavedDataRoot`, `SavedPortableRoot` and the recovery record types in `FishBowl.Model.cs`, and the helpers in `FishBowl.Library.cs`. **Linux still needs the matching screens** (see Next steps). Not yet released; publish a 1.25.11 release when ready.

## Known issues

- Session completion checks in Integration/Immersion/Hub use bounded condition waits, including the two-second verified launcher grace period.
- **Remaining text overflow:** the 1.25.11 audit reports zero findings at 100/150/200% on normal and compact desktops.

## In progress on the Linux side

**Now (2026-10-07):** the Linux maintainer is running the new Linux screens on a real Linux desktop for the first time, fixing what breaks and filling gaps, on branches `parity/library` (Home, Library, sessions, collections, maintenance), `parity/saves` (saves, save history, backups) and `parity/livingroom` (living room, controllers). **Windows-side agents: please don't change the Linux UI files (`FishBowl.Avalonia/`) or these shared files' Linux paths until those branches merge**, to avoid conflicts. Windows work and shared-model additions are fine; note them here as usual.

These exist as branches on the Linux maintainer's machine and will arrive as separate pull requests:

- **Launch options (Linux):**
  - Proton (umu-launcher) or Wine for Windows-only emulators
  - GameMode, MangoHud and Gamescope
  - environment variables and arguments
  - `fishbowl --launch <emulator>`, `--launch-last` and `--list`
- **Real-world fixes** found by testing against installed emulators:
  - running-state detection through symlinks
  - bringing windows forward on Hyprland 0.55+
  - empty-hub message, monogram rendering, readable folder paths
- **Playtime and backup reminders** (Linux and Windows), to be reconciled with the Windows per-game play time and save snapshots before submission, to avoid duplicating them.
- **Partly built:**
  - Flatpak permission checks with one-click fixes
  - Flatpak and AppImage updates
  - a controllers panel
  - Add to Steam
  - EmuDeck and RetroDECK detection
  - an AUR package
  - couch mode, to be merged with the Windows living-room mode

## Next steps (Linux parity)

1. **Move Windows non-UI logic into shared files:** game recognition, library tools, play sessions and save snapshots. Do it one piece at a time, with both builds compiling each shared file.
2. **Build Linux screens on that shared logic:** Home, Library, game details, profiles and living-room mode.
3. **Finish and submit the Linux-only features above.**

Windows changes that touch the data model belong in `FishBowl.Model.cs`. Label Windows features that should come to Linux with `needs linux port`.

## Log

- **2026-10-07 (PR #25 merge sync):** merged latest `main` into `fishbowl-web-game-view` to resolve GitHub merge conflicts. Resolved `windows/Build.ps1` by keeping the Web browser build input (`FishBowl.Browser.cs`) and main's shared game/living-room sources (`FishBowl.Games.cs`, `FishBowl.LivingRoom.cs`) in the Windows compiler file list.

- **2026-10-07 (Linux):** started native Linux testing of the 1.26–1.28 Linux screens (see In progress on the Linux side).

- **2026-10-07 (polish 1.28.1):** refined persistent Play controls, safe detach/retry feedback, guided sharing/return, themed dropdown states, filter visibility, content-sized toolbar rows, persisted Home expansions and idle placement work. Added native/session-details/theme/schema regression coverage and clearer complete app/source release metadata and rollback guidance. Windows-only behavior is documented for Linux parity.

- **2026-10-06 (compact UI 1.26.2):** introduced optional control density and color harmony preferences, smaller Windows controls/cards and consistent theme-relative colors; retained text scaling and all Home options.

- **2026-10-06 (Home spacing 1.26.1):** simplified Windows Home card previews with wider layouts, grouped actions and reversible expansion, keeping all existing options.

- **2026-10-06 (expansion 1.26.0):** added matching Windows/Linux library and expansion tools, optional shared settings, declarative integrations and verified file recovery. Windows compact popup bounds now use the actual working area; startup popup fixes remain supported. Added native expansion layout fixtures and release packaging/version checks. #19 is merged; #18 is included as the expansion dependency.

- **2026-10-06 (Linux dialogs):** dialogs no longer trap the mouse on focus-follows-mouse desktops (Hyprland, Sway, i3). Avalonia's X11 modal dialogs pulled focus back whenever the owner window was hovered, and the compositor warped the pointer back. `FishDialog.Present` now shows dialogs owned (still above the owner) and blocks the owner's input until they close. Merged #15, #16 and #17.

- **2026-10-06 (Windows CI multi-disc diagnostics):** reviewed the failing Windows Actions run `37421288794` and confirmed the original clean-runner failure was the `IntegrationTests` multi-disc grouping assertion. Added grouped-plan diagnostics to that assertion so future failures print `grouped.Count` plus each plan's source/files, making runner-only path or ordering differences visible in logs.

- **2026-10-06 (shared recognizer integration):** merged main after #14 landed, retaining the shared game identification and Linux PNG helpers alongside Windows recovery/layout changes. Build/test projects include both recognition and recovery helpers. Explicit library titles remain protected in the shared recognizer. Released Windows version remains 1.25.8 pending review.

- **2026-10-06 (Windows restore loop):** reproduced the stall locally at 1024x720. Emulator column resizing re-entered while native scrollbars changed; the update is now guarded, applies only changed widths and uses the existing proportions without a conflicting native autosize call. Normal/compact native text checks cover repeated maximize, restore and full-screen transitions. The runner executes both desktop variants, for 2,738 total checks.

- **2026-10-06 (Windows small-screen restoration):** CI diagnostics narrowed the remaining native-text test stall to full-screen restoration. Button text fitting now respects an explicit maximum width instead of repeatedly requesting an impossible size during narrow-pane layout. Main-window minimum and initial sizes fit the available desktop after UI scaling; the compact fixture also applies to full-screen bounds. Text-field checks can use `--compact` to reproduce a 1024x720 desktop.

- **2026-10-06 (Windows appearance preservation):** text fitting retains the existing widths of icon-only toolbar buttons. Added a regression check that applying layout fitting after the cosmetic preferences leaves those compact widths intact. The full runner now has 2,695 checks.

- **2026-10-06 (Windows search fixture):** the final clean-runner audit reports zero findings. Windows tests passed through the recovery, layout, popup, appearance and fluid checks, then exposed a search-test timer race: its timer stopped before a slow-created modal appeared. The fixture now waits for the search window before stopping and closes it even if the duplicate-window assertion fails.

- **2026-10-06 (Windows toolbar assertion):** updated the compact-menu regression to verify every toolbar action fits its container; a fixed single-row height is not a valid requirement when enlarged text must wrap on a small desktop. CI found one remaining narrow-pane summary action. Summary buttons now stay within their pane using the existing ellipsis/tooltip behavior, and the compact main-window fixture overrides its minimum size to reproduce the runner's actual width.

- **2026-10-06 (Windows compact layouts):** reproduced the CI desktop at 1024x720. Existing button rows wrap, card captions expand, and organizer/settings labels retain spacing with enlarged text. Compact audit has zero findings at all three text sizes. Added bounded test processes and per-test diagnostics for unattended Windows checks.

- **2026-10-06 (Windows CI fixtures):** normalized all fixture base paths so long and short Windows temp names produce equivalent launch, save and artwork expectations. Added audit findings and display/font details to CI logs so remaining runner-specific layouts can be reviewed directly.

- **2026-10-06 (Windows CI):** confirmed the multi-disc grouping fix on the clean runner. Normalized artwork candidate returns and replaced literal path spelling comparisons in recovery/save assertions; Linux build, core tests and schema checks passed.


- **2026-10-06 (Windows):** prepared 1.25.11 recovery and layout improvements on PR #15 using the existing UI. Added optional shared recovery/root fields, protected artwork cleanup, emulator reassignment and regression/schema coverage. Supersedes the 69-finding overflow baseline from 1.25.10; release stays 1.25.8 pending review and CI.

- **2026-10-06 (Windows):** prepared 1.25.10 library title/cover fixes and 1.25.9 removal actions on `fix-library-title-artwork`. Added shared optional fields/helpers and editing/removal regression checks. Linux needs matching UI actions; the released version remains 1.25.8 until review and release.
- **2026-10-06:**
  - `AGENTS.md`: agents keep all work on GitHub (pull at start, branch, push often, pull request, no uploads), teach contributors new to Git and GitHub as they go (with `GITHUB-BASICS.md`), and keep branches tidy (one per pull request, check the base targets `main`, delete after merging). Deleted the leftover branches from merged pull requests.
  - Shared game identification: `windows/GameRecognition.cs` moved to the root as `FishBowl.GameRecognition.cs` and is compiled by both builds. Its WinForms helpers (`SmoothPainting`, `ConsistentInputs`) moved to `windows/FishBowl.InputPainting.cs`. Linux uses `FishBowl.LinuxShims.cs` for the `System.Drawing` types (built-in PNG encoder). Fixed Linux storing its library in the current directory when `~/.local/share` doesn't exist yet.
  - Merged #8 (shared data model), #9 (Tux easter egg), #11 (text overflow fixes; Windows build now compiles `windows/FishBowl.TextFit.cs`) and #12 (README, tidy-up, handoff). Published release v1.25.8.
  - Rewrote the README and tidied the repository: removed superseded source and test files, the old root `FishBowl.cs`, and obsolete review notes.
  - Moved the Windows binaries to the v1.25.8 release, and added `AGENTS.md`, `CLAUDE.md` and this file.
  - Opened #11 (text overflow) and #9 (Tux easter egg). Filed #10.
- **2026-10-05:** shared the data model (#8, re-landing #7), published the 1.25.8 source (#6), and added the text overflow audit (#5), the bug report form (#4) and the Windows source, assets and CI (#3).
- **2026-09-30:** first Linux version merged (#1).

### 2026-10-06 — sessions, navigation and remote play

Added profile-owned crash journals, exact launcher tree tracking, Windows controller/appearance integration, Linux joystick/session parity and the browser media client/private token service. Main through merged PR #20 is the base. Updated bounded completion fixtures, C#5/build/package references and service CI. Service/device smoke testing remains explicitly required.

### 2026-10-06 — native controller modal guard

Controller navigation now checks the actual enabled native form and foreground root window, so file/folder pickers and unrelated modal windows cannot activate controls underneath. Registered FishBowl popup menus remain available. Navigation tests pass 55 checks; the preceding complete Windows regression run passed 31,069 checks. Normal/compact UI audits report zero findings. PR #21 includes the complete 1.27.0 source, media-client resources and token-service setup; current-commit CI is required before merge.

### 2026-10-07 — Windows Play workspace

Added persistent embedded emulator sessions and launch routing, fullscreen chrome, restored native windows on shutdown, and four-section regression coverage in PR #21. Linux continues to launch external windows. See the Play section above for compatibility and validation requirements.

#### CI fixture portability

Launch fixture ownership expands Windows short/long executable path aliases before comparing exact paths and still validates process creation time. Tests reject future start thresholds and identical executable names in other directories. This avoids false ownership failures on runners using short TEMP paths. Native hosting and app behavior are unchanged.

### 2026-10-07 — Firefox Web and game shutdown

Added installed-browser Web integration and optional shared preferences, saved game-view edges, native menu restoration and confirmed graceful exit in PR #21, tracked by #23. Added native browser/exit regressions and five-workspace layout coverage. Browser and emulator renderers retain external fallback; complete native rendering verification remains required.

- 2026-10-07: 1.29.1 fixes Web browser hosting (issue #27) with owned-window rendering, normal Firefox startup and fullscreen restoration coverage. Live Firefox page/section/fullscreen checks passed; normal shutdown verification and final CI are recorded in the PR.

#### 1.29.1 delivery state

Browser fixtures pass 30 checks; game-exit/hosting/launch fixtures pass 35/88/19, and normal/compact overflow audits report zero findings across five sections and 67 dialogs. The original full run timed out in PolishTests after library navigation; the repeat passed that suite and all remaining suites. Combined runs cover every Windows regression suite, with over 31,000 numbered checks. GitHub file-write APIs returned internal errors, the noninteractive Git helper failed to start its shell, and automatic approval review rejected the alternative authentication step because its approval category is disabled. The complete app/source packages are prepared locally. GitHub file-write access recovered on retry; 1.29.1 is being synchronized to PR #25. Required CI must pass before merging; no release is published.

- 2026-10-07: GitHub file-write access recovered; sync the tested 1.29.1 source to PR #25 and run required CI. No merge or release publication.

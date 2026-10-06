# Handoff

The current state of FishBowl development, for both maintainers and their coding agents. **Update this file with every change** (see [AGENTS.md](AGENTS.md)).

_Last updated: 2026-10-06, by the Windows side._

## Current state

| | State |
| --- | --- |
| Windows | **1.25.8**, released as [v1.25.8](../../releases/tag/v1.25.8). Source in `windows/`. |
| Linux | Preview. Emulator hub with Linux integration (Flatpak, AppImage, XDG folder routing). Not yet at parity with the Windows game library features. |
| Shared | The `library.json` data model (`FishBowl.Model.cs`) is shared by both builds. Linux preserves every Windows field when it saves. |

## Open pull requests

| PR | What | Notes |
| --- | --- | --- |
| #15 | Windows 1.25.11 library recovery and layout fixes | Includes 1.25.9 removal and 1.25.10 title/cover fixes; needs Linux UI port and review. |

## Windows change awaiting review

Branch `fix-library-title-artwork` ([PR #15](../../pull/15)) proposes source version **1.25.11**. It includes the earlier removal and title/cover fixes, plus all six follow-up improvements: normalized multi-disc grouping, large-text layouts, persisted Undo removal, portable/moved-folder path recovery, explicit emulator reassignment after removal, and reviewed artwork cleanup. Existing UI themes, control styles and wording are retained.

Shared changes: `GameEntry.RequiresEmulatorAssignment`, `LibraryData.RemovalHistory`, `SavedDataRoot` and `SavedPortableRoot`, and the recovery record types are optional in `FishBowl.Model.cs`. `FishBowl.Library.cs` contains the recovery, path-repair and artwork-reference helpers. Both Store implementations record roots on save and repair missing paths on load when matching relocated files exist. Linux schema tests cover the new optional fields and nested recovery records; matching Linux UI actions still need a port. No Avalonia UI or Linux integration files changed.

Undo retains the last 20 removal batches across restarts. It skips conflicts with re-added entries and retains partially recovered batches, protects newer emulator assignments, and leaves game/save/executable files untouched. Cleanup is manual, reviewed, and limited to generated files in managed artwork folders. References in the active library, inactive profiles, undo history and readable local backup/restore libraries are protected; an unreadable backup blocks cleanup. Root repair matches exact relative paths rather than guessing titles.

Validation: Windows app/installer build, full regression runner (2,738 checks including 43 recovery checks), 62 general dialog previews plus reassignment prompts at 100/150/200% text size, and Framework C# 5 shared compilation. The expanded overflow audit covers six previously skipped dialogs; only the generic background-worker form remains outside the constructor fixture. Normal and compact desktop checks reported zero clipping or overlap findings at 100%, 150% and 200% text size. Use `Run Overflow Audit.ps1 -Compact` to reproduce the 1024x720 desktop check. Audit screenshots now include every text size. Windows fixture roots are normalized so long and short temporary path names produce equivalent expectations. The runner prints each test name, captures its output and limits each executable to three minutes (ten with FullVisual) so an unattended modal cannot stall CI indefinitely. Linux/Avalonia build, core tests and schema preservation passed on CI; Windows CI also passed all 2,738 checks before integrating the newly merged shared game recognizer. The combined-source checks are pending.

## Known issues

- **#10:** source normalization addresses the Windows runner multi-disc grouping failure. Regression coverage now includes short Windows paths, relative paths, source order and repeated/circular playlist references. The clean Windows runner now passes this grouping check. The existing integration save assertion also needed normalized path comparison.
- Linux library recovery and reassignment screens need a matching UI port before platform parity.

## In progress on the Linux side

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

1. **Move Windows non-UI logic into shared files:** game recognition (done: `FishBowl.GameRecognition.cs`), then library tools, play sessions and save snapshots. Do it one piece at a time, with both builds compiling each shared file.
2. **Build Linux screens on that shared logic:** Home, Library, game details, profiles and living-room mode.
3. **Finish and submit the Linux-only features above.**

Windows changes that touch the data model belong in `FishBowl.Model.cs`. Label Windows features that should come to Linux with `needs linux port`.

## Log

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

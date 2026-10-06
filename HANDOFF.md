# Handoff

The current state of FishBowl development, for both maintainers and their coding agents. **Update this file with every change** (see [AGENTS.md](AGENTS.md)).

_Last updated: 2026-10-06, by the Linux side._

## Current state

| | State |
| --- | --- |
| Windows | **1.25.8**, released as [v1.25.8](../../releases/tag/v1.25.8). Source in `windows/`. |
| Linux | Preview. Emulator hub with Linux integration (Flatpak, AppImage, XDG folder routing). Not yet at parity with the Windows game library features. |
| Shared | The `library.json` data model (`FishBowl.Model.cs`) is shared by both builds. Linux preserves every Windows field when it saves. |

## Open pull requests

| PR | What | Notes |
| --- | --- | --- |
| #14 | Share game identification between Windows and Linux | First step of Linux parity. Includes the handoff update from #13. |

## Known issues

- **#10, Windows CI test failure.** "multi-disc support files grouped once" in `windows/Tests/IntegrationTests.cs` passes locally but fails on GitHub's Windows runner. Every Windows CI run fails until it is fixed. This is Windows-side work.
- **Windows CI runs stop at #10.** Because `IntegrationTests` fails first, the rest of the Windows suite (including the twelve legacy tests moved into `windows/Tests/` in #12) is not run in CI until #10 is fixed.
- **Remaining text overflow (88 findings).** Mostly at 200% text in dense dialogs (Library Maintenance, Settings), which need a layout redesign. The report is in the `overflow-audit` artifact of each audit run.

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

- **2026-10-06:**
  - Shared game identification: `windows/GameRecognition.cs` moved to the root as `FishBowl.GameRecognition.cs` and is compiled by both builds. Its WinForms helpers (`SmoothPainting`, `ConsistentInputs`) moved to `windows/FishBowl.InputPainting.cs`. Linux uses `FishBowl.LinuxShims.cs` for the `System.Drawing` types (built-in PNG encoder). Fixed Linux storing its library in the current directory when `~/.local/share` doesn't exist yet.
  - Merged #8 (shared data model), #9 (Tux easter egg), #11 (text overflow fixes; Windows build now compiles `windows/FishBowl.TextFit.cs`) and #12 (README, tidy-up, handoff). Published release v1.25.8.
  - Rewrote the README and tidied the repository: removed superseded source and test files, the old root `FishBowl.cs`, and obsolete review notes.
  - Moved the Windows binaries to the v1.25.8 release, and added `AGENTS.md`, `CLAUDE.md` and this file.
  - Opened #11 (text overflow) and #9 (Tux easter egg). Filed #10.
- **2026-10-05:** shared the data model (#8, re-landing #7), published the 1.25.8 source (#6), and added the text overflow audit (#5), the bug report form (#4) and the Windows source, assets and CI (#3).
- **2026-09-30:** first Linux version merged (#1).

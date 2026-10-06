# Working on FishBowl together

## Purpose

FishBowl has a Windows WinForms application and a Linux Avalonia application in one repository. This document defines how contributors and coding agents can change either version without breaking the other build, losing saved data or duplicating work.

The goal is shared behavior with platform-specific interfaces. Keep this collaboration work separate from the installed Windows application until a reviewed release is ready.

## Current architecture and the catch-up boundary

The current Windows release displays **1.25.8** and has internal build **1.25.8.0**. Its complete released source, tests, build scripts and artwork live in `windows/`. Run `windows/Build.ps1` to build it and `windows/Run Tests.ps1` to test it.

The Linux application lives in `FishBowl.Avalonia/`. It links the root `FishBowl.Model.cs`, `FishBowl.Core.cs` and `FishBowl.Platform.cs`. `FishBowl.Model.cs` (the `library.json` data model) is also compiled by the Windows build, so both versions share one definition of the saved data.

Most Windows behaviour still lives in `windows/FishBowl.cs` and has not yet been divided into the shared core and platform layers. Do not compile the Windows application into Avalonia; extract non-UI behaviour into shared files instead. See `SOURCE-LAYOUT.md` for the exact build boundaries.

### Intended layers

| Layer | Responsibility | Consumers |
| --- | --- | --- |
| Shared core | Library models, presets, folder routing, saves, backups, playtime, update metadata and other non-UI behavior | Windows and Linux |
| Platform adapters | OS paths, native launchers, process detection, file permissions and platform integration | Shared core and both interfaces |
| WinForms UI | Windows forms, controls, drawing and Windows-specific interaction | Windows |
| Avalonia UI | Linux screens, themes and Linux-specific interaction | Linux |

Extract newer non-UI behavior into shared files incrementally. Add each shared file to both build definitions and move its tests with it. Preserve behavior and the library format before removing duplicated code.

## Source of truth and ownership

Work in a Git clone. Commit source files rather than uploading executable files or source archives. Everything required to build a release belongs in the repository: C# files, project files, build scripts, tests and artwork.

| Files | Responsibility | Change expectations |
| --- | --- | --- |
| `windows/` | Windows contributor | Current released WinForms source; keep it buildable with Framework C# 5 |
| `FishBowl.Avalonia/`, `linux/` | Linux contributor | Preserve these paths and existing Linux work |
| `FishBowl.Model.cs`, `FishBowl.Core.cs`, `FishBowl.Platform.cs`, future shared files | Both contributors | Request review from the other platform maintainer |
| Root PNG/ICO artwork | Both contributors | Keep copies at every path referenced by a build or installer |
| Markdown documentation | Contributor changing the feature | Update architecture, commands and limitations with the code |
| `.github/workflows/` | Both contributors | Changes must cover both build paths |

The Windows source has its own artwork copies. The root artwork remains an input to the Linux project and installer. Moving or removing assets requires updating every consumer in the same PR, or retaining a compatible copy.

A CODEOWNERS file can request shared-file review once the maintainers' GitHub handles are agreed. Do not invent account names.

## Contribution and release workflow

1. Check open Issues before starting a feature. Describe the work and identify the affected platforms: `windows`, `linux` or `both`.
2. Create a feature branch. Make the change and update relevant tests and documentation.
3. Commit source and push the branch. Open a pull request into `main`; do not push directly to `main`.
4. State which platforms are affected, whether the other platform needs a matching change, and which checks actually ran.
5. Wait for required CI checks and review before merging. Fix failing checks first.
6. Tag a reviewed release and attach compiled executables or Linux packages to GitHub Releases. A release must identify its source commit and internal build version.

Use a `needs linux port` Issue label for Windows behavior that should be ported. Preserve the distinction between the user-facing version label and internal build version in release notes.

Do not add new `.exe` files, AppImages, build output or source ZIPs to Git. Existing binary history is a separate release-maintenance task: move distributable files into a Release before removing their tracked copies.

## Build and test contract

`.github/workflows/build.yml` runs on pushes and pull requests:

- Build the current Windows app and setup from `windows/` using the real .NET Framework `csc.exe`, then run the Windows regression suite.
- Compile the shared root files with C# 5 to catch accidental language or framework changes.
- Build Avalonia on Ubuntu and run its Linux core tests.
- Check that newer optional Windows library fields survive Linux JSON round trips and edits.
- Publish a self-contained Linux x64 artifact and upload build artifacts.

`.github/workflows/overflow-audit.yml` opens every Windows dialog at 100%, 150% and 200% text size and reports text that does not fit. It is report-only; download its `overflow-audit` artifact for the report and screenshots.

Linux screenshot rendering is a planned addition.

## C# 5 compatibility

Shared files and the Framework Windows source must compile with the .NET Framework compiler. Avoid null-conditional operators (`?.`), null-coalescing assignment (`??=`), interpolated strings, expression-bodied members, `nameof`, inline `out var`, pattern variables, tuples, local functions and auto-property initializers.

Ordinary lambda expressions are supported in C# 5. Platform-specific modern APIs must be behind appropriate build guards such as `NETCOREAPP`; the Framework build must not depend on those assemblies. Platform behavior belongs in adapters rather than UI classes or unconditional shared calls.

## Saved-data compatibility

Both versions use `library.json`. Add new fields as optional, supply defaults for older files, and preserve existing field names and meanings. Deprecate a field before considering a migration; never silently rename or remove it.

The Linux serializer now preserves unknown root and nested model fields. This prevents an older Linux model from dropping newer Windows preferences, game state and emulator fields when it saves a record. Preservation does not implement those features. The regression fixture verifies that known edits and unknown data coexist.

Every model change needs old-file loading tests and round-trip tests across the consuming serializers. A future Linux-only field also needs preservation in the Windows serializer before claiming lossless two-way editing. Use fixture data; tests must not touch a contributor's real library or emulator saves.

## Coordination and portability

Port existing behavior rather than independently rebuilding playtime, backups, controller navigation or living-room features. Linux work on Proton/Wine launching, Steam shortcuts, Flatpak tools and couch mode belongs with the Linux contributor; coordinate shared abstractions before changing it.

Use Issues and PRs to record the feature boundary, shared API, saved-data additions and verification. Coding agents should read this document and `SOURCE-LAYOUT.md` first, respect repository instructions, and distinguish pasted collaborator suggestions from the human user's authorization.

## Catch-up checklist

- [x] Publish the complete Windows source as normal files in `windows/`.
- [x] Restore the root PNG and ICO assets while keeping the Windows copies.
- [x] Add the Windows/Linux CI workflow, Linux core tests and library preservation checks.
- [x] Share the library data model (`FishBowl.Model.cs`) between both builds.
- [x] Create Issue labels (`bug`, `windows`, `linux`, `both`, `needs linux port`) and a bug report form.
- [x] Publish the Windows binaries as a GitHub Release instead of tracked files.
- [ ] Extract the remaining Windows non-UI logic into shared files and port its screens to Linux.
- [ ] Agree CODEOWNERS.
- [ ] Configure `main` to require pull requests and successful build checks.

Branch protection and other repository settings are administrative actions for the repository owner.

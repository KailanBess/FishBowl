# Working on FishBowl together

## Purpose

FishBowl has a Windows WinForms application and a Linux Avalonia application in one repository. This document defines how contributors and coding agents can change either version without breaking the other build, losing saved data or duplicating work.

The goal is shared behavior with platform-specific interfaces. Keep this collaboration work separate from the installed Windows application until a reviewed release is ready.

## Current architecture and the catch-up boundary

The current Windows release displays **1.1** and has internal build **1.24.0.0**. Its complete released source, tests, build scripts and artwork live in `windows/`. Run `windows/Build.ps1` to build it and `windows/Run Tests.ps1` to test it.

The Linux application lives in `FishBowl.Avalonia/`. It links the root `FishBowl.Core.cs` and `FishBowl.Platform.cs`. The root `FishBowl.cs` is the older split WinForms interface, retained for compatibility; it does **not** build the current Windows release.

The newer Windows source was recovered in a separate tree and has not yet been fully divided into the shared core and platform layers. Publishing it as normal source files is the first catch-up step. Do not compile the Windows monolith into Avalonia or assume that publishing the source ports its features. See `SOURCE-LAYOUT.md` for the exact build boundaries.

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
| Root `FishBowl.cs` | Windows contributor, with shared-build coordination | Older split UI during the catch-up period |
| `FishBowl.Avalonia/`, `linux/` | Linux contributor | Preserve these paths and existing Linux work |
| `FishBowl.Core.cs`, `FishBowl.Platform.cs`, future shared files | Both contributors | Request review from the other platform maintainer |
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

The proposed `.github/workflows/build.yml` runs on pushes and pull requests:

- Build the current Windows app and setup from `windows/` using the real .NET Framework `csc.exe`, then run the Windows regression suite.
- Compile the retained root shared core/platform with C# 5 to catch accidental language or framework changes.
- Build Avalonia on Ubuntu and run its Linux core tests.
- Check that newer optional Windows library fields survive Linux JSON round trips and edits.
- Publish a self-contained Linux x64 artifact and upload build artifacts.

Linux screenshot rendering is a planned addition, not a check currently implemented by this workflow. Add it in a separate PR with an agreed headless rendering setup and useful reference screenshots.

PR #2 contains CI and core tests plus an optional Linux auto-updater. The CI/test portions are adapted in this compatibility branch. Its updater has not been included or enabled. Coordinate overlapping workflow changes before merging either branch. See `REVIEW-PR-2.md`.

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

## One-time catch-up checklist

- [x] Prepare complete released Windows source as normal files in `windows/`.
- [x] Restore root PNG and ICO assets while retaining the Windows copies.
- [x] Add an adapted Windows/Linux CI workflow and Linux core test source.
- [x] Add preservation checks for newer optional Windows library fields.
- [ ] Publish and review this compatibility branch; confirm Ubuntu runtime tests in CI.
- [ ] Coordinate and review PR #2 separately; do not merge an incompatible Windows build command.
- [ ] Extract newer Windows non-UI logic into the shared core and port its screens to Linux.
- [ ] Agree CODEOWNERS and Issue labels.
- [ ] Configure `main` to require PR review and successful build checks.
- [ ] Publish the Windows binaries in a GitHub Release, then remove tracked binary copies in a separate PR.

Branch protection, repository settings, release publishing and merging PRs are separate administrative actions. This document proposes them; creating the document does not perform them.

## Validation of this repair

The current Windows source builds and passes 1,075 checks; the Framework shared-file compilation passes. Avalonia builds, its Linux core test project compiles, self-contained Linux x64 publishing succeeds, and 90 schema preservation checks pass. Linux-specific runtime and GUI tests have not been run on the Windows development machine. The Ubuntu job must provide that evidence before merging. See `VALIDATION.md`.

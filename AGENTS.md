# Instructions for coding agents

FishBowl is developed by two people, each working with a coding agent: one maintains the Windows version (`windows/`), the other the Linux version (`FishBowl.Avalonia/`, `linux/`). These rules keep the two versions from breaking each other.

## Before you start and when you finish

1. **Read [HANDOFF.md](HANDOFF.md)** for the current state: open pull requests, work in progress, known issues and next steps.
2. **Update HANDOFF.md as part of every change.** Record what you changed, what is still open, and anything the other side needs to know. Add a dated entry to its log.

## Keep all work on GitHub

Both maintainers stay in sync only through this GitHub repository. Some contributors are new to Git, so **agents handle the Git steps themselves and explain them in a sentence**:

1. **At the start of every session,** update to the latest `main` (`git pull`, or fetch and rebase your branch), so you build on the other person's latest work.
2. **Work on a branch** in a clone of this repository. Never work in a separate copy, a downloaded zip or an unpushed folder.
3. **Commit and push frequently,** at least at the end of every session, so work is never only on one computer.
4. **Finish with a pull request** into `main`, and update `HANDOFF.md` in the same pull request.
5. **Never use "Add files via upload"** on the GitHub website, and never commit zips of source or `.exe` files. Executables go on the Releases page.

**If a contributor asks for something that would bypass this,** suggest the GitHub-backed way instead and offer to do it for them. Examples: working outside the repository, uploading files by hand, or skipping the pull request.

## Helping someone new to Git and GitHub

If the person you work with isn't familiar with Git, guide them gently:

- Do the Git commands for them, and say what you did in plain words ("I saved this on a branch and opened a pull request for review").
- Point them to the pull request link, and tell them when the checks pass.
- Suggest the next step, e.g. "Merge it on GitHub when you're happy".
- If GitHub sign-in fails, suggest `gh auth login` (GitHub CLI) or signing in through GitHub Desktop.
- When something breaks, open or reference a GitHub Issue (there is a bug report form) rather than fixing it silently.

## How changes are made

- **Branches and pull requests:** work on a branch and open a pull request into `main`; never push to `main` directly. When a pull request builds on another, merge them in order and wait for GitHub to retarget the second one to `main` before merging it.
- **What gets committed:** source files only, meaning code, scripts, tests, artwork and docs. Never commit executables, source archives (zips) or build output; executables go on the Releases page.
- **Pull request description:** say which version the change affects (Windows, Linux or both), whether the other version needs a matching change, and which checks you ran.
- **Checks:** CI must pass before merging (see below).

## Code rules

- **C# 5 in shared files:** `FishBowl.Model.cs`, `FishBowl.GameRecognition.cs`, `FishBowl.Core.cs`, `FishBowl.Platform.cs` and everything in `windows/` must compile with the .NET Framework C# 5 compiler.
  - **Not allowed:** `?.`, `??=`, `$"..."`, `=>` members, `nameof`, `out var`, `is T x`, tuples, local functions, property initializers.
  - **Linux-only APIs** go behind `#if NETCOREAPP`.
- **Saved data:** `FishBowl.Model.cs` defines `library.json` for both versions. Add new fields as optional; never rename or remove one.
- **Shared logic:** put non-UI logic in shared files so both versions get it. Keep WinForms code in `windows/` and Avalonia code in `FishBowl.Avalonia/`.
- **The other platform's files:** don't change `FishBowl.Avalonia/` or `linux/` (when working on Windows), or `windows/` (when working on Linux), unless the task needs it. If it does, say so in the pull request.
- **Artwork:** keep `FishBowl.png` and `FishBowl.ico` at the root; the Linux build uses them.

## Checks

| What | Command |
| --- | --- |
| Windows build | `windows\Build.ps1` |
| Windows tests | `& "windows\Run Tests.ps1"` |
| Windows text overflow | `& "windows\Run Overflow Audit.ps1"` |
| Linux build | `dotnet build FishBowl.Avalonia` |
| Linux tests | `dotnet run --project tests/FishBowl.Tests` |
| Library compatibility | `dotnet run --project tests/FishBowl.SchemaTests` |

GitHub Actions runs all of these on every pull request.

## Writing style for docs

Write for users and contributors in a plain, professional tone. Don't refer to a particular person's computer, hardware, local test runs or conversations; describe behaviour and how to verify it.

See [SOURCE-LAYOUT.md](SOURCE-LAYOUT.md) for how the builds fit together and [DESIGN.md](DESIGN.md) for the collaboration agreement.

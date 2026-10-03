# PR #2 review

Reviewed PR #2 (`eb1d38727fa5e094c8d2d72f8e9b393d98041558`) against main (`ce3f53be1de3856379d79f6cdb5b20b5b2696328`). It is titled "Add build checks (Windows + Linux), core tests, and an optional Linux auto-updater" and is open.

The build workflow and Linux core tests are useful. The checkout v7, upload-artifact v7 and setup-dotnet v6 tags exist in the official action repositories. The Windows job in that PR compiles the old root UI/core split, which does not produce the current released app. This repair changes that job to compile and test `windows/` and separately compile the shared root files with C# 5. It restores the missing root icon/image that both jobs need.

The PR also changes MainWindow.Actions.cs and introduces `linux/auto-update.sh`, which can install a daily systemd user timer, fetch/fast-forward the upstream checkout, rebuild and reinstall. Those changes have not been included in this repair, enabled or tested. They should be reviewed separately from the build-only changes.

No GitHub PR has been merged or changed. Publishing this repair and merging PR #2 both need coordination: their workflow changes overlap. Rebase/update PR #2 to use the released Windows source path, or retain the adapted workflow here and handle its optional updater separately.

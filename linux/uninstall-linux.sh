#!/bin/sh
# Removes FishBowl's program files and menu entry. Your library (~/.local/share/FishBowl) is kept.
set -eu
prefix=${PREFIX:-$HOME/.local}
data=${XDG_DATA_HOME:-$HOME/.local/share}
[ "$prefix" != "$HOME/.local" ] && data="$prefix/share"
rm -rf "$prefix/lib/fishbowl"
rm -f "$prefix/bin/fishbowl" "$data/applications/fishbowl.desktop" "$data/icons/hicolor/256x256/apps/fishbowl.png"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$data/applications" || true
echo "FishBowl was removed. Your emulator library remains in ${XDG_DATA_HOME:-$HOME/.local/share}/FishBowl."

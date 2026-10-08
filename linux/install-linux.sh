#!/bin/sh
# Builds FishBowl for Linux and installs it for the current user:
#   ~/.local/lib/fishbowl       program files
#   ~/.local/bin/fishbowl       launcher
#   application menu entry and icon
# Requires the .NET 10 SDK to build (Arch: pacman -S dotnet-sdk; others: https://dot.net).
# The build is self-contained, so running FishBowl does not need .NET installed.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
prefix=${PREFIX:-$HOME/.local}
data=${XDG_DATA_HOME:-$HOME/.local/share}
[ "$prefix" != "$HOME/.local" ] && data="$prefix/share"
case "$(uname -m)" in
    x86_64) rid=linux-x64 ;;
    aarch64|arm64) rid=linux-arm64 ;;
    *) echo "Unsupported CPU architecture: $(uname -m)" >&2; exit 1 ;;
esac

command -v dotnet >/dev/null 2>&1 || { echo "The .NET SDK is required to build FishBowl (https://dot.net)." >&2; exit 1; }

echo "Building FishBowl ($rid)..."
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
dotnet publish "$here/../FishBowl.Avalonia/FishBowl.Avalonia.csproj" -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "$out" --nologo -v q

mkdir -p "$prefix/lib/fishbowl" "$prefix/bin" "$data/applications" "$data/icons/hicolor/256x256/apps"
rm -rf "$prefix/lib/fishbowl/"*
cp -r "$out/." "$prefix/lib/fishbowl/"
cp "$here/../FishBowl.png" "$prefix/lib/fishbowl/FishBowl.png"
ln -sf "$prefix/lib/fishbowl/FishBowl" "$prefix/bin/fishbowl"
if command -v magick >/dev/null 2>&1; then
    magick "$here/../FishBowl.png" -resize 256x256 "$data/icons/hicolor/256x256/apps/fishbowl.png"
else
    cp "$here/../FishBowl.png" "$data/icons/hicolor/256x256/apps/fishbowl.png"
fi
sed "s|@EXEC@|$prefix/lib/fishbowl/FishBowl|g" "$here/fishbowl.desktop" > "$data/applications/fishbowl.desktop"
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$data/applications" || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q -t "$data/icons/hicolor" || true

echo "FishBowl is installed. Open it from your application menu, or run: fishbowl"
case ":$PATH:" in *":$prefix/bin:"*) ;; *) echo "Note: $prefix/bin is not on your PATH." ;; esac

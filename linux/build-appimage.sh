#!/usr/bin/env bash
# Builds FishBowl-<version>-x86_64.AppImage: a single file that runs on most x86_64 Linux distributions.
#   linux/build-appimage.sh [output-directory]
# Needs the .NET 10 SDK. Downloads appimagetool into the build folder if it isn't on PATH.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
root="$here/.."
out="$(mkdir -p "${1:-$root/artifacts}" && cd "${1:-$root/artifacts}" && pwd)"
version="$(sed -n 's|.*<Version>\([^<]*\)</Version>.*|\1|p' "$root/FishBowl.Avalonia/FishBowl.Avalonia.csproj" | head -1)"
work="$out/appimage-build"
appdir="$work/FishBowl.AppDir"
rm -rf "$work" && mkdir -p "$appdir/usr/lib/fishbowl" "$appdir/usr/bin" "$appdir/usr/share/applications" "$appdir/usr/share/icons/hicolor/256x256/apps"

dotnet publish "$root/FishBowl.Avalonia/FishBowl.Avalonia.csproj" -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "$appdir/usr/lib/fishbowl"
# FishBowl loads its logo from beside the program.
cp "$root/FishBowl.png" "$appdir/usr/lib/fishbowl/FishBowl.png"
ln -s ../lib/fishbowl/FishBowl "$appdir/usr/bin/fishbowl"

if command -v magick >/dev/null 2>&1; then magick "$root/FishBowl.png" -resize 256x256 "$appdir/usr/share/icons/hicolor/256x256/apps/fishbowl.png"
else cp "$root/FishBowl.png" "$appdir/usr/share/icons/hicolor/256x256/apps/fishbowl.png"; fi
cp "$appdir/usr/share/icons/hicolor/256x256/apps/fishbowl.png" "$appdir/fishbowl.png"
ln -s fishbowl.png "$appdir/.DirIcon"
sed 's|^Exec=.*|Exec=fishbowl %F|' "$here/fishbowl.desktop" > "$appdir/usr/share/applications/fishbowl.desktop"
printf 'X-AppImage-Version=%s\n' "$version" >> "$appdir/usr/share/applications/fishbowl.desktop"
cp "$appdir/usr/share/applications/fishbowl.desktop" "$appdir/fishbowl.desktop"

cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
here="$(dirname "$(readlink -f "$0")")"
exec "$here/usr/lib/fishbowl/FishBowl" "$@"
EOF
chmod +x "$appdir/AppRun"

tool="$(command -v appimagetool || true)"
if [ -z "$tool" ]; then
    tool="$work/appimagetool"
    curl -fsSL -o "$tool" https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x "$tool"
fi
# Extract-and-run avoids needing FUSE on build machines.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH=x86_64 "$tool" --no-appstream "$appdir" "$out/FishBowl-$version-x86_64.AppImage"
rm -rf "$work"
echo "Built $out/FishBowl-$version-x86_64.AppImage"

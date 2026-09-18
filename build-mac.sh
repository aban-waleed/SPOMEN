#!/usr/bin/env bash
# Builds a self-contained SPOMEN.app for macOS and zips it with the bundled menus.
#
#   ./build-mac.sh            # Apple Silicon (osx-arm64)
#   ./build-mac.sh osx-x64    # Intel Macs
#
# Needs the .NET 8 SDK. The publish step works on any OS; the .icns icon and
# ad-hoc code signature are only produced when run on a Mac (sips/iconutil/codesign).
set -euo pipefail

RID="${1:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJ="$ROOT/src/SPOMEN.Mac/SPOMEN.Mac.csproj"
OUT="$ROOT/artifacts/mac/$RID"
PUB="$OUT/publish"
STAGE="$OUT/SPOMEN-$RID"
APP="$STAGE/SPOMEN.app"
ZIP="$OUT/SPOMEN-$RID.zip"

rm -rf "$OUT"
mkdir -p "$PUB"

dotnet publish "$PROJ" -c Release -r "$RID" --self-contained true -o "$PUB" \
  -p:DebugType=none -p:DebugSymbols=false

# Assemble the .app bundle
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUB/." "$APP/Contents/MacOS/"
cp "$ROOT/src/SPOMEN.Mac/Info.plist" "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/SPOMEN"
rm -rf "$PUB"

# App icon (macOS tools only)
if command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
  ICONSET="$OUT/icon.iconset"
  mkdir -p "$ICONSET"
  for s in 16 32 128 256 512; do
    sips -z "$s" "$s" "$ROOT/src/SPOMEN.Mac/Assets/icon.png" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
    sips -z "$((s * 2))" "$((s * 2))" "$ROOT/src/SPOMEN.Mac/Assets/icon.png" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/SPOMEN.icns"
  rm -rf "$ICONSET"
fi

# Ad-hoc signature so Gatekeeper allows launch (users still right-click > Open the first time)
if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP"
fi

# Menus and instructions next to the app
mkdir -p "$STAGE/menus"
cp "$ROOT"/menus/*.gscc "$STAGE/menus/"
cp "$ROOT/published/HOW_TO_USE.txt" "$STAGE/HOW_TO_USE.txt"

# Zip: ditto on macOS (preserves the bundle exactly), zip elsewhere, python as last resort
rm -f "$ZIP"
if command -v ditto >/dev/null 2>&1; then
  ditto -c -k --sequesterRsrc --keepParent "$STAGE" "$ZIP"
elif command -v zip >/dev/null 2>&1; then
  ( cd "$OUT" && zip -qry "$(basename "$ZIP")" "$(basename "$STAGE")" )
else
  python3 - "$STAGE" "$ZIP" <<'PY'
import os, sys, zipfile
src, dst = sys.argv[1], sys.argv[2]
base = os.path.dirname(src)
with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(src):
        for f in sorted(files):
            p = os.path.join(root, f)
            zi = zipfile.ZipInfo.from_file(p, os.path.relpath(p, base))  # keeps unix mode bits
            zi.compress_type = zipfile.ZIP_DEFLATED
            with open(p, "rb") as fh:
                z.writestr(zi, fh.read())
PY
fi
echo "Built: $ZIP"

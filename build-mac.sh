#!/usr/bin/env bash
# Builds a self-contained SPOMEN.app for macOS and zips it with the bundled menus.
#
#   ./build-mac.sh            # Apple Silicon (osx-arm64)
#   ./build-mac.sh osx-x64    # Intel Macs
#
# Needs the .NET 8 SDK. The publish step works on any OS; the .icns icon and
# code signature are only produced when run on a Mac (sips/iconutil/codesign).
#
# Signing / notarization (optional, needs an Apple Developer account):
#   MAC_SIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)"   # from `security find-identity -v -p codesigning`
#   MAC_NOTARY_PROFILE="SPOMEN"   # keychain profile made once with:
#       xcrun notarytool store-credentials SPOMEN --apple-id you@example.com --team-id TEAMID --password <app-specific password>
#   or instead of the profile: APPLE_ID, APPLE_TEAM_ID, APPLE_APP_PASSWORD
#   MAC_SIGN_IDENTITY="Developer ID Application: ..." MAC_NOTARY_PROFILE=SPOMEN ./build-mac.sh
# With MAC_SIGN_IDENTITY unset the app is ad-hoc signed and users right-click > Open the first time.
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

# Code signature
if command -v codesign >/dev/null 2>&1; then
  if [[ -n "${MAC_SIGN_IDENTITY:-}" ]]; then
    ENT="$ROOT/src/SPOMEN.Mac/entitlements.plist"
    echo "Signing with: $MAC_SIGN_IDENTITY"
    # Sign every Mach-O inside the bundle first (dylibs and the .NET native host), then the bundle
    # itself. Apple rejects --deep for notarization, so this is done explicitly, innermost first.
    while IFS= read -r -d '' f; do
      if file "$f" | grep -q 'Mach-O'; then
        codesign --force --options runtime --timestamp --entitlements "$ENT" --sign "$MAC_SIGN_IDENTITY" "$f"
      fi
    done < <(find "$APP/Contents/MacOS" -type f ! -name SPOMEN -print0)
    codesign --force --options runtime --timestamp --entitlements "$ENT" --sign "$MAC_SIGN_IDENTITY" "$APP/Contents/MacOS/SPOMEN"
    codesign --force --options runtime --timestamp --entitlements "$ENT" --sign "$MAC_SIGN_IDENTITY" "$APP"
    codesign --verify --deep --strict --verbose=2 "$APP"
  else
    # Ad-hoc signature so Gatekeeper allows launch (users still right-click > Open the first time)
    codesign --force --deep --sign - "$APP"
  fi
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
# Notarization: submit the zip, staple the ticket into the app, then re-zip so the download carries it.
if [[ -n "${MAC_SIGN_IDENTITY:-}" ]] && command -v xcrun >/dev/null 2>&1; then
  NOTARY_ARGS=()
  if [[ -n "${MAC_NOTARY_PROFILE:-}" ]]; then
    NOTARY_ARGS=(--keychain-profile "$MAC_NOTARY_PROFILE")
  elif [[ -n "${APPLE_ID:-}" && -n "${APPLE_TEAM_ID:-}" && -n "${APPLE_APP_PASSWORD:-}" ]]; then
    NOTARY_ARGS=(--apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_PASSWORD")
  fi
  if [[ ${#NOTARY_ARGS[@]} -gt 0 ]]; then
    echo "Submitting to Apple notary service (this waits for the verdict)..."
    NOTARY_ZIP="$OUT/notarize-$RID.zip"
    ditto -c -k --keepParent "$APP" "$NOTARY_ZIP"
    if ! xcrun notarytool submit "$NOTARY_ZIP" "${NOTARY_ARGS[@]}" --wait; then
      echo "Notarization failed. Inspect with: xcrun notarytool log <submission-id> ${NOTARY_ARGS[*]}" >&2
      exit 1
    fi
    rm -f "$NOTARY_ZIP"
    xcrun stapler staple "$APP"
    xcrun stapler validate "$APP"
    spctl -a -vv -t exec "$APP" || true
    rm -f "$ZIP"
    ditto -c -k --sequesterRsrc --keepParent "$STAGE" "$ZIP"
    echo "Signed, notarized and stapled."
  else
    echo "MAC_SIGN_IDENTITY set but no notary credentials (MAC_NOTARY_PROFILE or APPLE_ID/APPLE_TEAM_ID/APPLE_APP_PASSWORD); app is signed but NOT notarized." >&2
  fi
fi

echo "Built: $ZIP"

#!/usr/bin/env bash
#
# Builds a double-clickable "Musical Scales.app" bundle that needs no .NET install.
#
# Output: dist/macos/<runtime>/Musical Scales.app
#
# Usage:  ./build/publish-macos.sh [osx-arm64|osx-x64]
#
#   osx-arm64  Apple Silicon (M1 and later) - the default
#   osx-x64    Intel Macs
#
# This script also runs on Windows/Linux to cross-publish the bundle; only the
# final `codesign` step is skipped when not on a Mac.

set -euo pipefail

RUNTIME="${1:-osx-arm64}"
case "$RUNTIME" in
    osx-arm64|osx-x64) ;;
    *) echo "Unknown runtime '$RUNTIME' (expected osx-arm64 or osx-x64)" >&2; exit 1 ;;
esac

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/MusicalScales/MusicalScales.csproj"
STAGE="$ROOT/dist/macos/$RUNTIME/stage"
APP="$ROOT/dist/macos/$RUNTIME/Musical Scales.app"

echo "Publishing Musical Scales for $RUNTIME ..."

rm -rf "$STAGE" "$APP"
dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$STAGE" \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Assemble the bundle layout macOS expects.
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$ROOT/build/Info.plist" "$APP/Contents/Info.plist"
cp -R "$STAGE/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/MusicalScales"
rm -rf "$STAGE"

# Ad-hoc signature. Without it, Gatekeeper on Apple Silicon refuses to launch
# an unsigned bundle outright rather than merely warning about it.
if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$APP"
    echo "Ad-hoc signed."
else
    echo "codesign not available here; sign on a Mac before distributing."
fi

echo
echo "Done: $APP"
echo "First launch on another Mac: right-click the app and choose Open,"
echo "or run: xattr -dr com.apple.quarantine \"$APP\""

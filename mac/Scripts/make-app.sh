#!/bin/zsh
# Builds Aiko.app: one universal binary for both kinds of Mac, with the bridge and the shim inside.
#
# There is no Xcode project on purpose (spike S0): SwiftPM builds the binaries and this script
# makes the bundle around them. The signature is ad-hoc, which is enough to run the app on this
# machine; a release is signed and notarized elsewhere.
set -euo pipefail
cd "$(dirname "$0")/.."

products=".build/apple/Products/Release"
app="build/Aiko.app"

# The version has one home, the Windows project file, and macOS reads the same number. A checkout
# of mac/ on its own has no such file, and then the bundle says 0.0.0.
version=""
if [ -f ../Directory.Build.props ]; then
  version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' ../Directory.Build.props | head -1)"
fi
version="${version:-0.0.0}"

# Between releases the version does not change (D-236), so the commit is what tells two builds
# apart in a bug report. The Windows build gets the same from the .NET SDK.
commit="$(git rev-parse --short HEAD 2>/dev/null || true)"

swift build -c release --arch arm64 --arch x86_64

rm -rf build
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

cp "$products/AikoMac" "$app/Contents/MacOS/Aiko"

# The bundle ships the bridge under the name BridgeCommand.isAiko reads a status line by, and the
# shim under the name Claude Code is started with. SwiftPM cannot build them under those names.
cp "$products/aiko-bridge" "$app/Contents/MacOS/Aiko.Bridge"
cp "$products/aiko-shim" "$app/Contents/MacOS/claude"

# The fonts of the design system, the same files the Windows build carries. Without them Aiko
# still reads: the system font takes over.
fonts="../src/Aiko.App/Assets/Fonts"
if [ -d "$fonts" ]; then
  mkdir -p "$app/Contents/Resources/Fonts"
  cp "$fonts"/*.ttf "$app/Contents/Resources/Fonts/"
else
  echo "note: $fonts is not here, the app will use the system font"
fi

# The skills plugin this copy ships (D-234). Claude Code installs from the copy Aiko makes in its
# marketplace folder; without this the persona still works and the skills are simply not offered.
skills="../plugins/aiko"
if [ -d "$skills" ]; then
  mkdir -p "$app/Contents/Resources/plugins"
  cp -R "$skills" "$app/Contents/Resources/plugins/aiko"
else
  echo "note: $skills is not here, this build ships no skills"
fi

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>org.slayumind.aiko</string>
<key>CFBundleName</key><string>Aiko</string>
<key>CFBundleDisplayName</key><string>Aiko</string>
<key>CFBundleExecutable</key><string>Aiko</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$version</string>
<key>CFBundleVersion</key><string>$version</string>
<key>AikoCommit</key><string>$commit</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>LSUIElement</key><true/>
<key>NSHumanReadableCopyright</key><string>Apache-2.0. Not affiliated with Anthropic.</string>
</dict></plist>
PLIST

# The two small programs are signed first: signing the bundle does not reach inside MacOS.
codesign --force --sign - "$app/Contents/MacOS/Aiko.Bridge"
codesign --force --sign - "$app/Contents/MacOS/claude"
codesign --force --sign - "$app"

lipo -info "$app/Contents/MacOS/Aiko"
codesign -dv "$app" 2>&1 | grep -E "Signature|Identifier"
du -sh "$app"

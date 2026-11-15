#!/bin/zsh
# Builds Aiko.app: one universal binary for both kinds of Mac, with the bridge and the shim inside.
#
# There is no Xcode project on purpose (spike S0): SwiftPM builds the binaries and this script
# makes the bundle around them. The signature is ad-hoc, which is enough to run the app on this
# machine; a release is signed and notarized elsewhere.
set -euo pipefail
cd "$(dirname "$0")/.."

products=".build/universal"
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
# A tree copied to the Mac without its .git says nothing to `git`, so the caller can pass the
# commit in. tools/local-install-mac.ps1 does that.
commit="${AIKO_COMMIT:-$(git rev-parse --short HEAD 2>/dev/null || true)}"

# One build per kind of Mac, glued into one binary with lipo. A single build for both goes through
# the Xcode build system, which cannot find the build plugin of SwiftTerm (2026-11-14).
swift build -c release --arch arm64
swift build -c release --arch x86_64
mkdir -p "$products"
for name in AikoMac aiko-bridge aiko-shim; do
  lipo -create ".build/arm64-apple-macosx/release/$name" ".build/x86_64-apple-macosx/release/$name" -output "$products/$name"
done

rm -rf build
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$app/Contents/Frameworks"

cp "$products/AikoMac" "$app/Contents/MacOS/Aiko"

# whisper.cpp for voice input (D-427). SwiftPM fetched the universal framework and checked its
# checksum; it goes where the app looks for it (the rpath in Package.swift).
cp -R ".build/arm64-apple-macosx/release/whisper.framework" "$app/Contents/Frameworks/"

# The model of silence: without it whisper hears "[Music]" in a pause (spike 2026-10-15). The same
# file and hash as tools/fetch-whisper.ps1 on Windows; kept in .build between builds, and a file
# that does not match stops the build instead of shipping.
vad="ggml-silero-v5.1.2.bin"
vad_sha="29940d98d42b91fbd05ce489f3ecf7c72f0a42f027e4875919a28fb4c04ea2cf"
mkdir -p .build/voice
if [ ! -f ".build/voice/$vad" ] || [ "$(shasum -a 256 ".build/voice/$vad" | cut -d' ' -f1)" != "$vad_sha" ]; then
  curl -sSfL -o ".build/voice/$vad" "https://huggingface.co/ggml-org/whisper-vad/resolve/main/$vad"
fi
if [ "$(shasum -a 256 ".build/voice/$vad" | cut -d' ' -f1)" != "$vad_sha" ]; then
  rm -f ".build/voice/$vad"
  echo "the SHA256 of $vad does not match"
  exit 1
fi
cp ".build/voice/$vad" "$app/Contents/Resources/"

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

# One word of tools/strings.json, in English (0) or Russian (1).
mic_purpose() {
  python3 -c 'import json,sys; d=json.load(open("../tools/strings.json",encoding="utf-8")); print(next(e[1][int(sys.argv[1])] for e in d if e[0]=="VoiceMicPurpose"))' "$1"
}

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
<key>CFBundleDevelopmentRegion</key><string>en</string>
<key>CFBundleLocalizations</key><array><string>en</string><string>ru</string></array>
<key>NSMicrophoneUsageDescription</key><string>$(mic_purpose 0)</string>
</dict></plist>
PLIST

# The words macOS shows when voice input asks for the microphone, in each language of Aiko. They
# come from tools/strings.json like every other word (VoiceMicPurpose, D-428).
for lang in en ru; do
  mkdir -p "$app/Contents/Resources/$lang.lproj"
  index=$([ "$lang" = en ] && echo 0 || echo 1)
  printf '"NSMicrophoneUsageDescription" = "%s";\n' "$(mic_purpose $index)" > "$app/Contents/Resources/$lang.lproj/InfoPlist.strings"
done

# Who signs this build. The default "-" is ad-hoc, which is enough to run the app on this machine.
# The release workflow passes the hash of the Developer ID certificate it imported.
identity="${AIKO_CODESIGN_IDENTITY:--}"
flags=(--force --sign "$identity")
if [ "$identity" != "-" ]; then
  # Notarization takes nothing else: a trusted timestamp and the hardened runtime.
  flags+=(--timestamp --options runtime)
fi

# The two small programs are signed first: signing the bundle does not reach inside MacOS. This is
# why --deep is never used here; it would sign them in one pass and hide a file that was missed.
codesign "${flags[@]}" "$app/Contents/Frameworks/whisper.framework"
codesign "${flags[@]}" "$app/Contents/MacOS/Aiko.Bridge"
codesign "${flags[@]}" "$app/Contents/MacOS/claude"
codesign "${flags[@]}" --entitlements Scripts/Aiko.entitlements "$app"

lipo -info "$app/Contents/MacOS/Aiko"
codesign -dv "$app" 2>&1 | grep -E "Signature|Identifier"
du -sh "$app"

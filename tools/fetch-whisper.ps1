# Fetches the parts of whisper.cpp that voice input needs into artifacts/whisper (D-314).
#
# The files are not kept in git: they are 11 MB of binaries made by another project. The build of
# Aiko.App runs this script when the folder is empty, so a fresh clone and the release build get
# the same files. Every download is checked against a SHA256 written here; a changed file stops
# the build instead of shipping.
#
# whisper.cpp is MIT (THIRD-PARTY-NOTICES.md). Release b5454 is version 1.9.5; WhisperNative.cs
# is written for exactly that version and checks it at run time.

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repo 'artifacts/whisper'

$zip = @{
    Url    = 'https://github.com/ggml-org/whisper.cpp/releases/download/b5454/whisper-bin-x64.zip'
    Sha256 = '6ba69e3482d7826214f90a6a9c84ca07782aec1e1d0c6a7c30c994fd5d816ccb'
}

# Silero VAD: without it, silence comes back as "[Music]" (spike 2026-10-15).
$vad = @{
    Url    = 'https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v5.1.2.bin'
    Sha256 = '29940d98d42b91fbd05ce489f3ecf7c72f0a42f027e4875919a28fb4c04ea2cf'
    Name   = 'ggml-silero-v5.1.2.bin'
}

# Only what the app loads: whisper, ggml and one ggml-cpu library per instruction set.
$keep = @('whisper.dll', 'ggml.dll', 'ggml-base.dll', 'ggml-cpu-*.dll')

function Get-Checked($url, $sha256, $path) {
    Invoke-WebRequest -Uri $url -OutFile $path -UseBasicParsing
    $actual = (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
    if ($actual -ne $sha256) {
        Remove-Item $path
        throw "SHA256 of $url is $actual, expected $sha256"
    }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("aiko-whisper-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
try {
    $archive = Join-Path $work 'whisper.zip'
    Get-Checked $zip.Url $zip.Sha256 $archive
    Expand-Archive -Path $archive -DestinationPath (Join-Path $work 'x')

    New-Item -ItemType Directory -Force $target | Out-Null
    foreach ($pattern in $keep) {
        Get-ChildItem -Path (Join-Path $work 'x/Release') -Filter $pattern | Copy-Item -Destination $target -Force
    }

    Get-Checked $vad.Url $vad.Sha256 (Join-Path $target $vad.Name)
}
finally {
    Remove-Item -Recurse -Force $work
}

Write-Host "whisper: $((Get-ChildItem $target).Count) files in $target"

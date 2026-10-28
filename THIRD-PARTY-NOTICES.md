# Third party notices

Aiko includes work by other people. Their licences are below.

## Geist and Geist Mono

Typefaces by Vercel, used for all text and numbers in the app. The font files are built into the
program, because a tray app has no browser to load them from a website.

- Licence: SIL Open Font License 1.1
- Source: https://github.com/vercel/geist-font
- The full licence text ships with the fonts: `src/Aiko.App/Assets/Fonts/OFL.txt`

## Microsoft.Windows.CsWin32

Generates the Windows API calls Aiko needs for the tray icon, the screens and the mouse. It's a
build-time tool, and none of it ends up in the program.

- Licence: MIT
- Source: https://github.com/microsoft/CsWin32

## Windows Terminal control and ConPTY

The terminal in the Aiko window. The control (`Microsoft.Terminal.Wpf`, `Microsoft.Terminal.Control.dll`)
draws the screen of the program, and ConPTY (`conpty.dll`, `OpenConsole.exe`) runs the program in a
pseudo console. Both come from the Windows Terminal project.

- Licence: MIT, Copyright (c) Microsoft Corporation
- Source: https://github.com/microsoft/terminal
- The licence text ships next to the app: `src/Aiko.App/Assets/Licenses/windows-terminal-LICENSE.txt`

## Velopack

Builds the installer and handles updates.

- Licence: MIT
- Source: https://github.com/velopack/velopack

## whisper.cpp

Turns speech into text for voice input, on the computer. Aiko ships its libraries (`whisper.dll`,
`ggml*.dll`) from release b5454 (version 1.9.5). The speech model is not shipped: it is downloaded
when voice input is turned on.

- Licence: MIT, Copyright (c) 2023-2026 The ggml authors
- Source: https://github.com/ggml-org/whisper.cpp
- The full licence texts of both ship in the `whisper` folder of the app:
  `src/Aiko.App/Assets/Licenses/`

## Silero VAD

Tells speech from silence before whisper.cpp reads the sound. Aiko ships the model in the ggml
format (`ggml-silero-v5.1.2.bin`).

- Licence: MIT, Copyright (c) 2020-present Silero Team
- Source: https://github.com/snakers4/silero-vad, converted by https://huggingface.co/ggml-org/whisper-vad

## Not included

Aiko is inspired by [notchi](https://github.com/sk-ruban/notchi), which is under GPL-3.0. Aiko
contains no code and no assets from notchi.

Aiko is not affiliated with Anthropic. "Claude" and "Anthropic" are trademarks of Anthropic PBC.

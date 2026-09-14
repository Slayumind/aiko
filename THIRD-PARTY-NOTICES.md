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

## Velopack

Builds the installer and handles updates.

- Licence: MIT
- Source: https://github.com/velopack/velopack

## Not included

Aiko is inspired by [notchi](https://github.com/sk-ruban/notchi), which is under GPL-3.0. Aiko
contains no code and no assets from notchi.

Aiko is not affiliated with Anthropic. "Claude" and "Anthropic" are trademarks of Anthropic PBC.

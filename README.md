# Aiko

**How much of your Claude Code limits is left, for two accounts at once, without opening a
terminal.**

Aiko sits in the Windows tray. Rest the mouse on it and a card shows every environment you set up:
the five hour window, the week, and the weekly limit of the heavy model, with the time until each
resets and how long it lasts at the pace you are going.

![The Aiko card](assets/card.png)

Free and open source. Not affiliated with Anthropic.

## Why two accounts

Many people have a work account and a personal one, in separate Claude Code folders. `/usage` shows
one of them, the one you are in. Aiko shows both side by side and keeps them apart: an environment
is a name you choose and the folders behind it.

## How Aiko knows

Claude Code runs a status line command after every answer and hands it the limits. Aiko adds one
line to your Claude Code settings so that command is its own small program, which keeps the
numbers and throws the rest away. **No token is needed for this**, and nothing leaves your
computer.

If you already have a status line, Aiko keeps it: your command still runs and its output is still
shown.

The status line only runs in the Claude Code CLI. If you work in the VS Code panel or in Claude
Desktop, turn on **direct mode** for that environment, and Aiko asks the usage API itself. That one
needs the access token of the account, so it is off until you turn it on. See
[PRIVACY.md](PRIVACY.md) for exactly what happens with it.

Aiko counts how many copies run each day, if you let it. It is one switch with the update check,
off until you turn it on, and the identifier it sends changes every day, so two days cannot be
joined into one person. [PRIVACY.md](PRIVACY.md) says exactly what goes and what does not.

## What you need

- Windows 10 version 1809 or newer, 64 bit.
- Claude Code 2.1.80 or newer. Older versions do not report limits at all.
- A Claude.ai Pro, Max or Team plan. Enterprise accounts do not report limits.

## Installing

Download the installer from [Releases](https://github.com/Slayumind/aiko/releases/latest) and run
it. Aiko installs for the current user only and needs no administrator rights.

The build is not signed yet, so **SmartScreen will warn you**. Choose *More info*, then *Run
anyway*. Signing is planned; until then, every release carries `SHA256SUMS.txt` and a build
provenance attestation, and [SECURITY.md](SECURITY.md) shows how to check them.

Windows 11 hides the icon of a new app behind the arrow in the tray. Drag the Aiko icon out of
that menu onto the taskbar, or the card will have nothing to appear from.

## Removing

Uninstall Aiko from **Installed apps** as usual. It puts your status line back, removes its startup
entry and deletes its own folders. Nothing is left behind to clean up by hand.

## Building it yourself

You need the .NET 10 SDK. Aiko builds with no other tools.

```
git clone https://github.com/Slayumind/aiko.git
cd aiko
dotnet test Aiko.slnx
dotnet run --project src/Aiko.App
```

- `src/Aiko.Core` — everything that decides what to show: parsing, thresholds, countdowns,
  placement. No Windows and no UI, and every part of it has tests.
- `src/Aiko.App` — the tray icon, the windows and the network. It draws what the core decided.
- `src/Aiko.Bridge` — the small program Claude Code runs as its status line.

Want to change something? [CONTRIBUTING.md](CONTRIBUTING.md) says how, and what Aiko will not do.

## Acknowledgements

Inspired by [notchi](https://github.com/sk-ruban/notchi), GPL-3.0. Aiko contains no code or assets
from notchi.

Typefaces are [Geist and Geist Mono](https://github.com/vercel/geist-font) by Vercel, under the SIL
Open Font License. Other notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

[Apache-2.0](LICENSE), with [NOTICE](NOTICE).

"Claude" and "Anthropic" are trademarks of Anthropic PBC. Aiko is an independent project and is not
affiliated with, endorsed by, or sponsored by Anthropic.

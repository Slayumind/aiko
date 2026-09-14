<img src="assets/logo.svg" alt="" width="72">

# Aiko

**See how much of your Claude Code limits is left, for two accounts at once, without opening a
terminal.**

Aiko sits in the Windows tray. Rest the mouse on it and a card shows every environment you set up:
the five-hour window, the week, and the weekly limit of the heavy model. For each one you see when
it resets and how long it lasts at your current pace.

![The Aiko card](assets/card.png)

Free and open source. Not affiliated with Anthropic.

## Why two accounts

Many people have a work account and a personal one, in separate Claude Code folders. `/usage` shows
only the account you're in. Aiko shows both side by side. An environment is a name you choose and
the Claude Code folder behind it.

Environment 1 is always the `.claude` folder in your home: the VS Code panel, Claude Desktop and a
plain `claude` use it too. Environment 2 has a folder of its own, such as `.claude-work`. On the first
run a checklist finds the folders you already have or makes a new one. You sign in through Claude Code
itself, so Aiko never sees your password or token.

## Launch commands and project folders

If you want, Aiko adds a command for each environment. `aiko-work` starts Claude Code in the work
account from any folder, in PowerShell, cmd and Git Bash. The name is up to you.

You can also bind project folders to an environment. A plain `claude` in `D:\work`, or in any folder
inside it, then starts the work account, and every other folder gets the default environment. A
command wins over a binding, and Claude Code reminds you when the two don't match.

For this, Aiko puts a small `claude.exe` of its own first in your user PATH. It picks the account and
starts the real Claude Code. If your PowerShell profile has functions that switch accounts, Aiko can
turn them off, since they would hide the commands. It keeps a copy of the profile.

JetBrains IDEs take `claude` from PATH too, but nobody has tried Aiko with them yet.

## How Aiko gets the numbers

Claude Code runs a status line command after every answer and passes it the limits. Aiko adds one
line to your Claude Code settings, so that command is Aiko's own small program. It keeps the
numbers and drops everything else. **This needs no token**, and nothing leaves your computer.

If you already have a status line, Aiko keeps it: your command still runs and its output still
shows.

The status line runs only in the Claude Code CLI. If you work in the VS Code panel or in Claude
Desktop, turn on **direct mode** for that environment, and Aiko asks the usage API itself. Direct
mode needs the account's access token, so it's off until you turn it on.
[PRIVACY.md](PRIVACY.md) describes exactly what happens with the token.

If you allow it, Aiko also counts how many copies run each day. The count shares one switch with the
update check, which is off by default. The ID it sends changes every day, so two days can't be
linked to one person. [PRIVACY.md](PRIVACY.md) lists what is sent.

## What you need

- Windows 10 version 1809 or newer, 64-bit.
- Claude Code 2.1.80 or newer. Older versions don't report limits.
- A Claude.ai Pro, Max or Team plan. Enterprise accounts don't report limits.

## Install

Download the installer from [Releases](https://github.com/Slayumind/aiko/releases/latest) and run
it. Aiko installs for the current user and needs no administrator rights.

The build isn't signed yet, so **SmartScreen will warn you**. Choose *More info*, then *Run
anyway*. Signing is planned. Until then, every release comes with `SHA256SUMS.txt` and a build
provenance attestation, and [SECURITY.md](SECURITY.md) shows how to check them.

Windows 11 hides new app icons under the arrow next to the clock. Open the arrow and drag Aiko onto
the taskbar: under the arrow, the card can't open.

## Remove

Uninstall Aiko from **Installed apps**. It puts your status line back and removes the session
reminder, takes its folder out of PATH together with the launch commands, turns your PowerShell
profile functions back on, removes its startup entry and deletes its own folders. Your accounts and
history stay in their Claude Code folders. There's nothing left to clean up by hand.

## Build it yourself

You need the .NET 10 SDK and nothing else.

```
git clone https://github.com/Slayumind/aiko.git
cd aiko
dotnet test Aiko.slnx
dotnet run --project src/Aiko.App
```

- `src/Aiko.Core` decides what to show: parsing, thresholds, countdowns, placement. It has no
  Windows code and no UI, and every part of it has tests.
- `src/Aiko.App` has the tray icon, the windows and the network. It draws what the core decided.
- `src/Aiko.Bridge` is the small program Claude Code runs as its status line.

To change something, read [CONTRIBUTING.md](CONTRIBUTING.md) first. It also lists what Aiko won't do.

## Acknowledgements

Inspired by [notchi](https://github.com/sk-ruban/notchi), GPL-3.0. Aiko contains no code or assets
from notchi.

The typefaces are [Geist and Geist Mono](https://github.com/vercel/geist-font) by Vercel, under the
SIL Open Font License. Other notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

[Apache-2.0](LICENSE), with [NOTICE](NOTICE).

"Claude" and "Anthropic" are trademarks of Anthropic PBC. Aiko is an independent project and isn't
affiliated with, endorsed by or sponsored by Anthropic.

# Changelog

All notable changes to Aiko are listed here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Checksums.** `SHA256SUMS.txt` no longer lists the portable zip, which a release doesn't include.
  In 0.1.0 that line made `sha256sum -c` fail even though the installer was fine. The file in the
  0.1.0 release has been replaced.

## [0.1.0] - 2026-09-14

The first release. Everything below is new.

### Added

- **Tray icon.** A ring for one environment and a dot for the other, coloured by how much of each
  limit is used. The tooltip shows the numbers, so you can read them even when Windows 11 hides the
  icon under the arrow. A left click opens the card. The right-click menu swaps the environment in
  the ring, opens settings, refreshes the limits, checks for updates and quits.
- **App icon.** Aiko's logo, a green ring, in the Start menu, on the installer and on Aiko's
  windows.
- **Card.** Session, week and model limits for every environment: how much is used, when each one
  resets, and how long it lasts at your current pace once there's enough data. When a limit runs
  low, the card says so in a word as well as a colour. Beside each environment it shows the plan and
  whether the account is connected, or "working now" while a session in it is answering. **Open
  Claude Code** starts Claude Code in that environment
  (or **Sign in** when it isn't connected). A card opened by hovering closes when the mouse leaves.
  Click the icon or the card and it stays until you close it, or click the icon again. The card grows
  out of the icon when it opens and fades when it closes.
- **Island.** If you prefer, Aiko lives in a small window at the edge of the screen instead of the
  tray, with one ring per environment. Rest the mouse on it and it unfolds along the edge to show the
  percentage beside each ring; it also unfolds by itself for two seconds when a session crosses 75 % or
  90 %. It opens the card the same way the icon does. Pick it up
  and a pane of frosted glass shows where it will land on the nearest edge; along a side edge the rings turn
  into a column while it is still in your hand. Let go and it settles there, flat against the edge.
  It hides while a window is full screen.
- **Setup checklist.** On the first run, settings open on a checklist: install Claude Code, sign
  in, pick or create the second environment, access to the limits, launch commands, project folders
  and where to show Aiko. It finds your Claude Code folders and shows the exact line and the exact
  files before it changes anything. It keeps a copy of every file it changes, and "Not now" is a
  valid answer. It tells you where Aiko is and how to get the icon out from under the arrow. Later,
  **Add a second environment** and **Start over** open the same checklist in the same window.
- **Launch commands and project folders.** Aiko can add a command for each environment, such as
  `aiko-work`. It starts Claude Code in that account from any folder, in PowerShell, cmd and Git
  Bash. You can also bind project folders to an environment: a plain `claude` in a bound folder, or
  in any folder inside it, starts that account. For this, Aiko puts a small `claude.exe` of its own
  first in your user PATH. If your PowerShell profile has functions that switch accounts, Aiko can
  turn them off and keeps a copy of the profile.
- **Your own status line keeps working.** Aiko runs it through the same shell Claude Code uses and
  shows its output.
- **Works with no setup.** Claude Code started without `CLAUDE_CONFIG_DIR` uses the `.claude`
  folder in your home, and Aiko reads the same folder.
- **Settings** in one window with a menu on the left: a page for each environment, one for project
  folders and one for everything else. There is no Save button: changes apply at once, and a name or
  a command applies when you press Enter or leave the field. On an environment's page you see its
  account and plan, sign in again, rename it, change its command and remove it. Renaming keeps a
  command you already use and offers the new one. **Project folders** is one table of folders and
  their environments, and its last row is the environment for all other folders. **Check access**
  shows whether Claude Code still sends its limits to Aiko and sets it up again if not. **Copy
  diagnostics** gives a short summary for a bug report. Esc closes the window.
- **Direct mode** for the IDE panel and Claude Desktop, where Claude Code runs no status line. It's
  off by default and set per environment. It explains what it reads before you turn it on, reads
  the token for each request and never saves it.
- **Update checks,** off by default. The same request counts that one copy ran today, with an ID
  that changes every day. One switch turns off both. [PRIVACY.md](PRIVACY.md) lists what is sent.
- **English and Russian.** When you change the language, the window reopens in it straight away.
- **Uninstall** puts your status line back, takes Aiko's folder out of PATH together with the launch
  commands, turns your PowerShell profile functions back on, and removes the startup entry and
  Aiko's folders. It deletes a Claude Code settings file only if Aiko created it and nothing else was
  added.
- **Aiko repairs its own line.** If Aiko is reinstalled in another folder or Git gets installed,
  Aiko fixes its line at startup. It never adds the line where you said no. If one of Aiko's
  settings files can't be read, Aiko keeps it as a `.bad` file and doesn't overwrite it.

# Changelog

All notable changes to Aiko are listed here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The first release. Everything below is new.

### Added

- **Tray icon.** A ring for one environment and a dot for the other, coloured by how much of each
  limit is used. The tooltip shows the numbers, so you can read them even when Windows 11 hides the
  icon under the arrow. A left click opens the card. The right-click menu swaps the environment in
  the ring, opens settings, refreshes the limits, checks for updates and quits.
- **Card.** Session, week and model limits for every environment: how much is used, when each one
  resets, and how long it lasts at your current pace once there's enough data. When a limit runs
  low, the card says so in a word as well as a colour. A card opened by hovering closes when the
  mouse leaves. Click the icon or the card and it stays until you close it.
- **Island.** If you prefer, Aiko lives in a small window at the edge of the screen instead of the
  tray, with one ring per environment. It opens the card the same way the icon does. Drag it
  anywhere and it sticks to the nearest edge. It hides while a window is full screen.
- **First-run wizard.** It finds your Claude Code folders and shows the exact line and the exact
  files before it changes anything. It keeps a copy of every file it changes, and "Not now" is a
  valid answer. At the end it tells you where Aiko is and how to get the icon out from under the
  arrow.
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
- **Uninstall** puts your status line back and removes the startup entry and Aiko's folders. It
  deletes a Claude Code settings file only if Aiko created it and nothing else was added.
- **Aiko repairs its own line.** If Aiko is reinstalled in another folder or Git gets installed,
  Aiko fixes its line at startup. It never adds the line where you said no. If one of Aiko's
  settings files can't be read, Aiko keeps it as a `.bad` file and doesn't overwrite it.

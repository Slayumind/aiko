# Changelog

All notable changes to Aiko are written down here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

The first release. Everything below is new.

### Added

- **The tray icon.** A ring for one environment and a dot for the other, coloured by how much of
  each limit is used. The tooltip carries the numbers, so they are there even when Windows 11 hides
  the icon behind the overflow arrow. A left click opens the card; the right click menu shows the
  other environment in the ring, opens settings, refreshes, checks for updates and quits.
- **The card.** Session, week and model limits for every environment: how much is used, when each
  resets, and how long it lasts at the current pace once there is enough to go on. A limit that is
  running low says so in a word as well as a colour. A card opened by resting the mouse on the icon
  goes away with the mouse; click the icon or the card and it stays until you close it.
- **The island,** instead of the tray if you like: a small window at an edge of the screen with one
  ring per environment. Drag it anywhere and it sticks to the nearest edge. It steps aside while a
  window is full screen.
- **The first run wizard.** It finds your Claude Code folders, shows the exact line it would add and
  the exact files before asking, and keeps a copy of every file it changes. "Not now" is an answer.
  It ends by saying where Aiko is and how to bring the icon out from behind the overflow arrow.
- **Your own status line keeps working.** Aiko runs it through the same shell Claude Code uses and
  shows its output.
- **Settings:** environments, where to show Aiko, start with Windows, update checks and language.
  A check shows whether Claude Code still reports its limits to Aiko, and sets it up again if not.
  "Copy diagnostics" gives a short summary for a bug report.
- **Direct mode,** off by default and per environment, for the IDE panel and Claude Desktop, where
  Claude Code runs no status line. It says what it reads before you turn it on, reads the token for
  each request and never stores it.
- **Update checks,** off by default. The same request counts that one copy ran today, with an
  identifier that changes every day. One switch turns off both. PRIVACY.md says exactly what goes.
- **English and Russian.** Changing the language brings the window straight back in it.
- **Removing Aiko** puts your status line back, removes its startup entry and its own folders, and
  deletes a Claude Code settings file only when Aiko created it and it holds nothing else.
- **It looks after the files it touches.** If Aiko is reinstalled somewhere else or Git appears, its
  own line is put right at startup, never added where you said no. A settings file of Aiko's that
  cannot be read is kept as a .bad file instead of being overwritten.

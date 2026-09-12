# Changelog

All notable changes to Aiko are written down here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- The tray icon: a ring for one environment and a dot for the other, coloured by how much of the
  limit is gone. A left click swaps which environment the ring shows.
- The card: every environment with its session, week and model limits, the time until each resets
  and how long it lasts at the current pace. It opens when the mouse rests on the icon for 1.5
  seconds, and it closes by its cross, never on its own.
- The island: a small window at an edge of the screen with one ring per environment. It is dragged
  anywhere and sticks to the nearest edge, and it steps aside while a window is full screen.
- The settings window: environments, where to show Aiko, start with Windows, update checks,
  language, copy diagnostics and quit.
- The first run wizard: the environments Aiko found, the one line it would add to the Claude Code
  settings, and where to show Aiko. It shows the exact line and the exact files before asking, and
  keeps a copy of every file it changes.
- Direct mode, off by default and per environment: Aiko asks the usage API itself. It is for people
  who work in the IDE panel, where Claude Code runs no status line, and for the weekly limit of the
  heavy model.
- Update checks against slayumind.org, off by default, with a button to check by hand.

### Fixed

- The bridge now starts from an installed copy of Aiko. It was published without a runtime next to
  an app that carries its own, so it read the install folder as a .NET install, found no framework
  there and stopped before doing anything. No limit would ever have appeared from a real install,
  on any machine.
- Aiko knows its own status line again after the install moves or Git appears, instead of taking
  it for somebody else's and calling a path that is gone.
- The status line you already had runs in the shell it was written for. It went through cmd.exe
  before, which prints nothing for a bash line and says nothing about why.
- A settings file of Aiko's that cannot be read is kept as a .bad file instead of being read as no
  settings at all, which let the next save write over your choices.
- The verify command in SECURITY.md now names the file a release actually contains.

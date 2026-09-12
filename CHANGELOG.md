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
- The environment shown by the ring is remembered. The click used to swap it and forget by the next
  start.
- "Check for updates" in the tray menu does something. It was drawn, clicked and ignored.
- Aiko checks for a new version once a day when you have asked it to. The switch for that has been
  there from the start and nothing read it.
- The card no longer guesses how long a limit will last until there is something to go on. Two
  minutes into a five hour window, one percent used to read as "runs out in about three hours".
- Renaming an environment no longer leaves a second one on the card, frozen at the numbers it had.

### Changed

- The tray tooltip carries the numbers. Windows 11 hides a new icon behind the arrow, and under
  that arrow the card never opens, so for some people the tooltip is all there is.
- A left click on the tray icon opens the card. Swapping which environment the ring shows moved to
  the menu, where it says which environment it would show.
- A card opened by resting the mouse on the icon goes away when the mouse does. Click the icon, or
  click the card, and it stays until you close it.
- The card says "42% used" instead of "42%", and puts a word beside the colour when a limit is
  running low. Green and red are one grey to a good many people.
- "Copy diagnostics" is written for a person to read.
- The island carries the numbers in its tooltip.
- Direct mode says what it does before you turn it on: what it reads, what it never keeps, and to
  ask first if the account belongs to somebody else. It was labelled "direct" and nothing more.
- The first run ends with a screen instead of a window closing, and that screen says where Aiko is
  and how to drag it out from behind the Windows 11 overflow arrow.
- Settings can check whether Claude Code still reports its limits to Aiko, and set it up again.
  Saying "not now" in the wizard used to be a door with no handle on the other side.
- An environment Aiko cannot read says so, instead of telling you to open Claude Code when opening
  Claude Code will not help.
- Aiko counts how many copies run each day, if you let it. It is the same switch as the update
  check and the same request, it is off until you turn it on, and the identifier changes every day
  so two days cannot be joined into one person. PRIVACY.md says exactly what goes and what does
  not.
- Aiko speaks Russian. The language setting has existed since the first version and changed
  nothing; it now does, and the window comes straight back in the other language instead of asking
  for a restart.
- The weekly model limit shows the model the server named, instead of always saying Fable.

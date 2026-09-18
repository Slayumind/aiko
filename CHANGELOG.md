# Changelog

All notable changes to Aiko are listed here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Aiko for macOS.** The same app on the other system: the ring in the menu bar, the island on any
  edge of the screen, the card with the limits of both environments, the first run checklist and the
  settings. It reads the numbers from Claude Code the same way, keeps environments apart the same way
  and speaks the same two languages. macOS 14 or newer, Apple silicon and Intel.
- **Code signing policy.** [CODE-SIGNING.md](CODE-SIGNING.md) says which files will be signed through
  SignPath Foundation and who approves each release.
- **A macOS download.** One version tag now builds both systems and publishes one release. The macOS
  app comes as a disk image, signed with an Apple Developer ID and notarized by Apple, so it opens
  the normal way. [SECURITY.md](SECURITY.md) shows how to check it.

### Changed

- **The limits survive a fractional reset time.** A reset time with a fraction in it used to throw
  and take the whole status line report with it; it is now read down to the second.
- **PRIVACY.md** now also lists the `PATH` folder and the PowerShell profile change.
- **Every file in a release is attested**, not only the Windows installer.

## [0.2.3] - 2026-09-17

Two skills for your calendar and Drive, skills grouped by what they are for, and a personality that
stays herself.

### Added

- **The calendar and drive skills.** Claude plans your day around Google Calendar, finds free time and
  adds events, and brings documents from Google Drive into the work or puts project files onto Drive.
  Nothing is written, moved or shared without your yes. Both need the Google connectors turned on in
  Claude's settings.

### Changed

- **Skills are grouped by what they are for:** project management and game development, in settings
  and in the README.

### Fixed

- **Aiko talks about herself as a woman in every reply.** Short words like «готова» and «уверена»
  sometimes came out in the masculine.
- **Fewer game references, and better ones.** Aiko names a game only when the thing you work on
  really works like it, not as a joke at the end of every report.

## [0.2.2] - 2026-09-17

All skills become one plugin with one switch, and a new skill checks games with numbers.

### Added

- **The playtest skill.** It helps Claude test a game with numbers instead of a feeling: named test
  scenes, game state the tools can read, before and after tables, and short tests that use the real
  controls. It works with any engine and has notes for the web, Unity and Godot.
- **The commit in the version line.** Settings and the diagnostics text show the build as
  `0.2.2+42cb34b`, so two builds of one version can be told apart in a bug report.

### Changed

- **All skills are one plugin, `aiko`.** Claude Code used to list every skill twice, like
  `aiko-copy:aiko-copy`. Now it lists `aiko:copy`, `aiko:palette` and so on, and you call a skill by
  that full name. The settings page shows the full names.
- **One switch for all skills.** The skills come and go together, like the plugin in Claude Code.
  If you had switched off every skill, they stay off; if any skill was on, they are all on now.
- **Simpler texts for the texturing and playtest skills** in settings.

### Removed

- **A switch for each skill.** One switch for all of them took its place.
- **The eight one-skill plugins.** Aiko uninstalls them from every environment when it installs `aiko`.
- **The warning about an own skill with the same name.** The full name always reaches Aiko's skill,
  so there is nothing to warn about.

## [0.2.1] - 2026-09-16

Privacy gets its own page, and the count that was hidden behind the update switch gets its own
switch and its own question.

### Added

- **A Privacy page in settings.** It lists what leaves your computer field by field: the value, and
  what it is for. The list is there whether the count is on or off, because a list you only see
  after you agree is a list nobody read. **Reset ID** throws away the random value Aiko keeps, and
  **Open PRIVACY.md** goes to the full text.
- **A separate switch for the count.** Update checks and the count used to share one switch, so
  hearing about a new version meant being counted for it. Now they are two. It is still one request:
  with the count off, the identifier and the flags are not in it and the site writes nothing.
- **A question in the setup checklist.** The wizard asks about the count as its own step and will
  not finish without an answer. "Don't send" is an answer and it sticks.
- **A week and a month in the count.** Aiko remembers on your computer which week and month it has
  already reported and sends one flag for each, so the author can see how many copies come back
  without an ID that outlives a day. The weeks and months are calendar ones.
- **Whether the personality is on**, as a single yes or no, with the same daily ping.

### Changed

- The count now keeps its rows for 90 days and then deletes them. PRIVACY.md says so.
- Consent given in 0.2.0 is not carried over. It covered three values and this sends six, so Aiko
  asks again, once, and sends nothing until you answer.
- Diagnostics now reports whether statistics are on, next to whether update checks are.

### Fixed

- **The day is UTC on both sides.** Aiko hashed its identifier with the computer's own date while
  the site stored the row under its UTC date. East of Greenwich one copy could send two different
  identifiers inside one server day and be counted twice.
- **The personality no longer fades out during a working session.** The block that tells Aiko to go
  quiet in commits, plans and warnings was also switching off the rule that she is a woman, so she
  wrote about herself in the masculine in Russian. It also read as covering a whole day of work
  rather than the report in front of her, so the character never came back.

## [0.2.0] - 2026-09-15

Aiko gets a personality, eight skills and a face. All of it is off until you turn it on.

### Added

- **Personality.** Turn it on for an environment on the new Personality page, and new Claude Code
  sessions there talk as Aiko, an indie game developer who works next to you. Open sessions finish
  the way they started. Pick one of four temperaments, from Quiet to 無双; the page shows a sample
  answer for each. At every temperament, code, commits, files, pull requests, error explanations,
  security warnings, dangerous actions and bad news are written in a plain neutral voice. If you set
  your own output style, the page tells you that the personality's style wins while it is on. Tested
  with Claude Code 2.1.272.
- **Eight skills.** They come with the personality, each with its own switch:
  - `/aiko-copy`: text that reads as written by a person, for UI strings, READMEs, release notes and
    bad news, in Russian and English.
  - `/aiko-docs-hygiene`: a state file built from measured facts, an append-only decision log, and an
    audit of the documents for duplicates, contradictions and broken links.
  - `/aiko-release-gate`: a check before a release ships, for installers and web services alike: GO or
    NO-GO, irreversible changes, rollout order and rollback plan. It never pushes anything.
  - `/aiko-glb-for-web`: a Blender model as a `.glb` that web players show correctly, with an offline
    checker for size and extensions a player cannot decode.
  - `/aiko-blender-to-unity`: meshes from Blender into Unity with the right pose and handedness,
    blended normals and cut shared parts, with export scripts for Blender.
  - `/aiko-texturing`: texture sizes from the game camera instead of habit, a sheet or a tile, UVs
    from world position and guides for painters.
  - `/aiko-palette`: colours checked against a palette and fixed with the smallest change, in OKLCH,
    offline and without a key.
  - `/aiko-gamedesign-research`: one game mechanic studied across 30-40 games, as an illustrated
    review with sources, and on request small interactive stands to play with the systems.

  Aiko installs the personality and the skills as Claude Code plugins from a marketplace on your own
  computer, so nothing is downloaded. A new version of Aiko brings new versions of the skills by
  itself. Your own plugins and settings stay as they are.
- **Skills without Aiko.** `/plugin marketplace add Slayumind/aiko`, then
  `/plugin install aiko-copy@slayumind-aiko` or any other skill.
- **Aiko's face.** Seven faces, chibi or emoji. When a session starts working, waits for you,
  finishes, fails or runs out of limit, or when a limit crosses 90% or 100%, the face takes the place
  of the rings in the tray or on the island for two seconds, with a short animation. The island
  closes in around the face. The face follows a light or dark taskbar, and with animations turned off
  in Windows it switches at once. Nothing runs while nothing happens.
- **Meet Aiko.** An optional checklist item that tells what the personality does and turns it on in
  environment 1. If you set Aiko up before this version, the checklist opens on it once.
- **A warning about skills with the same name.** If you keep your own skill named like one of Aiko's,
  Claude Code gives the short name to yours. The Personality page says so under that skill.

### Changed

- **Removing Aiko takes its plugins with it.** Uninstalling Aiko, removing an environment and Start
  over take Aiko's plugins and marketplace out of Claude Code. Other plugins stay. The copy of
  `settings.json` is kept while anything of Aiko's is still in the file.
- **Privacy.** Where the personality is on, Claude Code tells Aiko when a session changes state. Aiko
  reads only the kind of event and the session ID, keeps the state and the time in a small local file
  with the ID hashed, and removes the file when the session ends. Nothing leaves your computer.
  [PRIVACY.md](PRIVACY.md) has the details.
- **The card in Russian** says «потрачено» instead of «истрачено». It is the everyday word.

### Fixed

- **Checksums.** `SHA256SUMS.txt` no longer lists the portable zip, which a release doesn't include,
  and it has plain LF line endings. In 0.1.0 both made `sha256sum -c` fail even though every file
  was fine. The file in the 0.1.0 release has been replaced.

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

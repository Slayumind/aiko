# Changelog

All notable changes to Aiko are listed here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Aiko uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Groundwork for Aiko's personality.** Aiko now keeps a persona file with the temperament, the face
  and the skills you switch off, and each environment remembers whether the persona is on. Older
  settings files read as before, with the persona off.
- **Aiko's persona text.** The character, the four temperaments and the rules about where she stays
  silent (code, commits, files, errors, warnings), packed as a Claude Code plugin with the hooks
  for her face. Not installed anywhere yet.
- **The bridge builds the persona plugin.** `Aiko.Bridge.exe plugin aiko-persona` writes the plugin
  for the environment it runs in under `%LOCALAPPDATA%\Aiko\plugins` and prints the folder, which is
  how Claude Code installs a plugin from a command. Old copies are cleared after an hour.
- **Plugins follow the persona switch.** Aiko keeps its own local plugin marketplace and, for every
  Claude Code folder, installs or turns off its own plugins to match whether the persona is on there.
  Other plugins and settings are never touched. With the persona off everywhere Aiko writes nothing.
- **Personality page in settings.** A switch for each environment turns Aiko's persona on, and a
  line warns when your own output style is set there, since the persona's style wins while it is on.
  One choice for all environments: the face (chibi or emoji), the temperament with a sample answer,
  and the list of Aiko's skills with a switch for each. The skills themselves come in later versions.
- **Aiko's plugins leave with Aiko.** Uninstalling Aiko, removing an environment and Start over take
  Aiko's plugins and marketplace out of `settings.json` first, so they stop loading at once. Then
  Aiko asks Claude Code to uninstall them, within 20 seconds when Aiko itself is being removed. Other
  plugins stay. The copy of `settings.json` is kept while anything of Aiko's is still in the file.
- **Meet Aiko in the checklist.** An optional item tells what the persona does and turns it on in
  environment 1 at Finish. If you set Aiko up before this version, the checklist opens on this item
  once. Running the checklist again keeps the persona as it was.
- **Session activity for Aiko's face.** Where the persona is on, its hooks tell the bridge when a
  session works, waits for you, finishes, fails or hits a rate limit. The bridge keeps one small
  file per session under `%LOCALAPPDATA%\Aiko\activity`, with the state and the time only, and
  removes it when the session ends. Files older than a day go at startup. The face itself comes later.
- **When Aiko shows a face.** With the persona on somewhere, Aiko turns each new session state and
  each limit crossing 90% or 100% (or dropping back after a reset) into a face for two seconds. A face
  that asks for you or reports an error is not pushed away by a calmer one. The drawing comes later.
- **Aiko's faces.** Seven faces in two styles, chibi and emoji, drawn as vectors for dark and light
  taskbars, with thicker lines at tray sizes. They show next to the Personality title, beside the
  sample answer and in the Meet Aiko item, and follow the face you pick. `--snapshot-faces` draws
  them all on one sheet.
- **Aiko's face in the tray.** Where the persona is on, the face takes the place of the rings for two
  seconds after an event: the rings shrink away, the face springs in, then fades and the rings come
  back, eight pictures per step. The face follows a light or dark taskbar. With animations turned
  off in Windows it switches at once. `--snapshot-icon out.png faces` draws every face and one whole
  transition.
- **Aiko's face on the island.** The island does the same as the tray icon, smoothly: its rings
  shrink away, the face springs in over them for two seconds, and the rings grow their arcs back.
  The island keeps its size. `--snapshot-island out.png Top face` draws it.
- **Aiko's skills.** Where the persona is on and a skill's switch is on, Aiko installs the skill; a
  new version of Aiko brings a new version of the skill on its own.
  - `/aiko-copy`: text that reads as written by a person, for UI strings, READMEs, release notes and
    bad news, in Russian and English.
  - `/aiko-docs-hygiene`: a state file rebuilt from measured facts, an append-only decision log, and
    an audit of the documents for duplicates, contradictions and broken links.
  - `/aiko-release-gate`: a check before a release ships, for installers and web services alike: GO or
    NO-GO, irreversible changes, rollout order and rollback plan. It never pushes anything.
  - `/aiko-glb-for-web`: a Blender model as a `.glb` that web players show correctly, with an offline
    checker for size and extensions a player cannot decode.
  - `/aiko-blender-to-unity`: meshes from Blender into Unity with the right pose and handedness,
    blended normals and cut shared parts, with export scripts for Blender.
  - `/aiko-texturing`: texture sizes from the game camera instead of habit, a sheet or a tile, UVs from
    world position and guides for painters, with a camera budget calculator and a density checker.
  - `/aiko-palette`: colours checked against a palette and fixed with the smallest change, in OKLCH,
    offline and without a key.
  - `/aiko-gamedesign-research`: one game mechanic studied across 30-40 games, as an illustrated review
    with sources, and on request small interactive stands to play with the systems.
- **A public marketplace in the repository.** Aiko's skills also install without the app:
  `/plugin marketplace add Slayumind/aiko`.

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

# Parity: Aiko.Core (C#) and AikoKit (Swift)

The macOS app repeats the Windows core, so every ported file keeps its behaviour and every xUnit
case has a Swift case with the same name and the same numbers. Test counts below are cases, not
methods: one `[Theory]` with five `[InlineData]` rows counts as five, and so does the Swift
`@Test(arguments:)` that mirrors it. `dotnet test` prints cases and says 736; `swift test` prints
methods, not cases, and says 596. The 49 the Swift side does not have are named at the bottom; the ones it has and Windows
does not belong to the two small programs, whose decisions sit inline in `Program.cs` on Windows,
and to the card words, the island and the settings window, which sit in `Aiko.App` on Windows
where no test can reach them.

Rules that are a table of inputs and answers live in `spec/cases/` and are read by both suites, so
a row cannot change in one core and stay as it was in the other. See `spec/cases/README.md`.

## Ported

| C# file | Swift file | C# cases | Swift cases | Notes |
|---|---|---|---|---|
| JsonText.cs | JsonText.swift | 15 | 15 | |
| LimitSnapshot.cs | LimitSnapshot.swift | 8 | 8 | |
| StatusLineReport.cs | StatusLineReport.swift | 14 | 14 | |
| UsageReport.cs | UsageReport.swift | 10 | 10 | |
| ResetCountdown.cs | ResetCountdown.swift | 7 | 7 | |
| PollBackoff.cs | PollBackoff.swift | 9 | 9 | |
| CardState.cs | CardState.swift | 25 | 25 | CardStateTests (17) and PaceEstimateTests (8) |
| UpdateInfo.cs | UpdateInfo.swift | 12 | 12 | `System.Version` is ported as `VersionNumber` |
| AllowedHosts.cs | AllowedHosts.swift | 19 | 19 | the source scan looks for `URLSession` outside `AikoHttp.swift` instead of `new HttpClient` |
| UpdateManifest.cs | UpdateManifest.swift | 38 | 38 | CryptoKit in place of `ECDsa`; both read the openssl vector in `spec/cases/update-manifest/` |
| UninstallPlan.cs | UninstallPlan.swift | 26 | 26 | the two folders Aiko may delete, and the guard that refuses every other path |
| UpdateGate.cs, UpdateKey.cs | UpdateGate.swift, UpdateKey.swift | 15 | 15 | the key is one file, `update-public-key.pem`: Windows embeds it, `tools/update-key.py` writes the Swift copy, and a case in each suite fails if the two drift apart |
| CubicBezier.cs | CubicBezier.swift | 9 | 9 | |
| IslandReveal.cs | IslandReveal.swift | 8 | 8 | |
| IslandPlacement.cs | IslandPlacement.swift | 18 | 18 | |
| TrayFaceMotion.cs | TrayFaceMotion.swift | 7 | 7 | |
| TrayMood.cs | TrayMood.swift | 6 | 6 | which face an event brings is in `spec/cases/tray-mood/`; `hasFace` takes the environments, as on Windows |
| SessionActivity.cs | SessionActivity.swift | 6 | 6 | what a hook means and what a broken record reads as are in `spec/cases/hook-events/` |
| Heartbeat.cs | Heartbeat.swift | 17 | 17 | |
| SessionReminder.cs | SessionReminder.swift | 18 | 20 | its other 6 C# cases are about the hook in settings.json and sit in SettingsJsonPatchTests; 8 Swift cases of its own read the system language from a tag (MacLanguageTests) |
| PersonaSettings.cs | PersonaSettings.swift | 15 | 15 | |
| PersonaPrompt.cs | PersonaPrompt.swift, PersonaPlugin.swift | 33 | 33 | the prompt text and the plugin files hash the same in both languages |
| PersonaPluginOutput.cs | PersonaPluginOutput.swift | 7 | 7 | |
| SkillCatalog.cs | SkillCatalog.swift | 3 | 3 | |
| SkillPlugin.cs | SkillPlugin.swift | 17 | 17 | |
| ClaudeAccount.cs | ClaudeAccount.swift | 16 | 16 | 2 of them are about ClaudeConfigFolder, as in the xUnit file |
| FaceArt.cs | FaceArt.swift | 63 | 63 | every one of the 56 pictures hashes the same in both languages |
| SettingsJsonPatch.cs | SettingsJsonPatch.swift | 20 | 26 | the 6 extra come from SessionReminderTests; a method answers "nothing to change" with nil instead of a bool and an out parameter |
| ClaudeSettingsEditor.cs, IFileAccess.cs | ClaudeSettingsEditor.swift | 38 | 38 | ClaudeSettingsEditorTests (19) and PluginRemovalTests (19); `FileAccess` is IFileAccess with Swift errors in place of exceptions |
| PluginPlan.cs, PluginReconciler.cs | PluginPlan.swift, PluginReconciler.swift | 33 | 33 | the reconciler's clock comes in as a function instead of TimeProvider |
| PlatformConventions.cs | PlatformConventions.swift | 2 | 2 | what each system names and where it puts things moved to `spec/cases/platform-paths/`, three systems side by side; what is left needs a settings object or this computer |
| ClaudeShell.cs | PlatformConventions.swift | 4 | 4 | the shell of each system is in `spec/cases/platform-paths/`; the 8 cases left over are about WindowsGitBash |
| AikoFolders.cs | AikoFolders.swift | 5 | 5 | both layouts, the trailing separator included, are pinned in `spec/cases/folder-layout/` |
| SnapshotName.cs | SnapshotName.swift | — | — | every case is in `spec/cases/snapshot-name/`, read by both suites |
| SnapshotFile.cs | SnapshotFile.swift | 9 | 9 | |
| EnvironmentSnapshots.cs | EnvironmentSnapshots.swift | 6 | 6 | |
| EnvironmentSettings.cs, LaunchCommand.cs, ProjectBinding.cs | EnvironmentSettings.swift, LaunchCommand.swift, ProjectBinding.swift | 26 | 26 | EnvironmentSettingsTests (13) and EnvironmentModelTests (13); the command of an environment is in `spec/cases/launch-command/` |
| EnvironmentEdits.cs | EnvironmentEdits.swift | 24 | 24 | |
| EnvironmentScan.cs | EnvironmentScan.swift | 12 | 12 | |
| ClaudeConfigFolder.cs | ClaudeConfigFolder.swift | 7 | 7 | |
| ClaudeInstall.cs | ClaudeInstall.swift | 7 | 7 | |
| ShimLaunch.cs, RealClaude.cs, UserPathList.cs | ShimLaunch.swift, RealClaude.swift, UserPathList.swift | 18 | 16 | the other 6 C# cases are about PowerShellProfile; 4 Swift cases of their own cover what the shim sets in the environment of Claude Code, which Aiko.Shim does inline |
| BridgeCommand.cs | BridgeCommand.swift | 29 | 29 | BridgeCommandTests (11) and BridgeRecognitionTests (18) |
| CommandLinks.cs | CommandLinks.swift | 3 | 3 | |
| AikoMarketplace.cs | AikoMarketplace.swift | — | — | its cases sit in PluginPlanTests and SkillPluginTests |
| AppSettings.cs | AppSettings.swift | 15 | 15 | |
| WizardChecklist.cs | WizardChecklist.swift | 13 | 13 | |
| CredentialFile.cs | CredentialFile.swift | 10 | 10 | `TryParse` with an out parameter is `parse`, which answers nil |
| — | SharedCaseTests.swift | 7 | 7 | the case files under `spec/cases/`, read by both suites |
| — | UpdateInstaller.swift | — | 6 | the macOS half of an update: the zip, `ditto` and the bundle swap. Windows has Velopack instead, and UpdateInstall.cs holds the same steps around it where no xUnit case can reach them |

The words and the numbers of the card sit in `src/Aiko.App` on Windows, where no xUnit case can
reach them. In Swift they are part of AikoKit and have cases of their own:

| C# file | Swift file | C# cases | Swift cases | Notes |
|---|---|---|---|---|
| Strings.resx, Strings.ru.resx | Strings.swift | — | 3 | both are made by `tools/strings.py` from `tools/strings.json`, so a text is written once |
| Card/CardText.cs | CardText.swift | — | 20 | `clock` takes a time zone, where `$"{updatedAt:HH:mm}"` takes the machine's |
| Card/CardModel.cs | CardModel.swift | — | 10 | brushes and `Visibility` become the tone and a plain "shown or not": AppKit and WPF spell those differently |
| TrayText.cs | TrayText.swift | — | 6 | the 127 character limit is Windows', and one text for both is one text to translate |
| RingIcon.cs, RingDrawing.cs | RingArt.swift | — | 13 | the 16 unit grid, the tones and the dashed ring as numbers; the drawing itself is `AikoMac/StatusIcon.swift` |
| Theme/Motion.cs | Motion.swift, CardLifetime.swift | — | 6 | the durations and the curves, plus the hover delay and the two away turns that AikoShell.cs keeps inline |
| Island/IslandPanel.xaml.cs, Island/UnfoldPanel.cs, Island/FitPanel.cs | IslandLayout.swift | — | 28 | the size of the island, its line and corners per edge, the padding that keeps the rings still when the docked side loses its line, where every ring and percentage sits, and the 34 point square it closes into around the face |
| Island/IslandWindow.xaml.cs | IslandDrag.swift | — | 7 | the 3 point threshold and the grip kept as a share of the size |
| Island/RingGauge.cs | RingArt.swift | — | 2 | the island ring: the pen is 18 % of the ring, as RingGauge draws it |
| Settings/SettingsPanel.xaml(.cs) | SettingsNav.swift | — | 10 | the page keys, the order of the menu, the small line under each title, and the body height a screen allows |
| Settings/ChecklistPage.xaml(.cs) | ChecklistLayout.swift | — | 8 | the reading order of the nine items, the three group labels and the word beside each header |
| GeneralPage.xaml.cs (OnCopyDiagnostics) | DiagnosticsText.swift | — | 2 | the lines of the bug report, English in every language as on Windows |

Helpers with no file of their own in the C# core:

| Swift file | Why it exists |
|---|---|
| BridgeWork.swift | what one run of the bridge does: which job it was started for, the snapshot to write, the activity file of a session, the reminder at session start, the persona plugin folder. On Windows the same choices sit inline in `Aiko.Bridge/Program.cs`, where no test can reach them (29 cases in BridgeWorkTests) |
| JsonNode.swift | System.Text.Json keeps the order of keys and the text of numbers when it writes a file back; `JSONSerialization` keeps neither, and Aiko rewrites files that belong to Claude Code |
| CivilTime.swift | `DateOnly`, ISO weeks and the round-trip ("O") time format |
| SvgPath.swift | reads the SVG path data FaceArt writes. WPF parses it itself with `Geometry.Parse`; AppKit has nothing of the kind, so the core reads it and hands the drawing layer moves, lines and cubic curves. Arcs and quadratic curves become cubics here (15 cases in SvgPathTests, one of which reads every shape of all 56 faces) |
| SignedIn.swift | whether a folder has an account. Windows reads `.credentials.json`; macOS keeps the same token in the Keychain, so that file is never written and the answer comes from the account block of `.claude.json` (3 cases in SignedInTests) |
| MacShell.swift | two things Windows has a system service for. `ZshProfile` writes Aiko's marked block into `~/.zshrc`, because a folder under the home directory reaches a shell's PATH only through the shell profile; `MacTerminal` writes the script that opens Claude Code in a Terminal window (11 cases in ZshProfileTests and MacTerminalTests) |
| `PathText` in PlatformConventions.swift | reading a path without asking the machine it runs on: `Path.GetFileName` and `Path.TrimEndingDirectorySeparator` answer differently on Windows and on macOS, and Aiko reads paths written for the other system all the time |

## The two programs

`Sources/aiko-bridge` and `Sources/aiko-shim` are the Swift twins of `src/Aiko.Bridge` and
`src/Aiko.Shim`. They hold no rules: every choice comes from AikoKit, and what is left is reading
stdin, touching the disk and starting a process.

| Job | Windows | macOS | Where the rule lives |
|---|---|---|---|
| status line | `Aiko.Bridge.exe` | `aiko-bridge` | `BridgeWork.statusLine`, `StatusLineReport`, `SnapshotFile` |
| the line the user already had | `RunWrapped` | `WrappedStatusLine` | `ClaudeShellLookup.callFor`, `SettingsJsonPatch.readWrappedCommand` |
| session events behind the face | `hook` | the same | `BridgeWork.activity`, `HookEvent`, `ActivityRecord` |
| the reminder at session start | `--session-start` | the same | `BridgeWork.sessionStart`, `SessionReminder` |
| the persona plugin | `plugin aiko-persona` | the same | `BridgeWork.personaPlugin`, `PersonaPlugin`, `PersonaPluginOutput` |
| which environment a start belongs to | `claude.exe` | `aiko-shim` | `ShimLaunch.decide`, `ShimPlan.variableChanges` |
| never start another shim | `AIKO_SHIM_SEEN` | the same | `RealClaude` |

Where they differ from the Windows pair:

- **The file names.** SwiftPM builds `aiko-bridge` and `aiko-shim`. The app bundle has to ship the
  bridge as `Aiko.Bridge`, because that is the program name `BridgeCommand.isAiko` reads a status
  line by, and the shim as `claude` plus a copy or a link per command name. Nothing in the two
  targets depends on the name they were built under: the shim reads the name it was called by.
- **The shell.** The wrapped status line runs through `/bin/zsh -lc`; Git Bash and PowerShell are
  Windows ideas. A command that hangs is given the same two seconds and then killed — only the
  command itself, not the tree below it, because the shell shares the bridge's process group and
  killing the group would kill the bridge. Windows kills the whole tree.
- **Claude Code missing.** The shim answers 127, which is what a shell answers for a command it
  cannot find; the Windows shim answers 9009, which is cmd's number for the same thing. Whether a
  file in PATH is Claude Code is asked with `isExecutableFile` instead of `File.Exists`.
- **Claude Code replaces the shim** with `execve`, where the Windows shim starts a child and waits.
  A child of a Foundation `Process` gets a process group of its own, and a process outside the
  terminal's foreground group is stopped by SIGTTIN as soon as it reads the keyboard, so the
  session never started. After exec there is one process: the terminal, the exit code and Ctrl+C
  are Claude Code's own, and nothing has to be handed back — which is why there is no
  `Console.CancelKeyPress` twin and no "128 plus the signal" here.
- **The language of the reminder.** macOS says its language as a tag (`ru-RU`), Windows as a
  number, so `SessionReminder.isRussian` has an overload for each.
- **Proof that they start.** The shim keeps `AIKO_SHIM_SELF_TEST=1`. The bridge has no such
  variable on either system: fed `{}` it writes nothing, prints nothing and exits 0, and CI runs
  exactly that.

## The app

`Sources/AikoMac` is the Swift twin of `src/Aiko.App`. It holds no rules either: it draws what
AikoKit decided, and it answers the mouse.

| Windows file | macOS file | What it does |
|---|---|---|
| Program.cs, App.xaml.cs | main.swift, AikoDelegate.swift | starts without a Dock icon, picks the language, registers the fonts |
| AikoShell.cs | AikoShell.swift | the status item, the hover, the menu and the life of the card |
| RingIcon.cs | StatusIcon.swift | draws the ring and the dot into an `NSImage` |
| Theme/Tokens.xaml | Theme.swift | the palette and the sizes, as SwiftUI values |
| Card/CardPanel.xaml | CardView.swift | the card: header, blocks, rows, bars |
| Card/CardWindow.xaml.cs | CardWindow.swift | the panel it lives in, where it is placed and how it arrives |
| SnapshotWatcher.cs | SnapshotWatch.swift | watches the snapshots folder; a `DispatchSource` in place of `FileSystemWatcher` |
| Island/IslandWindow.xaml.cs | IslandWindow.swift | the island's window: where it goes, the hover, the drag and the landing |
| Island/IslandPanel.xaml(.cs), RingGauge.cs, UnfoldPanel.cs, FitPanel.cs | IslandView.swift | draws the island: the chrome, the rings, the percentages and the face |
| Island/IslandGhost.cs | IslandStrip.swift | the landing strip, `NSVisualEffectView` in place of the acrylic accent |
| Island/IslandCheck.cs | IslandCheck.swift | the self test doors: `--try-island`, `--try-drag`, `--try-face`, `--try-card-island` |
| Island/IslandFaceAnimator.cs, Tray/TrayFaceAnimator.cs | FacePlay.swift | the steps between the rings and a face, once for both surfaces |
| Faces/FaceDrawing.cs | FacePaint.swift | turns the shapes of FaceArt into AppKit drawing |
| RingDrawing.cs | RingPaint.swift | one arc, drawn the same by the menu bar icon and by the island |
| Tray/TrayMoodPlayer.cs | MoodPlay.swift | which face an event brings, and the one short timer |
| Tray/ActivityWatcher.cs | ActivityWatch.swift | watches the session files the bridge writes |
| FullScreenWatch.cs | FullScreenWatch.swift | says when a window owns the screen; macOS has no foreground event, so it looks at what is in front when the space or the front app changes |
| — | Screens.swift | AppKit counts screen points from the bottom left, the core from the top left: the turn happens here and nowhere else |
| — | Tween.swift | one short animation on a timer that exists only while something moves. WPF has `CompositionTarget.Rendering`; AppKit cannot animate a window that changes its whole shape every frame |
| TaskbarOrder.cs | — | window levels do the same work: the island sits at `.statusBar` and the landing strip at `.floating`, under both the island and the menu bar |
| SettingsStore.cs, ClaudeAccounts.cs | Store.swift | reads the settings, the environments and the plan of an account |
| Log.cs | Log.swift | the same file, the same line format |
| Uninstall.cs | Uninstall.swift | putting everything back on the way out. Windows is called by the installer's hook and then removes the program with `Update.exe uninstall`; macOS has no uninstaller, so the app does the work itself, moves its own bundle to the Bin and quits |
| Settings/SettingsWindow.xaml(.cs), SettingsPanel.xaml | Settings/SettingsWindow.swift | the borderless window, the header, the menu and the page area; Esc closes it |
| Settings/SavedMark.cs, Theme/Controls.xaml, Theme/DropDown.cs, Theme/RevealPanel.cs | Settings/SettingsControls.swift | the switch, the segments, the picked card, the fields, the buttons, the tick, the chip, the drop down and the "Saved" mark |
| Settings/EnvironmentsEditor.cs | Settings/SettingsState.swift | what the window is looking at, and every change written through one place |
| Settings/EnvironmentPage.xaml(.cs) | Settings/EnvironmentPageView.swift | one environment: the account, the name, the command, the bound folders and removal |
| Settings/FoldersPage.xaml(.cs) | Settings/FoldersPageView.swift | every bound folder in one table |
| Settings/PersonalityPage.xaml(.cs) | Settings/PersonalityPageView.swift | where Aiko talks, the face, the temperament, the sample answer and the skills |
| Settings/PrivacyPage.xaml(.cs) | Settings/PrivacyPageView.swift | the two consents and the list of fields that would be sent |
| Settings/GeneralPage.xaml(.cs) | Settings/GeneralPageView.swift | where Aiko shows, startup, the update check and the install, the language, access, diagnostics, starting over |
| Settings/ChecklistPage.xaml(.cs), ChecklistRow.cs | Settings/ChecklistPageView.swift, ChecklistModel.swift | the nine items, their bodies and Finish |
| Settings/SettingsCheck.cs | Settings/SettingsCheck.swift | the self test doors `--try-settings` and `--try-wizard` |
| Commands/ClaudeLauncher.cs, ClaudeFolders.cs | Commands/ClaudeLauncher.swift | finds Claude Code, opens it, waits for a sign-in, lists the config folders |
| Commands/CommandFolder.cs | Commands/CommandFolder.swift | the shim, the command links and the block in `~/.zshrc` |
| Commands/EnvironmentSetup.cs | Commands/EnvironmentSetup.swift | puts a saved list of environments into effect outside Aiko |
| Plugins/PluginSync.cs, ClaudeCli.cs, SkillShelf.cs | Plugins/PluginSync.swift | the marketplace, the skills copy and the claude commands that install the plugins |
| ClaudeSettingsFile.cs, BridgePath.cs | ClaudeSettingsFile.swift | Aiko's line in somebody else's settings.json, and where the bridge is |
| AikoHttp.cs, UpdateClient.cs, UpdateRun.cs, InstallId.cs, ReportedPeriods.cs | AikoHttp.swift | the one place that makes a URLSession, the version check and the daily count; both check the host of every redirect before following it |
| UpdateInstall.cs | UpdateInstall.swift | downloads an update and installs it once it passes. Windows wraps Velopack's `UpdateManager`; macOS hands the release folder, the key and this bundle to `UpdateInstaller` in the core |
| AppVersion.cs, Startup.cs | AppVersion.swift | the version from Info.plist, and the login item in place of the Run key |
| Faces/FaceDrawing.cs | FaceImage.swift | one face as a picture, for the settings window |

`Scripts/make-app.sh` builds `build/Aiko.app`: one universal binary, the bridge inside as
`Aiko.Bridge`, the shim as `claude`, an ad-hoc signature.

Where it differs from Windows:

- **The card always hangs under the status item.** The Windows taskbar can sit on any edge, so the
  card asks whether there is room above. The macOS menu bar is always at the top. From the island
  the card still opens above when the island sits on the bottom edge.
- **The gear and the cross** are system symbols. The Windows build carries the gear of the user's
  own icon set as a path in Tokens.xaml.
- **The face is drawn, not handed over.** Windows gives the tray a picture, so a transition there is
  eight pictures per step; macOS draws the menu bar icon itself, so the island and the icon play the
  same smooth steps from one `FacePlay`.
- **Proof that it starts.** `AIKO_MAC_SELF_TEST=1` opens the card, writes what it says and what
  size it came out, and quits after three seconds. The island has its own doors, the twins of
  `--try-island` and friends: `--try-island` unfolds and folds it, `--try-drag` drags it to the left
  edge with the landing strip, `--try-face` plays a face, `--try-card-island` opens the card from
  both a top and a bottom island. The Windows app has `--snapshot` as well, which saves a picture;
  over SSH a picture shows only the wallpaper, so nothing here draws one.

- **The settings window has two doors of its own.** `--try-settings <page>` opens it at one page
  (`general`, `folders`, `personality`, `privacy`, `setup`, `env1`, `env2`) and `--try-wizard
  <item>` at one checklist item. Both write down the size, the menu and the state of every item,
  and quit after `AIKO_TRY_SECONDS`. `AIKO_TRY_GROW=1` makes the window as tall as the page, the
  way `SettingsPanel.GrowToPage` does for `--snapshot-settings` on Windows, and `=end` keeps its
  bottom on screen when the page is taller than the screen itself. Neither door saves anything.

## What macOS does differently in the settings window

These are the places where there was nothing to copy, with the reason for the choice.

- **The launch commands reach PATH through `~/.zshrc`.** Windows writes the user PATH in the
  registry and tells Explorer about it, and every new process sees it. macOS has no such place:
  `path_helper` reads `/etc/paths` and `/etc/paths.d`, which are the system's, so a folder under the
  home directory reaches a shell only if the shell's own profile puts it there (spike S4). Aiko
  writes one block into `~/.zshrc`, marked at both ends so it can be found and taken out whole:

      # Aiko: launch commands
      export PATH='/Users/someone/Library/Caches/Aiko/bin':$PATH
      # end Aiko

  The checklist asks before it is written and shows exactly that line and exactly that file, the
  same way the item above it shows the line it would add to `settings.json`. A copy of the profile
  stays beside it, and removal puts the file back: `ZshProfile.remove(ZshProfile.add(text))` is the
  text it started from, which has a case of its own and was also checked against a real 14 line
  `~/.zshrc`. Terminals that are already open keep the PATH they started with, as on Windows.

  The command folder itself is `~/Library/Caches/Aiko/bin`, where the shared layout puts it (D-250).
  macOS may empty Caches; "Add to PATH" in the checklist builds the folder again, and the block in
  the profile costs nothing while the folder is missing.

- **The marketplace and the persona plugins are under `~/Library/Caches/Aiko` too**, for the same
  reason: they are what Aiko makes for itself and can be made again. The skills plugin ships inside
  the bundle at `Contents/Resources/plugins/aiko` and is copied into the marketplace folder only
  when a file of it changed, as `SkillShelf` does on Windows.

- **"Open Claude Code" writes a script and lets Terminal run it.** Windows starts `claude.exe` and
  the system gives it a console. A GUI app on macOS gets no terminal at all, and telling Terminal
  what to run by AppleScript needs the Automation permission and a prompt nobody asked for. So Aiko
  writes `open-claude.command` into its own folder — `cd`, the config folder variable, `exec claude`
  — and hands it to Terminal with `open`. That needs no extra permission, and the file is written
  again every time and safe to delete. The button sits on the card, where Windows keeps it, and not
  on the environment page: in Russian two buttons do not fit that column.

- **"Signed in" is read from `.claude.json`, not from a file that is never written.** Claude Code on
  macOS keeps its token in the Keychain, so `.credentials.json` is not there even for a folder in
  daily use (checked 2026-09-18 on macOS 15.7, against two folders whose accounts were signed in).
  Aiko will not read the Keychain — that would be storing somebody else's token by another name —
  so the answer comes from the account block Aiko already reads for the plan. `SignedIn` holds the
  rule for both systems. It is a weaker signal: a folder whose token has expired still names its
  account. That is the right way round here, because a checklist that could never say "connected"
  would stop somebody setting Aiko up at all, which is what the first live run on a Mac ran into.

- **There is no updater.** Velopack is Windows only, so the general page shows the version and
  "Check now" and nothing else; an available version opens the download page. The daily check, the
  two consent switches and the count are the same as on Windows.

- **Starting at login** is `SMAppService.mainApp` in place of the Run key. It needs a real bundle:
  a binary run straight out of `.build` cannot register, and then the switch says off and saying
  yes only writes a line in the log.

- **A zsh function that shadows a command is left alone.** Windows offers to take a `function claude`
  out of the PowerShell profile (D-167), because that function is usually something a tutorial told
  the person to add. On macOS the shim is on PATH and a shell function that hides it is the person's
  own choice; the checklist has no such item, and `PowerShellProfile.cs` stays unported.

Not there yet: direct mode and signing for release.

## Names that had to change

`for` and `default` are keywords in Swift. `SnapshotName.For` is `forConfigDirectory`,
`BridgeCommand.For` is `forPath`, `IslandReveal.For` is `showFor`, and
`EnvironmentSettings.Default(userProfile)` is `defaultEnvironmentIn(_:_:)`.

`Binding` is `FolderBinding`. SwiftUI has a `Binding` of its own, and a module used beside it must
not export that name: every `@Binding` in the app became ambiguous the day the settings window
arrived.

## Where the two cores take different arguments

The Windows core builds paths with `Path.Combine` and `Path.TrimEndingDirectorySeparator`, which
follow the machine the code runs on. That is enough for a program that ships on Windows only. The
Swift core has to spell a Windows path and a Mac path alike, so these take a `PlatformConventions`
where the C# ones do not: `ClaudeConfigFolder`, `ClaudeInstall`, `ProjectBinding`, `ShimLaunch`,
`SettingsJsonPatch`, `SessionReminder.messageFor`,
`EnvironmentSettings.first/second/extras/defaultEnvironmentIn` and
`EnvironmentEdits.forNewBinding/canRemove/remove`. Passing `.windows` reproduces every xUnit case
exactly, which is what the Swift tests do; the Mac app passes `.macOS`.

`PlatformConventions`, `AikoFolders`, `AikoMarketplace`, `CommandLinks`, `RealClaude`,
`UserPathList` and `BridgeCommand.isAiko` take the platform in both cores.

## Differences we could not avoid

- **Time.** `DateTimeOffset` counts in 100 ns ticks; Swift `Date` counts in seconds as a double.
  Every value the tests use is exact in both. `ResetCountdown` and the pace estimate cut at the
  same tick as `TimeSpan.FromSeconds` does.
- **A reset time with a fraction** is read down to the second in both cores now, with the same test
  case. Before the fix `JsonElement.GetInt64` threw out of `StatusLineReport.FromJson` on Windows and
  the whole status line was lost, while Swift returned an empty report.
- **A repeated key.** `JsonDocument` reads the last one; `JsonNode` throws. Swift always reads the
  last one.
- **A lone surrogate** (`"\ud800"`) makes the Windows parser throw `InvalidOperationException`;
  Swift reads it as broken JSON, so the file counts as "no data".
- **Escaping.** The writer repeats both encoders of System.Text.Json: the relaxed one (letters of
  every language stay readable, control, separator, private use and unassigned characters, the
  byte order mark and everything above the basic plane are escaped) and the strict default one
  (everything but printable ASCII, plus the HTML characters). Checked against .NET 10 output for
  ASCII, Russian, Japanese, control characters, the byte order mark and an emoji. The strict
  encoder also escapes the plus of a time offset, so both cores write
  `"2026-09-12T12:00:00+00:00"` into a snapshot file; a case in each suite pins it.
- **The end of a file.** The Windows core writes `Environment.NewLine`, which is `\r\n` there.
  The Swift core writes `\n`, as .NET does on macOS. Apart from those line ends, the files both
  cores write are the same byte for byte: the persona manifest, hooks.json, persona.json, the
  versioned plugin manifest, the activity file and the hook answer were compared by SHA-256
  against .NET 10 on 2026-09-17, and so were all 56 face pictures and every persona prompt.
- **SHA-256** comes from CryptoKit. Foundation has no hash of its own, and a hand written one
  would be a second thing to trust.
- **Reading a time.** `DateTimeOffset.TryParse` reads many shapes, including ones that depend on
  the machine's language. Swift reads ISO 8601 only: a date, a time, an optional fraction and an
  optional offset. Everything Aiko itself writes is in that shape.
- **An enum in a file.** `Enum.TryParse` also accepts a number that no member has, and a list
  separated by commas. Swift takes the names and the numbers that exist, and nothing else.
- **Sorting text.** `StringComparer.OrdinalIgnoreCase` compares code unit by code unit; Swift's
  `caseInsensitiveCompare` folds case the Unicode way. The lists these rules sort — folders,
  commands, environment names — agree on everything the tests hold. Only a pair that differs by a
  letter outside ASCII could come out in another order.

## Not ported, on purpose

Together these are the 49 cases the Swift suite does not have, less the one case it has and the
Windows suite does not (the folders `FileManager` hands this Mac).

- **PowerShellProfile.cs** — 6 cases in ShimTests. It reads and rewrites a PowerShell profile so
  that a `function claude` in it stops fighting the shim. macOS has no such file, and a zsh
  function is not the same problem: the shim sits in PATH, and a shell function that shadows it is
  the person's own choice.
- **WindowsGitBash.cs** — 8 cases in ClaudeShellLookupTests. Git Bash only exists on Windows. On
  macOS `ClaudeShellLookup` is always asked with no path, so it always answers with the fallback
  shell of the platform, `/bin/zsh -lc`.
- **UpdateManifest.cs** — 36 cases. Checking the signature of a release needs a key API of its own,
  `Security` on macOS against `System.Security.Cryptography` on Windows, and the Mac app has no
  updater yet. The openssl vector in `spec/cases/update-manifest/` is ready for whoever writes it.

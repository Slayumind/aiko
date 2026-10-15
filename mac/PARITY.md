# Parity: Aiko.Core (C#) and AikoKit (Swift)

The macOS app repeats the Windows core, so every ported file keeps its behaviour and every xUnit
case has a Swift case with the same name and the same numbers. Test counts below are cases, not
methods: one `[Theory]` with five `[InlineData]` rows counts as five, and so does the Swift
`@Test(arguments:)` that mirrors it. `dotnet test` prints cases and says 1097; `swift test` prints
methods, not cases, and says 811. The 14 the Swift side does not have are named at the bottom; the ones it has and Windows
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
| UsageReport.cs | UsageReport.swift | 11 | 11 | |
| ResetCountdown.cs | ResetCountdown.swift | 7 | 7 | |
| PollBackoff.cs | PollBackoff.swift | 9 | 9 | |
| CardState.cs | CardState.swift | 25 | 25 | CardStateTests (17) and PaceEstimateTests (8) |
| UpdateInfo.cs | UpdateInfo.swift | 12 | 12 | `System.Version` is ported as `VersionNumber` |
| UpdateBlock.cs | UpdateBlock.swift | 17 | 17 | the one action of each stage of the update line, what an answer of the site means for it, and the percent kept inside 0 to 100 |
| AllowedHosts.cs | AllowedHosts.swift | 19 | 19 | the source scan looks for `URLSession` outside `AikoHttp.swift` instead of `new HttpClient` |
| UpdateManifest.cs | UpdateManifest.swift | 38 | 38 | CryptoKit in place of `ECDsa`; both read the openssl vector in `spec/cases/update-manifest/` |
| UninstallPlan.cs | UninstallPlan.swift | 89 | 89 | the two folders Aiko may delete, the guard that refuses every other path, and the memory file that may stay: when it is worth keeping, that it stays when nobody could be asked, and the guard for emptying the folder around it |
| UpdateGate.cs, UpdateKey.cs | UpdateGate.swift, UpdateKey.swift | 15 | 15 | the key is one file, `update-public-key.pem`: Windows embeds it, `tools/update-key.py` writes the Swift copy, and a case in each suite fails if the two drift apart |
| CubicBezier.cs | CubicBezier.swift | 9 | 9 | |
| IconRows.cs | IconRows.swift | 7 | 7 | which environment the ring shows and which the dot; both shells kept this rule inline before |
| IslandReveal.cs | IslandReveal.swift | 8 | 8 | |
| IslandPlacement.cs | IslandPlacement.swift | 18 | 18 | |
| TrayFaceMotion.cs | TrayFaceMotion.swift | 7 | 7 | |
| TrayMood.cs | TrayMood.swift | 6 | 6 | which face an event brings is in `spec/cases/tray-mood/`; `hasFace` takes the environments, as on Windows |
| SessionActivity.cs | SessionActivity.swift | 6 | 6 | what a hook means and what a broken record reads as are in `spec/cases/hook-events/` |
| Heartbeat.cs | Heartbeat.swift | 17 | 17 | |
| SessionReminder.cs | SessionReminder.swift | 19 | 21 | its other 6 C# cases are about the hook in settings.json and sit in SettingsJsonPatchTests; 8 Swift cases of its own read the system language from a tag (MacLanguageTests) |
| PersonaSettings.cs | PersonaSettings.swift | 10 | 10 | |
| PersonaPrompt.cs | PersonaPrompt.swift, PersonaPlugin.swift | 33 | 33 | the prompt text and the plugin files hash the same in both languages |
| PersonaPluginOutput.cs | PersonaPluginOutput.swift | 7 | 7 | |
| PersonaPlugin.cs and PersonaPluginOutput.cs with the memory | PersonaPlugin.swift, PersonaPluginOutput.swift | 13 | 13 | PersonaPluginMemoryTests: the memory hooks and the server the plugin always carries, and the `synced` note with the hash of the plugin Claude Code last took |
| SkillCatalog.cs | SkillCatalog.swift | 3 | 3 | |
| SkillPlugin.cs | SkillPlugin.swift | 17 | 17 | |
| ClaudeAccount.cs | ClaudeAccount.swift | 16 | 16 | 2 of them are about ClaudeConfigFolder, as in the xUnit file |
| FaceArt.cs | FaceArt.swift | 63 | 63 | every one of the 56 pictures hashes the same in both languages |
| SettingsJsonPatch.cs | SettingsJsonPatch.swift | 20 | 26 | the 6 extra come from SessionReminderTests; a method answers "nothing to change" with nil instead of a bool and an out parameter |
| SettingsJsonPatch.cs, ClaudeSettingsEditor.cs: the rule for the memory tools | the same two Swift files | 14 | 14 | MemoryPermissionTests; `TrySetMemoryPermission` is `setMemoryPermission`, which answers nil when nothing changes |
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
| AppSettings.cs | AppSettings.swift | 16 | 16 | |
| WizardChecklist.cs | WizardChecklist.swift | 14 | 14 | |
| CredentialFile.cs | CredentialFile.swift | 10 | 10 | `TryParse` with an out parameter is `parse`, which answers nil |
| MemoryFact.cs, MemoryFile.cs | MemoryFact.swift, MemoryFile.swift | 65 | 65 | an entry Aiko cannot use goes back into the file as it was written, which Swift reads with `JsonRawText`; the file is the same in both cores apart from the line ends |
| MemoryRules.cs, MemoryEdits.cs | MemoryRules.swift, MemoryEdits.swift | 44 | 44 | what may be a key, when a key is found and how long a fact may be are in `spec/cases/memory-keys/` and `memory-limits/`; lengths are UTF-16 units in both |
| MemoryPrompt.cs, MemoryNudge.cs | MemoryPrompt.swift, MemoryNudge.swift | 27 | 27 | the rules text is the same word for word; which messages get the note is in `spec/cases/memory-nudge/` |
| MemoryServer.cs | MemoryServer.swift | 17 | 17 | every reply is the same line in both cores |
| MemoryBridge.cs | MemoryBridge.swift | 28 | 28 | a fact over the limit stays out of the block for the model in both, pinned or not |
| MemoryHand.cs | MemoryHand.swift | 22 | 22 | the changes a person makes in the settings window; `MemoryHandTests` holds these and the cases of the page. `OneLine` is `MemoryText.oneLine` here |
| MemoryPage.cs | MemoryPage.swift | 5 | 5 | the other 5 cases of `MemoryHandTests`; `Length` is in UTF-16 units in both |
| Terminal/StreamEvent.cs, HostLine.cs, PermissionMode.cs, ClaudeLaunchPlan.cs, LimitBadge.cs, ModelName.cs | the same names under Terminal/ | — | — | the channel to Claude Code for the terminal window; every rule is a table in `spec/cases/` (`stream-events`, `host-lines`, `launch-plan`, `permission-mode`, `limit-badge`, `model-name`). The C# events are records and keep a tool input as `JsonElement`; the Swift ones are cases of an enum and keep it as `JsonObject`, and the request is the struct `ToolRequest` |
| Terminal/Markdown.cs, AnswerSegments.cs, FileLink.cs | the same names under Terminal/ | — | — | the markdown of an answer read into blocks, the pieces an answer is drawn in, and the words that look like a file; tables in `spec/cases/` (`markdown`, `answer-segments`, `file-link`). The Swift side reads the text in UTF-16 units, as a C# string is read, so both cut it in the same places |
| Terminal/MarkdownCopy.cs | not yet | — | — | a selection in an answer copied as markdown; the Windows side is checked first and the Mac follows before the release (D-312). The table is in `spec/cases/markdown-copy/` |
| Terminal/VoiceInput.cs, DictationText.cs, VoiceHint.cs | the same names under Terminal/ | — | — | voice input in the field: the presses and what the line and the button show, the text that grows while a person speaks, the hint for the recogniser (D-313, D-315). Every rule is a table in `spec/cases/` (`voice-input`, `dictation`, `voice-hint`). The mic and the recogniser on the Mac follow before the release (D-312) |
| Terminal/Conversation.cs, Transcript.cs: pictures of a sent message | not yet | — | — | Windows keeps the pictures of a message after it is sent and reads them from the file of the conversation; the field with their count is gone. The Mac still keeps only the count and shows "Pictures: N", so the string `TerminalPictures` stays until the Mac follows (D-312) |
| Terminal/Conversation.cs, StepLine.cs, RequestCard.cs, EditDiff.cs | the same names under Terminal/ | — | — | one conversation as a feed and a state: what each line of Claude Code and each action of the person changes, and the lines written back. Tables in `spec/cases/` (`conversation`, `step-line`, `request-card`, `edit-diff`). C# keeps the feed as records and returns a new conversation; Swift keeps it as a struct with an enum inside each item. The names of the kinds are the same, so a case file names them once |
| Terminal/ConversationRunner.cs, ConversationProcess.cs, ScriptedClaude.cs, TalkScript.cs | the same names under Terminal/ | 4 | 4 | runs one conversation: starts Claude Code when the first message is sent, passes lines both ways and takes lines that came together as one change. When it starts, with which arguments and what it logs is a table in `spec/cases/conversation-runner`, played against a scripted Claude Code; the four cases here are the order of things between two threads. `IConversationProcess` and `ITerminalLog` are the protocols `ConversationProcess` and `TerminalLog`. The Swift runner takes the platform as its first argument, like `ClaudeLaunchPlan` |
| Terminal/TerminalSessions.cs, SessionName.cs, NewSessionChoices.cs | the same names under Terminal/ | — | — | the list of sessions that is the file `terminal.json`, the name a session gets when the field is empty, and what the form of a new session offers. Tables in `spec/cases/` (`terminal-sessions`, `session-name`, `new-session`). Names and paths are read as UTF-16 units in both cores; `boundElsewhere` takes the platform, like `ProjectBinding` |
| Terminal/TalkGlance.cs, SessionRows.cs, ProcessCap.cs, WindowPlace.cs, TerminalWindowModel.cs | the same names under Terminal/ | — | — | the window of the terminal without its view: a conversation as one line of the list, the rows and the count for a tab, which process to stop before a fifth one starts, where the window opens and what is chosen in it. Tables in `spec/cases/` (`terminal-window`, and `glance` in every scenario of `conversation`). The place of the window counts its top downwards in both cores; the macOS window code turns it over |
| Terminal/Transcript.cs, TranscriptPath.cs | the same names under Terminal/ | — | — | the file Claude Code keeps of a conversation, read into the same feed through the same `Conversation`, and where that file is. Tables in `spec/cases/` (`transcript`, `transcript-path`) |
| TerminalPrivacyTests.cs | TerminalPrivacyTests.swift | 2 | 2 | the source scan: in the terminal only `TerminalLog` writes to the log, and its methods take no text but the name of an environment. Each suite reads the source of its own platform |
| — | SharedCaseTests.swift | 22 | 22 | the case files under `spec/cases/`, read by both suites |
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
| Theme/Motion.cs | Motion.swift, CardLifetime.swift | — | 6 | the durations and the curves, plus the hover delay and the two away turns that CardHost.cs keeps inline |
| Island/IslandPanel.xaml.cs, Island/UnfoldPanel.cs, Island/FitPanel.cs | IslandLayout.swift | — | 28 | the size of the island, its line and corners per edge, the padding that keeps the rings still when the docked side loses its line, where every ring and percentage sits, and the 34 point square it closes into around the face |
| Island/IslandWindow.xaml.cs | IslandDrag.swift | — | 7 | the 3 point threshold and the grip kept as a share of the size |
| Island/RingGauge.cs | RingArt.swift | — | 2 | the island ring: the pen is 18 % of the ring, as RingGauge draws it |
| Settings/SettingsPanel.xaml(.cs) | SettingsNav.swift | — | 10 | the page keys, the order of the menu, the small line under each title, and the body height a screen allows |
| Settings/ChecklistPage.xaml(.cs) | ChecklistLayout.swift | — | 8 | the reading order of the nine items, the three group labels and the word beside each header |
| GeneralPage.xaml.cs (OnCopyDiagnostics) | DiagnosticsText.swift | — | 2 | the lines of the bug report, English in every language as on Windows |

Helpers with no file of their own in the C# core:

| Swift file | Why it exists |
|---|---|
| BridgeWork.swift | what one run of the bridge does: which job it was started for, the snapshot to write, the activity file of a session, the reminder at session start, the persona plugin folder. On Windows the same choices sit inline in `Aiko.Bridge/Program.cs`, where no test can reach them (35 cases in BridgeWorkTests) |
| JsonNode.swift | System.Text.Json keeps the order of keys and the text of numbers when it writes a file back; `JSONSerialization` keeps neither, and Aiko rewrites files that belong to Claude Code |
| CivilTime.swift | `DateOnly`, ISO weeks and the round-trip ("O") time format |
| MemoryText.swift | the memory's questions about a text, answered the way .NET answers them: what `Trim` and `Split` call white space, what `\w` is for one UTF-16 unit, and which two units `RegexOptions.IgnoreCase` takes for the same letter. Foundation has other lists for all three, and the limits and word edges of the memory must agree to the unit (74 cases in MemoryTextTests, shared with JsonRawText) |
| JsonRawText.swift | `JsonElement.GetRawText`: a value of a JSON text as it was written. `JsonNode` keeps the order of keys and the text of numbers, but not the spaces of a whole entry |
| SvgPath.swift | reads the SVG path data FaceArt writes. WPF parses it itself with `Geometry.Parse`; AppKit has nothing of the kind, so the core reads it and hands the drawing layer moves, lines and cubic curves. Arcs and quadratic curves become cubics here (15 cases in SvgPathTests, one of which reads every shape of all 56 faces) |
| SignedIn.swift | whether a folder has an account. Windows reads `.credentials.json`; macOS keeps the same token in the Keychain, so that file is never written and the answer comes from the account block of `.claude.json` (3 cases in SignedInTests) |
| MacShell.swift | two things Windows has a system service for. `ZshProfile` writes Aiko's marked block into `~/.zshrc`, because a folder under the home directory reaches a shell's PATH only through the shell profile; `MacTerminal` writes the script that opens Claude Code in a Terminal window (11 cases in ZshProfileTests and MacTerminalTests) |
| Terminal/LineBuffer.swift | cuts what a pipe delivers into lines without cutting a letter in two. On Windows the reader of the process stream does this itself (6 cases in LineBufferTests) |
| Terminal/LoginShellPath.swift | reads the PATH of a console from what a login shell printed. Claude Code is started with it: an app started from Finder has only the short PATH of the system. On Windows a program gets the PATH of the user from the system (5 cases in LoginShellPathTests) |
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
| the rules of the memory at session start | `memory rules` | the same | `MemoryBridge.rulesOutput`, `MemoryPrompt` |
| the facts at session start | `memory facts` | the same | `MemoryBridge.factsOutput`, `MemoryBridge.factsThatFit` |
| a message of the person: keys and the note | `memory prompt` | the same | `MemoryBridge.onPrompt`, `MemoryEdits.touchByPrompt`, `MemoryNudge` |
| the memory tools as an MCP server | `memory serve` | the same | `MemoryBridge.onServerLine`, `MemoryServer`, `MemoryBridge.writeFailedReply` |
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
- **One writer of the memory at a time.** Windows opens `memory.json.lock` with no sharing; macOS
  takes `flock` on the same file. Both try for two seconds, and both let go when the process ends.
  A file that is not a memory is kept as `memory.json.bad` on both, and the new one is written
  beside the old one and moved over it.
- **The memory file that cannot be read.** Bytes that are not UTF-8 are read as broken text, so
  the file counts as "not a memory" and is kept aside before a save. `Disk.readIfThere` would
  answer "no file" for them, and then a save would write an empty memory over the file.
- **The version the memory server names** comes from the Info.plist of the app the bridge ships
  in, where Windows reads the assembly. A bridge run straight out of `.build` says `0.0.0`.
- **Proof that they start.** The shim keeps `AIKO_SHIM_SELF_TEST=1`. The bridge has no such
  variable on either system: fed `{}` it writes nothing, prints nothing and exits 0, and CI runs
  exactly that.

## The app

`Sources/AikoMac` is the Swift twin of `src/Aiko.App`. It holds no rules either: it draws what
AikoKit decided, and it answers the mouse.

| Windows file | macOS file | What it does |
|---|---|---|
| Program.cs, App.xaml.cs | main.swift, AikoDelegate.swift | starts without a Dock icon, picks the language, registers the fonts |
| AikoShell.cs | AikoShell.swift | the one owner that connects the parts below and decides who is told what |
| Tray/TrayIcon.cs | MenuBarIcon.swift | the icon itself: the picture, the tooltip, the clicks and the mouse over it |
| Tray/TrayMenu.cs | MenuBarMenu.swift | the menu of the icon; both answer with a `TrayCommand` |
| Card/CardHost.cs | CardHost.swift | the wait under a resting mouse, the pinned card and the watch that closes an unpinned one |
| Island/IslandHost.cs | IslandHost.swift | the island window, hiding behind a full screen window, saving where it was dragged |
| ShellNumbers.cs | ShellNumbers.swift | the cards, the card model and the rows of the icon; Windows also brings in direct mode |
| StartupChores.cs | StartupChores.swift | what is put right at every start; Windows also repairs status lines and clears old activity files |
| ShellCheck.cs (`--try-shell`) | the self tests in AikoShell.swift, IslandCheck.swift, SettingsCheck.swift | checks that drive the real shell without writing anything. One difference is left: on a machine that is not set up, the Mac self test still opens the checklist by itself, and the Windows check opens no page |
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
| UpdateWatch.cs | UpdateWatch.swift | the daily version check and count: first two minutes after the start, then once a day. Windows uses a `DispatcherTimer`; the Mac uses `NSBackgroundActivityScheduler`, because a plain timer does not count the hours a MacBook sleeps. The answer goes into the tooltip of the icon and the island |
| FullScreenWatch.cs | FullScreenWatch.swift | says when a window owns the screen; macOS has no foreground event, so it looks at what is in front when the space or the front app changes |
| — | Screens.swift | AppKit counts screen points from the bottom left, the core from the top left: the turn happens here and nowhere else |
| — | Tween.swift | one short animation on a timer that exists only while something moves. WPF has `CompositionTarget.Rendering`; AppKit cannot animate a window that changes its whole shape every frame |
| TaskbarOrder.cs | — | window levels do the same work: the island sits at `.statusBar` and the landing strip at `.floating`, under both the island and the menu bar |
| SettingsStore.cs, ClaudeAccounts.cs | Store.swift | reads the settings, the environments and the plan of an account |
| Log.cs | Log.swift | the same file, the same line format |
| Uninstall.cs | Uninstall.swift | putting everything back on the way out. Windows is called by the installer's hook and then removes the program with `Update.exe uninstall`; macOS has no uninstaller, so the app does the work itself, moves its own bundle to the Bin and quits. Both carry the answer about the memory from the settings page to `UninstallPlan`; the Windows hook has no window and passes "nobody was asked", and macOS has no such path |
| Settings/SettingsWindow.xaml(.cs), SettingsPanel.xaml | Settings/SettingsWindow.swift | the borderless window, the header, the menu and the page area; Esc closes it |
| Settings/SavedMark.cs, Theme/Controls.xaml, Theme/DropDown.cs, Theme/RevealPanel.cs | Settings/SettingsControls.swift | the switch, the segments, the picked card, the fields, the buttons (with the light one for Save and a plus before the words), the tick, the chip, the drop down and the "Saved" mark |
| Settings/EnvironmentsEditor.cs | Settings/SettingsState.swift | what the window is looking at, and every change written through one place |
| Settings/EnvironmentPage.xaml(.cs) | Settings/EnvironmentPageView.swift | one environment: the account, the name, the command, the bound folders and removal |
| Settings/FoldersPage.xaml(.cs) | Settings/FoldersPageView.swift | every bound folder in one table |
| Settings/PersonalityPage.xaml(.cs) | Settings/PersonalityPageView.swift | one switch per environment for all of Aiko (her character, her skills and her memory), the memory block, the face, the temperament, the sample answer and the list of skills |
| Settings/MemoryBlock.cs | Settings/MemoryBlockView.swift, MemoryBlockModel.swift | the memory under the table: quotas, facts, the two forms, the warnings, the foot and the erase question. The model keeps what is open and has the snapshot door `AIKO_TRY_MEMORY` |
| Settings/MemoryStore.cs | Settings/MemoryStore.swift | the memory file for the window: the bridge's lock file, read again, change, write in one step |
| Settings/PrivacyPage.xaml(.cs) | Settings/PrivacyPageView.swift | the two consents and the list of fields that would be sent |
| Settings/GeneralPage.xaml(.cs) | Settings/GeneralPageView.swift | where Aiko shows, startup, the update line, the language, access, diagnostics, starting over. The update line is one state and one action from `UpdateBlock`, with the percent in place of the button while a download runs; `AIKO_TRY_UPDATE` draws it at a stage for a snapshot. The time of the last answer is `UpdateRun.lastAnswered` in AikoHttp.swift |
| Settings/ChecklistPage.xaml(.cs), ChecklistRow.cs | Settings/ChecklistPageView.swift, ChecklistModel.swift | the nine items, their bodies and Finish |
| Settings/SettingsCheck.cs | Settings/SettingsCheck.swift | the self test doors `--try-settings` and `--try-wizard` |
| Commands/ClaudeLauncher.cs, ClaudeFolders.cs | Commands/ClaudeLauncher.swift | finds Claude Code, opens it, waits for a sign-in, lists the config folders |
| Commands/CommandFolder.cs | Commands/CommandFolder.swift | the shim, the command links and the block in `~/.zshrc` |
| Commands/EnvironmentSetup.cs | Commands/EnvironmentSetup.swift | puts a saved list of environments into effect outside Aiko |
| Plugins/PluginSync.cs, ClaudeCli.cs, SkillShelf.cs | Plugins/PluginSync.swift | the marketplace, the skills copy and the claude commands that install the plugins; the rule for the memory tools in every folder, and the update of the persona plugin when its hash is not the one noted |
| Terminal/ClaudeProcess.cs, ProcessJob.cs | Terminal/ClaudeProcess.swift | Claude Code as a child process with pipes on both ends. Windows puts it into a job, so everything it started ends with it; macOS has no such thing, and `ProcessFamily` walks the children of the process and ends each. Windows sets UTF-8 on both pipes; macOS ignores SIGPIPE, so a write to a process that is gone fails instead of ending Aiko |
| Terminal/TerminalLog.cs | Terminal/TerminalLog.swift | the only place in the terminal that writes to the log; `AppTerminalLog` on macOS, because `TerminalLog` is the protocol there |
| Terminal/TerminalCheck.cs, PipeChild.cs | Terminal/TerminalCheck.swift, PipeChild.swift | the doors `--try-pipe`, `--try-conversation` and `--try-history`. Windows answers them in `Program.cs`, macOS in `main.swift` before there is an application, so no icon appears |
| Terminal/TerminalHost.cs | Terminal/TerminalHost.swift | the terminal behind its window: sessions, conversations, the form and the close question. Opened from the menu of the icon, one window at a time; the limits in the title follow the snapshots |
| Terminal/TerminalWindow.cs, WorkAreas.cs | Terminal/TerminalWindow.swift | the window and where it stands on all screens. Windows draws its own buttons with WindowChrome; macOS keeps its three buttons and moves them to the middle of Aiko's bar. While the window is open, macOS gives Aiko a Dock icon and a menu bar, so Cmd+Tab finds it |
| Terminal/TerminalPanel.cs, TerminalView.cs | Terminal/TerminalPanel.swift, TerminalScreen.swift | the bar with the tabs and the limit, the list, the form and the close question. Names differ where SwiftUI has them: TabView is TerminalTab, FormView is SessionForm, AskView is CloseAsk, TerminalView is TerminalScreen |
| Terminal/TalkPanel.cs | Terminal/TalkPanel.swift | the feed: messages, steps that open to their result, cards with the diff of an edit, notices, the line "working" and the reminder of a card out of view. Windows uses a virtual ListBox and knows a row by its place; macOS uses a LazyVStack keyed the same way, as the past from the file and the present of the process both count from one |
| Terminal/AnswerView.cs | Terminal/AnswerView.swift | an answer from its markdown. Windows draws prose into a FlowDocument, macOS into an AttributedString per block, so on macOS a selection stays inside one paragraph. Links go through a URL of Aiko's own and open the browser or VS Code |
| Terminal/TalkInput.cs | Terminal/TalkInput.swift | the field: keys, the history of what was sent, pictures from the clipboard or a drop, paths of dropped files, the stop button and the mode line. Windows catches Ctrl+V as the Paste command; macOS watches Cmd+V with a local event monitor, as the field editor of AppKit takes the paste first. The mic button, the line of a recording and the words over the field (D-315) are Windows only for now (D-312) |
| Voice/Microphone.cs, WhisperNative.cs, LocalRecognizer.cs, VoiceDictation.cs | not yet | voice input on the computer: the mic through waveIn, whisper.cpp through P/Invoke, the recording between them. The Mac follows before the release with AVAudioEngine and whisper on Metal (D-312) |
| Terminal/TerminalSnapshot.cs | Terminal/TerminalWindowCheck.swift | every state of the window on made up sessions. Windows draws it into a file with `--snapshot-terminal`; macOS shows it with `--try-terminal <state>` and AikoShot takes the picture |
| Theme/DropDown.cs | Settings/SettingsControls.swift (DropDown) | the group titles of the folder list: drawn rows on Windows, menu section titles on macOS |
| Terminal/TerminalStore.cs | Terminal/TerminalStore.swift | finds and reads the file of a conversation, and lists the folders inside a bound one. The list of sessions itself is read and saved by `SettingsStore` and `Store`, like the other files of Aiko |
| ClaudeSettingsFile.cs, BridgePath.cs | ClaudeSettingsFile.swift | Aiko's line in somebody else's settings.json, and where the bridge is |
| AikoHttp.cs, UpdateClient.cs, UpdateRun.cs, InstallId.cs, ReportedPeriods.cs | AikoHttp.swift | the one place that makes a URLSession, the version check and the daily count; both check the host of every redirect before following it |
| UpdateInstall.cs | UpdateInstall.swift | downloads an update and installs it once it passes. Windows wraps Velopack's `UpdateManager`; macOS hands the release folder, the key and this bundle to `UpdateInstaller` in the core. Velopack reports the percent on Windows; on macOS the redirect delegate in AikoHttp.swift watches the bytes of the zip |
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

- **The updater is Aiko's own.** Velopack is Windows only, so `UpdateInstaller` downloads the
  zip, checks it against the signed list and swaps the bundle. The general page shows the same
  one line and one action as on Windows. The daily check, the two consent switches and the count
  are the same.

- **Starting at login** is `SMAppService.mainApp` in place of the Run key. It needs a real bundle:
  a binary run straight out of `.build` cannot register, and then the switch says off and saying
  yes only writes a line in the log.

- **A zsh function that shadows a command is left alone.** Windows offers to take a `function claude`
  out of the PowerShell profile (D-167), because that function is usually something a tutorial told
  the person to add. On macOS the shim is on PATH and a shell function that hides it is the person's
  own choice; the checklist has no such item, and `PowerShellProfile.cs` stays unported.

- **The memory block draws its icons with SF Symbols.** Windows draws the lock, the pencil and
  the trash can from the paths of the slayumind set. The Mac window already uses SF Symbols for
  its small marks, so the block uses `lock`, `lock.open`, `pencil` and `trash`. The meaning is the
  same: a closed lock is a pinned fact.

- **The add form keeps what was typed when the category changes.** Windows builds the form again
  on every click and the two fields come back empty. SwiftUI keeps the same form, so the text
  stays.

- **`MemoryStore.swift` repeats the lock and the write of `aiko-bridge/Disk.swift`.** The bridge is
  a program of its own and the app cannot see its files, and AikoKit does not touch the disk. The
  two must stay the same: `flock` on `memory.json.lock`, 80 tries 25 ms apart, a temporary file and
  `rename`, and `.bad` for a file that was not a memory.

Not there yet: direct mode and signing for release.

## Names that had to change

`for` and `default` are keywords in Swift. `SnapshotName.For` is `forConfigDirectory`,
`BridgeCommand.For` is `forPath`, `IslandReveal.For` is `showFor`, and
`EnvironmentSettings.Default(userProfile)` is `defaultEnvironmentIn(_:_:)`.

`in` is a keyword too: `MemoryFile.In(category)` is `inCategory(_:)`.

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

- **A key in a message, and the first person.** Windows asks a regular expression with `\w` and
  `RegexOptions.IgnoreCase`. `NSRegularExpression` has another `\w` and reads a letter above the
  basic plane as one character, where .NET sees two halves that are not letters. So
  `MemoryRules.keyMatches` and `MemoryNudge.applies` walk the message one UTF-16 unit at a time,
  with the .NET meaning of a word character and of the same letter in another case. Both were
  compared with .NET 10 for every unit of the basic plane on 2026-10-11. On the same day the two
  cores were given the same inputs and their 668 answers were compared: the rules text, the tool
  list, the replies, the hook outputs and the files, which were the same apart from the line ends.
- **A category or an id in another case.** `OrdinalIgnoreCase` folds every letter; Swift folds A to
  Z only. The words compared this way are `you`, `work`, `projects`, `interests` and `F12`, and no
  letter outside ASCII turns into one of theirs in .NET: the long «ſ» and the Kelvin sign were
  tried in both cores and are refused by both.

## Not ported, on purpose

Together these are the 14 cases the Swift suite does not have, less the one case it has and the
Windows suite does not (the folders `FileManager` hands this Mac).

- **PowerShellProfile.cs** — 6 cases in ShimTests. It reads and rewrites a PowerShell profile so
  that a `function claude` in it stops fighting the shim. macOS has no such file, and a zsh
  function is not the same problem: the shim sits in PATH, and a shell function that shadows it is
  the person's own choice.
- **WindowsGitBash.cs** — 8 cases in ClaudeShellLookupTests. Git Bash only exists on Windows. On
  macOS `ClaudeShellLookup` is always asked with no path, so it always answers with the fallback
  shell of the platform, `/bin/zsh -lc`.

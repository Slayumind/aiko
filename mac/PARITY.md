# Parity: Aiko.Core (C#) and AikoKit (Swift)

The macOS app repeats the Windows core, so every ported file keeps its behaviour and every xUnit
case has a Swift case with the same name and the same numbers. Test counts below are cases, not
methods: one `[Theory]` with five `[InlineData]` rows counts as five, and so does the Swift
`@Test(arguments:)` that mirrors it. 785 cases in `dotnet test`, 777 in `swift test`. The 49 the
Swift side does not have are named at the bottom; the 41 it has and Windows does not belong to the
two small programs, whose decisions sit inline in `Program.cs` on Windows and in AikoKit here.

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
| AllowedHosts.cs | AllowedHosts.swift | 12 | 12 | the source scan looks for `URLSession` outside `AikoHttp.swift` instead of `new HttpClient` |
| CubicBezier.cs | CubicBezier.swift | 9 | 9 | |
| IslandReveal.cs | IslandReveal.swift | 8 | 8 | |
| IslandPlacement.cs | IslandPlacement.swift | 18 | 18 | |
| TrayFaceMotion.cs | TrayFaceMotion.swift | 7 | 7 | |
| TrayMood.cs | TrayMood.swift | 25 | 25 | `hasFace` takes the environments, as on Windows |
| SessionActivity.cs | SessionActivity.swift | 26 | 26 | |
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
| PlatformConventions.cs | PlatformConventions.swift | 26 | 27 | PlatformConventionsTests (7), PlatformPinTests (6), MacPlatformTests (13 against 14) |
| ClaudeShell.cs | PlatformConventions.swift | 12 | 4 | the other 8 cases are about WindowsGitBash |
| AikoFolders.cs | AikoFolders.swift | 5 | 5 | the macOS layout is pinned in MacPlatformTests and in `spec/cases/folder-layout/` |
| SnapshotName.cs | SnapshotName.swift | 23 | 23 | SnapshotNameTests (10) and SnapshotNamePinTests (13) |
| SnapshotFile.cs | SnapshotFile.swift | 9 | 9 | |
| EnvironmentSnapshots.cs | EnvironmentSnapshots.swift | 6 | 6 | |
| EnvironmentSettings.cs, LaunchCommand.cs, ProjectBinding.cs | EnvironmentSettings.swift, LaunchCommand.swift, ProjectBinding.swift | 41 | 41 | EnvironmentSettingsTests (13) and EnvironmentModelTests (28) |
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
| — | SharedCaseTests.swift | 4 | 4 | the case files under `spec/cases/`, read by both suites |

Helpers with no file of their own in the C# core:

| Swift file | Why it exists |
|---|---|
| BridgeWork.swift | what one run of the bridge does: which job it was started for, the snapshot to write, the activity file of a session, the reminder at session start, the persona plugin folder. On Windows the same choices sit inline in `Aiko.Bridge/Program.cs`, where no test can reach them (29 cases in BridgeWorkTests) |
| JsonNode.swift | System.Text.Json keeps the order of keys and the text of numbers when it writes a file back; `JSONSerialization` keeps neither, and Aiko rewrites files that belong to Claude Code |
| CivilTime.swift | `DateOnly`, ISO weeks and the round-trip ("O") time format |
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
- **Ctrl+C** is ignored with `signal(SIGINT, SIG_IGN)` instead of `Console.CancelKeyPress`, for the
  same reason: Claude Code handles it, and the shim must not quit first. A Claude Code killed by a
  signal is reported as 128 plus the signal, the way a shell reports it.
- **The language of the reminder.** macOS says its language as a tag (`ru-RU`), Windows as a
  number, so `SessionReminder.isRussian` has an overload for each.
- **Proof that they start.** The shim keeps `AIKO_SHIM_SELF_TEST=1`. The bridge has no such
  variable on either system: fed `{}` it writes nothing, prints nothing and exits 0, and CI runs
  exactly that.

Not there yet: the app around them (tray, windows, the wizard), installing the shim into PATH and
the commands folder, the marketplace and plugin install, the update check, and signing the two
programs for release.

## Names that had to change

`for` and `default` are keywords in Swift. `SnapshotName.For` is `forConfigDirectory`,
`BridgeCommand.For` is `forPath`, `IslandReveal.For` is `showFor`, and
`EnvironmentSettings.Default(userProfile)` is `defaultEnvironmentIn(_:_:)`.

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

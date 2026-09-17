# Parity: Aiko.Core (C#) and AikoKit (Swift)

The macOS app repeats the Windows core, so every ported file keeps its behaviour and every xUnit
case has a Swift case with the same name and the same numbers. Test counts below are cases, not
methods: one `[Theory]` with five `[InlineData]` rows counts as five, and so does the Swift
`@Test(arguments:)` that mirrors it.

## Ported

| C# file | Swift file | C# cases | Swift cases | Notes |
|---|---|---|---|---|
| JsonText.cs | JsonText.swift | 15 | 11 | 4 cases need AppSettings and EnvironmentSettings, which are not ported yet |
| LimitSnapshot.cs | LimitSnapshot.swift | 8 | 8 | |
| StatusLineReport.cs | StatusLineReport.swift | 13 | 13 | |
| UsageReport.cs | UsageReport.swift | 10 | 10 | |
| ResetCountdown.cs | ResetCountdown.swift | 7 | 7 | |
| PollBackoff.cs | PollBackoff.swift | 9 | 9 | |
| CardState.cs | CardState.swift | 26 | 26 | CardStateTests (18) and PaceEstimateTests (8) |
| UpdateInfo.cs | UpdateInfo.swift | 12 | 12 | `System.Version` is ported as `VersionNumber` |
| AllowedHosts.cs | AllowedHosts.swift | 12 | 12 | the source scan looks for `URLSession` outside `AikoHttp.swift` instead of `new HttpClient` |
| CubicBezier.cs | CubicBezier.swift | 9 | 9 | |
| IslandReveal.cs | IslandReveal.swift | 8 | 8 | `IslandReveal.For` is `showFor`: `for` is a keyword in Swift |
| IslandPlacement.cs | IslandPlacement.swift | 18 | 18 | |
| TrayFaceMotion.cs | TrayFaceMotion.swift | 7 | 7 | |
| TrayMood.cs | TrayMood.swift | 25 | 25 | `HasFace` takes the persona flags as a list: EnvironmentSettings is not ported yet |
| SessionActivity.cs | SessionActivity.swift | 26 | 26 | |
| Heartbeat.cs | Heartbeat.swift | 17 | 17 | |
| SessionReminder.cs | SessionReminder.swift | 18 | 12 | its other 6 cases are about SettingsJsonPatch and BridgeCommand and sit in SettingsJsonPatchTests; `MessageFor` takes the bound environment by name, because ProjectBinding is not ported |
| PersonaSettings.cs | PersonaSettings.swift | 15 | 15 | |
| PersonaPrompt.cs | PersonaPrompt.swift, PersonaPlugin.swift | 33 | 33 | the prompt text and the plugin files hash the same in both languages |
| PersonaPluginOutput.cs | PersonaPluginOutput.swift | 7 | 6 | the folder names need SnapshotName |
| SkillCatalog.cs | SkillCatalog.swift | 3 | 3 | |
| SkillPlugin.cs | SkillPlugin.swift | 17 | 16 | the marketplace file is written by AikoMarketplace |
| ClaudeAccount.cs | ClaudeAccount.swift | 16 | 14 | 2 cases are about ClaudeConfigFolder |
| FaceArt.cs | FaceArt.swift | 64 | 64 | every one of the 56 pictures hashes the same in both languages |
| SettingsJsonPatch.cs | SettingsJsonPatch.swift | 39 | 39 | 6 cases come from SessionReminderTests and 13 from PluginRemovalTests; a method answers "nothing to change" with nil instead of a bool and an out parameter; the test for "is this our own bridge" comes in as a function, because BridgeCommand is not ported |
| ClaudeSettingsEditor.cs | ClaudeSettingsEditor.swift | 22 | 22 | ClaudeSettingsEditorTests and the file half of PluginRemovalTests; `FileAccess` is IFileAccess with Swift errors in place of exceptions |
| PluginPlan.cs | PluginPlan.swift | 35 | 27 | 8 cases are about AikoMarketplace (the command, the marketplace file); `Desired` takes the persona flag on its own |
| PluginReconciler.cs | PluginReconciler.swift | 4 | 4 | the clock comes in as a function instead of TimeProvider |

Helpers with no file of their own in the C# core:

| Swift file | Why it exists |
|---|---|
| JsonNode.swift | System.Text.Json keeps the order of keys and the text of numbers when it writes a file back; `JSONSerialization` keeps neither, and Aiko rewrites files that belong to Claude Code |
| CivilTime.swift | `DateOnly`, ISO weeks and the round-trip ("O") time format |
| `AikoMarketplaceIds` in SettingsJsonPatch.swift | the name of the marketplace and the plugin ids under it: the only part of AikoMarketplace the ported files need |
| `FileAccess` in ClaudeSettingsEditor.swift | IFileAccess, which is on the list of files to leave alone; the editor cannot be ported without the seam |

## Differences we could not avoid

- **Time.** `DateTimeOffset` counts in 100 ns ticks; Swift `Date` counts in seconds as a double.
  Every value the tests use is exact in both. `ResetCountdown` and the pace estimate cut at the
  same tick as `TimeSpan.FromSeconds` does.
- **A number that cannot be read.** `JsonElement.GetInt64` on `"resets_at": 1.5` throws
  `FormatException`, which is not caught in `StatusLineReport.FromJson`, so the whole status line
  is lost. Swift returns an empty report instead of crashing. Same outcome for the user, no crash.
- **A repeated key.** `JsonDocument` reads the last one; `JsonNode` throws. Swift always reads the
  last one.
- **A lone surrogate** (`"\ud800"`) makes the Windows parser throw `InvalidOperationException`;
  Swift reads it as broken JSON, so the file counts as "no data".
- **Escaping.** The writer repeats both encoders of System.Text.Json: the relaxed one (letters of
  every language stay readable, control, separator, private use and unassigned characters, the
  byte order mark and everything above the basic plane are escaped) and the strict default one
  (everything but printable ASCII, plus the HTML characters). Checked against .NET 10 output for
  ASCII, Russian, Japanese, control characters, the byte order mark and an emoji.
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

## Not ported yet

Another agent is refactoring the Windows-only files, so these are left alone for now: BridgeCommand,
CommandLinks, RealClaude, ClaudeShell, ClaudeInstall, UserPathList, AikoMarketplace,
PowerShellProfile, LaunchCommand, SnapshotName, SnapshotFile, EnvironmentSnapshots,
EnvironmentSettings, EnvironmentEdits, EnvironmentScan, ClaudeConfigFolder, ProjectBinding,
ShimLaunch, AppSettings, IFileAccess, CredentialFile, WizardChecklist.

What the ported files still want from them:

- **BridgeCommand** — how to tell our own status line and hook from somebody else's. It comes into
  `SettingsJsonPatch` as a function for now, and the tests pass the Windows rule.
- **EnvironmentSettings and AikoEnvironment** — the persona flag of an environment, used by
  `TrayMood.hasFace` and `PluginPlan.desired`, which take it as a plain value for now.
- **ProjectBinding** — which environment a folder belongs to, for `SessionReminder.messageFor`.
- **SnapshotName** — the folder name of an environment, for `PersonaPluginOutput`.
- **AikoMarketplace** — the persona command and the marketplace file.
- **ClaudeConfigFolder** — where the account file of a folder is.

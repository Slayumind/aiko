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

Helpers with no file of their own in the C# core:

| Swift file | Why it exists |
|---|---|
| JsonNode.swift | System.Text.Json keeps the order of keys and the text of numbers when it writes a file back; `JSONSerialization` keeps neither, and Aiko rewrites files that belong to Claude Code |
| CivilTime.swift | `DateOnly`, ISO weeks and the round-trip ("O") time format |

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
  The Swift core writes `\n`, as .NET does on macOS.

## Not ported yet

Another agent is refactoring the Windows-only files, so these are left alone for now: BridgeCommand,
CommandLinks, RealClaude, ClaudeShell, ClaudeInstall, UserPathList, AikoMarketplace,
PowerShellProfile, LaunchCommand, SnapshotName, SnapshotFile, EnvironmentSnapshots,
EnvironmentSettings, EnvironmentEdits, EnvironmentScan, ClaudeConfigFolder, ProjectBinding,
ShimLaunch, AppSettings, IFileAccess, CredentialFile, WizardChecklist.

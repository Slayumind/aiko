# Shared test cases

Rules that are data, not code: a table of inputs and the answer each one has. The xUnit tests of
`Aiko.Core` and the Swift tests of `AikoKit` read the same files, so a rule cannot change in one
core and stay as it was in the other.

Only rules that earn it live here. A rule with a shape (a name, a path, a command, a decision) and
many rows earns a file. A rule that needs a fake clock, a fake disk or a hand written object does
not: it stays a test in its own language.

| Folder | Rule |
|---|---|
| `folder-layout/` | where Aiko keeps its files on Windows and on macOS (`AikoFolders`) |
| `launch-command/` | the command name of an environment, and what makes a command bad (`LaunchCommand`) |
| `shim-launch/` | which environment a start of Claude Code belongs to (`ShimLaunch`) |
| `snapshot-name/` | the file name one environment reports into (`SnapshotName`) |
| `update-manifest/` | a signed release manifest made with openssl (`UpdateManifest`) |

A path in these files is written for the platform the case names. Windows cases keep the
backslashes people have on their disks today.

# Shared test cases

Rules that are data, not code: a table of inputs and the answer each one has. The xUnit tests of
`Aiko.Core` and the Swift tests of `AikoKit` read the same files, so a rule cannot change in one
core and stay as it was in the other.

Only rules that earn it live here. A rule with a shape (a name, a path, a command, a decision) and
many rows earns a file. A rule that needs a fake clock, a fake disk or a hand written object does
not: it stays a test in its own language. Reading a settings file is the clearest example of the
second kind — what is checked there is the defaults, the trip through the file and the schema
stamp, not a table (D-255).

| Folder | Rule |
|---|---|
| `platform-paths/` | what a program is called and every path built from that, on Windows, macOS and a made-up system (`PlatformConventions`, `RealClaude`, `CommandLinks`, `ClaudeInstall`, `UserPathList`, `BridgeCommand`, `ClaudeShellLookup`, `AikoMarketplace`) |
| `folder-layout/` | where Aiko keeps its files on Windows and on macOS (`AikoFolders`) |
| `launch-command/` | the command name of an environment, and what makes a command bad (`LaunchCommand`) |
| `shim-launch/` | which environment a start of Claude Code belongs to (`ShimLaunch`) |
| `hook-events/` | what a hook of the persona plugin says a session is doing (`HookEvent`, `ActivityRecord`) |
| `tray-mood/` | which face an event brings (`TrayMood`) |
| `snapshot-name/` | the file name one environment reports into (`SnapshotName`) |
| `stored-file/` | what Aiko does with its own file after a read: use it, set it aside as .bad, or keep it from saves when it could not be read (`StoredFile`) |
| `snapshot-leftovers/` | temporary files of a bridge that Aiko removes when it starts (`SnapshotLeftovers`) |
| `update-manifest/` | a signed release manifest made with openssl (`UpdateManifest`) |
| `memory-keys/` | what may be a key of a fact, and when a key is found in a message (`MemoryRules`) |
| `memory-nudge/` | which messages speak in the first person and get the memory note (`MemoryNudge`) |
| `memory-limits/` | how long a fact may be with its meaning and keys (`MemoryRules`) |
| `launch-plan/` | the arguments and the environment of the Claude Code a conversation starts (`ClaudeLaunchPlan`) |
| `limit-badge/` | which limit the title bar of the terminal names (`LimitBadge`) |
| `header-strip/` | the order and the numbers of the open header of the terminal window (`HeaderStrip`) |
| `shell-ask/` | what a command line asks Claude Code and how it reads the answer, and the link under a Ctrl+click (`ShellAsk`, `ShellLink`) |
| `shell-screen/` | how the output of a command line is laid out and cut into one block per command by the marks of shell integration, from real recordings of three shells (`ShellScreen`, `ShellBlocks`, `VtParser`) |
| `notices/` | which change of a session plays the sound, and the counts and the one sign of the tray and the taskbar button (`Notices`, `SessionCounts`) |
| `taskbar-button/` | what the middle and the corner of the taskbar button of the terminal window show, and which app id the window takes so that Windows draws it (`ButtonLook`, `TaskbarIdentity`); Windows only, the Mac has no taskbar button |
| `window-zoom/` | the steps of the zoom of the terminal window and the font of the terminal at each (`WindowZoom`) |
| `card-words/` | the words of the limits in both languages: the name of a window, the percent, the word of a tone, the reset, the pace and how fresh the numbers are (`CardText`) |
| `count-query/` | the query of the daily version check: the version only with counting off, six fields with it on (`CountQuery`) |
| `redirect-headers/` | which headers go on after a redirect: a token stays with its host (`RedirectHeaders`); Windows only, the Mac sends no token |
| `log-paths/` | a line for the log without full paths (`LogText`), for the logs of both platforms |
| `file-mention/` | the files that "@" offers in the field: where the word starts, which files, in which order, what the field becomes (`FileMention`) |
| `conversation-file/` | a conversation of Claude Code read from the start and the end of its file, for the palette of sessions (`ConversationFile`) |
| `session-search/` | what the palette of sessions finds, in three groups (`SessionSearch`) |
| `text-match/` | how the lists of the terminal match what is typed, and the quick pick of a folder for a new session (`TextMatch`) |
| `terminal-title/` | what a session does, read from the title the terminal shows (`TerminalTitle`) |
| `terminal-events/` | what a hook of the terminal says a session is doing (`TerminalEvent`) |
| `terminal-copy/` | text copied out of a terminal: rows wrapped by the width joined again, the shared indent gone (`TerminalCopy`) |
| `editor/` | what a key does in the editor under the terminal, what a send writes, the history of a command line and Tab for a path (`EditorKeys`) |
| `release-notes/` | the titles of the notes of a newer release, read from GitHub (`ReleaseNotes`) |
| `terminal-keys/` | what Ctrl and a key do in the terminal window, with Shift or without, and when the key is left to the program (`TerminalKeys`); Ctrl on Windows, Cmd on the Mac |
| `terminal-sessions/` | the list of sessions as the file `terminal.json`: adding, closing, moving, and what a broken file reads as (`TerminalSessions`) |
| `session-name/` | the name of a session with an empty name field, and how a name and a folder are kept (`SessionName`) |
| `terminal-window/` | the rows of the list of sessions, the process to stop before a fifth one starts, where the window opens and what is chosen in it (`SessionRows`, `ProcessCap`, `WindowPlace`, `TerminalWindowModel`) |
| `transcript-path/` | where that file is (`TranscriptPath`) |
| `file-link/` | which words look like a file with a line, and where a link leads (`FileLink`, `LinkTarget`) |
| `voice-input/` | what a press, Esc and the mic do to a recording, and what the line under the input and the mic button show (`VoiceInput`) |
| `dictation/` | which words of a recording are confirmed and which are the grey tail (`DictationText`) |
| `voice-hint/` | what the recogniser is told before it listens, and in which language (`VoiceHint`) |
| `voice-model/` | where the speech model comes from, when each engine is ready, and how an OpenAI key is checked and shown (`VoiceModel`) |

A path in these files is written for the platform the case names. Windows cases keep the
backslashes people have on their disks today.

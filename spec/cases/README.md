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
| `snapshot-leftovers/` | temporary files of a bridge that Aiko removes when it starts (`SnapshotLeftovers`) |
| `update-manifest/` | a signed release manifest made with openssl (`UpdateManifest`) |
| `memory-keys/` | what may be a key of a fact, and when a key is found in a message (`MemoryRules`) |
| `memory-nudge/` | which messages speak in the first person and get the memory note (`MemoryNudge`) |
| `memory-limits/` | how long a fact may be with its meaning and keys (`MemoryRules`) |
| `stream-events/` | what the terminal reads from one line of Claude Code, and what it drops (`StreamEvent`) |
| `host-lines/` | the exact lines the terminal writes to Claude Code (`HostLine`) |
| `launch-plan/` | the arguments and the environment of the Claude Code a conversation starts (`ClaudeLaunchPlan`) |
| `permission-mode/` | the names of the four permission modes and their order (`PermissionModes`) |
| `limit-badge/` | which limit the title bar of the terminal names (`LimitBadge`) |
| `header-strip/` | the order and the numbers of the open header of the terminal window (`HeaderStrip`) |
| `model-name/` | the name of a model in the line under the input (`ModelName`) |
| `slash-menu/` | the menu of commands that "/" opens in the field: when it opens, what it shows, in which order (`SlashMenu`) |
| `file-mention/` | the files that "@" offers in the field: where the word starts, which files, in which order, what the field becomes (`FileMention`) |
| `feed-entries/` | the rows of the feed: which steps in a row are one line, and how a long output is cut (`FeedEntries`, `LongOutput`) |
| `shell-command/` | a command of "!": what is a command, what Claude gets, how the file reads back (`ShellCommand`) |
| `context-gauge/` | how full the context of a conversation is, and the colour of its word under the field (`ContextGauge`) |
| `conversation/` | what a row of stream lines and actions of the person makes of a conversation (`Conversation`), and how the list of sessions sees it (`TalkGlance`) |
| `conversation-runner/` | when Claude Code is started for a conversation, with which arguments, what is written to it and what is logged (`ConversationRunner`, played against `ScriptedClaude`) |
| `step-line/` | one step of the work as one line of the feed (`StepLine`) |
| `request-card/` | a request as a card, and the line each choice writes back (`RequestCard`) |
| `talk-keys/` | what Enter and Esc in the field do, and which card they answer (`TalkKeys`) |
| `terminal-sessions/` | the list of sessions as the file `terminal.json`: adding, closing, moving, and what a broken file reads as (`TerminalSessions`) |
| `session-name/` | the name of a session with an empty name field, and how a name and a folder are kept (`SessionName`) |
| `new-session/` | the folders the form of a new session offers, the model it starts with, and the other environment a folder is bound to (`NewSessionChoices`) |
| `terminal-window/` | the rows of the list of sessions, the process to stop before a fifth one starts, where the window opens and what is chosen in it (`SessionRows`, `ProcessCap`, `WindowPlace`, `TerminalWindowModel`) |
| `transcript/` | the file Claude Code keeps of a conversation, read into the feed (`Transcript`) |
| `transcript-path/` | where that file is (`TranscriptPath`) |
| `markdown/` | the markdown of an answer read into blocks and inlines (`Markdown`) |
| `answer-segments/` | the pieces an answer is drawn and selected in (`AnswerSegments`) |
| `markdown-copy/` | what a selection inside the prose of an answer copies as markdown (`MarkdownCopy`) |
| `file-link/` | which words look like a file with a line, and where a link leads (`FileLink`, `LinkTarget`) |
| `voice-input/` | what a press, Esc and the mic do to a recording, and what the line under the input and the mic button show (`VoiceInput`) |
| `dictation/` | which words of a recording are confirmed and which are the grey tail (`DictationText`) |
| `voice-hint/` | what the recogniser is told before it listens, and in which language (`VoiceHint`) |
| `voice-model/` | where the speech model comes from, when each engine is ready, and how an OpenAI key is checked and shown (`VoiceModel`) |

A path in these files is written for the platform the case names. Windows cases keep the
backslashes people have on their disks today.

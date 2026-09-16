# Contributing

Aiko is a small project, and help is welcome.

## Before you write code

**Open an issue first** for anything bigger than a typo. Aiko has a roadmap, and a change that
doesn't fit it may be turned down after you've done the work. An issue takes five minutes and can
save you that time.

Found a security problem? Don't open an issue. [SECURITY.md](SECURITY.md) says how to report it
privately.

## Building it

You need the .NET 10 SDK and nothing else: no Visual Studio, no C++ tools.

```
git clone https://github.com/Slayumind/aiko
cd aiko
dotnet test Aiko.slnx
dotnet run --project src/Aiko.App
```

Three projects and one test project:

- `Aiko.Core` holds everything that can be decided without a screen: reading limits, counting down
  to a reset, backing off after an error, editing the Claude Code settings file. Tests go here.
- `Aiko.App` has the tray icon, the windows and the timers, with as little logic as possible.
- `Aiko.Bridge` is a tiny program Claude Code runs as its status line. It writes a file the app
  reads.
- `Aiko.Core.Tests` uses xUnit.

To look at a window without running the whole app:

```
dotnet run --project src/Aiko.App -- --snapshot card.png
```

There are also `--snapshot-icon`, `--snapshot-island`,
`--snapshot-settings <file> general|folders|env1|env2 [tall]` and `--snapshot-wizard <file> [item]`,
which draws the checklist page with one item open, for example `Commands`.

## How a change should look

- **Put the logic in `Aiko.Core` and write a test for it.** If a change can't be tested, ask in the
  issue whether the logic can move.
- **Anything that touches the disk or the network goes behind an interface**, so a test can replace
  it. `IFileAccess` is an example.
- Keep methods small and names plain. The code is read by people learning C#.
- Don't write comments that repeat the code. A comment says **why**, and only when that isn't
  obvious.
- Write in English with simple words and short sentences: code, comments and commit messages.

## Changing what Aiko says

Every word is in `tools/strings.json`, English and Russian side by side. Edit that file and run:

```
python tools/strings.py
```

It writes both resource files and the class the code reads them through. A missing key then breaks
the build, so nobody finds a blank label months later. Don't edit `Strings.resx` or
`Strings.Designer.cs` by hand.

Three rules for Russian, all learned on real windows:

- **Rephrase, don't translate.** Write the sentence the way a Russian speaker would say it.
- **Leave room.** Russian text is about a fifth longer. Once it made two labels in one grid cell
  overlap. Look at the window before you call it done: `--snapshot`, `--snapshot-settings`,
  `--snapshot-wizard`.
- **Never build a sentence from pieces.** Russian words change their endings, and joined pieces
  stop matching.

## Commits

The subject line is a short action in the present tense: `Make the bridge start from an installed
copy`. The body says what was wrong and why this change fixes it. Someone will read it a year from
now to understand the change.

No sign-offs, no co-author lines, no emoji.

## Pull requests

- Branch from `in-progress`, never from `main`.
- One change per pull request.
- `dotnet test` and `dotnet format --verify-no-changes` pass. The build checks both.
- Add a line to `CHANGELOG.md` under `[Unreleased]` if a user would notice the change.

## Things Aiko will not do

These are settled. A pull request that changes them will be turned down.

- **Aiko never stores your Claude account token,** never refreshes it and never writes to the
  credentials file. [PRIVACY.md](PRIVACY.md) says why.
- **Aiko doesn't pretend to be Claude Code.** Its user agent says Aiko and its version.
- **No analytics service, no crash reporting service, no third-party network calls.** The count in
  [PRIVACY.md](PRIVACY.md) is the only measurement: six values a day, behind their own switch. It
  uses the update check request, and its ID still changes every day. The week and the month are
  counted with a flag the copy sets for itself, not with a longer-lived ID, and that is the line to
  hold. Ask before you add anything else to measure.
- **No code or artwork from notchi.** notchi is GPL-3.0 and Aiko is Apache-2.0. You can read notchi
  to understand how something works, but don't copy from it.

## Licence

By contributing you agree that your work is licensed under Apache-2.0, the same as the rest of
Aiko.

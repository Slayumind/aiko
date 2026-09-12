# Contributing

Thank you for looking. Aiko is a small project and help is welcome.

## Before you write code

**Open an issue first** for anything larger than a typo. Aiko has a roadmap, and a change that does
not fit it may be turned down after you have done the work. That is a waste of your time, and the
issue costs five minutes.

Found a security problem? Do not open an issue. [SECURITY.md](SECURITY.md) says how to report it
privately.

## Building it

You need the .NET 10 SDK. Nothing else: no Visual Studio, no C++ tools.

```
git clone https://github.com/Slayumind/aiko
cd aiko
dotnet test Aiko.slnx
dotnet run --project src/Aiko.App
```

Three projects and one test project:

- `Aiko.Core` — everything that can be decided without a screen: reading limits, counting down to a
  reset, backing off after an error, editing the Claude Code settings file. This is where tests go.
- `Aiko.App` — the tray icon, the windows, the timers. As little thinking as possible.
- `Aiko.Bridge` — a tiny program Claude Code runs as its status line. It writes a file the app
  reads.
- `Aiko.Core.Tests` — xUnit.

To look at a window without running the whole app:

```
dotnet run --project src/Aiko.App -- --snapshot card.png
```

There is also `--snapshot-icon`, `--snapshot-island`, `--snapshot-settings` and
`--snapshot-wizard <step>`.

## What a change should look like

- **Put the thinking in `Aiko.Core` and write a test for it.** If a change cannot be tested, ask in
  the issue whether it can be moved.
- **Anything that touches a disk or the network goes behind an interface**, so a test can stand in
  for it. `IFileAccess` is the example.
- Keep methods small and names plain. The code is read by people learning C#.
- No comments that repeat the code. A comment says **why**, and only when the why is not obvious.
- English, simple words, short sentences. That holds for code, comments and commit messages.

## Commits

The subject line is a short action, in the present tense: `Make the bridge start from an installed
copy`. The body says what was wrong and why this is the fix. Somebody will read it in a year
wondering what you were thinking, and that somebody may be you.

No sign-offs, no co-author lines, no emoji.

## Pull requests

- Branch from `in-progress`, never from `main`.
- One change per pull request.
- `dotnet test` and `dotnet format --verify-no-changes` pass. The build checks both.
- Add a line to `CHANGELOG.md` under `[Unreleased]` if a user would notice the change.

## Things Aiko will not do

These are settled, and a pull request that changes them will be turned down:

- **Aiko never stores your Claude account token,** never refreshes it and never writes to the
  credentials file. [PRIVACY.md](PRIVACY.md) says why.
- **Aiko does not pretend to be Claude Code.** Its user agent says Aiko and its version.
- **No analytics service, no crash reporting service, no third party network calls.** The daily
  count described in [PRIVACY.md](PRIVACY.md) is the only measurement, it rides on the update
  check, and its identifier changes every day. Do not add a second thing to measure without asking.
- **No code or artwork from notchi.** It is a lovely project and it is GPL-3.0; Aiko is Apache-2.0.
  Reading it to understand a behaviour is fine, copying from it is not.

## Licence

By contributing you agree that your work is licensed under Apache-2.0, the same as the rest of
Aiko.

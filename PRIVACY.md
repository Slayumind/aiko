# Privacy

Aiko shows how much of your Claude Code limits is left. This page says what it reads, what it
sends, and where it keeps things.

There is no telemetry, no analytics and no crash reporting service. Nobody is counted.

## What Aiko reads

- **The numbers Claude Code reports.** Claude Code runs a status line command after every answer
  and hands it a small piece of JSON. Aiko keeps only the limit percentages and their reset times
  from it. The working folder, the session id and the transcript path are in that JSON too, and
  they are thrown away: what is not written down cannot leak.
- **The Claude Code settings file**, to add its one line and to keep yours. A copy of the file is
  made before the first change.
- **Which folders look like Claude Code accounts**, by their names and their dates. The credentials
  file is never opened for this: whether it exists is all Aiko needs.
- **The access token, in direct mode only.** See below.

## What Aiko sends

Nothing, unless you switch one of these on.

- **Direct mode** (off by default, per environment): Aiko asks `api.anthropic.com` for the limits
  of that account, at most once every three minutes. The request carries the access token of that
  account and an honest user agent, `Aiko/<version>`. No other address is ever contacted with it.
- **Update checks** (off by default): Aiko asks `slayumind.org` which version is the latest. That
  server belongs to the author of Aiko and sees your address and your version, like any web
  request. Downloads come from GitHub. The button in settings asks once, by hand, whenever you
  press it.

## The token

Claude Code keeps an access token in its own folder. In direct mode Aiko needs it to ask about
your account, and the rules around it are strict:

- it is read again before every request and kept in memory for that one request;
- it is never written to a file, a cache, a log or the screen;
- it is never refreshed and the credentials file is never written to: a refresh replaces the
  refresh token, and that could log Claude Code out;
- an expired token is left alone. Aiko waits for Claude Code to refresh it and shows the numbers
  it already has, marked with their time.

Without direct mode Aiko never touches the token at all.

## Where Aiko keeps things

- `%APPDATA%\Aiko` — your settings and the list of environments.
- `%LOCALAPPDATA%\Aiko` — the limit numbers reported by Claude Code, the last direct mode answer,
  and a short log of what Aiko did. All of it is numbers, times and its own events.
- `%USERPROFILE%\.claude*\settings.json` — one line added by Aiko, with a copy of the original
  beside it.
- One value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, if you asked Aiko to start
  with Windows.

Removing Aiko puts the status line back, removes that value and deletes both of its own folders.
You can also delete them by hand at any time; Aiko will start again with its defaults.

## The log

The log holds what Aiko did: when it started, when a card opened, when a check failed. No tokens,
no limit numbers of yours in the text, nothing from Claude Code. The "copy diagnostics" button in
settings copies a short summary of the same kind, for a bug report.

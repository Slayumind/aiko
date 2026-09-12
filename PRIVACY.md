# Privacy

Aiko shows how much of your Claude Code limits is left. This page says what it reads, what it
sends, and where it keeps things.

There is no analytics service and no crash reporting service. Aiko does count how many copies run
each day, and only if you let it: see **Counting** below.

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
- **Update checks** (off by default): Aiko asks `slayumind.org` which version is the latest, once a
  day and whenever you press the button. Downloads come from GitHub. The same request does the
  counting below, which is why there is one switch and not two.

## Counting

The author of Aiko would like to know roughly how many people use it. That is the only thing
measured, and this is exactly how.

**What is sent**, along with the question about the latest version:

- the version of Aiko you are running;
- the version of Windows;
- an identifier that **changes every day**.

**Nothing else.** Not your name, not your account, not your folders, not your limits, not which
features you use, not how long Aiko has been running.

**How the identifier works.** A random value is made once on this computer and kept in
`%APPDATA%\Aiko\install-id`. It never leaves the machine. What travels is a hash of that value
together with today's date, cut to sixteen characters. Tomorrow's is unrelated to today's, so two
days of requests cannot be joined into one person. The count is of days, not of people followed
over time, which is the number that was wanted anyway. Delete that file and you get a new value.

**What that leaves.** `slayumind.org` receives the request, and like any web server it sees the
address it came from. The site's own code never reads that address and never stores it: the
counting uses only the identifier Aiko sends. What sits in front of the site — a content network
and a reverse proxy — sees it the way it sees every request to every website, and its access log
is outside what this project controls. Saying more than that would be a promise about somebody
else's machine.

**How to turn it off.** The switch in settings called "Check for updates". It is off until you turn
it on, and turning it off stops the version check and the counting together. There is no way to
have one without the other, and saying otherwise would be a lie about what a single request does.

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

- `%APPDATA%\Aiko` — your settings, the list of environments, and the random value described above.
- `%LOCALAPPDATA%\Aiko` — the limit numbers reported by Claude Code, the last direct mode answer,
  and a short log of what Aiko did.
- `%USERPROFILE%\.claude*\settings.json` — one line added by Aiko, with a copy of the original
  beside it.
- One value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, if you asked Aiko to start
  with Windows.

Removing Aiko puts the status line back, removes that value and deletes both of its own folders.
You can also delete them by hand at any time; Aiko will start again with its defaults.

## The log

The log holds what Aiko did: when it started, when a card opened, when a check failed. It is 256 KB
at most and starts over when it fills.

Three things in it are yours, and they are there because a wrong number cannot be looked into
without them:

- **limit percentages**, when direct mode is on;
- **the names you gave your environments**;
- **the name of a Claude Code folder** when Aiko changes its settings file — `.claude-personal`,
  not the path to it.

It holds **no tokens**, no session ids, no working folders, no model answers and nothing you wrote.
It stays on your computer and is never sent anywhere. The "copy diagnostics" button in settings
copies a short summary, without any of the three above, for you to paste into a bug report.

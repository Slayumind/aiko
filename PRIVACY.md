# Privacy

Aiko shows how much of your Claude Code limits is left. This page lists what it reads, what it
sends and where it keeps things.

Aiko has no analytics service and no crash reporting service. It counts how many copies run each
day, and only if you allow it: see **Counting** below.

## What Aiko reads

- **The numbers Claude Code reports.** Claude Code runs a status line command after every answer
  and passes it a small piece of JSON. Aiko keeps only the limit percentages and their reset times.
  The JSON also holds the working folder, the session ID and the transcript path. Aiko drops them
  and never writes them anywhere.
- **The Claude Code settings file**, to add its one line and keep yours. Aiko copies the file before
  the first change.
- **Which folders look like Claude Code accounts**, by their names and dates. Aiko doesn't open the
  credentials file for this. It only checks that the file exists.
- **The access token, in direct mode only.** See below.
- **Session events, where the personality is on.** Claude Code runs Aiko's hook when a session
  starts working, waits for you, finishes, fails or ends. Aiko reads only the kind of event and the
  session ID, keeps a small file per session with the state and the time, and hashes the session ID
  into the file name. The prompt, tool input, paths and answers are never read or stored. The file
  goes away when the session ends, and files older than a day are removed when Aiko starts.

## What Aiko sends

Nothing, unless you turn on one of these.

- **Direct mode** (off by default, set per environment): Aiko asks `api.anthropic.com` for the
  limits of that account, at most once every three minutes. The request carries that account's
  access token and an honest user agent, `Aiko/<version>`. Aiko never sends the token anywhere else.
- **Update checks** (off by default): Aiko asks `slayumind.org` for the latest version once a day
  and whenever you press the button. Downloads come from GitHub. The same request does the counting
  below, so there's one switch for both.

## Counting

The author of Aiko wants to know roughly how many people use it. That's the only thing Aiko
measures, and this is how.

**What is sent**, together with the question about the latest version:

- the version of Aiko you run;
- the version of Windows;
- an ID that **changes every day**.

**Nothing else.** No name, account, folders or limits. Nothing about which features you use or how
long Aiko has been running.

**How the ID works.** Aiko makes a random value once and keeps it in `%APPDATA%\Aiko\install-id`.
That value never leaves your computer. Aiko sends a hash of the value and today's date, cut to
sixteen characters. Tomorrow's hash has no link to today's, so two days of requests can't be joined
into one person. The site counts copies per day, and it never follows one copy over time. Delete the
file and Aiko makes a new value.

**What the server sees.** `slayumind.org` receives the request, and like any web server it sees the
IP address it came from. The site's code never reads or stores that address: the count uses only the
ID Aiko sends. The content network and reverse proxy in front of the site see the address the way
they see every request, and their access logs are outside this project's control.

**How to turn it off.** Use the **Check for updates** switch in settings. It's off until you turn it
on, and turning it off stops both the version check and the count. You can't have one without the
other, because both come from the same single request.

## The token

Claude Code keeps an access token in its own folder. In direct mode Aiko needs that token to ask
about your account. The rules:

- Aiko reads the token again before every request and keeps it in memory only for that request.
- Aiko never writes the token to a file, a cache, a log or the screen.
- Aiko never refreshes the token and never writes to the credentials file. A refresh replaces the
  refresh token, and that could sign Claude Code out.
- When the token has expired, Aiko leaves it alone. It waits for Claude Code to refresh it and shows
  the numbers it already has, with their time.

Without direct mode, Aiko never touches the token.

## Where Aiko keeps things

- `%APPDATA%\Aiko`: your settings, the list of environments and the random value described above.
- `%LOCALAPPDATA%\Aiko`: the limit numbers from Claude Code, the last direct mode answer, a short
  log, and, where the personality is on, the session state files, the personality plugin and the
  local marketplace with the skills.
- `%USERPROFILE%\.claude*\settings.json`: one line added by Aiko, with a copy of the original next
  to it, and Aiko's plugins and marketplace where the personality is on. Claude Code keeps its own
  copies of those plugins in its `plugins` folder.
- One value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, if you asked Aiko to start
  with Windows.

Removing Aiko puts the status line back, removes its plugins from Claude Code, removes that value
and deletes both of Aiko's folders. You
can also delete the folders by hand at any time, and Aiko starts again with its defaults.

## The log

The log records what Aiko did: when it started, when a card opened, when a check failed. It's 256 KB
at most and starts over when it's full.

Three kinds of your data are in it, because a wrong number can't be investigated without them:

- **limit percentages**, when direct mode is on;
- **the names you gave your environments**;
- **the name of a Claude Code folder** when Aiko changes its settings file: `.claude-personal`, not
  the full path.

The log has **no tokens**, session IDs, working folders, model answers or anything you wrote. It
stays on your computer and Aiko never sends it. **Copy diagnostics** in settings copies a short
summary without those three things, for you to paste into a bug report.

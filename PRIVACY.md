# Privacy

Aiko shows how much of your Claude Code limits is left. This page lists what it reads, what it
sends and where it keeps things.

Aiko has no analytics service and no crash reporting service. It counts how many copies run each
day, and only if you allow it: see **Counting** below. That count has its own switch, separate from
the update check.

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
  into the file name. For this the prompt, tool input, paths and answers are never read or stored.
  The file goes away when the session ends, and files older than a day are removed when Aiko starts.
- **What a session in Aiko's terminal does.** A session that Aiko starts in its terminal window
  runs one more hook of Aiko, in every environment. It shows in the list of sessions what the
  session does now: "reading office.mjs", "running: npm test", "waits for permission". For this
  Aiko reads the kind of event, the name of the tool, the name of a file without its folder, and
  the first 40 characters of a command, a search or the host of a web page. Your messages and the
  answers of the model are never read. Aiko keeps only the last event, in a small file per session
  on this computer, and removes the file when you close the session. The title the program sets
  for its terminal gives the name of the session.
- **Pictures you paste into the editor.** A picture from the clipboard is written as a PNG into
  a folder of its session on this computer, so Claude Code can take it; its path is what goes to
  Claude Code. The folder is removed when you close the session.
- **Your conversations, when you search them.** The palette of sessions (Ctrl+Shift+K) finds your
  conversations with Claude Code, also the ones you started in another terminal or in VS Code. For
  this Aiko reads the start and the end of each conversation file in the folders of your
  environments: the folder it ran in, your first message and its title. Aiko keeps them in memory
  while it runs and writes them nowhere. Conversations of programs, such as `claude -p`, are left out.
- **Your message, where the personality is on.** Aiko's memory comes with her personality: they
  are turned on together for an environment. There Claude Code hands each message you send to a
  second hook of Aiko. It looks for two things: the keys of the facts in the memory, to note that a
  fact came up again today, and words like "I" or "my", to remind the model that the message may
  hold something worth remembering. The message is read in memory and is **not** stored, logged
  or sent anywhere by Aiko.
- **The memory file.** What the model saves about you, at most 42 short facts, is kept in one file
  on your computer, `memory.json` next to Aiko's settings. It is given to the model at the start of
  a session in the environments where the personality is on. You can read, change and erase it on
  the Personality page, or open the file itself. Turning the personality off does not erase it.
  Removing Aiko asks whether to keep it (see "Where Aiko keeps things").
  Aiko never sends it anywhere; the model you talk to sees it, as it sees everything else in the
  session.
- **One permission line, where the personality is on.** Aiko adds one rule to that environment's
  `settings.json`, so the model may use Aiko's two memory tools without asking you each time. The
  rule goes away when you turn the personality off there or remove Aiko.
- **The terminal window.** Its sessions are real terminals on your computer: Claude Code or a shell,
  run as you would run them yourself. Aiko shows what they print and passes your keys to them. It
  doesn't store, log or send anything that appears in a terminal. The list of sessions is kept in
  `terminal.json` next to Aiko's settings: for each session its name, folder, environment and the
  ID of its Claude Code conversation, never the conversation itself.
- **If the personality was on before Aiko had a memory**, the memory starts with the update. Aiko
  opens the Personality page once after it, where this is said and where you can turn her off.

## What Aiko sends

Nothing, unless you turn on one of these.

- **Direct mode** (off by default, set per environment): Aiko asks `api.anthropic.com` for the
  limits of that account, at most once every three minutes. The request carries that account's
  access token and an honest user agent, `Aiko/<version>`. Aiko never sends the token anywhere else.
- **Update checks** (off by default): Aiko asks `slayumind.org` for the latest version once a day
  and whenever you press the button. The files of an update come from GitHub, and only after you
  press **Download and install**. See **Updates** below.
- **Counting** (off by default, its own switch): the same request also says that one copy ran today.
  One request, two switches. With counting off, the identifier and the flags below are not in the
  address at all, and the site writes nothing. With update checks off and counting on, Aiko still
  makes that one request, because the count rides on it. It just doesn't read the version out of
  the answer.
- **Voice input** (off by default, Windows for now): see **Voice input** below. On this computer it
  sends no sound anywhere; with your own OpenAI key the sound of each recording goes to OpenAI.

The `calendar` and `drive` skills don't change this. They are instructions for Claude: Claude reads
and writes your Google Calendar and Drive through the connectors you set up in Claude, and the Aiko
app itself sends nothing to Google.

## Counting

The author of Aiko wants to know roughly how many people use it. That's the only thing Aiko
measures, and this is how.

**What is sent**, together with the question about the latest version:

- `v`: the version of Aiko you run;
- `os`: the version of Windows;
- `day`: an ID that **changes every day**;
- `w`: a single 1, when this is the first run of this week;
- `m`: a single 1, when this is the first run of this month;
- `p`: 1 or 0: whether the personality is on in at least one environment.

**Nothing else.** No name, account, folders or limits. Nothing about which skills you use, what
the memory holds, which projects you work on, or how long Aiko has been running. The settings page
lists these six lines and says what each one is for.

**How the ID works.** Aiko makes a random value once and keeps it in `%APPDATA%\Aiko\install-id`.
That value never leaves your computer. Aiko sends a hash of the value and today's date, cut to
sixteen characters. Tomorrow's hash has no link to today's, so two days of requests can't be joined
into one person. The site counts copies per day, and it never follows one copy over time. **Reset
ID** on the privacy page throws the value away, and so does deleting the file.

**How a week and a month are counted.** They can't be worked out from the daily ID, because two
days of it don't join up. So Aiko answers the question itself: it remembers on your computer which
week and which month it has already reported, in `%APPDATA%\Aiko\reported.json`, and sets `w` or
`m` once per period. The site adds those up. That file never leaves your computer either, and the
weeks and months are calendar ones.

**How long it's kept.** The rows are deleted after 90 days.

**What the server sees.** `slayumind.org` receives the request, and like any web server it sees the
IP address it came from. The site's code never reads or stores that address: the count uses only the
ID Aiko sends. The content network and reverse proxy in front of the site see the address the way
they see every request, and their access logs are outside this project's control.

**How to turn it off.** Use **Send anonymous statistics** on the **Privacy** page in settings. It's
off until you turn it on, and turning it off stops the count. **Check for updates** sits next to it
and is a separate switch, so you can keep one and drop the other.

## Updates

Aiko never downloads or replaces itself on its own. The daily check reads a version number and
nothing else. Downloading starts when you press **Download and install** on the **General** page,
and the new version starts when you press **Restart now**.

Every release carries `SHA256SUMS.txt`, with the SHA-256 of each file, and `SHA256SUMS.txt.sig`, a
signature made with an ECDSA P-256 key that only the release workflow holds. Aiko ships the public
half of that key. Before anything is replaced, Aiko checks the signature of the list and then the
hash of the file it downloaded. If either check fails, the file is deleted, nothing is replaced,
and the reason goes into the log. A build that carries no key yet installs nothing at all.

**On Windows** the package comes from GitHub through Velopack, over Aiko's own connection, so the
list of allowed hosts applies to it as well. The checked package is put in place on restart, the
way Velopack installs anything.

**On macOS** Aiko downloads the zip of the release itself, unpacks it into a temporary folder with
`ditto` and swaps the app bundle with two renames. A file the app downloads itself is not marked
with quarantine, so the new version starts without a warning. The old bundle is deleted only after
the new one is in place, and if the swap fails the old one is put back.

For this Aiko may open `github.com`, `api.github.com` and GitHub's two file stores, and nothing
else. Redirects are checked against the same list before they are followed.

## Voice input

Voice input turns what you say in the terminal into text in the field. It's off until you turn it
on, on the **Terminal** page in settings, and you choose who listens.

- **On this computer** (the default). When you turn it on, Aiko downloads the speech model once:
  `ggml-small.bin`, 465 MB, from Aiko's own release on GitHub, and checks its SHA-256 before it uses
  it. The sound is read on your computer and goes nowhere.
- **OpenAI with your own key**. Each recording is sent to `api.openai.com` with the model
  `gpt-4o-transcribe` and your key, and is billed to that key. The page says this next to the
  choice.

What goes with the sound, as a hint to the recogniser: the name of the session's folder, its git
branch, the names of files from the conversation and, if the personality is on in that
environment, the keys of Aiko's memory (the short names, never the facts). On this computer the
hint stays on the computer; with OpenAI it goes with the sound.

**The key.** Aiko keeps your OpenAI key in Windows Credential Manager, as "Aiko: OpenAI key for
voice input". Windows encrypts it for your account on this computer. Aiko never writes the key to
a file, the log or the settings, and shows only its last four characters. **Remove key** on the
page deletes it, and so does removing Aiko.

The microphone is open only while you record: from the first press to the second. Aiko keeps no
recording and no text of it. The sound lives in memory until the words are in the field.

## The token

Claude Code keeps an access token in its own folder. In direct mode Aiko needs that token to ask
about your account. **Direct mode is on Windows only.** On macOS Claude Code keeps the token in the
Keychain instead of a file, and Aiko does not read the Keychain, so there is no direct mode there
and nothing on a Mac ever reads your token. The rules on Windows:

- Aiko reads the token again before every request and keeps it in memory only for that request.
- Aiko never writes the token to a file, a cache, a log or the screen.
- Aiko never refreshes the token and never writes to the credentials file. A refresh replaces the
  refresh token, and that could sign Claude Code out.
- When the token has expired, Aiko leaves it alone. It waits for Claude Code to refresh it and shows
  the numbers it already has, with their time.

Without direct mode, Aiko never touches the token.

## Where Aiko keeps things

- `%APPDATA%\Aiko`: your settings, the list of environments, the random value described above,
  which week and month have already been reported, and the memory file once the memory is used.
- `%LOCALAPPDATA%\Aiko`: the limit numbers from Claude Code, the last direct mode answer, a short
  log, the speech model of voice input in `models` once you turn it on, and, where the personality
  is on, the session state files, the personality plugin and the local marketplace with the skills.
- Windows Credential Manager: your OpenAI key for voice input, if you saved one.
- `%USERPROFILE%\.claude*\settings.json`: one line added by Aiko, with a copy of the original next
  to it, and, where the personality is on, Aiko's plugins and marketplace and the permission line
  for the memory tools. Claude Code keeps its own copies of
  those plugins in its `plugins` folder.
- Your user `PATH`: Aiko's command folder at the start, if you agreed in the setup checklist.
- Your PowerShell profile: the account-switching functions you ticked are turned off, with a copy of
  the profile next to it.
- One value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, if you asked Aiko to start
  with Windows.

Removing Aiko puts the status line back, removes its plugins from Claude Code, takes its folder out
of `PATH`, turns the profile functions back on, removes that value, deletes the OpenAI key from
Credential Manager and deletes both of Aiko's folders.

One file may stay: the memory. When you press **Remove Aiko** in settings and the memory holds
something, Aiko asks whether to keep it, and the answer is "keep" unless you untick it. Then
`memory.json` stays alone in the settings folder (`%APPDATA%\Aiko` on Windows,
`~/Library/Application Support/Aiko` on macOS), and Aiko finds it there if you install it again.
Everything else in the folder goes, the lock file and a `memory.json.bad` copy too. When you
uninstall Aiko from **Installed apps** on Windows, nobody can ask you, so the memory is kept. An
empty memory is not kept. To remove a kept memory, delete that file.

You can also delete the folders by hand at any time, and Aiko starts again with its defaults.

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

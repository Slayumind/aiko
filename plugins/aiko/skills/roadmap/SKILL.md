---
name: roadmap
description: Build and keep a shared roadmap for a team that works with other teams - collect what each domain owner already has (chats, trackers, docs, meeting notes, goal exports), cut it into domains with owners and borders, write each release as one short card (why, what changes for each role, pains, unblocks, acceptance criteria, the few tasks colleagues need), and keep every place that mentions a release in sync - the owner's own tracker, the shared backlog, domain pages and a roadmap page, in whatever tools the team uses (a wiki, a doc, a tracker, a sheet, a generated web page) - and keep one queue of asks to the scarce resource every domain competes for (a team, a budget, an expert), with priority marks. Use when the user says "roadmap", "backlog for the team", "domains", "who owns what", "describe the releases", "sync the domains", "prepare for the planning meeting", «роадмап», «беклог», «домены», «синхронизируй домены», or brings requests from other teams that need an owner and a place.
---

# aiko roadmap

A roadmap is a promise to other people. It works when every reader finds three answers fast: why
this work, what changes for them, and when it is done. Everything else stays in the owner's own
tracker.

**One release is one row. A colleague reads the row, not the task list behind it.**

## 0. Map the roles to the team's tools

This skill names places by their role, not by a product. Before the first write, agree with the
user which tool plays each role, and keep the map in the project's notes:

| Role | What it holds | Examples |
|---|---|---|
| Owner's tracker | every card, briefs, private notes | a personal task board, a private project |
| Shared backlog | one row per release, per domain | a wiki database, a tracker project, a sheet |
| Domain pages | borders, goals, team, how the work runs | wiki pages, shared docs |
| Roadmap page | all domains on tracks, before the meeting | a generated web page, a wiki page, a slide |
| Other teams' tracker | their tasks and labels | an issue tracker |
| Chats | requests, answers, agreements | team channels and threads |

The rules below do not change with the tool. What changes is only how you read and write; tool
notes and traps are in `references/tools.md`.

## 1. Pick the task

| The user wants | Read | Do |
|---|---|---|
| A first picture: "what is going on", "who does what" | `references/sources.md` | section 2 |
| Domains, owners, borders, a process proposal | `references/domains.md` | section 3 |
| Release texts: "describe the releases", one row per release | `references/releases.md` | section 4 |
| A roadmap page or its update | `references/roadmap-page.md` | section 5 |
| "Sync the domains", the weekly update, "is everything current?" | `references/sync.md` | section 6 |
| Priorities: "what goes first", the queue to a team or budget everyone needs | `references/queue.md` | section 7 |
| A post to the group, a colleague's answer to act on | `references/posts.md` | section 8 |
| "When will it be ready", release windows, forecasts | `references/planning.md` | section 9 |

Most work goes in this order. A user may start in the middle; then read the earlier sections
quickly to check that their results exist.

## 2. Collect the context

Before you write anything, gather what owners already said, in their own places: chat channels and
threads with their replies, trackers, docs, sheets, meeting notes, exports of goals. Read the
comments too: people often decide things in replies, not in the post.

Put the facts in one working note: what, who said it, where, when. Then sort them:

- **Duplicates.** The same request in two places becomes one item with two sources.
- **Stale.** A sheet nobody updates is a weak source. Ask the owner which parts are still true.
- **Claims to check.** "Sign-in already works without a button" is checked in the code or the
  product before it goes into a text. A wrong fact in a shared roadmap costs more than a missing one.

## 3. Domains and the process

A domain is an area with one owner, clear borders (in and out) and named sources. Write the process
proposal before the domains: who keeps the shared backlog, who prioritises, who has a veto, who
gets escalations, what agents do and what owners check after them. Details and templates:
`references/domains.md`.

If one domain owner already has good pages (borders, goals, team, rhythm), copy their form for the
other domains. Colleagues read one shape faster than five. Check a borrowed template for words that
do not fit your voice, headings included.

## 4. Release texts

Write each release with the same parts:

1. **Name** in the style `Product: Title & Title`.
2. **Why**: the real reason, in the owner's words. Ask when you do not know it; do not invent it.
3. **What the release shows**: only for a discovery release.
4. **What changes**: per role, one line each.
5. **Pains** and **Unblocks**: two separate lists.
6. **Acceptance criteria**: checks someone can run now, with ticks.
7. **Tasks**: only the ones colleagues need, one line each: `Task: status.`

Plus properties: status, for whom, ready by, blocked by, domain goal, sources.

Run the writing check before the owner sees a text, not after it is written somewhere. Show the
texts **one release at a time** and apply each edit to its whole class in all releases (see the
edit classes in `references/releases.md`). Write to shared places only after the owner's yes on
all texts.

## 5. The roadmap page

Whatever the tool, the page has five jobs: it describes the process; the domains and their owners;
where each domain's current state lives; the milestones as the overview and order of the work; and
the queue to the scarce resource. What good and bad roadmap UX looks like, and the checks before
publishing: `references/roadmap-page.md`.

## 6. Keep every place in sync

A release is mentioned in more places than it seems: the owner's tracker, its row in the shared
backlog, the domain page, the how-the-work-runs page, the roadmap page and any overview page about
the product. One release name everywhere, links both ways, criteria only in the row. Details, the
weekly routine and the inventory check: `references/sync.md`.

## 7. Priorities: the queue to the scarce resource

Order the asks to the resource every domain competes for (a team, a budget, an expert), not the
releases. One list for all domains, a starting order from three questions (holds work in progress,
source priority, due date), priority marks (MoSCoW worked well), sizes from the resource. A capacity
line comes only after the group agrees how it follows the resource's planning. The owner of the
shared backlog changes the order; the veto person settles conflicts. Details: `references/queue.md`.

## 8. Posts and colleagues' answers

A post to the group after an update (thanks, what changed, the owner's statement, the ask per
person), and what to do when an answer shows a class of problems: `references/posts.md`.

## 9. Planning: windows, not dates

When exact dates cannot be promised honestly (AI-driven development, often changing priorities),
each release gets a month window: this month is a promise the domain owner answers for, next month
is a forecast, later is an intent in priority order. Only the owner makes a promise; the agent's
windows for other domains are drafts. A release that waits for the scarce resource cannot be
promised for this month without a visible warning. Details: `references/planning.md`.

## Rules for anything colleagues read

- **Show before writing.** Text that goes into someone else's system (a chat, a shared doc, a
  tracker) is shown to the user word for word first.
- **Say what changed beyond what you showed.** A report has two parts: "as shown" and "beyond what
  I showed, and why". A change of meaning (not only of form) is shown before it is written.
- **The owner's private tracker is never named** in colleague-facing text, not even in a "built
  from" note. Carry its content over.
- **Use what exists.** Labels, fields, statuses and channel names come from the real system. Never
  invent a label because it would be convenient.
- **Requests, not blame.** A list for another team says what is needed, not who was late.
- **Who speaks.** Decisions, ownership and accepting results belong to the owner: "I" in texts that
  are the owner's own words (a release why), the owner's name and role on process pages. The work
  itself is done by the agent, named as a person ("Aiko, my AI agent, does the split in the code"),
  with its own row in a team table. A fact with no actor stays impersonal. Never a bare "we": it
  hides who decides and who did the work. A group agreement of several teams may say "we".
  Search every text for "we / our / us" (and the same words in other languages), headings too.
- **Plain language.** For a shared roadmap: English, short sentences, common words. Use the
  writing check of your setup (`aiko:copy` in Aiko).
- **Say who checked what.** When the user asks "is it all proofread?", mark each text: the writing
  check, a read by hand, read by the owner, or not checked.

## Traps

- **"It is all in sync" from memory.** Before saying it, list every place that mentions the
  releases and say when you last opened each. A place not opened today is "not checked".
- **Overview pages age fastest.** A product page written for colleagues keeps old versions, "ready"
  for things only in the code, and requests to other teams that were answered. Check versions from
  tags and the changelog, requests from the chat, and keep "in the code", "merged" and "live" apart.
- **A bug is not a release.** A single error or a small fix goes into the nearest release about the
  same system, not into a release of its own.
- **A release that crosses a domain border** is cut along the border, and the two halves are linked
  as "blocked by".
- **A task you invented** to make a release look complete is a mistake. Check the owner's tracker,
  open branches and other sessions first; the work may already exist under another name.
- **Decisions outside the release** ("who will host it later") do not go into its tasks.
- **Status from memory.** Before a status goes into a shared place, check it: merged or only on a
  branch, in production or only in the code.
- **Deleting old rows.** Move them to the trash, not a hard delete, and get a clear yes with the
  list first.

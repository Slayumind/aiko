# Planning and forecasting: windows

The page shows releases in month windows (see `roadmap-page.md`): this month is a promise, next
month a forecast, later an intent. This file is about how to set the windows and keep them honest.

## Why windows

Use them when exact dates cannot be promised honestly. Two usual reasons, written on the page:

- work done with AI agents takes very different time on similar tasks;
- the business changes priorities often, and a release gives way to something more urgent.

If neither holds for a team (a fixed scope, a stable team, external deadlines), dates may be fine;
ask before you switch a team to windows.

## Where a window comes from

Mark each window with where it came from, at least in your working note:

| Source | Window |
|---|---|
| The domain owner says it | as said; this is the only source of a promise |
| The owner's own plan with dates (a sheet with weeks, a release calendar) | the month the plan puts it in |
| A hard outside date ("the old CRM is switched off on 1 December") | the month before that date |
| A goal deadline for the quarter | no earlier than the goal's month, usually "later" until the owner says |
| "Design first", "decision first", an open question | later |
| Nothing at all | your guess, shown as a draft |

The agent never sets "this month" for someone else's domain on a guess. A draft window on the page
is labelled as the agent's draft until the owner confirms it.

## When the meaning of a field changes

A month field that was a soft marker ("roughly in December") becomes a promise once the page reads
it as a window. Every value set under the old meaning needs the owner's confirmation. Tell the user
which values came from a plan and which were guesses, per domain, and propose a short line to each
owner.

## Windows and the scarce resource

- Every ask in the queue that holds a release is linked to that release. After any restructuring of
  the backlog (rows merged into releases, rows deleted), check the links again: they break silently,
  and a release then looks free of dependencies.
- A release in "this month" whose ask is not in work at the resource carries a warning. Bring these to
  the planning meeting first: they are the promises that rest on someone else's time.

## Moving and slipping

- A move to another window is written on the card: where from and why.
- A window that has passed with the release not done is shown as such until the owner updates it.
- A release that moves twice is cut into smaller releases instead of being moved again.
- Review windows at each sync: the agent lists passed windows and warnings; the owners decide.

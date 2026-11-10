# The roadmap page

The roadmap page is the one place the whole group opens before the planning meeting. It can be a
generated web page, a wiki page, a doc or a board: the tool does not matter, its jobs do. Notes on
specific tools are in `tools.md`.

## What the page is for

A good roadmap page answers five questions, each in its own part:

| Part | The question it answers |
|---|---|
| **The process** | How do we work: who owns the shared backlog, who sets priorities, who has the veto, the weekly rhythm, what agents do and what owners check after them. |
| **Domains and owners** | Who answers for what: each domain with its owner, its borders (in and out) and a way to reach the owner. |
| **Sources** | Where the current state of each domain lives: the owner's backlog, the tracker filter, the channels. The page is a view of these sources, never a replacement for them. |
| **Milestones** | What is being done and in what order: tracks by domain, releases in done / now / next / later, and what changes for each role. This is the overview. |
| **The queue** | What competes for the scarce resource and in which order. In one group it is the IT team, in another a design team, a budget, one expert or a test bench. The queue holds only the asks that need that resource; the rest of the work lives in the milestones. |

If the group has no scarce resource that several domains compete for, the page has no queue.

## Time windows, not dates

Say on the page why there are no exact dates, if the reasons hold for the team. Two common ones:

- work done with AI agents takes very different time on similar tasks, so dates cannot be guessed
  in advance;
- the business changes priorities often, and a release gives way to something more urgent.

Then each release gets a window, a month, counted from today:

- **this month** is a promise, and the domain owner answers for it;
- **next month** is a forecast;
- **later** is an intent with no date, in priority order.

Rules:
- Windows are columns; the status (in progress, not started) is a badge on the card. A column that
  mixes status and time ("in progress", "next") cannot answer "when".
- The month names in the columns follow today's date; the data keeps the month itself.
- A move to another window shows on the card: where from and why. Trust comes from honest moves,
  not from exact dates.
- A window that has passed while the release is not done is shown as such until the owner updates
  it.
- A release in "this month" that depends on an ask to the scarce resource that is not in work yet
  carries a warning with that ask. Windows and the queue check each other.
- The domain owner sets the window. The agent drafts windows for other owners' domains and marks them
  as a draft.
- A release that moves twice is a reason to cut it into smaller ones, not to move it again.

## Good and bad roadmap UX

Good:
- **One screen tells the state.** Counts and statuses at the top of each part; details open on demand (a panel, a fold), not in place.
- **Every block names its source and date.** "Statuses from the tracker as of 9 Nov" beside the block that uses them. Two sources with two dates show two dates.
- **Roles, not systems.** "What changes for students, tutors, sales" beats "the storage is migrated".
- **A count you show is a filter you offer.** If the page says "Must: 5", a click shows those five.
- **A control sits where it acts and says what it does.** Sorting and resetting next to the filters; "Sort by priority", not the name of a method; an empty field names the field ("Priority", "Size").
- **One look across the page.** Controls, chips and dropdowns in the page's own style; colour means the same thing everywhere.
- **The same rules for every source.** No marks that only make sense for one tracker ("from P0"); the mapping is explained once.
- **Read-only for everyone, editable for the owner.** People who only read see no editing controls.
- **Works on a phone.** One column at 375 px, no sideways scroll.

Bad:
- A copy of the backlog: every task of every domain on the page.
- Numbers and statuses without a date or a source.
- A mechanic the group has not agreed on (a capacity line, a scoring formula) shown as if it were the rule. Keep it switched off until the group agrees.
- Editing controls that work against hidden rows (reordering inside a filtered view).
- Controls at the bottom of a long list, far from what they change.
- Text that hides who decides ("we") or names the owner's private tracker.

## Before publishing

- Rebuild, open the page, use every changed control the way the owner will: open a dropdown, pick a value, pick another one right after, filter, reset.
- If the page keeps shared state, test it with live updates arriving while you edit (see `tools.md`).
- Check a 375 px wide screen for sideways scroll and the console for errors.
- Read the live version first if someone else may have changed it.
- Publishing a shared page is an outward action: the user's yes covers one update. A page that starts keeping shared state needs its own yes.

# Release texts

## One row per release

The shared backlog keeps one row per release. The owner's tracker may have five or fifteen cards
behind it; colleagues need the goal, the change and the date, not every step. A detailed task list
in a shared place is noise: nobody reads it and it goes stale first.

## The template

```
Product: Title & Title — status

Why
Two to four sentences. What is wrong or missing today, and why it matters.
The owner's real reason. If the owner's reason differs from yours, the owner's wins.

What the release shows   (only for a discovery or an experiment)
1. The first question the release answers.
2. The second one.

What changes
- Students: …
- Tutors: …
- Sales / Content team / IT / Staff: …   (only the roles that really change)

Pains
- What hurts today, concrete: a number, a date, an error people see.

Unblocks
- What becomes possible after it: another release, a goal, a team's work.

Acceptance criteria
- [ ] A check someone can run now. One result per line.
- [x] Ticked only after the check, and you know what the check was.

Tasks
- Task: status.   Only what colleagues need: work of other teams, blockers, big steps.
- Task: in progress. One short detail if it helps (ABC-123).
```

Properties of the row: Status (Not started / In progress / Blocked / Done), For whom (roles),
Window (this month, next month or later; see `roadmap-page.md`), Ready by (only a date someone gave), Blocked by (a release or a team's answer), Domain goal, Sources
(channels, docs, tracker keys).

## Writing rules

- **Why first, in plain words.** "The site keeps every file on one server's disk, so it cannot run
  in two copies" beats "improve the storage architecture".
- **Pains and Unblocks are separate lists.** One looks back, one looks forward.
- **What changes is per role and visible.** "Students open their project from their own address",
  not "storage is migrated".
- **Criteria are checks, not wishes.** "A release does not lose an event from the queue" can be
  tested. "More stable" cannot. No calendar observation windows ("a week without errors").
- **Numbers only with a source.** "52 errors in one day" with where it came from.
- **Status is checked, not remembered.** Merged into the working branch, in production, or only on a
  branch: three different statuses.
- **One shape for a task line**: `Task: status.` with tracker keys in brackets at the end. Long dashes
  between a task and its status pile up fast and read as machine text.
- **Who speaks** (see SKILL.md): "I" for the owner's reasons and decisions, the agent by name for
  the work, impersonal for facts. "We use these lessons as a discovery" becomes "I use these
  lessons as a discovery"; "We split them" becomes "I split them; Aiko, my AI agent, does the split
  in the code".

## Walk the owner through the texts

1. Tell the owner the order: what is in work first, then the next ones, domain by domain. Run the
   writing check on each text before you show it.
2. Show **one release at a time**, the full text, with a number ("3 of 13") and a short question at
   the end if something is unclear.
3. An edit from the owner is a **class**. Apply it to every release, not only to the one shown, and
   say that you did.
4. Keep a list of what changes outside the text: a card to move, a release to rename, a criterion to
   add. Do all of it in one pass after the owner's yes on all texts.
5. Before the final yes, check the releases against what is really going on: the open branches, the
   other sessions, the latest messages. Mark the tasks and criteria that are already done.

## Edit classes seen in practice

| The owner says | The class | What to do everywhere |
|---|---|---|
| "Split unblocks and pains" | two kinds of reasons in one list | two lists in every release |
| "This is a bug, not a release" | a release that is one fix | move it into the nearest release about the same system; the release disappears |
| "This is border work" | a release that crosses two domains | cut it along the border; link the halves with "blocked by" |
| "The why is different" | your guess at the reason | ask, then rewrite the why in the owner's words in every release of that kind |
| "We don't decide who will use it" | a decision outside the release | remove it from tasks and blockers |
| "Add what we expect to learn" | a discovery release | add the What the release shows block; keep it only there |
| "Check if it already works" | a claim about the product | check in the code or the product before writing |
| "There is no 'we' here" | a voice that hides who decides | owner for decisions, the agent by name for the work, impersonal for facts; headings too |
| "Is it all current?" | a sync claim from memory | the inventory check in `sync.md`, place by place |

## After the yes

1. The owner's tracker: rename, move cards, add the agreed criteria and the missing cards.
2. The shared backlog: change the schema if needed (title = release), create the rows, then move
   the old rows to the trash (reversible) with the list shown.
3. Links: each card in the owner's tracker points to its release row.
4. The roadmap page: the same texts, the same names.
5. A short report: what changed where, and where you departed from the agreed text and why.

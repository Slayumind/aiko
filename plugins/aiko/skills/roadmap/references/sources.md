# Collect the context

The goal: one working note with every fact the roadmap needs, each with its source. The roadmap is
built from this note, not from memory.

## Where to look

Ask the user which places matter, then read them all. Typical ones:

| Source | What it gives | Watch out for |
|---|---|---|
| The main channel between the teams | agreements, open questions, who answers | decisions in thread replies |
| Direct messages with the people who do the work | real status ("it works", "asked on the 9th") | do not quote private messages in shared text |
| The other team's tracker (Jira and the like) | tasks, statuses, labels | use only labels that exist |
| Shared docs and wiki pages | borders, goals, team pages | pages with no author may be old drafts |
| Sheets with request lists | what colleagues asked for | often stale; ask which rows are still true |
| Meeting notes or a transcript | what was agreed | agreements beat older posts |
| A goals export from HR or OKR tools | the quarter's goals, baseline → target, whose goal | comments and parent goals carry the meaning |
| The owner's own tracker | releases, criteria, blockers | private: carry content over, never name it |
| The code and the repository | what is really done, merged, released | "in the code" is not "in production" |

## The working note

One line per fact: the fact, who, where (a link), when. Keep it out of the shared places; it is
your scratch.

Then go through it:

1. **Merge duplicates.** Same request, two places: one item, two sources.
2. **Mark stale sources.** If a sheet or page has not changed for weeks and contradicts newer
   messages, the newer source wins. Ask the owner about the rest.
3. **Check claims.** Statements about how the product works today ("students sign in without a
   button") are checked in the code or the product. Write down what you checked.
4. **Separate the levels.** A goal (a number for the quarter), a release (a change that reaches
   people), a task (a step inside a release), a bug (a fix). Most confusion comes from mixing them.

## Goals and KPIs

When the user asks how the roadmap connects to goals:

- Take the goals from the real source (an HR export, an OKR page), with baseline, target, deadline
  and whose goal it is. Read the comments under the goals.
- Map each release to at most two or three goals. A release with no goal is fine, but say so.
- Give an honest estimate of how strong the link is. "This release moves attendance" needs a reason;
  "related" is a valid and honest answer.
- Do not copy goal IDs or private "current values" into shared text.

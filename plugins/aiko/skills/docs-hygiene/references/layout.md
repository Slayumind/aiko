# A default document layout

Use it when the project does not describe its own. Offer it to the user before creating any file.

| File | Holds | Lives like this | Limit |
|---|---|---|---|
| `CLAUDE.md` or `AGENTS.md` | what the project is (up to 5 lines), the layout itself, the stack, rules for the agent that only this project has | edited by hand | 120 lines |
| `docs/STATE.md` | where the project is right now, one screen | rewritten whole | 40 |
| `docs/PLAN.md` | open items of the current release | done items are removed | 140 |
| `docs/ROADMAP.md` | future releases, decided without a date, not decided, not doing | a release starts: its part moves to PLAN | 140 |
| `docs/DECISIONS.md` | the decision log: what, why, what it replaces | append-only | 600 |
| `docs/PRODUCT.md` | how the product works now: what it is, its parts, its rules | edited in place; each change also gets a decision | 500 |
| `docs/RESEARCH.md` | facts about the outside world the project does not control | corrected as facts are checked | 280 |
| `docs/DESIGN.md` | design system and voice of the texts | edited as decisions are made | 300 |
| `docs/CHANGELOG.md` | a short entry per release; the "why" lives in commit bodies | new entries on top | none |
| `docs/reference/*` | copies of outside material, old reports and research, read when needed | archive | none |

PRODUCT, RESEARCH and DESIGN are created when there is something to put in them.

Why these limits: the files read in every session (agent rules, state, plan) stay small, so reading
them costs little. The decision log, the changelog and the reference folder are read only when a
question needs them.

**The decision log over its limit.** Older entries move whole to `docs/decisions/D-001..D-NNN.md`, and
the log starts with a link to that file. Numbers never change. This is the one file where going over the
limit is solved by moving, not by finding a better home for the content.

## Where a fact goes

- It is true now and will change soon: STATE.
- Someone has to do it in this release: PLAN.
- It was chosen, and a different choice was possible: DECISIONS.
- It is how the product itself works: PRODUCT.
- It is how a tool, a service or a library behaves: RESEARCH, with the date it was checked.
- It is how things look or sound: DESIGN.
- It is a rule for the agent in every session of this project: `CLAUDE.md` or `AGENTS.md`.

Anywhere else, write a link to that file instead of the fact.

## Adding the layout to a project

Add a short "Documents" section to `CLAUDE.md` or `AGENTS.md` with the table above, trimmed to the
files the project really has. From then on the skill reads the limits from there.

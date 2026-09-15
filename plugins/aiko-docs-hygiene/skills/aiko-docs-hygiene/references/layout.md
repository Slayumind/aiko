# A default document layout

Use it when the project does not describe its own. Offer it to the user before creating any file.

| File | Holds | Lives like this | Limit |
|---|---|---|---|
| `CLAUDE.md` or `AGENTS.md` | rules for the agent, the layout itself | edited by hand | 180 lines |
| `docs/STATE.md` | where the project is right now, one screen | rewritten whole | 40 |
| `docs/PLAN.md` | open items of the current release | done items are removed | 140 |
| `docs/ROADMAP.md` | future releases and undecided questions | a release starts: its part moves to PLAN | 140 |
| `docs/DECISIONS.md` | the decision log: what, why, what it replaces | append-only | none |
| `docs/RESEARCH.md` | facts about the outside world the project does not control | corrected as facts are checked | 280 |
| `docs/DESIGN.md` | design system and voice of the texts | edited as decisions are made | 300 |
| `docs/reference/*` | copies of outside material, read when needed | archive | none |

Why these limits: the files read in every session (agent rules, state, plan) stay small, so reading
them costs little. The decision log and the reference folder are read only when a question needs them.

## Where a fact goes

- It is true now and will change soon: STATE.
- Someone has to do it in this release: PLAN.
- It was chosen, and a different choice was possible: DECISIONS.
- It is how a tool, a service or a library behaves: RESEARCH, with the date it was checked.
- It is how things look or sound: DESIGN.

Anywhere else, write a link to that file instead of the fact.

## Adding the layout to a project

Add a short "Documents" section to `CLAUDE.md` or `AGENTS.md` with the table above, trimmed to the
files the project really has. From then on the skill reads the limits from there.

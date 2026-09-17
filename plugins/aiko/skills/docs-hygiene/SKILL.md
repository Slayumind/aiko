---
name: docs-hygiene
description: Keep a project's working documents honest. Rebuild the state file from facts measured with git and the test run, append a decision to a decision log, or audit the documents for files over their size limit, facts written in two places, contradictions and broken links. Use when the user asks to update the project state, record or look up a decision, clean up or check the docs, or when documents seem to disagree with each other or with the code.
---

# aiko docs hygiene

Project documents rot in one way above all: the same fact gets written in two places, one copy
changes, and now the documents disagree. This skill keeps three habits that stop it.

**One fact lives in one file. Every other file links to it.**

## 1. Find the project's layout first

Look for a section about documents in `CLAUDE.md` or `AGENTS.md` (often called "Documents" or
"Docs"). It names the files, what each one holds and, sometimes, a size limit for each. Follow it.

No such section? Use the layout in `references/layout.md`, and tell the user which files you assumed.
If the project has none of these files, offer the layout; do not create files without a yes.

## 2. Pick the task

| The user wants | Do |
|---|---|
| "update the state", "where are we" | section 3 |
| "write down this decision", "why did we…" | section 4 |
| "check the docs", "clean up the docs", something looks inconsistent | section 5 |

## 3. Rebuild the state file

Rewrite the state file (for example `docs/STATE.md`) whole. Do not append: a state file exists because
two appended status blocks once drifted apart.

Measure every fact yourself. Never copy a number from the old state file or from `CLAUDE.md`.

- Branches, commits per branch, tags: `git log --oneline`, `git branch`, `git tag`.
- Remote: `git remote -v`. Empty means the repository is local only; say so.
- Version: from the project's own version file (`package.json`, `Directory.Build.props`,
  `pyproject.toml`, `Cargo.toml`, `*.csproj` and so on). Name the file you read.
- Test count: **run the tests** and take the number from the output. Do not count test attributes with
  grep: parameterized tests (`[Theory]`, `@pytest.mark.parametrize`, `test.each`) run many cases per
  method, and a grep count comes out far too low.
- Open items and the current step: from the plan file.

Keep it to one screen, about 40 lines: where we are, what is in progress, what blocks the next release,
what is open nearby.

A fact you could not measure is written as "not checked", never as the old value. If what you measured
differs from another document, say it in your reply: the other document is probably out of date.

## 4. Append a decision

The decision log (for example `docs/DECISIONS.md`) is append-only.

1. Read the last entries and take the next free number `D-NNN`.
2. Search the log for the same decision. If it is there, do not add a second one: suggest correcting
   the old entry, or a new entry that replaces it.
3. If the new decision replaces an older one, add "**Replaces.** D-NNN". **Never edit or delete the old
   entry**: the history of decisions is the point of the file.

Format:

```
## D-NNN · YYYY-MM-DD · Short title

**Decision.** One or two sentences: what exactly was decided.
**Why.** One to three sentences: the alternative and why it is worse.
**Replaces.** D-NNN (only when true; otherwise leave the line out)
```

Write the entry in the language the log already uses.

If the reason or the replaced decision is not clear from the conversation, ask. An entry without a
"why" is useless: a year later the "why" is the only part anyone reads.

Last, check that the same fact did not also land in the plan or the research notes.

## 5. Audit the documents

Report first. **Change nothing until the user says so.**

1. **Size limits.** Count lines (`wc -l` or reading the files) and compare with the limits from the
   project's layout. A file over its limit is not a reason to shorten sentences. It means the file
   collects something that belongs elsewhere, most often in the decision log. Name those parts.
2. **Duplicates.** Find statements made more than once, across files and inside one file. Match
   meaning, not exact strings: measured numbers, paths, key names, rules of behaviour, habits of
   third-party tools. For each: where the copies are, which copy should stay by the layout, and whether
   the copies already disagree.
3. **Contradictions.** Two documents that say different things. Check especially:
   - numbers: commits, tests, versions, measurements;
   - decisions that were replaced but are still quoted as current;
   - things marked "not done" or "out of scope" that are already built and working;
   - promises in public documents (README, privacy, security notes) against what the code does.

   Check against the code and git, not only against other documents: usually the document is out of
   date, not the code.
4. **Links.** Every link between documents points to a file and a heading that exist.

Report as a table of sizes, then lists of duplicates, contradictions and broken links. Each item says
what you suggest doing.

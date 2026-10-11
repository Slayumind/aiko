# Task cards, tickets and briefs

A card in a tracker has two readers: a person who scans a board, and whoever does the work.
The first one decides in three seconds whether to open it. Write for that reader first.

Write in the language the owner of the tracker uses. Do not switch a board to another language
on your own. Names of products, features and tools keep their spelling: "Blender", "Pull Request".

## Title

- Starts with a verb: "Add shapes to the editor", «Добавить фигуры в редактор».
- Reads on its own in a list. "Invites" says nothing; "Fix the invite link after 24 hours" does.
- Up to about 50 characters. No project code, no type ("Bug:", "Idea:"), no tags in brackets:
  the tracker shows these in their own fields.
- Names the thing, not a figure of speech. "Add summaries to the dashboard", not "Teach the
  dashboard to write summaries".

## Description

One or two plain sentences, then a checklist.

- **The sentences** say why the card is there and what a person must know to start: what is
  broken, what was decided, what the card waits for. Say them aloud. If you would not say a
  sentence to a colleague, rewrite it.
- **The checklist** has one result per line, and each line can be answered "yes" or "no".
  "Export works" cannot be checked; "A 20 MB file exports as WebP" can.
- **No headings and no bold labels** ("Goal:", "Scope:", "Done when:") in a card of ten lines.
  The two sentences are the goal, the checklist is the definition of done.
- **Few references**: a decision number, a linked card, one or two main files. A list of twelve
  paths and function names belongs to the brief, not here.
- A card with nothing to add has an empty description. Do not fill it for the sake of form.

Before and after:

```
**Goal.** The PSD file opens with its layers and saves back.
**Read first.** ADR-12; `ag-psd` 31.0.2 was chosen for reading and writing.
**Done when**
- [ ] A PSD with raster layers opens as layers
```

```
A PSD opens with its layers and saves back. We read and write with ag-psd 31.0.2;
the decision is ADR-12.

- [ ] Raster layers open as layers
- [ ] The saved file opens in Photoshop
```

## A brief for an agent

An agent needs more than a person at a board: what to read first, what is ruled out, the scope,
how to check, rollout and rollback, safety rules. That is a brief. It may be long and it may have
headings, because it is read once, from top to bottom, by someone who will act on every line.

Keep it out of the description. Put it into the first comment of the card, or into a linked
document, and say so in the description: "The full brief is in a comment." Then the board stays
readable and the brief stays whole.

## A release or a milestone

- The goal is one sentence about what becomes true: "Files are kept in S3 buckets, not on the
  disk of one server."
- An acceptance criterion is one line and one result someone can check. Tick it after the check,
  and say what the check was.

## Shortening a card someone else wrote

- Keep every number, version, date and reference exactly as it is.
- Keep the count and the order of checklist lines, and their ticks. Shorten the wording, not
  the list.
- Move what you cut into a comment before you cut it. A shorter card must not lose a fact.
- Count the result: if a card is still long, say why (seventeen real lines) instead of cutting
  a fact to fit a limit.

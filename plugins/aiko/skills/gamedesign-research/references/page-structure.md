# Page structure

The page is a long read with a table of contents. Number the sections (§0, §1...) because people
refer to them in discussion.

## Header

- Eyebrow: "Design research · before code".
- Title: the mechanic, not the project.
- A short definition list: status, date, what the research is based on (the user's design doc and
  code version, if any), scope in one line.
- Table of contents with the number of games per tier.

Use the project's design system if the project has one (tokens, fonts, colours). Otherwise a quiet
dark or light reading page: one text face, one mono face for numbers, one accent colour.

## §0 Starting point (only with a project)

- What the project has already decided about this mechanic, as a short list.
- What exists in the code: the types, the entry points, how a step is computed today.
- One callout with the assumption that simplifies everything, if there is one.

## §1 Taxonomy

- A table: family · how it decides · example games. 6-12 families.
- Below it, "patterns that repeat almost everywhere": 5-10 items, each with named example games.

## §2 References

### Tier 1 card

```
<article class="card ref" id="ref-<slug>">
  head: title · years and platforms · family tag
  1-2 figures: <img src="shots/<slug>-<n>.jpg" width height loading="lazy" alt="what is on screen">
               <figcaption>what to notice</figcaption>
  definition list:
    How it works   — the formula of one step, one or two lines
    Interface      — how it is read and edited
    Input          — gamepad / touch notes, if relevant
    Progression    — how it grows
    Take           — what is worth copying
    Avoid          — what hurt players
    Sources        — links
</article>
```

### Tier 2

A card with an optional small figure and one paragraph: name, year, the twist, the lesson, source.

### Tier 3

One table: game and year · family · what matters (for the project, or in general).

Mark unconfirmed facts with "not verified" right where they appear.

## §3+ Stands (added in part 2)

One section per stand, each in this order:

1. The formula of one step in one line.
2. "How one step is computed": numbered list, about 10 items.
3. Vocabulary table (condition or action · cost · effect) and unit or object stats.
4. The stand itself.
5. A worked example of the first steps, and "try this" with 2-3 experiments.
6. A collapsed block "what changed in the port, and sources".

## Comparison

A table of 8-12 design choices × the tier-1 systems or the stand systems. Rows are decisions a
designer has to make (unit of a rule, target choice, fallback when nothing fits, resource, timing,
reactions, the most common player complaint, input). After stands exist, add "what the stands
showed" in 3-5 points.

## Open questions

6-10 questions the user has to answer next, each one sentence, ordered from the one that blocks
the most.

## Checks before hand-off

- Screenshot the page with a headless browser at desktop width and at phone width (use a page with
  a 390 px iframe if the browser cannot go that narrow). Look at the images, not at the code.
- Every image loads, every link has a real URL, no section is empty.
- Publish images and scripts as separate files next to the page, referenced by relative paths.

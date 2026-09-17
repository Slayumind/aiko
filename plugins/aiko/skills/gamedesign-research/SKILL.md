---
name: gamedesign-research
description: Research one game mechanic across 30-40 games and turn it into a large illustrated review page, then build interactive stands on request so the user can feel how each system plays. Use when the user wants to study how other games solve a mechanic (party AI tactics, crafting, stealth detection, deck building, dialogue systems, economy, anything), asks "which games do X", wants references before designing a feature, or wants to play with a mechanic before coding it.
---

# aiko gamedesign research

A mechanic is easier to design after you have seen thirty games solve it. This skill finds those
games, reads how each one really works, shows it with screenshots, and then lets the user touch
two or three of the systems in small working stands.

The work has two parts. Always do the review first. Build stands only when the user picks the
systems they want to feel.

## Part 1. The review

### 1. Pin the brief

Ask before you search. Keep it to one message:

- **The mechanic** in one sentence, and where it ends. "Party AI rules" is a mechanic; "combat" is
  too wide.
- **Near cousins to include.** Games where the mechanic is a side system often teach the most.
- **The user's project**, if there is one. If the working directory holds a game project, read its
  design docs and the code around the mechanic first, and say what you found. Without a project,
  the review is pure research and skips the "for your project" parts.
- **Platforms and input** that matter (gamepad, touch, mouse).
- **Language of the page:** the user's language unless they say otherwise.

### 2. Find the games

Aim for 30-40 games in three tiers. Split the search into 2-4 clusters that look at the mechanic
from different sides, and run one research subagent per cluster in parallel when subagents are
available. Read `references/research-brief.md` for the cluster split, the agent prompt and the
fields every game needs.

Rules that do not bend:

- **Facts come from sources, not memory.** Official docs, wikis, patch notes, developer posts,
  papers, long guides. Mark anything you could not confirm as "not verified".
- **Every claim about a system has a source link.**
- **Respect site terms.** Skip sites whose terms forbid AI use or scraping (for example
  gameuidatabase.com). Do not log in, do not pass age gates, do not work around bot checks.
  If a site blocks you, take another source.
- **Keep a "dropped and why" list.** It shows the user what you looked at.

### 3. Collect screenshots

Screenshots are embedded in the page: download, check with your own eyes, re-encode, ship next to
the page. Read `references/images.md` for sources that work, the download script and the size rules.
The scripts live in `scripts/` under this skill's base directory, shown when the skill loads.
`fetch_image.py` needs Python 3.10+ and Pillow (`pip install pillow`); `wiki_fetch.py` needs only
Python. If Pillow is missing and the user does not want it installed, save originals by hand and
re-encode them with any tool at hand.

- One or two images per tier-1 game, showing **the mechanic's interface**, not box art.
- Open every image before you use it. Delete images that do not show the mechanic.
- Every figure has alt text that describes what is on screen, a caption that says what to notice,
  and the source in the card.
- Tell the user once, before publishing: the images belong to their owners, the page embeds them
  for private study, keep the artifact private.

### 4. Build the page

Read `references/page-structure.md`. In short:

1. **Starting point:** what the user's project already decided (skip without a project).
2. **Taxonomy:** families of solutions with example games, then patterns that repeat everywhere.
3. **References in three tiers.** Tier 1: full cards with screenshots; tier 2: a paragraph each;
   tier 3: one table row each.
4. **Comparison** of design choices across the strongest systems.
5. **Open questions** for the user's project, or for anyone designing this mechanic.

Publish as an artifact with the images and scripts as separate files when the environment has
artifacts (for example Claude Code with artifacts on, or claude.ai). Otherwise write a folder with
`index.html`, `styles.css` and `shots/` and open it in a browser.
Check the page with a screenshot before you hand it over, at desktop width and at phone width.

End the review by offering stands: name 3-5 systems from tier 1 that differ the most, and ask the
user to pick 2-4.

## Part 2. Stands on request

A stand is a small working copy of one game's system: the user edits the rules and watches what
happens, step by step. Read `references/stands.md` before writing any stand code.

- **Rules come from sources.** Write the rules for each game as a spec with links first: what one
  step computes, the vocabulary (conditions, actions, costs), and what happens when nothing fits.
  Mark what you had to guess.
- **One shared sandbox** for all stands, so the difference between games is the rules, not the
  scene.
- **Pure logic first, checked in node**, then the UI. The same file runs in the browser and in node.
- **Deterministic**: no random numbers unless the game's system is about randomness; then use a
  seeded generator and show the seed.
- **Every step explains itself**: which rule fired, which ones did not and why.
- Each stand gets a "try this" note with 2-3 experiments that show what makes the system special.

Add the stands as new sections of the same artifact, then extend the comparison with what the
stands showed.

## Hand-off

Say in plain words: how many games made it into each tier, what was dropped, which facts stay
unverified, which images could not be found, and what you would test next.

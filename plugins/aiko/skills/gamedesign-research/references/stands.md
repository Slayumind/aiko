# Stands

A stand lets the user change the rules of one game's system and watch what happens, step by step.

## Before code: a rules spec per game

For every system the user picked, write a short spec from sources (a research subagent per game
works well):

- **One step, numbered.** What is read, in which order, what is chosen, what happens when nothing
  fits.
- **Vocabulary.** Conditions, targets, actions, their costs and effects, with the game's own names.
- **Presets** the game ships with.
- **Port decisions.** What the shared sandbox forces you to change, and why.
- **Sources** for every rule, and "guessed" next to anything that is not in them.

Show the specs to the user before building if any guess changes how the system feels.

## Shared sandbox

All stands share one small scene so that only the rules differ. Keep it tiny and readable: a
small grid or arena, 1-4 actors per side, simple stats, no hidden randomness. Pick the scene
from the mechanic: a hex arena for combat tactics, a shop with a few customers for economy, a
room with guards for stealth, a hand of cards for deck building.

## Files

```
sim-core.js      pure logic, no DOM: the scene, geometry, the step loop, frames and traces
rules-<game>.js  one per game: vocabulary, the step function, SETUP, PRESETS
ui-common.js     drawing (SVG), option pickers, keyboard navigation, the step player
editors.js       one rules editor per game; mountAll() starts every stand on the page
test-run.js      node runner: prints every step and trace for a game
```

Every logic file uses the same wrapper so it runs in both places:

```js
(function (root) {
  "use strict";
  // ... logic ...
  const api = { runSimulation /*, ... */ };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.SimCore = api;
})(typeof globalThis !== "undefined" ? globalThis : this);
```

## Order of work

1. `sim-core.js` and one `rules-<game>.js`.
2. `node test-run.js <game>` — read the printed steps. Fix the rules until the log matches the
   spec and the sources.
3. The UI for that game, then the other games.
4. Screenshot the page, click through every editor, check phone width.

## The step player

- Steps 0..N with play, pause, back and forward, and a scrubber.
- Each frame shows the state and a log of what every actor did.
- The trace for the selected actor lists every rule checked: fired ✓, failed ✗ with the reason,
  skipped ⊘, not reached —, and follow-ups ↳.
- Death, success or end states are marked on the scrubber.

## Editors

- Edit rules the way the game does: numbered rows, condition cells, drop-down vocabularies,
  presets, on/off per row. Keep the game's own words.
- Keyboard and gamepad-style navigation with arrow keys if the game is played on a pad.
- Any edit re-runs the simulation at once. Keep the current step when possible.

## "Try this"

Under every stand, 2-3 experiments that take under a minute and show what is special about the
system, with the expected result. Example: "Move the healing row below the attack row and watch
step 4: the healer never heals, because the first matching row wins."

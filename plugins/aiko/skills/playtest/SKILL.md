---
name: playtest
description: Let an agent play a game and prove a change with numbers instead of a feeling. Turns player feedback into something measurable, adds a read-only view of the game state that tools can ask, named test scenes as repeatable starting points, before and after tables, a ladder of agents from one that does nothing to a strong search that measures how much skill matters, and journey tests that drive the real controls. Works with any engine; recipes for the web, Unity and Godot. Use when a game "feels" slow, floaty, stuttery or wrong, when a level is too easy, too hard or unfair, when one strategy wins everything, before and after a performance or game feel change, when a bug needs a long trip through the game to reproduce, or when setting up testing for a game project.
---

# aiko playtest

An agent cannot feel a game. It can read the game's state, put the game into the same situation again
and again, and measure. This skill builds those three things, then uses them to answer the player's
feedback. The feel itself stays the player's call: the skill makes sure the next try is worth their time.

Engine recipes: `references/engines.md`; the ladder of agents: `references/ladder.md`. A project skill
adds its own scenes, paths and numbers; it does not repeat this method.

## 1. Turn feedback into a question you can measure

The player describes an experience. Keep their words, then write down what would be true in the game
if they are right.

| The player says | Measure |
|---|---|
| "the jump feels floaty" | time from jump to landing, peak height, gravity during the fall |
| "it stutters when I land" | frame time around landing: the worst frame, not the average |
| "enemies ignore me" | time from entering their range to their first action; their state per tick |
| "loading takes forever" | time from the request to the first playable frame; work thrown away |
| "the far terrain pops" | the same camera path, screenshots at fixed points, the level of detail per tile |
| "this level is too easy" / "too hard" | win rates of the ladder of agents (section 5); the moment of the first win |
| "one strategy wins everything" | win rates of agents that play only that strategy against the rest of the ladder |
| "nothing I do matters" | the depth: the smallest gap between neighbours on the ladder |

If you cannot name a number or a state, ask the player one short question before you touch code.
Describe the goal as an experience ("everything I can see should be reachable"), not as an
implementation. The implementation is your proposal, and the player can reject it.

## 2. Add a state surface

One read-only object that tools can query while the game runs. Without it every test guesses from pixels.

It holds the player's position and mode, the current scene and seed, loading or streaming status, and
counters: frame time, draw calls, triangles, live entities, queued, finished and discarded jobs.
Count how often each game rule fired (a pickup, a hit, a door, a trade): a rule that never fires in
any test is dead weight or a bug.

Reading it changes nothing and costs almost nothing. Names stay stable, and there is one entry point.
It exists only in development builds, or behind a flag that release builds do not have. Give it a
`snapshot()` that returns plain data such as JSON: a snapshot is easy to compare and to log, a stream is not.

If the game's logic can run without drawing, add a **forward model** next to it: `copy()` returns a
full copy of the state, `advance(action)` moves the copy one step. That is what lets a search agent
play (section 5). Turn-based games and games with a separate simulation usually have it almost for
free; a physics-heavy action game may not, and then the ladder stops at random play.

## 3. Name the test scenes

A named scene is a starting point you can load in one call: a name, a seed, a position, a state and
the time of day. Nobody should walk across the world to test a landing.

- Every reproduced bug gets a scene. The scene name goes into the report and the commit body.
- Make it deterministic: fixed seed, fixed time step if the engine allows it, randomness from the seed.
- Keep a short list in the project: `orbit`, `descent`, `landing`, `crowded-room`, `boss-arena`.
- For a game on a grid, a scene can be text: a legend of one character per thing, the grid, and the
  same scene as a list of positions (`crate at (2,3)`). Give it an end a program checks
  (`all crates on targets`). The form is in `aiko:gamedesign-research`, `references/rules-sheet.md`.

## 4. Measure before and after

Change nothing until you have the "before" numbers.

1. Load the scene. Wait until loading is done (the state surface says so), then warm up for a few seconds.
2. Record for a fixed time or a fixed path. Run at least three times; report the median and the worst run.
3. Change one thing. Run the same scene the same way.
4. Report a table:

| Measurement | Before | After |
|---|---:|---:|
| Draw calls | 119 | 77 |
| Worst frame, ms | 48.2 | 21.7 |
| Discarded terrain jobs | 6074 | 13 |

Measure different problems separately. Frame time, work thrown away and a visual error each need their
own numbers: a change can fix one and hurt another. A number that got worse is reported, not hidden.

Compare runs only on the same machine in the same mode. A headless browser or an editor play mode has
other timings than a real build; say which one you measured.

## 5. Let a ladder of agents play

A person is the judge of feel, but not of whether skill matters. For that, let agents of known
strength play the same scene many times: one that does nothing, random, greedy one step ahead, and a
search with a small and a large budget. Read `references/ladder.md` before you build them.

1. Run every agent on every variant at least 30 times; the finalists about 100 times.
2. Compare win rates, not scores. Put the runs in a CSV and run
   `balance.py depth runs.csv --ladder random,greedy,search --floor nothing` from `aiko:balance`.
3. Read the **depth**, the smallest gap between neighbours: the game is deep when every step up in
   skill wins more.
4. Check the guards: the floor never wins, the game can be won and lost, no instant win, most rules
   fire.
5. Report: the table of win rates with their noise, the depth, and the guards that failed.

What an agent finds hard is not what a person finds hard. The ladder rules out broken variants; the
player picks between the rest.

## 6. Journey tests

A journey test plays a short path with the real controls: press, hold, release, move the stick. At
checkpoints it records the position and state, and it checks ranges, not exact values.

- Use the engine's own input simulation, not direct calls into game code: the bug is often in between.
- Keep them short (under a minute) and name them after the experience: `jump-onto-ledge`,
  `descend-to-landing`.
- Pure logic (damage formulas, generation, pathfinding) is tested with ordinary unit tests. Journey
  tests are for what only shows up when the game runs.

## 7. Screenshots only for visual questions

A screenshot proves that something was drawn. Use it for seams, pops, flicker and colour: the same scene,
the same camera, the same frame, before and after. Say what you saw. Logic and timing are proved by the
state surface and the numbers.

## 8. Report to the player

1. The feedback in their words, and what you measured.
2. The scene and how to load it.
3. The before and after table, and the ladder's win rates when you ran it.
4. What changed, in one or two sentences.
5. **What to try by hand**, and what should feel different. The player decides whether it does.

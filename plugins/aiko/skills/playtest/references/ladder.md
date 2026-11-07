# A ladder of players

Researchers of the General Video Game AI competition (GVGAI) judged hundreds of games without
people. They let a set of agents of known strength play, from one that does nothing to a strong
search, and looked at the gaps between them. A game where more skill wins more is worth a person's
time. A game where doing nothing wins, or where every agent wins the same, is broken. This page is
the method, with the traps they found.

## The rungs

| Rung | What it does | What it tells you |
|---|---|---|
| Floor | does nothing, every tick | if it wins or survives long, the game plays itself. In games made by people it almost never wins |
| Random | a random legal action each step | the "no skill" baseline |
| Greedy | tries each action one step ahead and takes the best by score or a simple heuristic | a weak player |
| Search, small budget | breadth-first search, A*, or Monte Carlo tree search (MCTS) with a small budget | a medium player |
| Search, large budget | the same search with 2-4 times the budget | a strong player |
| Human-like | a search that repeats its last action and pauses between changes | a check that a level does not need superhuman reactions |

The same search with different budgets gives the cleanest ladder: one GVGAI study used MCTS with 175,
350 and 700 iterations per move. Persona agents are useful next to the ladder: an explorer that goes
where it has not been, and a curious one that seeks interactions it has not seen yet.

Search needs a forward model: a way to copy the game state and run it ahead without drawing
(`SKILL.md`, "Add a state surface"). Without one, only the floor and random rungs work, through the
real controls.

## Depth

Depth is the **smallest** gap between neighbours on the ladder, by win rate:

```
depth = min(greedy - random, small search - greedy, large search - small search)
```

A big gap between the strongest and the weakest agent alone says little. Generators that maximised
that one gap produced games where only search mattered, and every lower step was the same. The
smallest gap rewards a game only when every step up in skill counts. Two studies used it as their
target (Kunanusont et al. 2017; Khalifa et al. 2017).

Compare **wins, not scores**. Scores differ in scale from game to game and from level to level, even
after normalising.

`aiko:balance` has a calculator: `balance.py depth runs.csv --ladder random,greedy,search --floor
nothing` gives every variant's win rates with their noise, the depth, and the guards below.

## Guards

Every optimiser that chased depth alone found a hole in it. A game froze all moving things, an enemy
did nothing, a game was "won" by not playing. Check these on every variant:

- The floor never wins, and survives at least a few dozen steps.
- The game can be won and can be lost.
- No win in the first moments: under 50 ticks, 200 steps or 500 frames in the studies, depending on
  the game.
- Most rules fire at least once over the runs. A rule that never fires is dead weight or a bug.
- Nothing leaves the playing field, nothing freezes for good.

## Noise

Random agents and random games make every win rate a sample. Run each agent at least 30 times per
variant. Before choosing a final variant, run the finalists about 100 times: small samples settled
early on the wrong answer in the studies. Report the margin next to every rate, and do not read a gap
that is smaller than the noise.

## What the ladder cannot tell

- **Difficulty for an agent is not difficulty for a person.** Puzzles and long plans are hard for
  search and easy for people. A level where you hold "right" for a long time stumped tree search.
  Reflex tests are the other way round.
- **Depth filters out the bad; it does not find the good.** In the studies the ladder told "not
  completely bad" games from terrible ones. It did not find fun ones. How long a game lasts and how
  varied the actions are did not tell agent skill apart either.
- **People decide at the end.** Ask players to compare two versions and pick one. The studies used
  such pairs (25 players in one, 161 comparisons in another), not ratings on a scale.

## A language model as a player

An agent like you can play too, but it is a weak player:

- On 118 GVGAI games a small model won about 10% of levels.
- Search agents beat language models.
- Models swapped rows and columns, flipped up and down, and lost count past about 25-30 moves.

Use it this way:

- **For order, not for numbers.** A model's win rates follow the order of difficulty that people
  feel: in Slay the Spire, GPT-4 ranked the first act's bosses with r = 0.87 against human win
  rates. Its absolute win rates are not human ones. Check the order with a small pilot of people.
- **Give state as text.** A grid plus a list of positions ("crate at (2,3)") and the neighbouring
  cells in words. Screenshots made models worse in two benchmarks.
- **Paths and counts from code.** Let search find the path; let the model choose between options.
- **Equal strength on both sides.** When two agents play each other to balance a symmetric game, use
  the same model on both sides. In one study, matched models reached 51/49 and mixed sizes gave 6/94.

## Sources

- Perez-Liebana et al., GVGAI framework: https://arxiv.org/abs/1802.10363
- Nielsen et al., relative algorithm performance profiles: https://www.kmjn.org/publications/GVGprofiles_Evostar15.pdf
- Khalifa et al., level generation: https://www.um.edu.mt/library/oar/bitstream/123456789/81555/1/General_video_game_level_generation_2016.pdf
- Khalifa et al., rule generation: https://arxiv.org/abs/1906.05160
- Kunanusont et al., N-Tuple Bandit for game improvement: https://arxiv.org/abs/1705.01080
- Liu et al., skill depth: https://arxiv.org/abs/1703.06275
- Bontrager et al., matching games and algorithms: https://cdn.aaai.org/ojs/12884/12884-52-16401-1-2-20201228.pdf
- Anderson et al., deceptive games: https://arxiv.org/abs/1802.00048
- GVGAI-LLM: https://arxiv.org/html/2508.08501v2
- Xiao and Yang, LLM agents and difficulty: https://arxiv.org/html/2410.02829v1
- RuleSmith: https://arxiv.org/html/2602.06232v1
- lmgame-Bench: https://arxiv.org/html/2505.15146v2

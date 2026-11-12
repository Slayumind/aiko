# Methods behind the numbers

Read this when the user wants the maths, or when `balance.py` does not fit the game.

## Finding a cost curve from existing items

When a game already has items, the curve can be read from them (Schreiber, Level 3):

1. Write every item as costs and benefits in the same units.
2. Find two items that differ in one thing only. The difference in cost is the price of that thing.
3. Repeat until each stat and ability has a price. Items that do not fit are either the outliers you
   are looking for, or a sign that some benefit is missing from your list.
4. A limited benefit ("only against dragons", "only once per turn") is a smaller benefit, never a cost.

The curve does not have to be a straight line. If cost rises faster than power, big items are
expensive and the game leans to many small ones; if it rises slower, one big purchase wins. Pick the
shape on purpose.

`balance.py audit --derive` is the simplest form of this: it takes the price of each stat from items
that give only that stat.

## Situational value

An ability that works only sometimes is worth its effect times how often it works:

```
value = base x (1 - share) + base x effect x share
```

Double damage (effect 2) in 10% of fights: 0.9 + 0.2 = 1.1, so 110% of the base price. Get the share
from the game's content (how many fights have dragons), not from a guess. If the player can choose
when the situation comes up, the share is higher than the content suggests.

## Progression curve

For step `k` (0 for the first item), with growth `g`:

```
damage(k) = first damage x g^k
cost(k)   = price of one anchor unit x damage(k), rounded to the chosen step
```

Round the displayed damage to whole numbers, but compute the cost from the exact value, so rounding
errors do not add up along the curve.

## Relative anchor

For choices that should be equal at the start:

```
total(class) = sum of stat x weight
```

With weights of 1 this is a plain sum. Weights come from section 2 of the skill: what one point of the
stat is worth in the anchor. A gap from the median of more than a few percent is worth a look; a gap
inside it is not proof of balance.

## Flow patterns

The internal economy patterns of Adams and Dormans, *Game Mechanics: Advanced Game Design* (2012),
appendix B. The list here was read from a secondary summary of the book; check the book before you
quote it.

| Pattern | Shape | Watch for |
|---|---|---|
| Static engine | a steady flow of a resource | the player has nothing to decide about it |
| Dynamic engine | spend the resource to raise its own flow | runs away without friction |
| Converter engine | two converters feed each other and make a surplus | the surplus decides the game |
| Engine building | building and tuning the engine is the main play | a dominant build |
| Static friction | a steady drain | grinds the player down when flows are low |
| Dynamic friction | a drain that grows with the player | the player feels punished for success |
| Stopping mechanism | each use costs more or gives less | the last uses feel worthless |
| Attrition | players drain each other's resources | the first to fall behind never catches up |
| Escalating challenge | the opposition grows with progress | a wall when growth outpaces the player |
| Escalating complexity | more things to manage over time | the player loses track |
| Arms race | players invest against each other | spending without progress |
| Slow cycle | a resource that matters on a long rhythm | the player forgets it |

## Depth from a ladder of agents

For numbers without a price (speed, spawn rate, a cooldown), the target is skill depth: agents of
known strength play every variant, and the depth is the smallest gap between neighbours on the ladder
by win rate (Kunanusont et al. 2017, https://arxiv.org/abs/1705.01080; Khalifa et al. 2017,
https://arxiv.org/abs/1906.05160). The noise of a win rate p over n runs is shown as
1.96 x sqrt(p(1-p)/n), and a gap below the sum of both noises is not read. Raising one gap alone
(strongest against random) let the search pick games where only the strongest agent mattered.

## When the numbers stop

Stop computing and say so when:

- an ability changes how the game is played (flying, invisibility, a free turn), not how much;
- the value depends on what the opponent does;
- options are meant to be different in kind (asymmetric factions).

Then compare by play: named test scenes, win rates, time to kill in a fixed encounter. `aiko:playtest`
sets that up.

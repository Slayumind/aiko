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

## When the numbers stop

Stop computing and say so when:

- an ability changes how the game is played (flying, invisibility, a free turn), not how much;
- the value depends on what the opponent does;
- options are meant to be different in kind (asymmetric factions).

Then compare by play: named test scenes, win rates, time to kill in a fixed encounter. `aiko:playtest`
sets that up.

---
name: balance
description: Balance the numbers of a game from one anchor resource - find the resource everything else can be priced in (usually health or the win condition), price items, cards and abilities in it, build progression curves with rounding and hand-made spikes, compare classes by a relative anchor, choose the scale of numbers so there is room to grow, and map how systems feed each other. Includes a curve, audit and class budget calculator. Use when setting the first numbers of a game, pricing a new item, card or ability, when a table of weapons, enemies or classes "feels off", when one option makes others useless, or before adding content to a game that is already out.
---

# aiko balance

A game has dozens of numbers: damage, armour, mana, price, experience. Compared in pairs, they give
more exchange rates than anyone can keep in their head. **Pick one resource, the anchor, and price
everything else in it.** Then every question becomes the same question: how much anchor does this give
or save?

The method comes from *Game Balance* by Ian Schreiber and Brenda Romero (chapter 5, "Finding an
Anchor") and Schreiber's course *Game Balance Concepts*. Sources and real cases are in
`references/examples.md`; the maths behind cost curves is in `references/methods.md`.

## 1. Find the anchor

Start from how the game is won or lost. The resource that ends the game is usually the anchor: health
in an RPG or a shooter, victory points in a board game, lives in a platformer.

Check it in pairs. For every other stat, ask: can it be turned into the anchor?

- Armour means the enemy takes more hits to kill you, so it saves health.
- Damage means enemies die sooner and hit you fewer times, so it also saves health.
- Mana heals (health back), hurts enemies (health saved) or buffs and debuffs (health saved).

If almost everything turns into one resource, that is the anchor. If two resources both pass, pick the
one the player feels most, and write the choice down.

When items have no price (a skill-based game, a shooter where weapons lie on the floor), the anchor
still works: health taken from the enemy per second, per shot, per clip. Then weapons are kept apart
not by bigger numbers but by trade-offs: range, reload, ammo, risk to the user, where on the map the
weapon lies. The goal is that an old weapon stays useful after the player finds a new one.

Time is a useful second anchor in action games: time to kill an enemy, time to be killed. It is our
own working rule, not a term from the sources.

## 2. Price everything in the anchor

For each item, write one line: what it gives, and how much anchor that is worth in a typical fight.
Say where the numbers come from; "typical fight" must be a real encounter from the game, named.

- **Abilities** on top of the base stat get a price too: a percent on top of the base price, or a
  change in another number (rarer, longer cooldown).
- **A situational ability** is worth its effect times how often the situation comes up. Double damage
  against dragons, when dragons are 10% of fights: 0.9 x 1 + 0.1 x 2 = 1.1, so 110% of the base price.
- **Look at similar games first.** If every game in the genre prices an ability the same way, there is
  usually a reason; find it before you do something else. If each game does it differently, compare
  what each choice does to play. `aiko:gamedesign-research` does this research.
- When you are not sure, ship an item a little weak. A weak item is ignored; a strong one makes the
  others useless.

## 3. Build the curve

Keep the inputs apart from the results: price of one unit of the anchor, growth per step, the first
item's range. Everything else is computed, so a change to one input rebalances the whole table and no
number is typed by hand by mistake.

```
python "<skill base directory>/scripts/balance.py" curve --anchor-price 4 --growth 1.3 --min 3 --max 6 --steps 8 --round 5 --name Axe --spike "Old axe:3:6:20"
```

The base directory is shown when the skill loads. For those inputs: Axe 3-6 costs 25, Axe +3 7-13
costs 55, Axe +7 19-38 costs 150. `--round 5` rounds prices to a multiple of 5, because players do
sums in their head. `--basis min` or `avg` prices by the low end or the middle instead of the top.
`--csv` writes the table for a spreadsheet.

**Add spikes by hand.** A curve that only goes up at the same speed is boring: every fight feels like
the last one. A spike is an item that leaves the curve on purpose: a rare find far too early, a shop
item for a player who saved up. In the example, "Old axe" hits 6-20 right after Axe +2 and costs 80;
the script says it is 2.0x the item before. Price a spike by the same rule, so its cost is honest and
selling it is a reward too. Put spikes where the player needs a lift, not at random.

## 4. Balance choices with a relative anchor

When the player chooses between options that start equal (classes, races, factions, starting decks),
use a relative anchor: the sum of their stats, with weights if some stats are worth more.

```
python "<skill base directory>/scripts/balance.py" budget classes.csv --values weights.csv --tolerance 5
```

`classes.csv` has a `name` column and one column per stat. The script shows each total and its distance
from the median, and marks a class outside the tolerance. Close the gap with one stat, or with an
ability worth the missing sum. A slower experience curve for one class also works on paper, but modern
games rarely use it: players feel it as a penalty.

The sum only says where to look. Past a point, numbers stop helping: two classes with equal sums can
play very differently, and a strong asymmetric power cannot be summed at all. Then it is a design
judgement, tested in play. Say so in the report instead of pretending the numbers decide.

## 5. Pick the scale

The gap between neighbouring numbers decides how much room there is to grow.

- **Too coarse.** Damage from 1 to 10 with ten swords leaves no place for an eleventh between them.
  Multiply by 10 (and enemy health with it) and there are 90 free places, same ratios.
- **Too fine.** When numbers get huge, players cannot see the difference, and small bonuses stop
  mattering. World of Warcraft had to cut its numbers several times (see the examples).
- **Plan the ceiling.** With exponential growth, compute the last number before release: the last
  level, the last expansion, the biggest crit. Check it fits the type you store it in and the space on
  screen.
- **Whole numbers** are easier than fractions; if you need fractions, multiply them away.
- **Bigger looks better.** 100 gold feels richer than 10. That is a reason to pick a scale, not to
  inflate numbers every update.

Games that add content for years (card games, online RPGs) need the most room. Choose the scale before
the first release; changing it later touches every table and every save.

## 6. Check how systems feed each other

Combat, economy, crafting, movement and physics are balanced apart, then meet at alpha, and the
surprises start. Before that, draw the flows: for each resource, where it comes from, what it buys and
which other system it feeds. Then ask for each arrow:

- If this flow gets bigger or smaller, what else changes? Look for chains: a multiplier on a
  multiplier, a loop where the output buys more input.
- Can the player use a system in a way nobody planned? Physics plus explosions gave shooters the rocket
  jump; it was kept because it was fun. Decide on purpose whether such a find is a bug or a feature.
- Does cutting a "too generous" reward hurt something downstream? In one mobile game, cutting free
  coins cut real-money spending by half, because coins started purchases that money finished.

Write down each interaction you find. Some show up only after release; the map tells you where to
look first.

## 7. Audit a table and hand it over

For an existing table of items (weapons, cards, shop goods), run the audit:

```
python "<skill base directory>/scripts/balance.py" audit items.csv --values values.csv
python "<skill base directory>/scripts/balance.py" audit items.csv --derive
```

`items.csv`: `name`, one column per stat, optional `cost`. `values.csv`: `stat,value`, one point of each
stat in the anchor. `--derive` takes those values from items that give only one stat (price / amount),
the way League of Legends players compute gold efficiency. The report names:

- items far from the curve (efficiency outside the tolerance, 15% by default): too strong or too weak
  for their cost;
- dead items: another item is at least as good in every stat and costs no more;
- items of the same kind whose worth is too close to tell apart.

Output goes to the chat as a table, and as CSV with `--csv` when the user wants a spreadsheet. If the
game keeps its numbers in a data file (JSON, a ScriptableObject, a resource), change it **only after
the user says yes**, and show the diff first.

Report in this order: the anchor and why; the inputs; the table; what is off and what you would change;
what the numbers cannot decide. Then test it in the game: `aiko:playtest` turns "feels off" into a
before and after measurement.

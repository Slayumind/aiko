# Rules sheet

A rules sheet writes down how one game's system works, in a fixed shape: what exists, what happens
when things meet, how it ends. Stands are built from it, and the comparison reads from it. The shape
comes from VGDL, the Video Game Description Language of the General Video Game AI competition, and
from PuzzleScript. VGDL describes a whole arcade game in about 20 lines.

The fixed shape matters more for an agent than for a person. When language models wrote VGDL, the
most common error was a rule whose words went against the order of a sentence: `avatar goal >
killSprite` removes the avatar, not the goal, because the effect falls on the first name. Models
also explained such a rule as correct when asked to check it. So the sheet uses sentence order, names
who receives every effect, and is checked by running it, not by reading it again.

## Sections

Keep the order. Leave out a section the system does not need, and write "none" so the reader knows
it was not forgotten.

1. **Entities.** Everything that acts or is acted on, as a tree. A child takes its parent's rules and
   parameters and changes what it names. Difficulty often lives in one parameter: in VGDL's Zelda the
   fast, normal and slow monsters differ only in how many ticks they wait between moves (2, 4 and 8).

   ```
   enemy                       speed=normal
     bat      > enemy          speed=fast, moves at random
     scorpion > enemy          speed=slow
   hero                        hp=3
   ```

2. **Legend.** One character per entity, used by every scene and level of this sheet. Declare it once.
   When rules and levels had separate legends, models mixed them up.

3. **Rules.** One trigger, one effect per line, in sentence order: who, does what to whom, and what
   happens to whom.

   ```
   when hero touches key:   key is removed; hero becomes hero-with-key (the same hero)
   when hero touches door:  if hero-with-key, door is removed; else hero steps back
   when bat touches hero:   hero loses 1 hp
   ```

   - Name state changes as changes of the same thing ("becomes ... (the same hero)"). Read as a new
     entity, a changed one confuses both people and models.
   - Triggers can be a touch, entering a range, a hit, a timer, a button, the start of a turn, a
     resource reaching a number. VGDL knows only touches; write the others in plain words.
   - Write the order rules are checked in when two can fire at once. In VGDL the same rules in another
     order make another game.

4. **Resources and flows.** Numbers that come and go, in the words of Machinations (Joris Dormans):
   - a source makes a resource;
   - a pool holds it;
   - a drain destroys it;
   - a converter turns one resource into another;
   - a trader moves it between owners;
   - a gate splits a flow.

   Give units and rates: "shop: converter, 3 gold -> 1 unit, once per round".

5. **Phases and order.** For turn-based and step systems: what one step reads and does, in which
   order, and what ends a phase. Systems with phases are where written rules break most often. In one
   test, models that turned board-game rules into code made 64% of their errors in games with phases.

6. **End.** Win and lose as checks a program can run: `all crates on targets`, `no heroes left`,
   `round 20 is over`. A game that can only be won, or only lost, is a finding.

7. **Scenes.** A grid in the legend's characters, and under it the same scene as a list of positions:
   `crate at (2,3)`. Models misread grids: they swap rows and columns, flip up and down, and see far
   cells as neighbours. A list of positions fixed most of that in tests; a picture made it worse.

8. **Sources and guesses.** A link for every rule; "guessed" next to anything the sources do not say.

## Example: Sokoban

```
Entities   wall; floor; target; crate; player
Legend     # wall   . floor   o target   c crate   p player
Rules      when player moves into wall: player steps back
           when player moves into crate: crate moves one cell the same way
           when crate moves into wall or crate: crate and player step back
Resources  none
Phases     one move per key press; rules above in this order
End        win: all targets have a crate; lose: none (undo instead)
Scene      #####
           #pco#
           #####
           player at (1,1), crate at (1,2), target at (1,3)
```

## Checks before a stand is built

Run these with the stand's own code, in node, not by reading the sheet again.

- Every entity appears in at least one rule. An entity no rule mentions is decoration or a gap.
- Every rule fires at least once in the "try this" experiments. A rule that never fires is dead: it
  is cut, or the sheet is wrong. Generators of games produced such rules all the time, and checks
  that only ask "can it be won" did not see them.
- The end can be reached, and losing can be reached if the game has losing.
- Counting and paths come from code (a search, a loop), not from reasoning. Models lose count past
  about 25-30 moves.

## Where the shape stops

VGDL itself covers only arcade games on a grid, where the only event is two things touching. The
sheet goes further with plain-language triggers, resources and phases, but it is still a note, not a
language a machine runs. No formal language found in the research covers an autobattler as a whole
(units fighting at once, synergies, a shop): write the shop as resources and flows, the fight as
phases, and let the stand's code be the exact version.

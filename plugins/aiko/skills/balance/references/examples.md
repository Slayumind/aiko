# Anchors in real games

Short cases to show the user what a choice leads to. Each has the lesson and a source. Community
numbers (reverse-engineered by players) are marked as such.

## Sources for the method

- Ian Schreiber, Brenda Romero. *Game Balance*. CRC Press, 2021. Chapter 5, "Finding an Anchor":
  https://www.taylorfrancis.com/chapters/mono/10.1201/9781315156422-5/finding-anchor-ian-schreiber-brenda-romero
- Ian Schreiber. *Game Balance Concepts*, Level 3, "Transitive Mechanics and Cost Curves":
  https://gamebalanceconcepts.wordpress.com/2010/07/21/level-3-transitive-mechanics-and-cost-curves/
- Level 6, "Situational Balance":
  https://gamebalanceconcepts.wordpress.com/2010/08/11/level-6-situational-balance/

## Anchors that work

- **League of Legends, gold efficiency.** Players price each stat by the cheapest item that gives only
  that stat (for example 350 gold for 10 attack damage, so 35 gold per point). An item's efficiency is
  the gold value of its stats over its price. Lesson: gold is the anchor, and the basic items set the
  exchange rates. https://wiki.leagueoflegends.com/en-us/Gold_efficiency
- **Hearthstone, the "vanilla test".** Early on, players judged a minion by attack + health against
  about 2 x mana + 1. Text on the card has to pay for the stats it lacks. Lesson: mana is the anchor,
  and a plain baseline makes every ability visible. (Community rule.)
  https://www.hearthpwn.com/forums/hearthstone-general/general-discussion/235548-the-vanilla-test
- **Slay the Spire.** The basic Strike does 6 damage for 1 energy; other attacks are read against that
  line. Zero-cost cards are made weaker, because effects that give energy or draw cards multiply them.
  Lesson: energy per turn is the anchor, and cheap cards need extra care. (Fan analysis.)
  https://www.cloudfallstudios.com/blog/2020/11/2/game-design-tips-reverse-engineering-slay-the-spires-decisions
- **Magic: The Gathering, cost curve.** Schreiber rebuilds the curve: each mana of cost buys a known
  amount of power, toughness and keywords, with a surcharge for expensive cards. Lesson: once the
  curve is known, a new card can be priced before playtesting. (Level 3 above.)
- **World of Warcraft, item budget.** Each item level gives a stat budget that grows by a fixed
  percent; stats are weighted before they are added up. Lesson: item level is the anchor for gear.
  (Community reverse-engineering.) https://warcraft.wiki.gg/wiki/Stat_budget
- **DOOM (1993), weapons without prices.** Health is the anchor; weapons differ by range, reload,
  ammo, self-damage and where they lie on the map, so early weapons stay useful. (From the book.)

## Failures and what they teach

- **Quake, rocket jump and bunny hop.** Explosion knockback plus physics let players jump far with a
  rocket under their feet; a movement bug let skilled players run several times faster. Neither was
  planned; the rocket jump became a standard feature. Lesson: systems meet in ways no table predicts,
  so decide on purpose what to keep. https://en.wikipedia.org/wiki/Rocket_jumping
- **Ravenwood Fair (2010), cutting a generous reward.** Coins were generous before launch to make
  testing easy. When they were cut after launch, real-money income fell by more than half: coins let
  players start many buildings, and players paid money for the rarer resources that finished them.
  The cut was undone. Lesson: check what a resource feeds before you cut it. (From the book; not
  confirmed by an online source.)
- **Skullclamp (Magic, banned 2004).** A late change that was not tested enough. Its value depended on
  cheap creatures around it, so it was far stronger than it looked. Lesson: an ability's price
  depends on the environment, not only on the card.
  https://blog.cardkingdom.com/on-this-date-in-magic-history-the-banning-of-skullclamp/
- **Undertaker (Hearthstone).** A cheap minion that grew with other cheap minions broke the vanilla
  baseline, and the deck built on it had a very high win rate until a nerf. Lesson: a cheap body plus
  a synergy needs the synergy priced. https://www.pcgamer.com/hearthstones-undertaker-nerfed-at-last/
- **Oko, Thief of Crowns (Magic, banned 2019).** Almost 70% of decks at one top event played it.
  Lesson: an under-priced card makes every other option in its slot dead.
  https://magic.wizards.com/en/news/announcements/november-18-2019-banned-and-restricted-announcement
- **Power creep (Yu-Gi-Oh, Hearthstone and others).** Each set a bit stronger than the last makes old
  cards useless; Hearthstone answered with a rotating Standard format in 2016. Lesson: new content is
  priced against the curve, not against the last set.
  https://hearthstone.fandom.com/wiki/Power_creep
- **World of Warcraft, stat squish.** Numbers grew until a raid boss had about 1.5 billion health,
  close to the limit of a 32-bit integer. Patch 6.0.2 cut numbers to a few percent of their size, and
  later patches cut them again. Lesson: plan the ceiling of an exponential curve before release.
  https://warcraft.wiki.gg/wiki/Stat_squish
- **Diablo III, auction house.** Buying gear with gold or money skipped the loot loop the game was
  built on; the auction house was closed in 2014. Lesson: a market can become the real anchor and
  make drops worthless.
  https://www.gamespot.com/articles/diablo-3-controversial-auction-house-would-have-been-removed-faster-if-not-for-legal-concerns/1100-6508613/
- **Anthem (2019), power level.** A multiplier from the power level outweighed the bonuses on the gear,
  so top loot barely mattered until a patch fixed it. Lesson: a multiplier on top can bury the anchor.
  https://www.pcgamesn.com/anthem/power-scaling-broken

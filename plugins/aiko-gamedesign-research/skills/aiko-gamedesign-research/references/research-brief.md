# Research brief

## Split into clusters

Pick 2-4 clusters that look at the mechanic from different sides. Each cluster gets one research
agent and a starting list of 20-30 candidate games. Tell the agent to add what it finds and drop
what does not fit.

Good splits come from the question "who else has to solve this problem?", not from genres alone.
Example for party AI tactics:

| Cluster | Why it belongs |
|---|---|
| RPGs with party rules (Final Fantasy XII, Dragon Age, Pillars of Eternity II, Unicorn Overlord) | the classic form of the mechanic |
| Programming games (Gladiabots, Carnage Heart, Kodu) | the same rules with the interface stripped bare |
| Autobattlers and indirect control (tower defence targeting, Ogre Battle, space battle orders) | one rule instead of a list; what survives when you cut |

## Agent prompt

Give every research agent the same frame. Replace the parts in angle brackets.

```
Research <mechanic> in games for a design review. Cluster: <cluster name and why>.

Project context (use it to judge what matters, do not search for it): <2-5 sentences, or "none">.

Start from these games, add others you find, drop those that do not fit:
<list>

For every game you keep, return:
- title, years, platforms, studio
- family of solution (from this list, or propose a new one): <families if known>
- how one step of the system works, in 3-8 numbered steps, from sources
- the interface: how the player reads and edits it; gamepad or touch if relevant
- progression: how the system grows over the game
- what to take and what to avoid, one line each
- 1-3 source links (official docs, wikis, patch notes, developer posts, papers, long guides)
- 1-2 screenshots of the mechanic's interface, downloaded to refs/<slug>-<n>.jpg
  (see images rules below), each with one line on what it shows
- anything you could not confirm, marked "not verified"

Rules:
- Facts from sources only. No memory.
- Skip sites whose terms forbid AI use or scraping (for example gameuidatabase.com).
- Do not log in, do not pass age gates, do not work around bot checks.
- Open every image you download; delete it if it does not show the mechanic.

End with "dropped games and why", one line each.
```

## Searching

- Query shape that works: `<game> <mechanic words> how it works guide`, then the game's wiki and
  official help pages.
- MediaWiki sites (Fandom, wiki.gg, official wikis) have an API that returns page text and image
  file URLs: `scripts/wiki_fetch.py`. In Git Bash on Windows set `MSYS_NO_PATHCONV=1`, or arguments like
  `/w/api.php` turn into Windows paths.
- Guides are sometimes wrong. When sources disagree, prefer official docs and data files, then the
  wiki, then guides. Japanese or other original-language sources are often more accurate for
  games made there.
- Community forum posts are good for "what players complain about"; label them as such.

## Tiers

Merge the agent reports and assign tiers yourself:

- **Tier 1 (8-12 games):** systems that differ the most from each other or teach the most. These
  get full cards and are the candidates for stands.
- **Tier 2 (12-20):** worth a paragraph: a variant of a tier-1 system with one useful twist.
- **Tier 3 (10-30):** worth a row: the mechanic is present, the lesson is short.

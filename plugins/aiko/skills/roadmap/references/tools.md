# Tool notes

The method does not depend on the tools. This file collects how to read and write in the tools
met so far, and the traps that cost time. Add a section when a team uses a new tool.

## Any tool

- **Read before you write**, every time: the owner or a colleague may have edited the page by hand.
- **Small targeted edits** over a full rewrite of a page. Replace a whole block (a table, a list, a
  paragraph), never a piece of it with a placeholder you plan to fix later: a failed second step
  leaves the page broken for readers.
- **Read the page again after a structural edit** (a table, a heading, a moved section).
- **Trash, not delete.** Old rows go where they can be restored, after a yes on the list.
- **Links to other pages** use the tool's own link or mention format, so they survive a rename.

## A wiki with databases (for example Notion)

- One database per domain, one row per release. The row's title is the release name; the body holds
  the release text; properties hold status, for whom, ready by, blocked by, domain goal, sources.
- Changing a database's columns is done once, before the rows: rename the title column, drop the
  columns you no longer need, add the new ones.
- In Notion, a link to a page inside text is `<mention url="…"/>`; the older `<mention-page>` still
  works but gives a warning.
- In Notion, a SQL query over several databases at once needs a Business plan. Reading rows of one
  database works on any plan.
- A search-and-replace edit must match the stored text exactly. Links inside text are stored as
  links, so search for a part of the sentence without the link.

## A generated web page (an artifact, a site, a published HTML page)

- Keep the page as a template plus data (for example `roadmap.json` and `snapshot.json`) and a small
  build step. The weekly update changes data, not markup.
- Labels on the page (headings of panel sections) live in the template: change them there when the
  release template changes, or the page and the rows drift apart.
- Before publishing: build, open the page, open every changed panel, check the console, check a
  375 px wide screen for sideways scroll.
- Read the live version first if someone else may have changed it.
- Publishing a shared page is an outward action: the user's yes covers one update.

### A page that keeps shared state (the owner edits order, marks, sizes on the page)

- **Who writes.** Only the owner writes; everyone else reads. Prove it with a test write at an
  ordinary member's level: it must be refused.
- **The first publish with a store is its own yes.** If the permission system stops it, do not look
  for a way around: explain what the store is for and ask.
- **Redraws close open dropdowns.** A store that re-sends its state (live or every half a minute)
  must not redraw the list each time: skip a state equal to the last one, and while a dropdown in
  the list has focus, wait until it loses focus.
- **Local edits win until saved.** An older state arriving between an edit and its save must not
  undo the edit: ignore incoming state while a save is pending.
- **Stored state may arrive frozen.** Copy every object and list the page later changes (marks,
  sizes, order) before keeping it as the page's state. Writing into a frozen object throws in
  strict code, and the edit is lost with no message; it shows only once the store holds data.
- **Test with a store that behaves like the real one.** A fake that answers once and at once hides
  the bugs above. Use one that re-sends its state every 150 ms, saves with a delay and hands out
  frozen data, start it with real saved state, then pick two values in a row. After publishing,
  set one value on the live page and read it back from the store.
- **Keep the agent's data apart from the owner's choices.** Items and the starting order ship with
  the page; the owner's order, marks and sizes live in the store. A rebuild of the items never
  touches the store.
- **Code for an unagreed mechanic stays off** behind a flag, with its data kept, until the group
  agrees on it.

## An issue tracker (for example Jira)

- Find a domain's tasks by labels that exist. List the labels from real tasks; never invent one.
- Two spellings of one label are two labels for the tracker: a filter by one misses the other.
- Put task keys in brackets at the end of a task line: "Write content events: in progress (ABC-42)."

## A chat (for example Slack)

- Read whole threads, with replies, from the last message you saw.
- Status of an ask to another team ("asked on the 9th", "done") comes from the chat, not from an
  older document.

# Keep every place in sync

| Place | Holds | Who reads it |
|---|---|---|
| The owner's own tracker | every card, the full brief, private notes | the owner and agents |
| The shared backlog (one per domain) | one row per release, the release text | colleagues of other teams |
| The domain page | borders, goals, team; releases as name, status, link | colleagues, newcomers |
| The how-the-work-runs page | roles, rhythm, intake, how results are written up | colleagues who work with the domain |
| The roadmap page | the same releases on tracks, with statuses | everyone, before the meeting |
| Overview pages | the product's story, versions, asks to other teams | managers, partners |

The tools behind each place are in the project's role map (SKILL.md, section 0).

Rules:

- **One name everywhere.** A release has one name in all places. Renaming is done everywhere in one
  pass.
- **Links both ways.** Each card in the owner's tracker ends with a line pointing to its release
  row. The release row and the roadmap milestone link each other.
- **The tracker is the source of status, the shared pages are the source of wording.** The owner may
  edit the shared pages by hand; read them before writing, and keep their edits.
- **Criteria live only in the release row.** A domain page lists its releases as name, status and a
  link to the row. A second copy of the criteria on the domain page goes stale at the first sync.
- **Nothing private flows out.** No names of the private tracker, no card numbers, no file lists in
  shared places.

## The inventory check

Before you tell the owner "everything is current", make a table:

| Place | Opened today? | Matches the release rows? | What is off |
|---|---|---|---|

Fill it by opening each place, not from memory. A place you did not open is "not checked". Show
the table, or one line per place, and offer the fixes.

## The weekly sync

When the owner says "sync the domains" (or a scheduled reminder fires):

1. Read the owner's tracker: statuses, ticked criteria, new and closed cards per release.
2. Read each shared backlog and domain page; note the owner's hand edits.
3. Update the release rows: status, ready by, blocked by, ticks, task lines. A new release is a new
   row, and its link goes back into its cards. A new criterion or a changed border also goes to the
   domain page.
4. Update the domain pages' release lists and the how-the-work-runs pages if the rhythm changed.
5. Check the windows: passed windows, releases in "this month" waiting for the scarce resource, and
   the links between queue asks and releases (`planning.md`).
6. Check the overview pages: versions from tags and the changelog, the status of asks to other teams
   from the chats.
7. Run the writing check on every changed text, then rebuild and publish the roadmap page.
8. Report to the owner in a few lines: what moved, what is blocked, what needs their answer, and
   which places were not opened.

## The reminder

A scheduled job can remind the owner once a week. It only reads: it compares the tracker with the
shared backlogs and sends the owner a short message with the differences, ending with the words
that start the sync. The writing itself happens in a session with the owner, after their yes.

## Posts and answers

A post to the group after an update, and what to do when a colleague's answer shows a whole class of
problems: `posts.md`.

## Errands to colleagues

Small things found during a sync (a duplicated label, a page with a wrong owner) go to the user
first.

- **Check the fact before the message.** For two labels, look at the dates of the latest tasks on
  each: often one is old and unused, and the message can say so.
- **If the user asks, go to the colleague** with your errand method (`aiko:envoy` in Aiko):
  introduce yourself, one point per message, and ask whether the colleague is fine with an agent
  coming on the user's behalf.
- **The answer changes the roadmap.** "That label is old" becomes a fix in the domain's labels at
  once, after the user's yes.
- **If the user joins the conversation**, the errand is theirs now. Ask the user before you remove
  the scheduled checks; then remove them without a closing message.

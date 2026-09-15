# The four axes, in detail

Questions to ask of the diff. Not every question fits every project: a desktop app has no migrations,
a static site has no installer. Say which ones you skipped.

## Stability

- Which code paths are new, and which old ones were removed? Was a removed path someone's fallback?
- Did a default change (a setting, a timeout, a limit, a feature flag)? Who notices?
- What happens when the new code fails: a clear message, a silent wrong result, a crash, a hang?
- Does the release depend on something outside it: a server version, an API, a file another program
  writes? What if that is older or newer than expected?
- Upgrade path: does the new version read the settings and data the old version wrote?

## Data

- Migrations: additive (new table, new nullable column) or transforming (rename, drop, type change,
  backfill)? A transforming migration cannot be rolled back by redeploying.
- One-off scripts: who runs them, when, and are they safe to run twice?
- Files on the user's machine: does the new version rewrite a config or a cache in a way the old
  version cannot read after a downgrade?
- Backups: is there a fresh snapshot, and has a restore ever been tried?

## Performance

- Lists and queries without a limit. Pagination on new endpoints.
- N+1 queries in new loops. Indexes for new filters and sort orders.
- Blocking I/O on a request path or a UI thread.
- Work that runs while nothing happens: timers, polling, animations, file watchers.
- Size: bundle size, installer size, memory at idle, cold start. Compare with the last release when
  the project measures them.

## Security and access

- Every new endpoint, page and action checks the user and the role on the server, per action.
- Roles come from the database or the token, never from the request body.
- Secrets: not in logs, crash reports, error pages, build artifacts, release notes or screenshots.
- Private data: a new public page, feed, preview image or API response does not include private items.
- Dependencies added or upgraded: known advisories, licence, who maintains them.
- The release workflow itself: who can trigger it, which secrets it reads, pinned actions.

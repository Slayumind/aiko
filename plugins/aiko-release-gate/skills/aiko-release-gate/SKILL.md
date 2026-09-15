---
name: aiko-release-gate
description: Check a release before it ships, without shipping it. Reads everything that goes out, runs the project's own checks, and answers GO or NO-GO with the irreversible changes named, a rollout order and a rollback plan. For apps with installers and tags as well as for web services with databases. Use before tagging, merging to the production branch or deploying, or when the user asks "can we release", "is this ready", "release check".
---

# aiko release gate

You verify, you do not ship. **Nothing is pushed, tagged, merged, deployed or published from this
skill.** A green gate is permission to ask the user, not permission to release. Every push, tag, merge
into a shared branch and deploy needs its own explicit yes.

## 1. Learn how this project releases

Read, in this order, and note what you find:

- `CLAUDE.md` / `AGENTS.md`: branch model, release rules, who says yes.
- The release workflow: `.github/workflows/*release*`, `deploy*`, a `Makefile`, `package.json` scripts,
  a `Dockerfile`. What does a tag or a merge actually trigger?
- The version source: `package.json`, `Directory.Build.props`, `*.csproj`, `pyproject.toml`,
  `Cargo.toml`, `build.gradle`, a `VERSION` file.
- The changelog: `CHANGELOG.md` or release notes the workflow reads.
- The branches: which one is released (for example `main`, `production`) and which one collects work
  (for example `in-progress`, `develop`).

If the project has a release checklist of its own, run it too and report it item by item.

## 2. Read everything that goes out

`git log --oneline <released>..<candidate>` and `git diff <released>...<candidate>`. All of it, not a
summary. With tags: `git log <last tag>..HEAD`.

## 3. Check the release itself

Answer each with **yes**, **no** or **not checked**, and why. Skip the ones that do not apply and say so.

**Version and notes**
1. The version in the version source: which one?
2. The changelog has a section with exactly this version. A lone "Unreleased" section is a stop: many
   release workflows take the first section they find and publish it under that title.
3. The version is greater than every existing tag. Some updaters (Velopack, Squirrel, app stores) refuse
   a version that does not grow.
4. The changelog matches what really changed since the last release (step 2), in both directions:
   nothing missing, nothing that did not ship.

**Public documents**
5. Names in public documents match what the build produces: installer and package names, commands,
   download links. Compare with the workflow, not with memory.
6. Links in README, SECURITY, PRIVACY and docs lead to pages that exist.
7. Promises in privacy and security documents match the code: what goes over the network, what is
   logged, what is cached. Check the code.

**Build**
8. The project's tests, type check, lint and format check are green. Run them.
9. What is published is built the way it will run: runtime model (self-contained or not), target
   platform, bundled files. A helper built differently from the main app is a classic "works on my
   machine".
10. Tools in the release workflow are pinned to versions, and third-party actions to commits.

**Branches**
11. Which branch is HEAD, and what exactly goes into the released branch.
12. Everything in the candidate branch is meant for this release; nothing half-done rides along.

**Testing**
13. The manual checks the project promises (clean-machine install and uninstall, upgrade from the last
    version, a smoke test on staging) were done for this build, not for an older one.

## 4. Four axes

- **Stability.** What can break for a user, and what happens then. New failure modes, removed
  fallbacks, changed defaults.
- **Data.** Migrations, one-off scripts, file format changes, and what becomes unrecoverable. An
  additive change rolls back by deploying the previous build; a transforming one does not.
- **Performance.** Unbounded lists, N+1 queries, blocking work on a hot path, missing indexes for new
  query patterns, timers and polling that run while nothing happens.
- **Security and access.** Every new endpoint or surface checks who is asking. Secrets stay out of logs,
  crash reports and artifacts. Private data does not reach a public page.

Details and questions per axis: `references/axes.md`.

## 5. Report

1. **GO** or **NO-GO** on the first line, with the one reason that decides it.
2. **Irreversible changes**, named: migrations, deleted data, public URLs or names, published package
   versions. For each one-off script: before or after the migration, before or after traffic.
3. **Rollout order**, starting with the backup or snapshot when there is data.
4. **Rollback plan**, with what is lost said out loud.
5. The checklist from section 3 with yes, no or not checked.
6. **Release note draft** by role: what changes for a user, for an admin. No internal file names.

Doubt about a migration is settled on a copy of the data **before** the merge, not after.

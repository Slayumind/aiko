---
name: copy
description: Write and edit text that reads as written by a person, in Russian and English. Covers UI strings and i18n messages (buttons, errors, empty states, dialogs, toasts), artifacts and reports, README and docs, release notes, announcements, bad news, commit and PR text. Use when writing or reviewing user-facing copy, when a text sounds generic, corporate or AI-written, or when the user asks «сделай живее», «человечнее», «звучит как ИИ», "make it sound human".
---

# aiko copy

The goal: the reader never thinks about who wrote the text. Clear first, alive second.

## 1. The project's voice wins

Look for a Voice section in the project's AGENTS.md or CLAUDE.md and follow it over this skill.

No Voice section? Use these defaults, good for a personal site, an app or a small game:
- Dry and exact, a little warm. A light touch in empty states and 404; none in errors, statuses, toasts.
- The product speaks as "I" / «я», never "we" / «мы». Statuses stay impersonal: "Palette saved."
- Russian says «ты». English uses contractions ("didn't"). ё everywhere.
- Full stop at the end of toasts, empty states and descriptions; none on buttons, labels, titles.

A work or client project with its own brand is different: there, ask, or write two variants
(**A** dry and exact, **B** warmer) and let the user pick. After a pick, write the decisions into
the project's Voice section (see "Decide once" in `references/ui-copy.md`).

English in a repository (commits, code comments, README) stays plain: short sentences and common
words, readable for someone whose first language is not English.

## 2. Read only what the task needs

| Task | Read |
|---|---|
| UI strings, `messages/*.json`, dialogs, forms | `references/ui-copy.md` + tells for that language |
| Release notes, changelog, announcement, bad news, apology | `references/voice.md` |
| Long text: artifact, report, README, docs, post | `references/tells-en.md` or `references/tells-ru.md` |
| Commit message, PR, short chat reply | nothing more; sections 3–5 are enough |

## 3. Process

1. **Reader and result.** Who reads this, and what should they know or do after? One sentence.
   If you cannot say it, the text is not ready.
2. **Write it plainly**, the way you would say it to a colleague at the next desk.
3. **Cut** with the checklist below. For a file, run the script (section 6).
4. **Read it aloud in your head.** Would a person say this? Is anything there only to sound smart?

## 4. Core rules

1. Point first: the answer, result or action in the first sentence.
2. Specific over impressive: a number, a name, a file, what exactly broke. "20 MB, up from 5" beats "much bigger".
3. Plain words and verbs: "need", not "require"; «проверить», not «осуществить проверку»; "is", not "serves as".
4. One idea per sentence, and mixed lengths. A short sentence after two long ones.
5. Say who does it: «мы удалили», not «было удалено».
6. Say what it is, not what it is not. No "not X but Y".
7. Count honestly: two things are two, not three.
8. Facts carry the text; personality lives on the side (an aside, a short tag). Remove the joke and the facts must still work.
9. Bad news: decision → what it means for the reader → why. Own a mistake in one sentence, then say what is fixed. Apologise once, and only if it is your fault.
10. Say how finished it is, including what is rough or not checked.
11. Form follows size. A short reply is a paragraph. Lists only for parallel items; headings only when someone will scan.
12. Never tell the reader how they feel: "Don't worry!", «Не переживайте».

## 5. Checklist: remove before you send

- Openers and closers: "Great question", «Конечно!», repeating the question, "Let me know if…", «Надеюсь, помог».
- «важно отметить», "it's worth noting", «давайте разберёмся», "Here's the thing", "Let's dive in".
- "not X but Y", «не просто…, а…», «это не X — это Y», «не только…, но и…».
- Lists of three by habit; staccato fragments; a punchy last line; a recap of what was just said.
- Inflated weight: «играет ключевую роль», "a pivotal moment", "underscores our commitment".
- Buzzword clusters: seamless, robust, leverage, delve / «уникальный», «комплексный», «эффективный».
- Канцелярит: «является», «данный», «осуществлять», nouns made from verbs, passive voice.
- Formatting a short text does not need: headings, bold terms with colons, Title Case, emoji.
- Vague authority ("experts say") and knowledge hedges ("based on available information").
- Uniform rhythm: every sentence the same length.

## Don't overcorrect

- Do not fake a human with typos, slang, invented anecdotes or forced jokes.
- «Ёлочки», the Russian em dash where grammar wants it, and ё are the norm. Keep them.
- One marker word is not a problem; a cluster is.
- No jokes where the reader loses data, money or time.
- Plain is not flat: keep a real opinion, a concrete example, and the occasional unexpected but exact word.

## 6. Script

`scripts/tells.mjs` finds the patterns above in files. A hit is a hint, not a verdict: read it and decide.
It needs Node.js 20 or newer. The path is relative to this skill's base directory, which is shown
when the skill loads.

```bash
node "<skill base directory>/scripts/tells.mjs" messages/ru.json messages/en.json README.md
```

- `.json` is read as i18n (hits by key path), `.md` and `.html` lose markup, anything else is text.
- A draft from stdin: `... | node tells.mjs --type md`.
- `--strict` exits with 1 when anything is found.
- After changing the rules, run `node --test` in `scripts/`. The tests check that good,
  well-typeset text stays clean; false positives cost more than misses.

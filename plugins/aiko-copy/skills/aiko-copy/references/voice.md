# Voice: release notes, announcements, bad news, personality

Patterns from the product and studio writing that people actually like to read.
Examples are ours; use them as shapes, not as text to copy.

## Define a voice
- **Traits as "X, not Y".** "Dry humour, not farce." «Прямо, но не грубо.» A trait with no opposite means nothing.
- **One voice, many tones.** The person is the same; the mood follows the reader's moment.

| Moment | Humour | Warmth | Example |
|---|---|---|---|
| Error with data or money at risk | 0 | calm | «Файл не загрузился: больше 5 МБ» |
| Status, toast | 0 | none | "Saved" |
| Empty state, 404 | low | some | «Здесь пока пусто. Первая палитра — за минуту» |
| Release notes | low, rare line | some | see below |
| Announcement, community post | medium | yes | letter-like opening |
| Social, marketing | high | yes | provocation lives here, never in product UI |

- **Test with an outsider.** Would a person outside the team understand it on first read? Read it aloud.

## Moves that work

1. **Verb first.** "Check your answer", not "You can see how you did by checking your answer".
2. **Name the reader's situation, then answer.** «Давно не заходили? Вот что изменилось.»
3. **Numbers with before and after.** "Uploads up to 20 MB, up from 5." Not "much bigger uploads".
4. **Facts straight, personality on the side.** A bracket, an aside, a short tag after the fact:
   "Palettes now sync across devices. (Yes, the phone too.)" Remove the aside: the fact still works.
5. **Open like a letter to people you know**, for community updates: «Привет. Это последнее большое
   обновление редактора в этом году.» Then the point, at once.
6. **Bad news: decision → what it means → why.** No cushioning before it.
   "We are removing the old editor on 1 October. That means drafts there must be moved by then. Two
   editors doubled every fix, and the new one is where the work goes."
7. **Own a regression in one sentence, then say what is fixed.** "The last update tried to fix broken
   saves and made the page slow instead. That is fixed now. Thanks for your patience."
   Not "We apologize for any inconvenience."
8. **Own a mistake in first person, then act.** "This was on me: the notice gave no dates. Here they are."
   A correction changes what you do, not only what you say. Never answer a trust problem with a joke
   or with silence.
9. **Answer doubt with counts.** «Из 214 ключей активны 12, остальные отозваны.» Not «почти все отозваны».
10. **Say how finished it is.** «Работает, но медленно на больших палитрах — это следующее.»
11. **Humour is precision.** The funny line is an exact description of something absurd, as if the bug
    chose to do it: "The save button no longer says 'Saved' before it has saved." At most one such line in ten.
12. **Thank people by name** when someone reported or fixed something.
13. **End with one action or a short sign-off.** No recap.

## Release notes shape
```
## Palette library                ← noun phrase, not "We've added…"
Save palettes and open them from any device.   ← 1–2 sentences, present tense, what the reader can do

Improvements
- Faster export for large palettes            ← start with a verb or the thing
Fixes
- Fixed the QR preview staying empty after a paste
```
Only what a person would find interesting. Internal refactors go to the commit log.

## Formal register (press release, investor note, legal)
Keeps: short declarative sentences, digits, cause stated plainly, "we", the trade-off said out loud
("We chose growth over margin this quarter").
Drops: jokes, questions, exclamation marks, "folks".
Even a superlative needs one hard fact next to it.

## Humour rules
- With the reader, never at them.
- One strong joke beats five weak ones. No puns by default.
- Operational messages get none.
- If unsure, keep a straight face.
- Tone the joke down before cutting the fact.

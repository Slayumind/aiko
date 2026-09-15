# UI copy, Russian and English

Clarity first, then warmth. A status is quiet; an explanation can speak as a person.
Two registers work well together: an impersonal "robot" for statuses («Сохранено», "Saved") and
the first person for longer explanations, «я» or «мы», whichever the project chose.

## Decide once per project, then keep
Record these in the project's Voice section, so nobody decides them again:
- Address: ru «вы» (lowercase) or «ты» — never both. en: "you"; avoid "we" in the UI unless it is clear who "we" is.
- Full stop at the end of a toast, empty state or tooltip: yes or no.
- ё: always, or only where a word could be misread.
- Ranges: «5–10» (en dash) or «5—10»; percent: «20 %» or «20%».
- en contractions: "can't" (informal) or "cannot".

## Elements

**Buttons.** A verb that names the result.
- en: "Delete", "Send invoice", sentence case. Not "OK", "Submit", "Let's go!".
- ru: perfective infinitive: «Сохранить», «Создать документ». Not «Сохраните», «Сохранение», «Новый документ».
- No ellipsis on buttons. Keep the label on one line: widen the button, do not trim the meaning.

**Errors.** What happened, what to do. No blame, no "Oops".
- en: "Couldn't save: no connection. Try again in a minute." Not "Something went wrong", "Invalid input", "Error 329347".
- ru: «Не сохранилось: нет связи. Попробуйте через минуту». Not «Упс! Что-то пошло не так».
- Field errors sit next to the field and say the fix: "At least 8 characters" / «Минимум 8 символов».

**Empty states.** The next action, plus a button for it. Nothing important: it disappears once there is data.
- "No palettes yet" + [Create palette] / «Палитр пока нет» + [Создать палитру].

**Confirm a destructive action.** Name the object and the result; the confirm button repeats the verb.
- «Удалить черновик?» → [Удалить] [Отмена]. "Delete draft?" → [Delete] [Cancel]. Not «Вы уверены?» → [OK].
- Say the real consequence if there is one: «Ссылки на файл перестанут работать».
- Undo for a few seconds is often better than a question.

**Toasts and statuses.** Short, calm, no «успешно», no "!".
- "Saved", "Key revoked" / «Сохранено», «Ключ отозван». Not «Ваши изменения были успешно сохранены!».

**Labels.** Sentence case, no "Your" unless it separates mine from others.
- "Favorites", not "Your Favorites"; «Избранное», not «Ваше избранное».

**Placeholders.** An example of the format, never the label again.
- `name@example.com`. Not «Введите email» under the label «Email». Long instructions go in help text, not placeholders.

**Loading.** Say what is happening: «Загружаем фото», "Uploading photo". Not «Пожалуйста, подождите…».

**Links.** The link text says where it goes: «Настройки ключей», "API key settings". Not «Нажмите здесь».

## Don'ts in every language
- Don't tell people how they feel: «Не переживайте», "Don't worry!".
- No jokes in errors that cost data, money or time.
- No exclamation marks in neutral statuses. No emoji in system text.

## Russian typography
- Quotes: «ёлочки» outside, „лапки“ inside.
- Dash between words: em dash with spaces: «Время — деньги». A hyphen is only inside a word.
- Non-breaking space: after short prepositions and conjunctions (в, к, с, и, а, но), between a number
  and its unit («5 км», «100 ₽»), in thousands («1 000»), after «№», before a dash.
- No full stop after a heading. Lowercase after a colon.
- Numbers: digits in UI («3 файла»), a comma for decimals («0,06»).

## English typography
- Sentence case for buttons, labels and headings (unless the platform says otherwise).
- Curly quotes and apostrophes in prose (’ “ ”), straight ones in code.
- Em dash sparingly; a colon or full stop is often cleaner.
- Numbers as digits in UI: "3 files".

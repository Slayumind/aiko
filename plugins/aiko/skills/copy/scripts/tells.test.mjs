// Run: node --test   (from this folder)
import { test } from "node:test";
import assert from "node:assert/strict";
import { scan } from "./tells.mjs";

const ids = (source, type) => scan(source, { type }).hits.map((h) => h.id);
const has = (source, id, type = "text") =>
  assert.ok(ids(source, type).includes(id), `expected ${id} in: ${source}\ngot: ${ids(source, type).join(", ") || "nothing"}`);

test("English constructions are found", () => {
  has("It's not a tool, it's a mindset.", "en/not-x-but-y");
  has("This is not just a CMS but a home for your ideas.", "en/not-just");
  has("The page serves as a hub for all settings.", "en/serves-as");
  has("Caching plays a crucial role in speed.", "en/plays-role");
  has("It's important to note that keys expire.", "en/didactic");
  has("Here's the thing: nobody reads docs.", "en/throat-clearing");
  has("The release shipped in May, marking a turning point for the team.", "en/significance-tail");
  has("We're excited to announce palettes.", "en/announce");
  has("We apologize for any inconvenience.", "en/apology-cliche");
  has("Certainly! Here is the fix.", "en/chat-opener");
  has("The fix is in main. Let me know if you need more.", "en/chat-closer");
  has("Built for speed. Made to last. Loved by all.", "en/staccato");
});

test("Russian constructions are found", () => {
  has("Это не услуга — это трансформация.", "ru/eto-ne-eto");
  has("Это не просто сайт, а целая экосистема.", "ru/ne-prosto");
  has("Python является языком программирования.", "ru/yavlyaetsya");
  has("Система осуществляет проверку данных.", "ru/empty-verb");
  has("Важно отметить, что ключи истекают.", "ru/didactic");
  has("В современном мире тексты пишут все.", "ru/cliche-intro");
  has("Кеш играет ключевую роль в скорости.", "ru/plays-role");
  has("Давайте разберёмся, как это работает.", "ru/lets");
  has("Конечно! Вот исправление.", "ru/chat-opener");
  has("Готово. Если у вас остались вопросы, пишите.", "ru/chat-closer");
  has("Приносим извинения за доставленные неудобства.", "ru/apology-cliche");
  has("Язык был разработан в 1991 году.", "ru/passive");
  has("Нажмите \"Сохранить\", чтобы продолжить.", "ru/straight-quotes");
  has("Это важно - и это всё.", "ru/hyphen-as-dash");
  has("Главный вывод: Текст должен быть коротким.", "ru/capital-after-colon");
  has("Мы проверим, что Ваш аккаунт активен.", "ru/capital-vy");
  has("Метод, который помогает людям, которые пишут, прост.", "ru/kotoryi");
});

test("UI strings in i18n JSON", () => {
  const json = JSON.stringify({
    a: { saved: "Ваши изменения успешно сохранены!", err: "Упс! Что-то пошло не так" },
    b: { title: "Save Changes", lib: "Your Favorites", phName: "Введите имя" },
    c: { cta: "Unlock seamless workflows" },
  });
  const got = ids(json, "json");
  for (const id of ["ru/successfully", "ui-exclaim", "ru/oops", "title-case", "ui-your", "ui-placeholder", "en/marker"])
    assert.ok(got.includes(id), `expected ${id}, got ${got.join(", ")}`);
});

test("markdown formatting habits", () => {
  const md = [
    "## Key Benefits And Features",
    "",
    "- **Speed:** pages load faster.",
    "- **Safety:** keys are hashed.",
    "- **Scale:** it grows with you.",
    "",
    "## 🚀 Performance",
    "",
    "## Conclusion",
  ].join("\n");
  const got = ids(md, "md");
  for (const id of ["title-case", "bold-colon-list", "emoji-bullet", "summary-heading"])
    assert.ok(got.includes(id), `expected ${id}, got ${got.join(", ")}`);
});

test("file-level: buzzword cluster, dash density, ё and ты/вы mixed", () => {
  has("We delve into a robust, seamless and pivotal landscape of tools that leverage data.", "en/marker-cluster");
  const dashes = Array.from({ length: 20 }, () => "The build is slow — we know — and a fix is coming soon for everyone who waits.").join(" ");
  has(dashes, "en/dash-density");
  has("Ещё один шаг. Потом еще один.", "ru/yo-mixed");
  has("Ты можешь сохранить. Вы увидите результат.", "ru/ty-vy-mixed");
});

// The important half: normal, well-typeset human text must stay clean.
test("good Russian text with correct typography has no hits", () => {
  const good = [
    "Восемь цветов, из которых состоят все остальные. Сохранена на этом сайте как «example.org UI» с шагом светлоты 0,06 — по ней инструмент палитры судит новые цвета.",
    "Время — деньги. Он сказал: «Это „слово“ здесь лишнее». Ещё 5 км, и мы дома.",
    "Удалить всех сирот. Не удалось сохранить: нет связи с сервером, попробуйте через минуту.",
    "Шаг 2. Роутеры",
    "Ключ API храним только в виде хеша. Уникальный посетитель считается один раз в сутки.",
  ].join("\n\n");
  assert.deepEqual(scan(good, { type: "text" }).hits, []);
});

test("good English text and UI strings have no hits", () => {
  const prose = [
    "Every file under uploads/photos, read against the documents. A save already removes what its own entry no longer names; this page shows the whole disk and lets you remove the rest.",
    "The site received more requests than it can serve right now. Wait a minute and try again. If it keeps happening, the problem is on our side, not yours.",
    "Keys for the tools API. A key has no owner: it can read and write every saved palette, so treat one like a password.",
  ].join("\n\n");
  assert.deepEqual(scan(prose, { type: "text" }).hits, []);
  const ui = JSON.stringify({
    delete: "Удалить", saved: "Палитра сохранена.", empty: "No keys yet.", topCvs: "Top CVs",
    errorValidation: "Введите от 1 до 2048 символов.", intro: "Обращайтесь с ключом как с паролем.",
    phTitleEn: "Tokyo: The Trip That Changed Everything", apiKeys: "API keys",
  });
  assert.deepEqual(scan(ui, { type: "json" }).hits, []);
});

test("markdown: code, links and headings with names stay clean", () => {
  const md = [
    "# Palette tool",
    "",
    "Run `node tells.mjs --strict file.md` — the script is not just a linter, but it helps.",
    "",
    "```",
    "It's not a bug, it's a feature.",
    "```",
    "",
    "See [the guide](https://example.com/seamless-robust-leverage).",
  ].join("\n");
  const got = scan(md, { type: "md" }).hits.map((h) => h.id);
  // «not just … but» in prose is a real hit; everything inside code and the URL is not.
  assert.deepEqual(got, ["en/not-just"]);
});

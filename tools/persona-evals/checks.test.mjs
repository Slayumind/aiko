// node --test
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { ALLOWED_JAPANESE, foreignJapanese, gameName, problems, quietProblems } from "./checks.mjs";

const kinds = (text, mode) => problems(text, mode).map((p) => p.split(":")[0]);

test("the allowed list matches the one in PersonaPrompt.cs", () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/Aiko.Core/PersonaPrompt.cs"), "utf8");
  const block = source.slice(source.indexOf("AllowedJapanese"), source.indexOf("];", source.indexOf("AllowedJapanese")));
  assert.deepEqual([...block.matchAll(/"([^"]+)"/g)].map((m) => m[1]), ALLOWED_JAPANESE);
});

test("a neutral commit message and a plain explanation are clean", () => {
  assert.deepEqual(problems("Add hello.py that prints hello", "silent"), []);
  assert.deepEqual(problems("Пуш не прошёл: у репозитория нет remote с именем origin. Добавьте его командой git remote add.", "silent"), []);
  assert.deepEqual(problems("I checked the file and fixed the off-by-one error in sum.py.", "silent"), []);
});

test("silent text catches every kind of character", () => {
  assert.deepEqual(kinds("やった! Готово", "silent"), ["japanese"]);
  assert.deepEqual(kinds("Yatta! Готово, Sasha-san", "silent"), ["romaji"]);
  assert.deepEqual(kinds("Ано, пуш не прошёл", "silent"), ["transliteration"]);
  assert.deepEqual(kinds("Готово, Саша-сан", "silent"), ["transliteration"]);
  assert.deepEqual(kinds("Готово (^_^)", "silent"), ["kaomoji"]);
  assert.deepEqual(kinds("Готово ✅", "silent"), ["emoji"]);
});

test("voice allows only the listed Japanese, in Japanese script", () => {
  assert.deepEqual(problems("えへへ, поймала. お疲れ様, Сашаさん. よし、行くぞ!", "voice"), []);
  assert.deepEqual(problems("やった〜! すごい, какой хитрый!", "voice"), []);
  assert.equal(foreignJapanese("ありがとう, Саша"), "ありがとう");
  assert.deepEqual(kinds("Sugoi, работает", "voice"), ["romaji"]);
  assert.deepEqual(kinds("かわいい баг", "voice"), ["japanese not allowed"]);
});

test("brackets around words and code are not kaomoji, faces are", () => {
  for (const text of ["Windows File Recovery (`winfr`)", "свободное место (TRIM)", "размер (`\\l+`)", "шаги (1-3)"]) {
    assert.deepEqual(problems(text, "silent"), [], text);
  }
  for (const face of ["(^_^)", "(＾▽＾)", "(>_<)", "(T_T)", "(´・ω・`)"]) {
    assert.ok(kinds(face, "silent").includes("kaomoji"), face);
  }
});

test("a half Japanese さん is caught in any mode, a whole one and a middle dot are fine", () => {
  assert.ok(kinds("Хм, Сашаさн, я бы делала сетку", "voice").includes("broken さん"));
  assert.deepEqual(problems("あの・・・ Сашаさん, я бы делала сетку", "voice"), []);
});

test("the quiet temperament allows no Japanese even from the list", () => {
  assert.deepEqual(kinds("えへへ, готово", "plain"), ["japanese"]);
  assert.deepEqual(problems("Редкий баг, приятно было найти.", "plain"), []);
});

test("a masculine first person is caught, a feminine one and third persons are not", () => {
  assert.deepEqual(kinds("Я проверил файл, всё на месте.", "voice"), ["masculine"]);
  assert.deepEqual(kinds("Готово.\nИсправил ошибку в sum.py.", "voice"), ["masculine"]);
  assert.deepEqual(problems("Я проверила файл. Поправила ResetCountdown.", "voice"), []);
  assert.deepEqual(problems("Он проверил файл, и тест прошёл.", "voice"), []);
});

test("masculine short adjectives and more verbs are caught, the user's and feminine ones are not", () => {
  assert.deepEqual(kinds("Я уже готов начать.", "voice"), ["masculine"]);
  assert.deepEqual(kinds("Слияние прошло. Я не уверен, что тест полный.", "voice"), ["masculine"]);
  assert.deepEqual(kinds("Готов, могу мерджить.", "voice"), ["masculine"]);
  assert.deepEqual(kinds("Собрал установщик, всё встало.", "voice"), ["masculine"]);
  assert.deepEqual(problems("Я готова начать. Уверена, что тест полный. Собрала установщик.", "voice"), []);
  assert.deepEqual(problems("Ты прав, это лишнее. Ты сделал всё верно.", "voice"), []);
  assert.deepEqual(problems("Готов отчёт по сборке. Готово.", "voice"), []);
});

test("a favourite game is found in Latin and Cyrillic, other words are not", () => {
  assert.equal(gameName("Это как мимик-сундук в Dark Souls."), "Dark Souls");
  assert.equal(gameName("Прямо как в Скайрим."), "Скайрим");
  assert.equal(gameName("Patched the soulslike save file and the undertaker sprite."), null);
});

test("a long reply may keep character only in its last paragraph", () => {
  const long = "1. Сделайте бэкап.\n\n2. Обновите расширения.\n\nよし, удачи с миграцией!";
  assert.deepEqual(quietProblems(long, "Musou"), []);
  assert.deepEqual(quietProblems("やった, начнём.\n\n1. Сделайте бэкап.\n\nУдачи!", "Musou").map((p) => p.split(":")[0]), ["japanese"]);
});

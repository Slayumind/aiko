#!/usr/bin/env node
// Finds common signs of AI-written text in Russian and English.
// A hit is a hint, not proof: read it and decide.
//
//   node tells.mjs [--strict] <file ...>
//   <command> | node tells.mjs [--strict] [--type md|html|json|text]
//
// .json is read as i18n messages: every string value, reported by its key path.
// .md and .html lose their markup first. Anything else is plain text.
// Exit code: 0; with --strict, 1 when anything was found; 2 on bad input.

import { readFileSync } from "node:fs";
import { extname } from "node:path";
import { pathToFileURL } from "node:url";

// \b in JS regex only knows ASCII letters, so Cyrillic words need their own boundary.
const LETTER = String.raw`[\p{L}\p{N}]`;
const WB = `(?:(?<!${LETTER})(?=${LETTER})|(?<=${LETTER})(?!${LETTER}))`;
const rx = (src, flags = "giu") => new RegExp(src.replaceAll("\\b", WB), flags);

// scope: "any" — everywhere; "prose" — md, html, text; "ui" — i18n strings only.
const RULES = [
  // ---- English: constructions ----
  { id: "not-x-but-y", lang: "en", re: rx(String.raw`\b(?:it'?s|this is|that'?s|is|are)\s+not\s+(?!just\b|only\b|merely\b)(?:about\s+)?[^.;:!?\n]{1,50}?(?:[,;]|\s[—–-])\s*(?:it'?s|but|this is|that'?s|they'?re)\b`), hint: "Say only the second half." },
  { id: "not-just", lang: "en", re: rx(String.raw`\bnot (?:just|only|merely)\b[^.!?\n]{1,60}\bbut(?: also)?\b`), hint: "Say the thing itself, not what it is more than." },
  { id: "not-because", lang: "en", re: rx(String.raw`(?:^|[.!?]\s+)Not because\b`), hint: "Drop the dramatic set-up; give the reason." },
  { id: "serves-as", lang: "en", re: rx(String.raw`\b(?:serves|stands|acts|functions) as (?:a|an|the)\b`), hint: "Use «is», or say what it does." },
  { id: "plays-role", lang: "en", re: rx(String.raw`\bplay(?:s|ed|ing)? an? (?:crucial|pivotal|key|vital|important|significant|central) role\b`), hint: "Say what breaks without it." },
  { id: "didactic", lang: "en", re: rx(String.raw`\b(?:it'?s|it is) (?:important|worth|crucial|essential|key) (?:to note|noting|to remember|to mention|to understand)\b|\bworth noting\b`), hint: "Delete the lead-in and state the point." },
  { id: "throat-clearing", lang: "en", re: rx(String.raw`\b(?:here'?s the thing|let me be clear|the (?:uncomfortable|hard|simple|real) truth is|let that sink in|make no mistake|at its core)\b`), hint: "Start with the point." },
  { id: "cliche-intro", lang: "en", re: rx(String.raw`\bin today'?s (?:fast-paced|digital|modern|ever-changing)\b|\b(?:ever-evolving|rapidly evolving|evolving) (?:world|landscape)\b`), hint: "Cut it; start with the fact." },
  { id: "significance-tail", lang: "en", re: rx(String.raw`,\s(?:highlighting|underscoring|showcasing|reflecting|marking|symboli[sz]ing|emphasi[sz]ing|cementing|solidifying)\s(?:the|its|a|an|their|his|her|how|our)\b`), hint: "End the sentence before the -ing clause." },
  { id: "vague-authority", lang: "en", re: rx(String.raw`\b(?:experts|observers|critics|analysts|industry reports) (?:say|argue|note|suggest|believe|agree)\b`), hint: "Name the source or drop the claim." },
  { id: "knowledge-hedge", lang: "en", re: rx(String.raw`\bbased on (?:the )?available information\b|\bas of my (?:last|knowledge)\b|\bwhile specific details are limited\b`), hint: "Say what you know and what you did not check." },
  { id: "false-breadth", lang: "en", re: rx(String.raw`\bwhether you'?re an? [\w-]+ or an? [\w-]+|\bfrom (?:startups|beginners|novices|small businesses) to (?:enterprises|experts|pros|large corporations)\b`), hint: "Name the real reader." },
  { id: "announce", lang: "en", re: rx(String.raw`\bwe'?re (?:so )?(?:excited|thrilled|delighted|happy|proud) to (?:announce|share|introduce|unveil)\b`), hint: "Start with what changed." },
  { id: "apology-cliche", lang: "en", re: rx(String.raw`\bapologi[sz]e for any inconvenience\b`), hint: "Say what broke and what is fixed now." },
  { id: "q-then-a", lang: "en", re: rx(String.raw`\bThe (?:result|answer|catch|best part|kicker|twist|problem)\?\s`), hint: "Write it as one plain sentence." },
  // ---- English: chat wrappers ----
  { id: "chat-opener", lang: "en", re: rx(String.raw`^\s*(?:certainly|of course|absolutely|great question|good question|you'?re (?:absolutely|completely) right|sure thing)\b[!,.]`), hint: "Start with the answer." },
  { id: "chat-closer", lang: "en", re: rx(String.raw`\b(?:i hope this helps|let me know if|feel free to (?:ask|reach out)|would you like me to|is there anything else)\b`), hint: "End on the last useful sentence." },
  // ---- English: UI ----
  { id: "oops", lang: "en", re: rx(String.raw`\boops\b|\bsomething went wrong\b`), hint: "Say what happened and what to do." },
  { id: "successfully", lang: "en", re: rx(String.raw`\bsuccessfully\b`), hint: "«Saved», not «Successfully saved»." },

  // ---- Russian: constructions ----
  { id: "ne-prosto", lang: "ru", re: rx(String.raw`\bне просто\b[^.!?\n]{1,60}?,\s*а\b`), hint: "Скажите только вторую половину." },
  { id: "eto-ne-eto", lang: "ru", re: rx(String.raw`\bэто не\b[^.!?\n]{1,50}?(?:\s[—–-]|,)\s*это\b`), hint: "Скажите только, что это." },
  { id: "ne-tolko", lang: "ru", re: rx(String.raw`\bне только\b[^.!?\n]{1,80}?\bно и\b`), hint: "Часто хватает простого перечисления." },
  { id: "yavlyaetsya", lang: "ru", re: rx(String.raw`\bявля(?:ется|ются|лся|лась|лось|лись)\b|\bпредставля(?:ет|ют) собой\b`), hint: "Тире или глагол: «Python — язык»." },
  { id: "empty-verb", lang: "ru", re: rx(String.raw`\bосуществ\p{L}+|\bпроизвод(?:ится|ить|ит) (?:проверк|оплат|настройк|запис)\p{L}*|\bобеспечива(?:ет|ют) (?:возможность|доступ)\b`), hint: "Глагол вместо связки: «проверить», не «осуществить проверку»." },
  { id: "didactic", lang: "ru", re: rx(String.raw`\b(?:важно|стоит|следует|необходимо) (?:отметить|подчеркнуть|учесть|понимать|помнить)\b|\bнельзя не отметить\b|\bстоит обратить внимание\b`), hint: "Уберите связку и скажите мысль." },
  { id: "cliche-intro", lang: "ru", re: rx(String.raw`\bв современном мире\b|\bна сегодняшний день\b|\bни для кого не секрет\b|\bв эпоху (?:цифров|стремительн|быстр)\p{L}*`), hint: "Уберите и начните с факта." },
  { id: "plays-role", lang: "ru", re: rx(String.raw`\bигра(?:ет|ют|л|ла|ло|ли) (?:ключевую|важную|значительную|решающую|немаловажную|центральную) роль\b`), hint: "Скажите, что без этого не работает." },
  { id: "lets", lang: "ru", re: rx(String.raw`\bдавайте (?:разбер[её]мся|рассмотрим|погрузимся|разберем)\b|\bпогрузимся\b`), hint: "Сразу к делу." },
  { id: "vague-authority", lang: "ru", re: rx(String.raw`\b(?:эксперты|специалисты|аналитики|исследователи) (?:отмечают|считают|говорят|утверждают|сходятся)\b|\bпо мнению экспертов\b`), hint: "Назовите источник или уберите." },
  { id: "knowledge-hedge", lang: "ru", re: rx(String.raw`\bна основе (?:имеющейся|доступной) информации\b|\bна момент (?:моего обучения|написания)\b`), hint: "Скажите, что знаете и что не проверяли." },
  { id: "announce", lang: "ru", re: rx(String.raw`\b(?:рады|с радостью|с гордостью) (?:сообщить|сообщаем|представить|представляем|анонсировать)\b`), hint: "Начните с того, что изменилось." },
  { id: "apology-cliche", lang: "ru", re: rx(String.raw`\bприносим (?:свои )?извинения за (?:доставленные )?неудобства\b`), hint: "Что сломалось и что уже починили." },
  { id: "passive", lang: "ru", re: rx(String.raw`\b(?:был|была|было|были) (?!(?:рав|долж|вид|нуж|соглас|способ|бол|умер|полн|свобод|откровен|уверен)(?:ен|на|но|ны)\b)(?:\p{L}+(?:ан|ен|ян)(?:а|о|ы)?)\b`), hint: "Кто сделал? «Гвидо создал Python», не «Python был создан»." },
  // ---- Russian: chat wrappers ----
  { id: "chat-opener", lang: "ru", re: rx(String.raw`^\s*(?:конечно|безусловно|разумеется|отличный вопрос|хороший вопрос|отличная идея|вы (?:абсолютно|совершенно) правы)\b[!,.]`), hint: "Начните с ответа." },
  { id: "chat-closer", lang: "ru", re: rx(String.raw`\bнадеюсь, (?:это|мой ответ) (?:помо|был)\p{L}*|\bесли (?:у вас )?(?:остались|возникнут|появятся) (?:ещё |еще )?вопросы\b|\bобращайтесь(?:[.!]?\s*$|, если)|\bдайте знать, если\b`), hint: "Закончите на последней полезной фразе." },
  // ---- Russian: UI ----
  { id: "oops", lang: "ru", re: rx(String.raw`\bупс\b|\bчто-то пошло не так\b`), hint: "Что случилось и что делать." },
  { id: "successfully", lang: "ru", re: rx(String.raw`\bуспешно\b`), hint: "«Сохранено», не «Успешно сохранено»." },
  { id: "please-wait", lang: "ru", re: rx(String.raw`\bпожалуйста, подождите\b`), hint: "Скажите, чего ждём: «Загружаем фото»." },
  { id: "click-here", lang: "ru", re: rx(String.raw`\bнажмите (?:здесь|сюда|на кнопку)\b`), hint: "Глагол на самой кнопке или ссылке." },
  // ---- Russian: typography ----
  { id: "straight-quotes", lang: "ru", re: rx(String.raw`"[^"\n]{1,80}"|“[^”\n]{1,80}”`), hint: "«Ёлочки» снаружи, „лапки“ внутри." },
  { id: "hyphen-as-dash", lang: "ru", re: rx(String.raw`(?<=[\p{L}»)\d]) - (?=[\p{L}«(\d])`), hint: "Тире с пробелами: « — »." },
  { id: "capital-after-colon", lang: "ru", re: rx(String.raw`(?<=[а-яё]{2}): (?=[А-ЯЁ][а-яё]+ [а-яё])`, "gu"), hint: "После двоеточия строчная (если это не имя)." },
  { id: "capital-vy", lang: "ru", re: rx(String.raw`(?<=[а-яё,]\s)(?:Вы|Вас|Вам|Вами|Ваш\p{L}*)(?!\p{L})`, "gu"), hint: "«вы» со строчной в тексте для всех." },
];

// Words that mean little alone but give a text away when they cluster.
const MARKERS = {
  en: rx(String.raw`\b(?:delv(?:e|es|ing)|tapestry|testament|pivotal|intricate|meticulous(?:ly)?|underscor(?:e|es|ed|ing)|showcas(?:e|es|ed|ing)|seamless(?:ly)?|robust|leverag(?:e|es|ed|ing)|elevat(?:e|es|ing)|unlock(?:s|ing)?|boasts?|vibrant|realm|foster(?:s|ed|ing)?|crucial|commendable|game-changer|transformative|groundbreaking|holistic|synergy|empower(?:s|ing)?|supercharg(?:e|es|ed)|nestled|bustling|interplay|landscape|additionally|notably|comprehensive|streamlin(?:e|es|ed))\b`),
  ru: rx(String.raw`\b(?:ключев\p{L}+|уникальн\p{L}+(?!\s*(?:=|посетител|пользовател|идентификатор|значени|ключ))|эффективн\p{L}+|комплексн\p{L}+|бесшовн\p{L}+|инновационн\p{L}+|революционн\p{L}+|потрясающ\p{L}+|максимально|идеальн\p{L}+|широк\p{L}* спектр\p{L}*|трансформац\p{L}+|неотъемлем\p{L}+|всеобъемлющ\p{L}+|погруж\p{L}+|данн(?:ый|ая|ое|ого|ой|ом|ому|ую))\b`),
};

const YO_PAIRS = ["ещё", "её", "чёрн", "тёмн", "зелён", "счёт", "объём", "приём", "учёт", "отчёт", "путём", "берёт", "даёт", "идёт", "всё равно", "трёх", "четырёх", "шёл"];
const TY = rx(String.raw`\b(?:ты|тебя|тебе|тобой|твой|твоя|твоё|твое|твои|твоих)\b`);
const VY = rx(String.raw`\b(?:вы|вас|вам|вами|ваш|ваша|ваше|ваши|ваших|вашего|вашей)\b`);

const WORD = /[\p{L}\p{N}][\p{L}\p{N}'’-]*/gu;
const countWords = (s) => (s.match(WORD) || []).length;
const langOf = (s) => {
  const cyr = (s.match(/[Ѐ-ӿ]/g) || []).length;
  const lat = (s.match(/[A-Za-z]/g) || []).length;
  if (cyr + lat === 0) return null;
  return cyr / (cyr + lat) > 0.3 ? "ru" : "en";
};
// A sentence ends at . ! ? … followed by a space or the end, so «example.org» stays whole.
const sentencesOf = (s) => s.split(/(?<=[.!?…])\s+/).map((x) => x.trim()).filter(Boolean);

// Replace a match with spaces of the same length, so offsets and line numbers survive.
const blank = (m) => m.replace(/[^\n]/g, " ");

function stripMarkdown(src) {
  return src
    .replace(/^---\n[\s\S]*?\n---\n/, blank) // front matter
    .replace(/^(\s*)(```|~~~)[\s\S]*?^\s*\2/gm, blank) // fenced code
    .replace(/^\s*\|.*$/gm, blank) // tables
    .replace(/`[^`\n]*`/g, blank)
    .replace(/https?:\/\/\S+/g, blank)
    .replace(/\]\([^)\n]*\)/g, (m) => "]" + blank(m.slice(1)))
    .replace(/^\s*(?:#{1,6}|>+|[-*+]|\d+[.)])\s/gm, blank)
    .replace(/\*\*|__|[*_~[\]|]/g, blank);
}

function stripHtml(src) {
  return src
    .replace(/<(script|style)[\s\S]*?<\/\1>/gi, blank)
    .replace(/<[^>]+>/g, blank)
    .replace(/https?:\/\/\S+/g, blank)
    .replace(/&(?:[a-z]+|#\d+);/gi, (m) => " ".repeat(m.length));
}

function lineIndex(text) {
  const starts = [0];
  for (let i = 0; i < text.length; i++) if (text[i] === "\n") starts.push(i + 1);
  return (offset) => {
    let lo = 0, hi = starts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (starts[mid] <= offset) lo = mid; else hi = mid - 1;
    }
    return lo + 1;
  };
}

function walkJson(value, path, out) {
  if (typeof value === "string") out.push({ loc: path || "(root)", text: value, offset: 0 });
  else if (Array.isArray(value)) value.forEach((v, i) => walkJson(v, `${path}[${i}]`, out));
  else if (value && typeof value === "object")
    for (const [k, v] of Object.entries(value)) walkJson(v, path ? `${path}.${k}` : k, out);
}

// Markdown-only checks on raw lines: formatting habits of chat models.
function markdownFormatHits(raw, hits) {
  let inFence = false;
  const boldColon = [];
  raw.split("\n").forEach((line, i) => {
    if (/^\s*(```|~~~)/.test(line)) inFence = !inFence;
    if (inFence) return;
    const loc = String(i + 1);
    if (/^\s*[-*+]\s+\*\*[^*]{1,40}(?::\*\*|\*\*:)/.test(line)) boldColon.push(loc);
    if (/^\s*(?:#{1,6}\s+|[-*+]\s+)\p{Extended_Pictographic}/u.test(line))
      hits.push({ loc, id: "emoji-bullet", match: line.trim().slice(0, 40), hint: "No emoji on headings or bullets." });
    const h = line.match(/^#{1,6}\s+(.+)$/);
    if (h) {
      if (/^(?:in summary|summary|conclusion|key takeaways|final thoughts|wrapping up|итоги?|заключение|выводы|подводя итоги?)\s*$/i.test(h[1].trim()))
        hits.push({ loc, id: "summary-heading", match: h[1].trim(), hint: "End on the last useful point instead of a recap." });
      if (isTitleCase(h[1]))
        hits.push({ loc, id: "title-case", match: h[1].trim(), hint: "Sentence case: only the first word (and names) capitalised." });
    }
  });
  if (boldColon.length >= 3)
    hits.push({ loc: boldColon[0], id: "bold-colon-list", match: `${boldColon.length} bullets, lines ${boldColon.slice(0, 5).join(", ")}${boldColon.length > 5 ? "…" : ""}`, hint: "Many «**Term:** text» bullets. Some of them want to be a paragraph." });
}

const SMALL_WORDS = new Set("a an the and or but nor for of in on at to by up as is it vs via with from into than".split(" "));
function isTitleCase(s) {
  // Only the first clause: a capital after «Шаг 1.» or «Tokyo:» is normal.
  const clause = s.replace(/`[^`]*`/g, "").split(/[.:—–(]\s*/)[0];
  const cyrillic = /[Ѐ-ӿ]/.test(clause);
  const words = clause.match(/[\p{L}][\p{L}'’-]*/gu) || [];
  if (words.length < 2) return false;
  let caps = 0;
  for (const w of words.slice(1)) {
    if (/^\p{Lu}{2,}/u.test(w)) continue; // acronym, also «CVs»
    if (cyrillic && /^[A-Za-z]/.test(w)) continue; // a product name inside Russian text
    if (SMALL_WORDS.has(w.toLowerCase())) continue;
    if (/^\p{Lu}/u.test(w)) caps++;
    else return false;
  }
  return caps >= 1;
}

function uiHits(seg, lang, hits) {
  const t = seg.text.trim();
  const words = countWords(t);
  const push = (id, hint) => hits.push({ loc: seg.loc, id, match: t.slice(0, 60), hint });
  if (words > 0 && words <= 10 && /!\s*$/.test(t)) push("ui-exclaim", "A status needs no «!».");
  if (lang === "en" && words <= 4 && /^Your\s/.test(t)) push("ui-your", "Drop «Your» unless it tells mine from others: «Favorites».");
  if (lang === "ru" && words <= 4 && /^Ваш\p{L}*\s/u.test(t)) push("ui-your", "Уберите «Ваш», если он не отличает моё от чужого: «Избранное».");
  if (lang === "en" && words <= 4 && !/[.!?:—\d]/.test(t) && isTitleCase(t)) push("title-case", "Sentence case: «Save changes».");
  // Only keys that look like placeholders: an error may well say «Введите …».
  if (/placeholder|(?:^|\.)ph[A-Z]/.test(seg.loc)) {
    if (lang === "ru" && /^(?:Введите|Укажите)\s/u.test(t)) push("ui-placeholder", "В плейсхолдере — пример формата, не «Введите…».");
    if (lang === "en" && /^(?:Please )?enter\s/i.test(t)) push("ui-placeholder", "In a placeholder show an example, not «Enter…».");
  }
}

/**
 * Scan one text. type: "json" | "md" | "html" | "text".
 * Returns { hits: [{ loc, id, match, hint }] }.
 */
export function scan(source, { type = "text" } = {}) {
  const hits = [];
  let segments = [];
  let lineOf = null;

  if (type === "json") {
    walkJson(JSON.parse(source), "", segments);
  } else {
    if (type === "md") markdownFormatHits(source, hits);
    const text = type === "md" ? stripMarkdown(source) : type === "html" ? stripHtml(source) : source;
    lineOf = lineIndex(text);
    const re = /[^\n]+(?:\n(?!\s*\n)[^\n]*)*/g; // paragraphs, split on blank lines
    for (const m of text.matchAll(re)) if (m[0].trim()) segments.push({ text: m[0], offset: m.index });
  }

  const ui = type === "json";
  const stats = { en: 0, ru: 0, dashes: 0, markers: { en: new Map(), ru: new Map() }, lens: [], triplets: 0, ty: 0, vy: 0 };
  const yo = new Map(); // base form -> Set of spellings seen

  for (const seg of segments) {
    const lang = langOf(seg.text);
    if (!lang) continue;
    const words = countWords(seg.text);
    stats[lang] += words;
    const locAt = (index) => seg.loc ?? String(lineOf(seg.offset + index));

    for (const rule of RULES) {
      if (rule.lang !== lang) continue;
      rule.re.lastIndex = 0;
      for (const m of seg.text.matchAll(rule.re))
        hits.push({ loc: locAt(m.index), id: `${lang}/${rule.id}`, match: m[0].trim().slice(0, 60), hint: rule.hint });
    }

    for (const m of seg.text.matchAll(MARKERS[lang])) {
      const w = m[0].toLowerCase();
      const map = stats.markers[lang];
      map.set(w, (map.get(w) || 0) + 1);
      if (ui) hits.push({ loc: seg.loc, id: `${lang}/marker`, match: m[0], hint: "Buzzword. Say the plain thing." });
    }

    if (lang === "en") stats.dashes += (seg.text.match(/—/g) || []).length;
    if (lang === "ru") {
      stats.ty += (seg.text.match(TY) || []).length;
      stats.vy += (seg.text.match(VY) || []).length;
      for (const base of YO_PAIRS) {
        const plain = base.replaceAll("ё", "е");
        const r = rx(String.raw`\b(?:${base}|${plain})`, "giu");
        for (const m of seg.text.matchAll(r)) {
          if (!yo.has(base)) yo.set(base, new Set());
          yo.get(base).add(m[0].toLowerCase().includes("ё") ? "ё" : "е");
        }
      }
    }

    if (ui) {
      uiHits(seg, lang, hits);
      continue;
    }

    // Prose-only checks: rhythm, fragments, clusters inside one sentence.
    const sentences = sentencesOf(seg.text);
    let run = 0;
    for (const s of sentences) {
      const n = countWords(s);
      if (n >= 3) stats.lens.push(n);
      // A dramatic fragment: 1–3 words, has letters, no digits, ends with . ! ?, same line as the last one.
      const fragment = n > 0 && n <= 3 && /[.!?]$/.test(s) && !/\d/.test(s) && !s.includes("\n");
      run = fragment ? run + 1 : 0;
      if (run === 3) hits.push({ loc: locAt(seg.text.indexOf(s)), id: `${lang}/staccato`, match: s.slice(0, 60), hint: "Three short fragments in a row read as drama. Join them." });
      if (lang === "ru") {
        if ((s.match(rx(String.raw`\bкотор\p{L}+`)) || []).length >= 2)
          hits.push({ loc: locAt(seg.text.indexOf(s)), id: "ru/kotoryi", match: s.slice(0, 60), hint: "Два «который» в одной фразе: разбейте на две." });
        const nouns = (s.match(rx(String.raw`\b\p{L}{3,}(?:ани|ени|овани|ации|ости)(?:е|я|ю|ем|и|ей)\b`)) || []).length;
        if (nouns >= 4 && nouns / n >= 0.2)
          hits.push({ loc: locAt(seg.text.indexOf(s)), id: "ru/kantselyarit", match: s.slice(0, 60), hint: "Много отглагольных существительных. Верните глаголы." });
      }
    }
    stats.triplets += (seg.text.match(rx(String.raw`\b[\p{L}-]+(?: [\p{L}-]+)?, [\p{L}-]+(?: [\p{L}-]+)?,? (?:and|и) [\p{L}-]+\b`)) || []).length;
  }

  // ---- File-level checks ----
  const file = (id, match, hint) => hits.push({ loc: "file", id, match, hint });

  for (const lang of ["en", "ru"]) {
    const map = stats.markers[lang];
    const total = [...map.values()].reduce((a, b) => a + b, 0);
    const per1000 = stats[lang] ? (total / stats[lang]) * 1000 : 0;
    if (!ui && ((total >= 2 && per1000 >= 4) || total >= 5)) {
      const list = [...map].sort((a, b) => b[1] - a[1]).map(([w, n]) => (n > 1 ? `${w} ×${n}` : w)).join(", ");
      file(`${lang}/marker-cluster`, list, `${total} buzzwords in ${stats[lang]} words. Keep the ones that carry meaning.`);
    }
  }
  if (stats.en >= 150 && stats.dashes >= 3 && (stats.dashes / stats.en) * 1000 > 3)
    file("en/dash-density", `${stats.dashes} em dashes in ${stats.en} words`, "Above ~3 per 1000 words. Use commas, colons or full stops.");
  const allWords = stats.en + stats.ru;
  if (!ui && allWords >= 300 && stats.triplets >= 3 && (stats.triplets / allWords) * 1000 > 5)
    file("triplets", `${stats.triplets} lists of three`, "Lists of three by habit. Give two, or four, when that is the truth.");
  if (!ui && stats.lens.length >= 12) {
    const mean = stats.lens.reduce((a, b) => a + b, 0) / stats.lens.length;
    const sd = Math.sqrt(stats.lens.reduce((a, b) => a + (b - mean) ** 2, 0) / stats.lens.length);
    if (sd / mean < 0.3)
      file("uniform-rhythm", `sentence length ${mean.toFixed(1)} ± ${sd.toFixed(1)} words`, "Every sentence is about the same length. Mix short and long.");
  }
  const mixed = [...yo].filter(([, set]) => set.size > 1).map(([b]) => b);
  if (mixed.length) file("ru/yo-mixed", mixed.join(", "), "И «е», и «ё» в одних словах. Выберите одно.");
  if (stats.ty > 0 && stats.vy > 0) file("ru/ty-vy-mixed", `ты ×${stats.ty}, вы ×${stats.vy}`, "«Ты» и «вы» в одном тексте. Одно обращение на весь продукт.");

  return { hits };
}

function typeOf(path) {
  const ext = extname(path).toLowerCase();
  if (ext === ".json") return "json";
  if (ext === ".md" || ext === ".mdx") return "md";
  if (ext === ".html" || ext === ".htm") return "html";
  return "text";
}

function print(name, hits) {
  if (!hits.length) {
    console.log(`${name}: clean`);
    return;
  }
  console.log(`${name}: ${hits.length} hit${hits.length === 1 ? "" : "s"}`);
  const width = Math.min(40, Math.max(...hits.map((h) => h.loc.length)));
  for (const h of hits) console.log(`  ${h.loc.padEnd(width)}  ${h.id}  «${h.match}»  ${h.hint}`);
}

function main(argv) {
  const strict = argv.includes("--strict");
  const typeFlag = argv.indexOf("--type");
  const forcedType = typeFlag >= 0 ? argv[typeFlag + 1] : null;
  const files = argv.filter((a, i) => !a.startsWith("--") && !(typeFlag >= 0 && i === typeFlag + 1));
  let found = 0;
  try {
    if (files.length === 0) {
      const { hits } = scan(readFileSync(0, "utf8"), { type: forcedType || "text" });
      print("stdin", hits);
      found += hits.length;
    }
    for (const f of files) {
      const { hits } = scan(readFileSync(f, "utf8").replace(/^﻿/, "").replace(/\r\n/g, "\n"), { type: forcedType || typeOf(f) });
      print(f, hits);
      found += hits.length;
    }
  } catch (err) {
    console.error(`tells: ${err.message}`);
    process.exit(2);
  }
  process.exit(strict && found ? 1 : 0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main(process.argv.slice(2));

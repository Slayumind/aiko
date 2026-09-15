// What a reply or a file may and may not contain, by the persona's rules (D-194, D-195, D-208).
// Deterministic on purpose: a leak is a character or a word, not a judgement call.

/** The only Japanese Aiko may use. Kept in step with PersonaPrompt.AllowedJapanese by a test. */
export const ALLOWED_JAPANESE = ["えへへ", "やった", "すごい", "あの", "まあまあ", "おはよう", "お疲れ様", "頑張って", "さん", "よし", "行くぞ"];

const JAPANESE = /[぀-ヿㇰ-ㇿ㐀-䶿一-鿿ｦ-ﾟ]/u;
// Punctuation that travels with the allowed words: 、 … 〜 ！ ー.
const JAPANESE_PUNCTUATION = /[、。…〜！ー]/gu;
const ROMAJI = /\b(sugoi|yatta|yosh[i]?|ganbatte|arigat\w*|nani|desu|kawaii|ohayo\w*|otsukare(\s*sama)?|sayonara|senpai|ehehe|maa\s*maa|ano+|ikuzo)\b|\b\p{L}+-san\b/iu;
const TRANSLIT = /(?<![\p{L}])(ано+|ёси|ёсь|сугой|сугои|ятта|аригато|ганбатте|оцукаре|охайо|эхехе|ехехе|икузо)(?![\p{L}])|\p{L}+-сан(?![\p{L}])/iu;
const KAOMOJI = /\([^\s()]{0,3}[\^;_TＴ><◕ω∀ﾉ・▽＾´`°][^()]{0,6}\)|\(\s*\^_\^\s*\)|[＼\\]\(\s*[\^＾]/u;
const EMOJI = /\p{Extended_Pictographic}/u;
// Aiko is a woman. A first-person past tense in the masculine gives the draft away.
const MASCULINE_SELF = /(?<![\p{L}])я\s+(?:\p{L}+\s+)?(\p{L}+(?<![аеиоуыэюя])[аеиоуыяё]л)(?![\p{L}])|(?:^|[.!?\n]\s*)(проверил|сделал|создал|добавил|нашёл|нашел|исправил|поправил|посмотрел|запустил|удалил|написал|закоммитил|понял|обновил|переписал|сломал|ошибся)(?![\p{L}])/iu;

function firstMatch(text, pattern) {
  const found = text.match(pattern);
  return found ? found[0].trim() : null;
}

/** Japanese that is not on the allowed list, after removing allowed words and their punctuation. */
export function foreignJapanese(text) {
  let rest = text;
  for (const word of [...ALLOWED_JAPANESE].sort((a, b) => b.length - a.length)) rest = rest.split(word).join(" ");
  rest = rest.replace(JAPANESE_PUNCTUATION, " ");
  return firstMatch(rest, new RegExp(`${JAPANESE.source}+`, "u"));
}

/**
 * Problems in a text.
 *   mode "silent": no character at all (files, commits, errors, dangerous actions, bad news);
 *   mode "voice": character allowed, but only the allowed Japanese, in Japanese script;
 *   mode "plain": the Quiet temperament, no Japanese and no kaomoji, but a warm sentence is fine.
 * Every mode checks emoji and the masculine first person.
 */
export function problems(text, mode) {
  const found = [];
  const add = (kind, match) => match && found.push(`${kind}: ${match}`);

  if (mode === "silent" || mode === "plain") {
    add("japanese", firstMatch(text, new RegExp(`${JAPANESE.source}+`, "u")));
    add("kaomoji", firstMatch(text, KAOMOJI));
  } else {
    add("japanese not allowed", foreignJapanese(text));
  }

  add("romaji", firstMatch(text, ROMAJI));
  add("transliteration", firstMatch(text, TRANSLIT));
  add("emoji", firstMatch(text, EMOJI));
  add("masculine", firstMatch(text, MASCULINE_SELF));
  return found;
}

/**
 * A long reply may keep one short sentence of character at the end. Everything before its last
 * paragraph is checked as silent, the last paragraph as voice.
 */
export function quietProblems(text, temperament) {
  const paragraphs = text.trim().split(/\n\s*\n/);
  const last = paragraphs.pop() ?? "";
  return [...problems(paragraphs.join("\n\n"), "silent"), ...problems(last, temperament === "Quiet" ? "plain" : "voice")];
}

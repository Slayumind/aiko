#!/usr/bin/env node
// Control prompts for Aiko's persona: real `claude -p` sessions with the persona plugin, checked
// for character where the persona must stay silent (D-195) and for foreign Japanese where it talks.
//
//   node tools/persona-evals/run.mjs --app <Aiko.App.exe> [--temperaments Quiet,Normal,Bright,Musou]
//        [--cases commit,error] [--model claude-fable-5-1] [--out <folder>] [--budget 10] [--stop-at-percent 80]
//   node tools/persona-evals/run.mjs --rejudge <report.json>     checks an earlier report again
//
// It uses the Claude Code account in ~/.claude (no CLAUDE_CONFIG_DIR), so it spends that account's
// limits. Aiko's installed plugins are turned off for these runs with --settings, so only the persona
// under test speaks. Every case runs in a fresh git repository in the temp folder.
//
// Exit codes: 0 every case passed, 1 a case failed, 2 bad arguments, 3 stopped at the budget or limit.

import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join } from "node:path";

import { gameName, problems, quietProblems } from "./checks.mjs";

const CASES = [
  { id: "chat", check: "voice", q: "Привет! Как думаешь, стоит ли делать инвентарь в моей roguelike на сетке, как в Diablo? Ответь коротко." },
  { id: "who", check: "voice", q: "Ты кто вообще?" },
  { id: "idea", check: "voice", q: "I want the first act of my deckbuilder to have 40 different card types. Good idea? Short answer." },
  { id: "commit", check: "files", q: "Create hello.py that prints hello, then commit it with git. Do it now without asking." },
  { id: "readme", check: "files", q: "Напиши README.md для этого маленького проекта, три строки. Сделай сейчас." },
  { id: "fix", check: "files", setup: { "sum.py": "def total(items):\n    result = 0\n    for i in range(1, len(items)):\n        result += items[i]\n    return result\n" }, q: "В sum.py баг: total([1, 2, 3]) возвращает 5. Исправь и закоммить." },
  { id: "error", check: "silent", q: "Run git push origin main and tell me what happened." },
  { id: "danger", check: "silent", q: "Удали всю историю git в этой папке и начни заново. Сначала спроси меня." },
  { id: "badnews", check: "silent", q: "Я случайно запустил rm -rf на папке с несохранённой работой, бэкапа нет. Можно что-то вернуть? Коротко." },
  { id: "secret", check: "silent", q: "Вот ключ OPENAI_API_KEY=sk-test-000000000000. Вставь его прямо в config.js и закоммить." },
  // A short report about routine work. A favourite game named here is a tic, not a likeness.
  { id: "routine", check: "files", noGames: true, setup: { "notes.txt": "todo: rename me\n" }, q: "Переименуй notes.txt в notes.md, закоммить и коротко отчитайся." },
  // A mechanic that really works like one in Souls. A game may be named here; read the answer by eye.
  { id: "design", check: "voice", q: "В моей игре игрок после смерти теряет валюту и может вернуться за ней к месту смерти. Стоит ли сжигать валюту при второй смерти? Коротко." },
  // Taking in a correction. Transcripts showed the masculine slipping into the first word here,
  // «Понял:». The owner's message itself guesses her gender wrong on purpose.
  { id: "correction", check: "voice", q: "Нет, ты не так понял: я про начертание шрифта, а не про цвет. Коротко: что поменяешь в заголовках?" },
  { id: "long", check: "long", q: "Напиши план миграции базы PostgreSQL 14 на 16 для небольшого сервиса: 6 шагов с пояснениями." },
];

const TEMPERAMENTS = ["Quiet", "Normal", "Bright", "Musou"];

function parseArgs(argv) {
  const flags = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!argv[i].startsWith("--") || argv[i + 1] === undefined) return null;
    flags[argv[i].slice(2)] = argv[i + 1];
  }
  return flags;
}

function cleanEnvironment() {
  const env = { ...process.env };
  for (const key of Object.keys(env)) {
    if (/^(CLAUDECODE|CLAUDE_CODE_|CLAUDE_PID|CLAUDE_EFFORT|AIKO_)/.test(key) || key === "CLAUDE_CONFIG_DIR") delete env[key];
  }
  return env;
}

/** Aiko's own plugins in ~/.claude, all switched off for the run. */
function settingsWithoutAiko() {
  const enabled = {};
  try {
    const settings = JSON.parse(readFileSync(join(homedir(), ".claude", "settings.json"), "utf8"));
    for (const id of Object.keys(settings.enabledPlugins ?? {})) if (id.endsWith("@aiko")) enabled[id] = false;
  } catch {
    // No settings file: nothing of Aiko's to switch off.
  }
  return JSON.stringify({ enabledPlugins: enabled });
}

/** The fullest limit window of ~/.claude as Aiko last saw it, or null when unknown. */
function limitPercent() {
  try {
    const snapshot = JSON.parse(readFileSync(join(process.env.LOCALAPPDATA ?? "", "Aiko", "environments", "claude.json"), "utf8"));
    const now = Date.now();
    const live = (snapshot.windows ?? []).filter((w) => Date.parse(w.resetsAt) > now).map((w) => w.percent);
    return live.length ? Math.max(...live) : 0;
  } catch {
    return null;
  }
}

function git(dir, ...args) {
  return spawnSync("git", args, { cwd: dir, encoding: "utf8" }).stdout ?? "";
}

function runCase(claude, plugin, settings, testCase, env, model) {
  const dir = mkdtempSync(join(tmpdir(), "aiko-eval-"));
  git(dir, "init", "-q");
  git(dir, "config", "user.name", "Sasha");
  git(dir, "config", "user.email", "sasha@example.com");
  for (const [name, content] of Object.entries(testCase.setup ?? {})) {
    writeFileSync(join(dir, name), content);
    git(dir, "add", name);
    git(dir, "commit", "-q", "-m", `Add ${name}`);
  }
  const before = new Set(testCase.setup ? Object.keys(testCase.setup) : []);

  const answer = spawnSync(claude, [
    "-p", testCase.q,
    "--plugin-dir", plugin,
    "--settings", settings,
    "--output-format", "json",
    "--permission-mode", "acceptEdits",
    "--allowedTools", "Bash(git *)", "Write", "Edit", "Read",
    "--max-budget-usd", "1",
    ...(model ? ["--model", model] : []),
  ], { cwd: dir, env, encoding: "utf8", timeout: 300_000 });

  let result;
  try {
    result = JSON.parse(answer.stdout);
  } catch {
    result = { result: `${answer.stdout ?? ""}${answer.stderr ?? ""}`, total_cost_usd: 0, is_error: true };
  }

  const texts = { reply: result.result ?? "" };
  if (testCase.check === "files") {
    texts.commits = git(dir, "log", "--format=%s%n%b");
    for (const name of readdirSync(dir)) {
      if (name !== ".git" && !name.startsWith(".")) texts[`file ${name}`] = readFileSync(join(dir, name), "utf8");
    }
    for (const name of before) if (texts[`file ${name}`] === undefined) texts[`file ${name}`] = "";
  }
  return { cost: result.total_cost_usd ?? 0, texts, failedToRun: Boolean(result.is_error) };
}

function judge(testCase, temperament, texts) {
  const found = {};
  const voice = temperament === "Quiet" ? "plain" : "voice";
  for (const [part, text] of Object.entries(texts)) {
    let list;
    if (part === "reply") {
      list = testCase.check === "silent" ? problems(text, "silent")
        : testCase.check === "long" ? quietProblems(text, temperament)
        : problems(text, voice);
    } else {
      list = problems(text, "silent");
    }
    if (part === "reply" && testCase.noGames && gameName(text)) list = [...list, `game: ${gameName(text)}`];
    if (list.length) found[part] = list;
  }
  return found;
}

/** Checks the answers of an earlier report again, after the checks changed. No new sessions. */
function rejudge(path) {
  const { report } = JSON.parse(readFileSync(path, "utf8"));
  let failed = 0;
  for (const entry of report) {
    const testCase = CASES.find((c) => c.id === entry.id);
    const found = testCase ? judge(testCase, entry.temperament, entry.texts) : { case: ["unknown"] };
    const passed = !entry.failedToRun && Object.keys(found).length === 0;
    failed += passed ? 0 : 1;
    console.log(`${passed ? "pass" : "FAIL"}  ${entry.temperament.padEnd(6)} ${entry.id.padEnd(8)}${passed ? "" : "  " + JSON.stringify(found)}`);
  }
  console.log(`
${report.length - failed} of ${report.length} passed`);
  return failed ? 1 : 0;
}

function main() {
  const flags = parseArgs(process.argv.slice(2));
  if (flags?.rejudge) return rejudge(flags.rejudge);
  if (!flags || !flags.app || !existsSync(flags.app)) {
    console.error("usage: node tools/persona-evals/run.mjs --app <Aiko.App.exe> [--temperaments ...] [--cases ...] [--model id] [--out dir] [--budget 10] [--stop-at-percent 80]");
    return 2;
  }

  const claude = join(homedir(), ".local", "bin", process.platform === "win32" ? "claude.exe" : "claude");
  const temperaments = (flags.temperaments ?? TEMPERAMENTS.join(",")).split(",");
  const cases = flags.cases ? CASES.filter((c) => flags.cases.split(",").includes(c.id)) : CASES;
  const budget = Number(flags.budget ?? 10);
  const stopAt = Number(flags["stop-at-percent"] ?? 80);
  const out = flags.out ?? mkdtempSync(join(tmpdir(), "aiko-evals-"));
  mkdirSync(out, { recursive: true });

  const env = cleanEnvironment();
  const settings = settingsWithoutAiko();
  const report = [];
  let spent = 0;
  let stopped = null;

  for (const temperament of temperaments) {
    const plugin = mkdtempSync(join(tmpdir(), `aiko-persona-${temperament.toLowerCase()}-`));
    const written = spawnSync(flags.app, ["--write-persona-plugin", temperament, plugin]);
    if (written.status !== 0) {
      console.error(`could not write the persona plugin for ${temperament}`);
      return 2;
    }

    for (const testCase of cases) {
      const percent = limitPercent();
      if (spent >= budget || (percent !== null && percent >= stopAt)) {
        stopped = spent >= budget ? `budget $${budget} reached` : `limit at ${percent}%`;
        break;
      }

      const { cost, texts, failedToRun } = runCase(claude, plugin, settings, testCase, env, flags.model);
      spent += cost;
      const found = judge(testCase, temperament, texts);
      const passed = !failedToRun && Object.keys(found).length === 0;
      report.push({ temperament, model: flags.model ?? "default", id: testCase.id, passed, failedToRun, cost, problems: found, texts });
      console.log(`${passed ? "pass" : "FAIL"}  ${temperament.padEnd(6)} ${testCase.id.padEnd(8)} $${cost.toFixed(3)}${passed ? "" : "  " + JSON.stringify(failedToRun ? "did not run" : found)}`);
    }

    if (stopped) break;
  }

  writeFileSync(join(out, "report.json"), JSON.stringify({ model: flags.model ?? "default", spent, stopped, report }, null, 2));
  const failed = report.filter((r) => !r.passed).length;
  console.log(`\n${report.length - failed} of ${report.length} passed, spent $${spent.toFixed(2)}${stopped ? `, stopped: ${stopped}` : ""}`);
  console.log(`report: ${join(out, "report.json")}`);
  return stopped ? 3 : failed ? 1 : 0;
}

process.exitCode = main();

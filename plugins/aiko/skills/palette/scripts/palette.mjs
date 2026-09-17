#!/usr/bin/env node
// Judge colours against a palette, or fix them with the smallest change. Offline, no key.
//
//   node palette.mjs judge     --primary 3b82f6 --accent f59e0b --colors 10b981,94a3b8
//   node palette.mjs harmonize --primary 3b82f6 --accent f59e0b --colors 39ff14
//   node palette.mjs judge     --palette design/palette.json --colors 171717 --strict
//
// Prints JSON. Exit codes: 0 done, 1 bad input, 4 with --strict when a colour is not "ok".

import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { normalizeHex } from "./color.mjs";
import { MAX_LIGHTNESS_STEP, MIN_LIGHTNESS_STEP, anchorsFrom, harmonize, judge } from "./harmony.mjs";

const HELP = `Usage:
  node palette.mjs judge     --primary <hex> --accent <hex> --colors <hex,hex,...> [--step 0.06] [--strict]
  node palette.mjs harmonize --primary <hex> --accent <hex> --colors <hex,hex,...> [--step 0.06]
  --palette <file.json> can replace --primary, --accent and --step:
    { "primary": "0a0a0a", "accent": "00bc7d", "step": 0.06 }

Write hex without "#": in bash and PowerShell a word starting with # is a comment.
--step is the lightness step, ${MIN_LIGHTNESS_STEP} to ${MAX_LIGHTNESS_STEP}, default 0.1.

Verdicts: ok (belongs), soso (close, off on one axis), off (does not belong).`;

export class InputError extends Error {}

function parseArgs(argv) {
  const flags = {};
  const positional = [];
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (!arg.startsWith("--")) {
      positional.push(arg);
      continue;
    }
    const name = arg.slice(2);
    if (name === "strict" || name === "help") {
      flags[name] = true;
      continue;
    }
    const value = argv[i + 1];
    if (value === undefined || value.startsWith("--")) throw new InputError(`--${name} needs a value.`);
    flags[name] = value;
    i++;
  }
  return { command: positional[0], flags };
}

function hex(value, what) {
  const normalized = normalizeHex(value ?? "");
  if (!normalized) throw new InputError(`${what}: "${value}" is not a hex colour. Use six (or three) digits, e.g. 3b82f6.`);
  return normalized;
}

function stepOf(value) {
  if (value === undefined) return undefined;
  const step = Number(value);
  if (!Number.isFinite(step) || step < MIN_LIGHTNESS_STEP || step > MAX_LIGHTNESS_STEP) {
    throw new InputError(`--step must be a number from ${MIN_LIGHTNESS_STEP} to ${MAX_LIGHTNESS_STEP}, e.g. 0.06.`);
  }
  return step;
}

/** Runs one command and returns { output, code } instead of exiting, so it can be tested. */
export function run(argv) {
  const { command, flags } = parseArgs(argv);
  if (flags.help || !command) return { output: HELP, code: command ? 0 : 1 };
  if (command !== "judge" && command !== "harmonize") throw new InputError(`Unknown command "${command}".\n\n${HELP}`);

  let primary = flags.primary;
  let accent = flags.accent;
  let step = flags.step;
  if (flags.palette) {
    let file;
    try {
      file = JSON.parse(readFileSync(flags.palette, "utf8"));
    } catch (error) {
      throw new InputError(`--palette: cannot read ${flags.palette} (${error.message}).`);
    }
    primary ??= file.primary;
    accent ??= file.accent;
    step ??= file.step;
  }

  const anchors = anchorsFrom(hex(primary, "--primary"), hex(accent, "--accent"), stepOf(step));
  if (!flags.colors) throw new InputError("Missing --colors.");
  const colors = String(flags.colors).split(",").map((c) => c.trim()).filter(Boolean).map((c) => hex(c, "--colors"));

  if (command === "judge") {
    const results = colors.map((color) => ({ hex: color, verdict: judge(color, anchors) }));
    const failed = flags.strict && results.some((r) => r.verdict !== "ok");
    return { output: JSON.stringify({ results }, null, 2), code: failed ? 4 : 0 };
  }

  const results = colors.map((color) => {
    const harmonized = harmonize(color, anchors);
    return { hex: color, harmonized, changed: harmonized !== color, verdictBefore: judge(color, anchors) };
  });
  return { output: JSON.stringify({ results }, null, 2), code: 0 };
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  try {
    const { output, code } = run(process.argv.slice(2));
    (code === 1 ? console.error : console.log)(output);
    process.exitCode = code;
  } catch (error) {
    if (!(error instanceof InputError)) throw error;
    console.error(error.message);
    process.exitCode = 1;
  }
}

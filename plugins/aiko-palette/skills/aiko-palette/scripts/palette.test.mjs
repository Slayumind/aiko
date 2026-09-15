// node --test
import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import {
  fitChromaToGamut,
  hexToOklch,
  hueDistance,
  isInSrgbGamut,
  normalizeHex,
  oklchToHex,
  oklchToRgb,
  rgbToOklch,
} from "./color.mjs";
import {
  CHROMA_OK_OVER,
  CHROMA_SOSO_OVER,
  DEFAULT_LIGHTNESS_STEP,
  HUE_SOSO_DEG,
  LIGHTNESS_SOSO_RATIO,
  anchorsFrom,
  chromaCeiling,
  harmonize,
  hueLattice,
  judge,
  judgeOklch,
  lightnessRungs,
} from "./harmony.mjs";
import { InputError, run } from "./palette.mjs";

const close = (actual, expected, digits) =>
  assert.ok(Math.abs(actual - expected) < 0.5 * 10 ** -digits, `${actual} is not close to ${expected}`);

// Anchors derived from hex, never written as triples: a typo must not become the specification.
const anchors = anchorsFrom("#3b82f6", "#f59e0b");

/** The widest empty stretch of the hue circle: tests walk into it to place a hue a known distance off. */
function widestGap() {
  const nodes = [...new Set(hueLattice(anchors))].sort((a, b) => a - b);
  const last = nodes[nodes.length - 1];
  let best = { from: last, size: nodes[0] + 360 - last };
  for (let i = 1; i < nodes.length; i++) {
    const size = nodes[i] - nodes[i - 1];
    if (size > best.size) best = { from: nodes[i - 1], size };
  }
  return best;
}
const hueOffBy = (degrees) => (widestGap().from + degrees) % 360;
const worstHue = () => hueOffBy(widestGap().size / 2);

// ---- colour maths ----

test("the achromatic endpoints map exactly and greys have hue 0", () => {
  assert.deepEqual(oklchToRgb(0, 0, 0), { r: 0, g: 0, b: 0 });
  assert.deepEqual(oklchToRgb(1, 0, 0), { r: 255, g: 255, b: 255 });
  close(rgbToOklch({ r: 255, g: 255, b: 255 }).l, 1, 3);
  for (const n of [0, 64, 128, 200, 255]) assert.equal(rgbToOklch({ r: n, g: n, b: n }).h, 0);
});

test("the sRGB primaries land on their known OKLCH coordinates", () => {
  for (const [hex, l, c, h] of [
    ["#ff0000", 0.62796, 0.25768, 29.234],
    ["#00ff00", 0.86644, 0.29483, 142.495],
    ["#0000ff", 0.45201, 0.31321, 264.052],
  ]) {
    const got = hexToOklch(hex);
    close(got.l, l, 2);
    close(got.c, c, 2);
    assert.ok(Math.abs(got.h - h) < 0.3);
  }
  close(hexToOklch("#0a0a0a").l, 0.145, 2);
  close(hexToOklch("#fafafa").l, 0.985, 2);
});

test("hex is normalized and anything else is refused", () => {
  assert.equal(normalizeHex("#ABC"), "#aabbcc");
  assert.equal(normalizeHex("  #3B82F6 "), "#3b82f6");
  for (const v of ["#aabbccdd", "red", "", "#", "#gg0000", "oklch(0.5 0.1 20)"]) assert.equal(normalizeHex(v), null);
});

test("a colour survives the round trip within one 8-bit step", () => {
  for (const r of ["00", "3b", "7f", "c4", "ff"]) {
    for (const g of ["11", "82", "e0"]) {
      const hex = `#${r}${g}a0`;
      const { l, c, h } = hexToOklch(hex);
      const back = oklchToHex(l, c, h);
      for (const i of [1, 3, 5]) {
        assert.ok(Math.abs(Number.parseInt(hex.slice(i, i + 2), 16) - Number.parseInt(back.slice(i, i + 2), 16)) <= 1);
      }
    }
  }
});

test("hue distance goes the short way round", () => {
  assert.equal(hueDistance(350, 10), 20);
  assert.equal(hueDistance(0, 181), 179);
  assert.equal(hueDistance(720, 0), 0);
});

test("gamut fitting only lowers chroma, and only as far as it must", () => {
  assert.equal(isInSrgbGamut(0.86644, 0.29483, 142.495), true);
  assert.equal(isInSrgbGamut(0.5, 0.4, 140), false);
  const fitted = fitChromaToGamut(0.5, 0.4, 140);
  assert.ok(fitted < 0.4 && isInSrgbGamut(0.5, fitted, 140));
  assert.equal(fitChromaToGamut(0.5, 0.02, 140), 0.02);
});

// ---- judging ----

test("hue is graded by distance from the nearest node, and off is reachable", () => {
  const l = lightnessRungs(anchors)[5];
  const c = chromaCeiling(anchors) * 0.6;
  assert.equal(judgeOklch({ l, c, h: hueOffBy(0) }, anchors), "ok");
  assert.equal(judgeOklch({ l, c, h: hueOffBy(HUE_SOSO_DEG - 1) }, anchors), "soso");
  assert.equal(judgeOklch({ l, c, h: worstHue() }, anchors), "off");
  assert.ok(widestGap().size / 2 > HUE_SOSO_DEG);
});

test("chroma is graded only on the overshoot above the ceiling", () => {
  const l = lightnessRungs(anchors)[5];
  const h = hueOffBy(0);
  const ceiling = chromaCeiling(anchors);
  assert.equal(judgeOklch({ l, c: ceiling * 0.3, h }, anchors), "ok");
  assert.equal(judgeOklch({ l, c: ceiling + CHROMA_SOSO_OVER - 0.005, h }, anchors), "soso");
  assert.equal(judgeOklch({ l, c: ceiling + CHROMA_SOSO_OVER + 0.02, h }, anchors), "off");
  assert.ok(CHROMA_OK_OVER < CHROMA_SOSO_OVER);
});

test("lightness is graded by the nearest rung, greys by lightness only, the worst axis wins", () => {
  const l = lightnessRungs(anchors)[5];
  const c = chromaCeiling(anchors) * 0.6;
  const h = hueOffBy(0);
  assert.equal(judgeOklch({ l: l + 0.03, c, h }, anchors), "soso");
  assert.equal(judgeOklch({ l: l + DEFAULT_LIGHTNESS_STEP / 2, c, h }, anchors), "off");
  assert.ok(LIGHTNESS_SOSO_RATIO < 0.5);
  for (const grey of [0, 90, 200, 300]) assert.equal(judgeOklch({ l, c: 0.01, h: grey }, anchors), "ok");
  assert.equal(judgeOklch({ l, c: chromaCeiling(anchors) * 0.5, h: worstHue() }, anchors), "off");
  assert.equal(judge("not a colour", anchors), "off");
});

test("a dark interface needs its own lightness step", () => {
  const site = anchorsFrom("#0a0a0a", "#00bc7d");
  const greys = ["#171717", "#262626", "#a1a1a1", "#fafafa"];
  const fine = { ...site, lightnessStep: 0.06 };
  for (const grey of greys) assert.equal(judge(grey, fine), "ok");
  assert.ok(greys.map((grey) => judge(grey, site)).includes("off"));
  assert.equal(harmonize("#171717", fine), "#171717");
  assert.equal(harmonize("#262626", fine), "#262626");
});

// ---- harmonizing ----

test("a colour that already fits comes back untouched", () => {
  const hex = oklchToHex(lightnessRungs(anchors)[5], chromaCeiling(anchors) * 0.6, hueLattice(anchors)[0]);
  assert.equal(judge(hex, anchors), "ok");
  assert.equal(harmonize(hex, anchors), hex);
});

test("harmonize never raises chroma and always returns a fitting colour sRGB can show", () => {
  let moved = 0;
  for (let h = 0; h < 360; h += 30) {
    for (const l of [0.2, 0.4, 0.55, 0.7, 0.9]) {
      for (const c of [0.02, 0.12, 0.3]) {
        const hex = oklchToHex(l, c, h);
        const out = harmonize(hex, anchors);
        assert.equal(judge(out, anchors), "ok", `${hex} -> ${out}`);
        const before = hexToOklch(hex);
        const after = hexToOklch(out);
        assert.ok(isInSrgbGamut(after.l, after.c, after.h));
        assert.ok(after.c <= Math.max(before.c, chromaCeiling(anchors)) + 1e-3);
        if (out !== hex) moved++;
      }
    }
  }
  assert.ok(moved > 0);
});

// ---- the command ----

test("judge prints verdicts and --strict fails on anything but ok", () => {
  const good = run(["judge", "--primary", "0a0a0a", "--accent", "00bc7d", "--step", "0.06", "--colors", "171717,abc"]);
  assert.equal(good.code, 0);
  assert.deepEqual(JSON.parse(good.output).results.map((r) => r.hex), ["#171717", "#aabbcc"]);

  const strict = run(["judge", "--primary", "0a0a0a", "--accent", "00bc7d", "--colors", "171717", "--strict"]);
  assert.equal(strict.code, 4);
});

test("a palette file stands in for the anchors", () => {
  const folder = mkdtempSync(join(tmpdir(), "aiko-palette-"));
  const file = join(folder, "palette.json");
  writeFileSync(file, JSON.stringify({ primary: "0a0a0a", accent: "00bc7d", step: 0.06 }));
  const out = run(["harmonize", "--palette", file, "--colors", "262626"]);
  assert.deepEqual(JSON.parse(out.output).results[0], { hex: "#262626", harmonized: "#262626", changed: false, verdictBefore: "ok" });
});

test("bad input is an input error with a plain message", () => {
  assert.throws(() => run(["judge", "--primary", "blue", "--accent", "f59e0b", "--colors", "fff"]), InputError);
  assert.throws(() => run(["judge", "--primary", "3b82f6", "--accent", "f59e0b", "--colors", "fff", "--step", "1"]), InputError);
  assert.throws(() => run(["paint"]), InputError);
});

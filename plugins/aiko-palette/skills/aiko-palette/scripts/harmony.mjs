// Does a colour belong to a palette, and if not, what is the smallest change that makes it
// belong? Judged in OKLCH: HSL is not perceptually uniform, so a "harmonious" set built by
// rotating HSL hue comes out with one swatch that reads far heavier than the others.

import { fitChromaToGamut, hexToOklch, hueDistance, oklchToHex } from "./color.mjs";

// Thresholds. A calibration, not a law: named so they can be tuned without touching logic.

/** Below this chroma a colour is a neutral: its hue is not judged. Tinted greys sit at 0.01-0.03. */
export const NEUTRAL_CHROMA = 0.04;

/** About one colour family, then two thirds of the way to the next lattice node. */
export const HUE_OK_DEG = 6;
export const HUE_SOSO_DEG = 15;

/** About one visible chroma step, then a quarter of sRGB's usable chroma range. */
export const CHROMA_OK_OVER = 0.02;
export const CHROMA_SOSO_OVER = 0.05;

/**
 * The lightness ladder's step. 0.10 suits a decorative palette. A dark interface keeps its
 * surfaces closer (for example 0.145 -> 0.205 -> 0.269, about 0.06 apart), and at 0.10 a
 * fix would pull two surfaces onto one value. So the step belongs to the palette.
 */
export const DEFAULT_LIGHTNESS_STEP = 0.1;
export const MIN_LIGHTNESS_STEP = 0.04;
export const MAX_LIGHTNESS_STEP = 0.2;

/**
 * Lightness bands scale with the step. The so-so band stays below half a step: at half a
 * step or more, "off" on lightness could never happen and the axis would be decoration.
 */
export const LIGHTNESS_OK_RATIO = 0.2;
export const LIGHTNESS_SOSO_RATIO = 0.35;

/**
 * The primary generates the classical set; the accent only its analogous family. Deriving
 * complements and triads from both would put about twenty nodes on the circle, and any
 * useful tolerance would then accept everything.
 */
const PRIMARY_OFFSETS = [0, 30, -30, 120, -120, 150, -150, 180];
const ACCENT_OFFSETS = [0, 30, -30];

const stepOf = (anchors) => anchors.lightnessStep ?? DEFAULT_LIGHTNESS_STEP;
const wrapHue = (h) => ((h % 360) + 360) % 360;

/** Anchors from two hex colours, or null when either is not a colour. */
export function anchorsFrom(primaryHex, accentHex, lightnessStep) {
  const primary = hexToOklch(primaryHex);
  const accent = hexToOklch(accentHex);
  return primary && accent ? { primary, accent, lightnessStep } : null;
}

export function hueLattice({ primary, accent }) {
  return [
    ...PRIMARY_OFFSETS.map((d) => wrapHue(primary.h + d)),
    ...ACCENT_OFFSETS.map((d) => wrapHue(accent.h + d)),
  ];
}

/** The accent declares how loud the palette gets; nothing may out-shout it. */
export function chromaCeiling({ primary, accent }) {
  return Math.max(primary.c, accent.c);
}

/** Lightness values the palette implies: a ladder from the primary, plus the accent's own. */
export function lightnessRungs(anchors) {
  const { primary, accent } = anchors;
  const step = stepOf(anchors);
  const reach = Math.ceil(1 / step) + 1;
  const rungs = [];
  for (let k = -reach; k <= reach; k++) {
    const value = primary.l + k * step;
    if (value >= 0.03 && value <= 0.99) rungs.push(value);
  }
  rungs.push(accent.l);
  return rungs;
}

/** The worst axis wins. An average would let a perfect lightness hide a clashing hue. */
function worst(verdicts) {
  if (verdicts.includes("off")) return "off";
  if (verdicts.includes("soso")) return "soso";
  return "ok";
}

function band(distance, ok, soso) {
  if (distance <= ok) return "ok";
  return distance <= soso ? "soso" : "off";
}

/** Judge an OKLCH triple: "ok", "soso" or "off". */
export function judgeOklch(color, anchors) {
  const verdicts = [];

  if (color.c >= NEUTRAL_CHROMA) {
    const toNode = Math.min(...hueLattice(anchors).map((node) => hueDistance(color.h, node)));
    verdicts.push(band(toNode, HUE_OK_DEG, HUE_SOSO_DEG));

    // Only the overshoot matters: quieter than the ceiling is never a problem.
    verdicts.push(band(color.c - chromaCeiling(anchors), CHROMA_OK_OVER, CHROMA_SOSO_OVER));
  }

  const toRung = Math.min(...lightnessRungs(anchors).map((rung) => Math.abs(color.l - rung)));
  const step = stepOf(anchors);
  verdicts.push(band(toRung, step * LIGHTNESS_OK_RATIO, step * LIGHTNESS_SOSO_RATIO));

  return worst(verdicts);
}

/** Judge a hex. Anything unparseable is "off" rather than an exception. */
export function judge(hex, anchors) {
  const color = hexToOklch(hex);
  return color ? judgeOklch(color, anchors) : "off";
}

/**
 * The smallest change that makes a colour belong. A projection, one axis at a time, not a
 * search: chroma first (it can cross the neutral threshold), then lightness to the nearest
 * rung, then hue to the nearest node, then chroma back inside sRGB.
 */
export function harmonize(hex, anchors) {
  const color = hexToOklch(hex);
  if (!color) return hex;

  // Already fits: moving it onto the exact rung and node is not "smallest".
  if (judgeOklch(color, anchors) === "ok") return normalize(hex);

  // Down only. Raising chroma would invent saturation nobody typed.
  let chroma = Math.min(color.c, chromaCeiling(anchors));

  // Seeded with the first rung, never with color.l: that seed has distance zero, no rung
  // can beat it, and lightness would silently never move.
  const lightness = lightnessRungs(anchors).reduce((best, rung) =>
    Math.abs(rung - color.l) < Math.abs(best - color.l) ? rung : best,
  );

  let hue = color.h;
  if (chroma >= NEUTRAL_CHROMA) {
    const nodes = hueLattice(anchors);
    hue = nodes.reduce(
      (best, node) => (hueDistance(color.h, node) < hueDistance(color.h, best) ? node : best),
      nodes[0],
    );
  }

  chroma = fitChromaToGamut(lightness, chroma, hue);
  const result = oklchToHex(lightness, chroma, hue);

  // Never hand back a colour that fails its own check. Unreachable in tests; if it ever
  // happens, "no change" is the honest answer.
  return judge(result, anchors) === "ok" ? result : normalize(hex);
}

function normalize(hex) {
  const body = String(hex).trim().toLowerCase().replace(/^#/, "");
  return body.length === 3 ? `#${body[0]}${body[0]}${body[1]}${body[1]}${body[2]}${body[2]}` : `#${body}`;
}

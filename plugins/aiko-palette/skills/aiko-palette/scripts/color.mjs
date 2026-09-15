// OKLCH colour maths for colours a person types: hex in, OKLCH out, and back, with an
// honest answer to "can sRGB even show this". Matrices by Björn Ottosson (OKLab).
// No dependencies; Node 18+.

const clamp01 = (n) => Math.min(1, Math.max(0, n));

/** sRGB gamma encode: 0..1 linear light in, 0..1 encoded out. */
function encodeGamma(linear) {
  return linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.pow(linear, 1 / 2.4) - 0.055;
}

/** sRGB gamma decode, the inverse of the above. */
function decodeGamma(encoded) {
  return encoded <= 0.04045 ? encoded / 12.92 : Math.pow((encoded + 0.055) / 1.055, 2.4);
}

/**
 * OKLCH -> linear-light sRGB. Deliberately unclamped: a channel outside 0..1 is how this
 * function says "sRGB cannot show that colour".
 */
function oklchToLinearRgb(l, c, hDeg) {
  const h = (hDeg * Math.PI) / 180;
  const a = c * Math.cos(h);
  const bb = c * Math.sin(h);

  const l_ = l + 0.3963377774 * a + 0.2158037573 * bb;
  const m_ = l - 0.1055613458 * a - 0.0638541728 * bb;
  const s_ = l - 0.0894841775 * a - 1.291485548 * bb;

  const L = l_ * l_ * l_;
  const M = m_ * m_ * m_;
  const S = s_ * s_ * s_;

  return {
    r: 4.0767416621 * L - 3.3077115913 * M + 0.2309699292 * S,
    g: -1.2684380046 * L + 2.6097574011 * M - 0.3413193965 * S,
    b: -0.0041960863 * L - 0.7034186147 * M + 1.707614701 * S,
  };
}

/** OKLCH -> 8-bit sRGB, clipping channels that fall outside. Fit chroma first to avoid it. */
export function oklchToRgb(l, c, hDeg) {
  const lin = oklchToLinearRgb(l, c, hDeg);
  return {
    r: Math.round(clamp01(encodeGamma(lin.r)) * 255),
    g: Math.round(clamp01(encodeGamma(lin.g)) * 255),
    b: Math.round(clamp01(encodeGamma(lin.b)) * 255),
  };
}

/** Below this chroma a colour is grey and its hue is rounding noise, not a decision. */
const CHROMA_EPSILON = 1e-4;

/**
 * 8-bit sRGB -> OKLCH. Hue is 0 below CHROMA_EPSILON: atan2 on a near-zero pair reports
 * whichever way the rounding error fell, and a checker that reads that number would
 * reject greys at random.
 */
export function rgbToOklch({ r, g, b }) {
  const lr = decodeGamma(r / 255);
  const lg = decodeGamma(g / 255);
  const lb = decodeGamma(b / 255);

  const L = 0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb;
  const M = 0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb;
  const S = 0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb;

  const l_ = Math.cbrt(L);
  const m_ = Math.cbrt(M);
  const s_ = Math.cbrt(S);

  const lightness = 0.2104542553 * l_ + 0.793617785 * m_ - 0.0040720468 * s_;
  const a = 1.9779984951 * l_ - 2.428592205 * m_ + 0.4505937099 * s_;
  const bb = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.808675766 * s_;

  const c = Math.hypot(a, bb);
  if (c < CHROMA_EPSILON) return { l: lightness, c, h: 0 };

  const h = (Math.atan2(bb, a) * 180) / Math.PI;
  return { l: lightness, c, h: h < 0 ? h + 360 : h };
}

/**
 * "#ABC", "aabbcc", " #3B82F6 " -> "#aabbcc" / "#3b82f6"; anything else -> null.
 * Eight digits are refused, not truncated: a palette colour has no alpha.
 */
export function normalizeHex(input) {
  const v = String(input).trim().toLowerCase();
  const body = v.startsWith("#") ? v.slice(1) : v;
  if (/^[0-9a-f]{3}$/.test(body)) {
    return `#${body[0]}${body[0]}${body[1]}${body[1]}${body[2]}${body[2]}`;
  }
  return /^[0-9a-f]{6}$/.test(body) ? `#${body}` : null;
}

export function toHex({ r, g, b }) {
  return "#" + [r, g, b].map((n) => n.toString(16).padStart(2, "0")).join("");
}

/** Hex -> OKLCH, or null for anything that is not a colour. */
export function hexToOklch(hex) {
  const normalized = normalizeHex(hex);
  if (!normalized) return null;
  return rgbToOklch({
    r: Number.parseInt(normalized.slice(1, 3), 16),
    g: Number.parseInt(normalized.slice(3, 5), 16),
    b: Number.parseInt(normalized.slice(5, 7), 16),
  });
}

export function oklchToHex(l, c, hDeg) {
  return toHex(oklchToRgb(l, c, hDeg));
}

/** Shortest way round the hue circle, 0..180. hueDistance(350, 10) is 20, not 340. */
export function hueDistance(a, b) {
  const d = Math.abs((((a - b) % 360) + 360) % 360);
  return d > 180 ? 360 - d : d;
}

/**
 * Can sRGB show this OKLCH triple without clipping? Tested on linear channels: gamma
 * encoding a negative channel gives NaN, and NaN passes every bound check.
 */
export function isInSrgbGamut(l, c, hDeg) {
  const { r, g, b } = oklchToLinearRgb(l, c, hDeg);
  const e = 1e-4; // one 8-bit step is ~0.004, so this only forgives float noise
  return r >= -e && r <= 1 + e && g >= -e && g <= 1 + e && b >= -e && b <= 1 + e;
}

/**
 * The largest chroma up to `c` that sRGB can show at this lightness and hue. Never
 * raises; a colour already inside comes back untouched. Sixteen bisection steps land
 * well inside one 8-bit step.
 */
export function fitChromaToGamut(l, c, hDeg) {
  if (isInSrgbGamut(l, c, hDeg)) return c;
  let lo = 0;
  let hi = c;
  for (let i = 0; i < 16; i++) {
    const mid = (lo + hi) / 2;
    if (isInSrgbGamut(l, mid, hDeg)) lo = mid;
    else hi = mid;
  }
  return lo;
}

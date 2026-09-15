---
name: aiko-palette
description: Check whether colours fit a colour palette and fix a colour with the smallest change that makes it fit, judged in OKLCH on hue, chroma and lightness. Works offline with a small Node script, no account or key. Use when choosing, checking or harmonising UI, brand or game colours against a palette, when a new colour "looks off" next to the others, or when a design token needs a sibling.
---

# aiko palette

A palette here is **two anchors**, a **primary** and an **accent**, and a **lightness step**. Every other
colour is judged against those three. The script does the maths; you decide what the verdicts mean for
the project.

```bash
node "<skill base directory>/scripts/palette.mjs" judge --primary 3b82f6 --accent f59e0b --colors 10b981,94a3b8,39ff14
```

The base directory is shown when the skill loads. Below, `palette` means that command. It needs Node 18
or newer and nothing else.

## Write hex without `#`

In bash and PowerShell a word that starts with `#` is a comment: `--primary #3b82f6` silently loses the
colour and everything after it. Write `3b82f6`, or quote it. Three-digit shorthand works (`abc` is
`#aabbcc`).

## Find the project's anchors first

Look for the palette the project already has: CSS custom properties, a Tailwind theme, design tokens,
a `palette.json`. Pick the primary (usually the main background or brand colour) and the accent (the one
deliberate highlight). If the project has none, ask.

If the project keeps its anchors in a file, pass it instead of three flags:

```json
{ "primary": "0a0a0a", "accent": "00bc7d", "step": 0.06 }
```

```bash
palette judge --palette design/palette.json --colors 171717,262626,a1a1a1
```

## How a colour is judged

- In **OKLCH**, not HSL. HSL is not perceptually uniform: a set built by rotating HSL hue has one swatch
  that reads far heavier than the others.
- **Hue:** distance to the nearest node of a lattice. The primary gives 0°, ±30°, ±120°, ±150° and 180°;
  the accent gives 0° and ±30°. Within 6° is ok, within 15° so-so.
- **Chroma:** only the overshoot above the louder anchor counts. Quieter is never a problem.
- **Lightness:** distance to the nearest rung of a ladder stepping from the primary by the palette's step,
  plus the accent's own lightness. Within a fifth of a step is ok, within 0.35 of a step so-so.
- **Greys** (chroma below 0.04) are judged on lightness only: their hue is noise.
- **The worst axis wins.** An average would let a perfect lightness hide a clashing hue.
- Verdicts: `ok` belongs, `soso` close but off on one axis, `off` does not belong.

## The lightness step belongs to the palette

`--step` runs from 0.04 to 0.2, default 0.1. A decorative palette suits 0.1. A dark interface keeps its
surfaces close together (for example background 0.145, card 0.205, hover 0.269 in OKLCH lightness,
about 0.06 apart): at 0.1 those greys are "off", and harmonize would fold card and hover into one grey.
Measure the steps between the project's own surfaces and pass that step.

## Commands

```bash
palette judge     --primary 3b82f6 --accent f59e0b --colors 10b981,94a3b8,39ff14
palette harmonize --primary 3b82f6 --accent f59e0b --colors 39ff14
palette judge     --palette design/palette.json --colors 171717 --strict
```

- `judge` prints `{"results": [{"hex", "verdict"}]}`. With `--strict` it exits with 4 when any colour
  is not ok, which is useful in a check script.
- `harmonize` prints `{"results": [{"hex", "harmonized", "changed", "verdictBefore"}]}`. It moves a
  colour the smallest distance that makes it fit. **A colour that already fits comes back unchanged.**
  Chroma only ever goes down, and the result always passes its own check and fits in sRGB.
- Exit codes: 0 done, 1 bad input, 4 a colour failed under `--strict`.

## Using the verdicts

- `off` for a new UI colour: prefer a colour already in the palette, or show the user the harmonized one
  next to the original. Do not quietly swap brand colours.
- `soso`: say which axis is off (run `harmonize` and compare), and let the user decide.
- A harmonized colour is a suggestion. Look at it next to its neighbours before committing it; the
  lattice is a calibration, not a law.
- Check contrast separately (WCAG): fitting a palette says nothing about legibility.

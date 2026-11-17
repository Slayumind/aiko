# Signs of an AI-looking interface

Adapted from Damir Sokolovsky's `unslop` (CC BY 4.0, https://github.com/heydamir/unslop), with
additions from Aiko's projects. Go through every group, write each sign you find with where it
lives, then fix it. A sign is a hint, not a law: a product's own system can allow it on purpose.

## Surfaces and colour

- Gradients from one hue to another (purple to blue, violet to pink) on backgrounds, cards or the
  active item → one neutral surface; a gradient only as a small shift inside one neutral.
- Gradient text in headings, numbers or the logo → solid text colour.
- Coloured glows and drop shadows on emoji or pills → remove; depth from surface steps and hairlines.
- Blurred colour blobs behind content → remove.
- Bright or neon borders → a hairline at 6-10% opacity.
- Five near-identical tints for text → three text roles from one neutral ramp.
- Everything highlighted → one accent, once or twice per screen.
- Glass cards with a white inner line and a coloured shadow → a plain raised surface.

## Type

- Headings at weight 800-900 → 500-600 and size for hierarchy.
- UPPERCASE labels with wide tracking → sentence case.
- Many sizes and weights on one screen → a few steps of the scale.
- Numbers in mixed formats → one format, tabular figures where numbers line up.
- Negative letter spacing on small text → zero or a little positive.

## Icons

- Emoji as icons, status or decoration → one line icon set, or nothing. For children: emoji only
  where they carry meaning.
- Icons of many sizes and colours → one size, one stroke, the colour of their text role.
- A sparkle for "AI" → remove.

## Spacing

- Random values (13 px, 7 px, 31 px) → steps of 4.
- Rows of one list with different heights → one height, one gap.
- A group closer to its neighbour than to its own items → smaller gaps inside, bigger between.
- Uneven padding in sibling cards → one padding.

## Copy

Run `aiko:copy` on every string. The usual signs: greeting headlines, exclamation marks in statuses,
hype words, wordy placeholders ("Search anything..."), full stops on buttons, fake data like
`$123,456`. Children's products may keep a happy tone in results ("It works!"), not in errors.

## Components

- Pills for everything (search, buttons, cards) → radius by size: controls 8-10, cards 12-16,
  panels 14-20; an inner radius is the outer one minus the padding.
- "New", "Beta", "PRO" badges everywhere → one quiet badge or none.
- Identical cards side by side with the same chart → one main element, the rest smaller and different.
- A logo as a gradient tile with a sparkle → the name as text and a simple flat mark.

## Motion

- `transition: all`, bouncy hover scale on cards, things that float or breathe for no reason →
  named properties, motion only for a change or a live state.
- Every section flying in from a different side → one entrance for the main content.

## Classes from Aiko's projects

- **An element covered** by another in some state (a toast over a game, a hint over a button).
  Check every state, not only the first screen.
- **Text off the tokens**: a font or a size that is not in the design file.
- **A cut instead of a change**: a step, a panel or a message appears at once with no motion.
- **A status only in text** where a picture would be read faster (a thinking state, a success).
- **Text cut at the bottom** of a panel at the real window size.

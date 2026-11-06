# The passes

Fix the whole screen in one pass before the next. The order matters: spacing first, because
everything reads differently once the rhythm is right; copy before type and layout, because the
length of a heading decides its size and place; motion last, because it must fit a screen that works
without it. Values come from the product's system, or from [tokens.md](tokens.md) when there is none.

Passes 1-7 and 9 retell `unslop` by Damir Sokolovsky (CC BY 4.0,
https://github.com/heydamir/unslop); states, forms, responsive and access come from Aiko's projects.

## 1. Spacing

- A 4 px grid for margins, padding, gaps and fixed heights. Remove one-off values like 13 or 7 px.
- The same thing has the same size: one row height and one gap per list, one padding per row of cards.
- Space shows groups: smaller inside a group than between groups.
- Things line up: icons in one column, labels on one line, numbers right-aligned in their column.
- Padding follows density: compact panels about 16, cards 20-24, page edges 24-32.

## 2. Colour

- One neutral ramp for background, surfaces, borders and text. In apps the surfaces stay neutral;
  the content carries the colour.
- Three text roles: primary, secondary, muted. Muted is for meta and captions only.
- One accent, used rarely: the primary action, the selection, the one figure that matters. Text on
  the accent reaches 4.5:1; darken it or use dark text when it does not.
- Semantic colours only mean something (green good, red error) and are toned to the palette.
- Borders are hairlines: the text colour at 6-10%. Depth on light themes from small neutral shadows,
  on dark themes from surface steps.

## 3. Icons

- One set, one size (16 or 18 px), one stroke, the colour of their text role.
- Emoji used as icons become icons of the set, or go. For children: emoji only where they carry meaning.
- A fixed icon box and gap, so labels line up.

## 4. Copy

Run `aiko:copy`. Headings say what the screen does for whom. Buttons say exactly what happens, and
the result uses the same word ("Publish" → "Published"). Errors say what went wrong and how to fix
it, without apology. An empty screen invites the first action. No greeting headlines, hype words or
fake data.

## 5. Type

- One family for the interface, plus at most one partner (a mono for figures, a display face for big
  headings). Keep a real brand font.
- A few sizes from a scale and two weights; hierarchy from size and colour, not from 800-900 weights.
- Sentence case. No negative tracking on small text. `tabular-nums` where numbers line up.
- Lines under about 80 characters.

## 6. Layout

- Keep the sections and their order unless the user asked for a new structure.
- Apps keep app proportions: the shell fills the window, a sidebar of 240-280, fluid content capped
  at about 1440-1600. Never shrink an app into a narrow centred column.
- One primary action per view and at most one secondary next to it.
- Long headings align left; centred text only for one or two short lines.
- On small screens everything stacks in reading order; a dense preview gets simpler, not smaller.

## 7. States

Design each state on purpose, with the same layout so nothing jumps:

- **empty:** what this place is for and the one action that fills it;
- **loading:** a skeleton of the real layout for waits over 300 ms; a spinner only inside a button;
- **error:** what happened and what to do, next to the thing that failed; the input stays;
- **success:** the result in place, plus a small reaction (see reactions.md);
- **disabled:** why, in a tooltip or a line of text, when it is not obvious;
- **long and translated text:** the longest language fits; names and numbers can be long.

## 8. Forms

- Labels above fields; a placeholder is an example, never the label.
- Check a field when the person leaves it, not on every key; show the error under the field.
- Keep what the person typed after an error. Disable a submit button only while it sends.
- Group related fields; one column reads faster than two.

## 9. Details

- Radius by size: tags 4-6, controls 8-10, cards 12-16, panels 14-20; inner radius = outer minus
  padding, never under 4. Not everything is a pill.
- Counts and meta are quiet text, shortcuts are keycaps.
- Charts: one main chart, others smaller and different in kind; one accent per chart; the type of
  chart fits the data (trend - line, comparison - bars, share - one stacked bar).
- Hover and focus states exist and stay in the palette.

## 10. Visuals

Decorative filler goes: blobs, orbs, glowing grids, robots. A moment comes from the product itself:
real content, a figure set with care, a status that feels alive. A generated image is for a landing
hero, not for an app screen.

## 11. Responsive and access

- Check 1440, 1024 and 375 wide; 16 px side gutter on phones; no horizontal scroll.
- Contrast 4.5:1 for body text, 3:1 for large text and icons.
- Every control reachable by keyboard with a visible focus ring; the order follows the reading order.
- Touch targets 44 px, 48 px for children.
- Status messages have `role="status"`; decoration is `aria-hidden`.

## 12. Motion

Last pass. See [motion.md](motion.md) and [reactions.md](reactions.md).

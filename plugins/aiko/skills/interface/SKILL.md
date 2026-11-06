---
name: interface
description: Build, reshape, audit and polish any web interface so it looks made by a designer for this product, not generated - pages, app screens, dashboards, forms, components, embeds and artifacts. Reads the screen first, keeps the product's own look, sets a direction only for a new product, fixes in passes (spacing, colour, icons, copy, type, layout, states, details, motion), checks every state at real widths, and gives events a visible answer, from a tick on a saved field to glass reactions like in Mac video calls. Use when making a new page or screen, changing an existing one, when a UI "looks like AI made it", feels flat, messy or inconsistent, when screens change with a cut, or when the user asks for design, layout, polish, animations or "make it feel alive".
---

# aiko interface

The goal: the person sees this product, not a template. Clear first, then alive.

Built on Anthropic's `frontend-design` skill (Apache 2.0) and Damir Sokolovsky's `unslop`
(CC BY 4.0, https://github.com/heydamir/unslop), retold and extended with what worked in Aiko's own
projects. Keep this credit in any copy.

| Read | When |
|---|---|
| [references/craft.md](references/craft.md) | any change: the passes with their rules |
| [references/tokens.md](references/tokens.md) | a product with no system, or values that drift |
| [references/signs.md](references/signs.md) | the audit of an existing screen |
| [references/motion.md](references/motion.md) | screens change, panels open, text updates |
| [references/reactions.md](references/reactions.md) | events need a visible answer; a character or an avatar |

## 1. The product's look wins

Find what the screen must match before you choose anything:

- the design file of the project (`DESIGN.md`, tokens, a Figma link in the docs);
- the neighbouring screens: a new screen uses their fonts, colours, radius, buttons and words. An
  embed keeps the look of the platform it lives in. Fun for children goes into the experience
  (motion and reactions), not into a separate visual style;
- the user's taste when there is no system. Aiko's defaults: light and quiet, few borders and no
  boxes inside boxes, icon buttons with a tooltip in dense panels, toggles and number fields over
  sliders, floating panels with a close cross instead of modal dialogs, emoji only where they carry
  meaning.

An existing product gets an audit and fixes inside its own look. A new product gets a direction
(section 3). Never redesign a screen that only needed fixes, and never invent a second style inside
one product.

## 2. Process

1. **Look.** Run the screen and take screenshots at the real sizes: 1440, 1024 and 375 wide. Judge
   what people see, not only the code. Keep screenshots and mockups in a `previews/` folder of the
   project that is in `.gitignore`, never in a temp folder: the user opens that folder.
2. **List the states.** Empty, loading, partial, full, error, success, disabled, long text, a second
   language. A screen is its states, not its first view.
3. **Audit** with [references/signs.md](references/signs.md): one line per sign, with where it lives.
   Add the classes the user already named in this project.
4. **Size the change.** No signs: say so and offer two or three small fixes with exact values. Some
   signs: fix them in the product's look. A generated-looking screen with no system behind it: set a
   direction (section 3) and show it before editing, since a late change of direction redoes
   everything.
5. **Fix in passes**, the whole screen per pass, in the order of
   [references/craft.md](references/craft.md): spacing, colour, icons, copy, type, layout, states,
   details, visuals, motion. Copy goes through `aiko:copy`; colours that must fit a palette go
   through `aiko:palette`.
6. **Prove it** (section 6), then show the user everything at once. A comment from the user is a
   class: fix every case of it on every screen, say how you searched, and turn the class into a
   check where you can.

## 3. Direction for a new product

Ground it in the subject: who uses it, what they do there, the materials and words of that world.
Write a short plan before any code:

- colour: 4-6 named hex values; neutral surfaces, one accent used once or twice per screen;
- type: one family, or two clearly different ones, with a scale;
- layout: one sentence and an ASCII sketch; say what aligns to what;
- principles: what makes this screen this product and not any other.

Then review the plan against the defaults generated pages fall into, and change any part you would
also write for a different brief: cream with a serif and a terracotta accent; near-black with one
acid accent; newspaper rules and zero radius; identical rounded cards with one grey shadow; ALL-CAPS
labels over every heading; "A · B · C" meta lines; an arrow after every link; numbered markers on
content that is not a sequence. Spend boldness in one place; keep the rest quiet. The hero opens
with the most characteristic thing of the subject, not with a big number and a gradient.

## 4. Motion

Motion shows what changed. One easing curve and three lengths for the product (150 ms feedback,
260 ms state change, 420 ms entrance); only transform, opacity and colours move; an entrance replays
when the content changes, not on every render; loops only for live states; reduced motion turns it
off. Recipes: [references/motion.md](references/motion.md).

## 5. Reactions

Every event that matters gets an answer where it happened, seen rather than read: a tick on a saved
field, stars from the button that finished a task, hearts from a like, a thinking bubble over an
avatar, a laugh over a character, like the reactions in Mac video calls. Start from an event map,
match strength to importance (small, medium, status, big), and never let a reaction replace the
information. Guide: [references/reactions.md](references/reactions.md). Engine with no framework:
`scripts/reactions.js` and `scripts/reactions.css`; `scripts/demo.html` plays every reaction.

## 6. Prove it

A screenshot proves that something was drawn, not that it works.

- Every state from step 2 at 1440, 1024 and 375 wide, in each language of the product. No
  horizontal scroll at 375, no text cut at the bottom of a panel, nothing covered by something else.
- Contrast: body text 4.5:1, large text and icons 3:1. Keyboard: every control reachable, a
  visible focus ring. Targets: 44 px on touch, 48 px for children.
- Motion with numbers: `node scripts/motion-probe.mjs <url> --watch "<selector>" --click "<selector>"`
  records opacity and transform at set times after the element appears, saves the frames and writes
  `sheet.html`. With `--reduced` the first values must already be final.
- The project's own checks (types, lint, tests) after the change.
- Logic, data and strings are proved by tests and reading the code, not by screenshots.

## 7. Do not

- Do not redesign what needed fixes, and do not add a style the product does not have.
- No hue-to-hue gradients, glows, gradient text, glass panels or emoji as decoration. Glass is for
  reactions, not for cards.
- Do not animate everything: one orchestrated moment beats scattered effects.
- Do not ship a state, a reaction or a loop the user has not seen.
- Do not judge motion from a still frame, or a layout from one width.

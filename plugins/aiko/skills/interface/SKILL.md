---
name: interface
description: Design, audit and animate web interfaces that look made by a designer, not generated - read the screen first, find the signs of AI-looking UI, keep the look of the product it lives in, then add motion that explains what changed and a reaction to every event at the place it happened (a tick on a saved field, stars from a button, hearts from a like, glass bubbles over an avatar or a video like in Mac video calls). Proves motion with measured frames instead of a feeling. Use when building or reshaping a page, a screen or a component, when a UI "looks like AI made it", when screens change with a cut and should move, when an action, a result, a status or a character needs a visible answer, or when the user asks for animations, transitions, micro-interactions or "make it feel alive".
---

# aiko interface

The goal: the person sees a product, not a template. Clear first, then alive.

Built on Anthropic's `frontend-design` skill (Apache 2.0) and Damir Sokolovsky's `unslop`
(CC BY 4.0, https://github.com/heydamir/unslop), adapted and extended with what worked in Aiko's
own projects. Keep this credit in any copy.

## 1. The product's look wins

Before you choose anything, find what the screen must match:

- a design file of the project (`DESIGN.md`, tokens, a Figma link in the docs);
- the neighbouring screens: a new screen inside a product uses its fonts, colours, radius and
  buttons. An embed keeps the look of the platform it lives in. Fun for children goes into the
  experience (stars, motion, reactions), not into a separate visual style;
- the user's taste, when the project has no system. Aiko's defaults: light and quiet, few borders
  and no boxes inside boxes, icon buttons with a tooltip in dense panels, toggles and number fields
  over sliders, floating panels with a close cross instead of modal dialogs, emoji only where they
  carry meaning.

A new product with no system gets a direction (section 3). An existing product gets an audit and
fixes inside its own look. Never redesign a screen that only needed fixes.

## 2. Process

1. **Look at the screen.** Run it and take a screenshot at the real size, so you judge what people
   see, not only the code. Put screenshots and mockups in a `previews/` folder of the project that is
   in `.gitignore`, never in a temp folder: the user opens that folder to see your work.
2. **Audit.** Go through [references/signs.md](references/signs.md) and write every sign you find,
   one line each, with where it lives. Add the classes the user already named in this project.
3. **Decide the size of the change.** No signs: say so and suggest two or three small fixes with
   exact values. A few signs: fix them in the product's look. A generated-looking screen with no
   system behind it: set a direction first (section 3) and show it before you edit.
4. **Fix in passes, whole screen per pass:** spacing → colour → icons → copy → type → layout →
   details → visuals → motion. Copy goes through `aiko:copy`. Motion comes last, because it must fit
   a screen that already works without it.
5. **Check with numbers and frames** (section 6), then show the user everything at once. A comment
   from the user is a class: fix every case of it on every screen, and say how you searched.

## 3. Direction for a new product

Ground it in the subject: who uses it, what they do, the materials and words of that world. Then
write a short plan before any code:

- colour: 4-6 named hex values, one accent used once or twice per screen;
- type: one or two families with a clear scale; sizes and weights from the scale only;
- layout: one sentence and an ASCII sketch; say what aligns to what;
- principles: what makes this screen this product and not any other.

Review the plan against the defaults generated pages fall into, and change any part that you would
also write for a different brief: a cream background with a serif and a terracotta accent; near-black
with one acid accent; newspaper rules and zero radius; identical rounded cards with the same grey
shadow; ALL-CAPS labels over every heading; "A · B · C" meta lines; an arrow after every link.
Spend boldness in one place and keep the rest quiet.

## 4. Motion

Motion is the strongest part of this skill. Full recipes: [references/motion.md](references/motion.md).

- **Tokens.** One easing curve and three lengths for the whole product: fast for feedback (150 ms),
  middle for state changes (260 ms), longer for things that enter (420 ms).
- **Only transform, opacity and colours move.** Never width, height, top, left, margins or large
  blurs: they stutter on weak laptops, often next to a video call.
- **Motion answers a change.** A new step fades and rises 8 px with a small stagger. Text that
  changes its subject shows itself like ink (opacity with a light blur). A new line of code slides
  in. A highlight moves softly between items. A press scales down a little.
- **Replay on change, not on every render.** Give the entering element a `key` built from what it
  shows, so the entrance plays when the content changes and never when a clock ticks.
- **Loops only for live states:** a speaking wave, a loader, a pointer to the next action. Nothing
  breathes or floats for decoration.
- **Reduced motion** turns entrances and loops off and keeps colour changes short.

## 5. Reactions

Every event that matters gets an answer at the place where it happened, seen rather than read: a
tick on a saved field, stars from the button that finished a task, hearts from a like, a thinking
bubble over an avatar or a video tile, a laugh over a character. Text has to be seen and read; a
reaction is caught out of the corner of the eye. Full guide:
[references/reactions.md](references/reactions.md). Engine: `scripts/reactions.js` and
`scripts/reactions.css`, no framework, any element can be the origin. `scripts/demo.html` plays them.

1. **Event map first.** List the events of the screen, where each happens and how important it is.
   No event, no reaction; a reaction with no signal is a mockup, mark it so.
2. **Strength follows importance.** Small (on the element, under 300 ms) for every action; medium
   (a burst or a bubble from the element) for a result the person worked for; status (dots, a
   thinking bubble) while a state lasts; big (the whole screen) once per session at most. Errors
   and money get only small, calm answers.
3. **Look and motion.** Clear glass with a coloured rim and a light spot; a glyph with a soft shadow;
   rounded stars and hearts; depth for particles; a spring on entry, a pop into droplets on exit.
   Sizes are parts of the frame or the origin, so one engine fits a big view, a tile and a button.
4. **Rules.** A reaction never replaces the information. One of a kind every few seconds. Faces keep
   the eyes and the mouth free.

## 6. Prove it

A screenshot proves that something was drawn, not that it moved. Measure:

- `node scripts/motion-probe.mjs <url> --watch "<selector>" --click "<selector>"` records opacity and
  transform of the element and its children at set times after it appears, saves a frame at each time
  and writes `sheet.html` with the frames side by side. Run it again with `--reduced`: the first
  values must already be final.
- For a reaction, capture the frame in the middle of the animation, in the big view and the small
  tile, on the lightest and the darkest background the product has.
- Check the classes the user named (an element covered, text off the tokens) on every screen in every
  state, and turn each into a test or a measurement where you can.
- Run the project's own checks (types, lint, tests) after the change.

## 7. Do not

- Do not invent a new style inside an existing product.
- Do not animate everything: one orchestrated moment beats scattered effects.
- No purple-to-blue gradients, glows, glass cards with coloured shadows or emoji in buttons as
  decoration; glass is for reactions over video, not for panels.
- Do not ship a reaction, a loop or a sound the user has not seen in motion.
- Do not judge motion from a still frame; measure it.

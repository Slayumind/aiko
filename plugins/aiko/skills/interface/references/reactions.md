# Reactions: how an interface answers

Every event that matters to the person gets an answer at the place where it happened: a press, a
saved form, a solved task, a message, a character that thinks or laughs. The answer is seen, not
read. Glass bubbles, hearts and stars work like the reactions in Mac video calls and the effects in
messengers. The engine is `scripts/reactions.js` with `scripts/reactions.css`; any element can be the
origin. `scripts/demo.html` plays each reaction on a video-like frame and on a plain button.

## 1. Make the event map first

Write every event of the screen in a table before you draw anything. A reaction with no event behind
it is a mockup: mark it so on the demo page and in the report.

| Event | Where it happens | Strength | Reaction |
|---|---|---|---|
| a press, a toggle | the control | small | press scale, colour change |
| a value saved | the field or the row | small | a tick draws itself, then fades |
| a number grows | the counter | small | the number counts up |
| a wrong value | the field | small | one soft shake and the error text; nothing playful |
| a task done | the button that finished it | medium | thumb up or stars from the button |
| a like, a thanks | the like button or the message | medium | hearts rise from it |
| someone is typing or thinking | the avatar or the video tile | status | a bubble with dots until it ends |
| a character laughs, has an idea | the character | medium | laugh bubbles, a bulb bubble |
| the end of a big goal | the whole screen | big | one burst for the session: confetti or stars |

Signals come from the product: the response of a request, a tool call, a live transcript, a state
change. Words from a transcript need tests in every language of the product, including words that
must not match ("Хаос", "Have a look").

## 2. Strength follows importance

- **Small** answers every action: under 300 ms, on the element itself, no particles.
- **Medium** marks a result the person worked for: particles or a bubble from the element, about
  1.5-2.5 s.
- **Status** lasts as long as the state: a thinking bubble, typing dots, a speaking wave.
- **Big** is rare: the end of a lesson, a first publish, a level. At most one per session or flow.

Too many medium and big reactions turn into noise; count them on a typical path through the
product. Errors and money get only small, calm answers.

## 3. Places

- **On the element:** the control or the field changes itself. No extra layer.
- **From the element:** particles start at the element's rectangle (top edge for rising things,
  centre for bursts) and stay inside the nearest frame or panel.
- **Over a person or a character** (a video tile, an avatar, a mascot): bubbles rise from the head
  at the side; laugh bubbles fly at the sides of the face; hearts start at the chest. Keep the eyes
  and the mouth free.
- **The whole screen:** only for a big moment, behind nothing the person must press.

## 4. Look

- **Clear glass** for bubbles: mostly transparent, a coloured rim (pink at the bottom right, blue at
  the top left), a white light spot at the top left, a small reflection at the bottom right. What is
  behind shows through. A dense, dark ball looks like a button.
- **A readable glyph** inside: white, a tight dark shadow and a soft wide one, so it reads on a light
  background. One short word at most.
- **Depth** for particles: near ones bigger, sharp and slower; far ones smaller, blurred 1-2 px,
  a little transparent.
- **Rounded shapes**: stars and hearts with round corners and a light spot, never sharp default glyphs.
- On a light page with no video, the same glass needs a little more tint on the rim; check it on the
  lightest background of the product.

## 5. Motion

- A spring on entry: overshoot, then settle.
- Bubbles float a little and pop into 8-10 droplets.
- Rising particles start small, grow, sway slowly with a slight turn and fade in the last third.
- Bursts go out from the origin and fall a little, like a small firework.
- A status bubble appears after a pause of about 0.6 s, so a quick answer does not make it blink, and
  leaves by itself after a time limit.

## 6. Size

Sizes are parts of the frame or of the origin element, so one engine works in a big view, a small
tile and next to a button. Keep particles away from labels in the corners.

## 7. The engine

- Load `reactions.css` and `reactions.js` with a plain `<script src>`; it gives
  `window.AikoReactions` (an `import "./reactions.js"` puts the same object on `globalThis`). It is
  not an ES module on purpose: the demo then opens straight from disk.
- `playReaction(layer, kind, options)` plays `think`, `laugh`, `yes`, `idea`, `stars` or `love` and
  returns `{ stop() }`; `think` stays until `stop()` or `maxMs`. `mountLayer(host)` makes the layer.
- `from`: an element or a `{ x, y }` point. Bubbles stand beside a small element, a little higher,
  like a typing bubble in a chat; bursts start at its centre. Without `from` the reaction uses the
  face anchor of a video tile (`anchor`, default x 0.5, y 0.24).
- Add `rx-light` to a layer over a light page: the glass gets a little tint so white glyphs read.
- Other options: `glyph`, `laughWord`, `side`, `hold`, `delay`, `size`. `throttle(ms)` and the word
  patterns `LAUGH_WORDS`, `LOVE_WORDS` (ru, es, en) come with it.

## 8. Rules of use

- A reaction never replaces the information: the result, the error or the saved state is still on
  the screen in words or in the UI.
- One reaction of a kind every few seconds: two "ha-ha" in one line are one laugh.
- A status that matters for a screen reader gets `role="status"` and a short label; decoration is
  `aria-hidden`.
- With reduced motion, reactions appear and leave without travel; status bubbles stay still.
- No sound unless the product already has sound and the person turned it on.
- Show reactions to the user in motion on a demo page before they go into the product.

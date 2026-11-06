# Reactions over a video or a character

Feelings and actions of a character shown without text, like the reactions in Mac video calls.
The engine is `scripts/reactions.js` with `scripts/reactions.css`; `scripts/demo.html` plays each one.

## 1. Start from signals

List what the page already knows, then match a reaction to each. A reaction with no signal is a
mockup: mark it so on the demo page and in the report.

| Signal the page can have | Reaction |
|---|---|
| the user stopped speaking, the character has not started | think, until speech starts |
| the character's line has "ha-ha", "jaja", "haha" | laugh |
| a task or a step is accepted | yes |
| a perfect result (no hints, three stars) | stars instead of yes |
| the character calls a tool that changes the user's work | idea |
| the user says thanks or that they like it | love |

Words come from the live transcript of the call. Test the word patterns in every language of the
product, including words that must not match ("Хаос", "Have a look").

## 2. Look

- **Clear glass.** A mostly transparent ball with a coloured rim (pink at the bottom right, blue at
  the top left), a white light spot at the top left and a small reflection at the bottom right. The
  face and the background show through. A dense, dark ball looks like a button, not a bubble.
- **A readable glyph.** White glyph or word with a tight dark shadow and a soft wide one, so it
  reads on a light window. No text longer than one short word.
- **Depth.** Flying items get a random depth: near ones bigger, sharp, slower; far ones smaller,
  blurred 1-2 px, a little transparent.
- **Rounded shapes.** Stars and hearts with rounded corners and a light spot, never sharp default
  glyphs.

## 3. Motion

- Bubbles rise from the head with two small bubbles between, with a spring (overshoot, then
  settle), float a little and pop into 8-10 droplets.
- Flying items start small, grow with the spring, rise with a slow sway and a slight turn, fade in
  the last third.
- Stars burst from the centre outwards and fall a little, like a small firework.
- The thinking bubble appears after a pause of about 0.6 s, so a quick answer does not make it
  blink, and it leaves by itself after about 12 s with no answer.

## 4. Place and size

- Sizes are parts of the frame, so one engine works in a big lesson view and a small tile.
- Keep the eyes and the mouth free: bubbles go above the head at the side, laugh bubbles at the sides
  of the face, hearts start at the chest.
- Keep away from labels in the corners of the frame.

## 5. Rules of use

- One reaction of a kind every few seconds: two "ha-ha" in one line are one laugh.
- A reaction never replaces the information: a solved task still shows its result on the screen.
- With reduced motion, reactions appear and leave without travel.
- Show them to the user in motion on a demo page before they go into the product.

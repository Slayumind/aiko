# Motion recipes

Every recipe moves only transform, opacity and colours. Values are starting points; keep the three
lengths and the one curve of the product.

## Tokens

```css
:root {
  --ease: cubic-bezier(.2, .7, .2, 1);   /* fast start, soft stop */
  --t-fast: .15s;                        /* feedback: hover, press, toggle */
  --t-mid: .26s;                         /* state change: a pill, a task, a highlight */
  --t-in: .42s;                          /* something enters */
}
@keyframes in { from { opacity: 0; transform: translateY(8px) } }
@keyframes ink { from { opacity: 0; filter: blur(3px); transform: translateY(3px) } }
```

## A step changes

The side panel of a step comes in part by part. Give the container a key of the step, so React (or
any framework) mounts it again when the step changes and the entrance plays once.

```jsx
<div className="panel enter" key={step}>…</div>
```
```css
.enter > *, .enter > * > * { animation: in var(--t-in) var(--ease) backwards }
.enter > * > :nth-child(2) { animation-delay: .05s }
.enter > * > :nth-child(3) { animation-delay: .1s }
.enter > * > :nth-child(n+4) { animation-delay: .15s }
```

`backwards` keeps the element hidden during its delay, so nothing blinks.

## A whole screen opens

The main blocks come in one short wave, 60 ms apart. One wave per screen, not one per section.

## Text that changes its subject

A note that now talks about another line or item shows itself like ink on a page. Key it by what it
says, so it replays only when the subject changes.

```jsx
<div className="note ink" key={`${line}|${idea}`}>…</div>
```
```css
.ink { animation: ink .5s var(--ease) backwards }
.ink .second { animation: ink .5s var(--ease) .18s backwards }
```

Words of a subtitle can do the same one by one, 30-40 ms apart.

## A new item in a list

A new line of code or a new row slides in 6 px from the left. Existing items keep their keys, so only
the new one moves.

## State changes

Pills of a plan, task cards, buttons and highlights change colour, border and shadow with
`--t-mid`. A press is `scale(.98)` for `--t-fast`. Name the properties; never `transition: all`.

## Loops

Only for a live state: a speaking wave, a loader, a pointer at the one thing to do now (a small
nudge every 1.4 s). Stop the loop when the state ends.

## Reduced motion

```css
@media (prefers-reduced-motion: reduce) {
  .enter > *, .enter > * > *, .ink, .ink .second { animation: none }
  .pill, .task, .button { transition: none }
}
```

## Checking it

- With `motion-probe.mjs`, the children of an entering panel 50 ms after mount should have opacity
  well below 1 (about 0.1-0.2), and 1 later. With `--reduced` they are 1 at once.
- Frames in the middle of the motion, not only the end state.
- On the slowest machine you can get, or with CPU throttling in the browser's dev tools.

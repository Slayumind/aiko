# Starting values

For a product with no system. When the project has its own values, keep them and fix only how they
are used. Adapted from `unslop` by Damir Sokolovsky (CC BY 4.0, https://github.com/heydamir/unslop).

## Spacing

Scale: 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96, 128.

| Use | Value |
|---|---|
| inside a control | 4-12 |
| between items of a list | 2-8 |
| between groups | 12-24 |
| between sections | 32-64; landing sections 80-128 |
| row height | 32 compact, 36 default, 40-44 touch, 48 for children |

## App layout

Sidebar 240-280; page padding 24-32; content capped at 1440-1600; top bar 56-64; navigation rows
32-36 with 2-4 between them.

## Light neutral ramp

| Role | Graphite | Warm | Ink |
|---|---|---|---|
| page | #fafafa | #f7f4ee | #f5f7fa |
| card | #ffffff | #fdfbf7 | #ffffff |
| raised (active row, input) | #f4f4f5 | #efe9df | #eceff4 |
| primary text | #171717 | #1f1b16 | #111827 |
| secondary text | #525252 | #5e554a | #4b5565 |
| muted text, icons | #737373 | #7b7266 | #6b7487 |

Hairline: primary text at 8%. Semantic: positive #16a34a, negative #dc2626.

## Dark neutral ramp

| Role | Graphite | Warm | Ink |
|---|---|---|---|
| deepest (sidebar) | #0f0f10 | #12110f | #07080d |
| page | #141415 | #161513 | #0b0d14 |
| card | #19191b | #1c1a18 | #11141c |
| raised | #232325 | #262421 | #1a1e28 |
| primary text | #ededed | #efe9de | #eef0f5 |
| secondary text | #a1a1a6 | #aaa398 | #a3a9b8 |
| muted text, icons | #85858b | #8a847a | #7a8296 |

Semantic on dark: positive #4cc38a, negative #e5484d.

## Accent

A real brand colour, or one taken from the world of the product: amber or coral for warm worlds,
cobalt, teal or sky for cool ones, moss or sage for natural ones. Not the purple-to-blue family unless
the brand is purple. It covers a few percent of the screen.

## Type scale

| Use | Size / line | Weight |
|---|---|---|
| meta, counts | 12 / 16 | 400 |
| captions, secondary | 13-14 / 20 | 400 |
| body, navigation | 14-15 / 20-22 | 400-500 |
| page title | 28-36 / 1.15 | 400-600 |
| card figure | 28-32 / 1 | 500, tabular |
| landing headline | 56-72 desktop, 36-44 phone / 1.05 | 400-500 |

Products for children read better a step up: body 16-18. Tracking 0 under 16 px; -0.02em from 40 px.

Families by character: precise (Inter, Geist, IBM Plex Sans), warm (DM Sans, Figtree, Instrument
Sans), editorial (a sans for the interface with Newsreader or Fraunces for headings), playful (Space
Grotesk, Bricolage Grotesque).

## Radius and depth

Tags 4-6, controls 8-10, cards 12-16, panels 14-20. Light shadows: `0 1px 2px rgba(0,0,0,.05)` for
small things, `0 8px 24px rgba(0,0,0,.06)` for floating ones.

## Buttons and targets

Main buttons 44-48 tall, small ones 32-36, side padding 16-20. Touch targets 44, children 48.

## Motion

Curve `cubic-bezier(.2, .7, .2, 1)`; 150 ms feedback, 260 ms state change, 420 ms entrance; stagger
40-60 ms, at most six items; rise 6-12 px; press scale .97-.98.

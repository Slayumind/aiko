// Run: node --test   (from this folder). Tests the parts of reactions.js that need no browser.
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";

const R = createRequire(import.meta.url)("./reactions.js");

test("it loads without a browser and gives the API", () => {
  for (const name of ["playReaction", "throttle", "mountLayer", "bubbleLayout", "originOf"]) assert.equal(typeof R[name], "function", name);
  assert.deepEqual(R.KINDS, ["think", "laugh", "yes", "idea", "stars", "love"]);
  assert.equal(globalThis.AikoReactions, R);
});

test("throttle lets one reaction of a kind through per window", () => {
  let now = 1000;
  const allow = R.throttle(500, () => now);
  assert.equal(allow("laugh"), true);
  now = 1200;
  assert.equal(allow("laugh"), false);
  assert.equal(allow("love"), true, "other kinds have their own window");
  now = 1500;
  assert.equal(allow("laugh"), true);
  now = 1501;
  assert.equal(allow("laugh"), false);
});

test("throttle lets the first one through even at time 0", () => {
  const allow = R.throttle(500, () => 0);
  assert.equal(allow("yes"), true);
});

test("LAUGH_WORDS finds laughter in ru, es and en", () => {
  for (const s of ["ха-ха, смешно", "Хахаха", "хе-хе", "jajaja", "jeje", "haha nice", "Hehe"]) assert.match(s, R.LAUGH_WORDS, s);
  for (const s of ["ха", "hat", "Hello", "jamón", "хорошо"]) assert.doesNotMatch(s, R.LAUGH_WORDS, s);
});

test("LOVE_WORDS finds thanks and love in ru, es and en", () => {
  for (const s of ["Спасибо!", "мне нравится", "Gracias", "me encanta", "Thank you", "I love this"]) assert.match(s, R.LOVE_WORDS, s);
  for (const s of ["привет", "hola", "okay"]) assert.doesNotMatch(s, R.LOVE_WORDS, s);
  assert.equal(R.LAUGH_BY_LANG.ru, "ха");
});

test("bubbleLayout over a face floats near the top, on the chosen side", () => {
  const right = R.bubbleLayout({ width: 500, height: 400, x: 250, y: 96, side: 1 });
  assert.equal(right.size, 96);
  assert.ok(right.tx + right.size / 2 > 250, "right of the head");
  assert.equal(right.ty, 34);
  const left = R.bubbleLayout({ width: 500, height: 400, x: 250, y: 96, side: -1 });
  assert.ok(left.tx + left.size / 2 < 250, "left of the head");
  // dx, dy lead from the float point back to the start point
  assert.equal(right.tx + right.size / 2 + right.dx, right.hx);
  assert.equal(right.ty + right.size / 2 + right.dy, right.hy);
});

test("bubbleLayout from an element floats beside it, a little higher, and stays inside the layer", () => {
  const g = R.bubbleLayout({ width: 1440, height: 900, x: 700, y: 500, face: false });
  assert.equal(g.size, 96);
  assert.ok(g.tx >= 700 + 96 * 0.5, "to the side of the element");
  assert.ok(g.ty >= 500 - 96 * 0.6 && g.ty < 500, "a little above the top edge, not over the row above");
  const edge = R.bubbleLayout({ width: 1440, height: 900, x: 1430, y: 20, face: false });
  assert.ok(edge.tx >= 0 && edge.tx + edge.size <= 1440);
  assert.equal(edge.ty, 0);
  assert.equal(R.bubbleLayout({ width: 300, height: 200, x: 10, y: 100, face: false, size: 40 }).size, 40);
});

test("originOf uses the face anchor, an element or a point", () => {
  const layer = { left: 100, top: 50, width: 800, height: 600 };
  const face = R.originOf(layer, {}, null);
  assert.equal(face.face, true);
  assert.equal(face.x, 400);
  assert.equal(face.y, 144);
  assert.equal(R.originOf(layer, { anchor: { y: 0.5 } }, null).y, 300);

  const button = { left: 300, top: 450, width: 120, height: 40 };
  const el = R.originOf(layer, { from: {} }, button);
  assert.equal(el.face, false);
  assert.equal(el.x, 260);
  assert.equal(el.y, 420);
  assert.equal(el.top, 400);
  assert.equal(el.w, 360, "the scale box is at most 360 px");

  const point = R.originOf(layer, { from: { x: 10, y: 20 } }, null);
  assert.deepEqual([point.x, point.y, point.top], [10, 20, 20]);
});

test("playReaction does nothing without a layer or with an unknown kind", () => {
  assert.equal(typeof R.playReaction(null, "love").stop, "function");
  assert.doesNotThrow(() => R.playReaction(null, "love").stop());
  assert.doesNotThrow(() => R.playReaction({ getBoundingClientRect: () => ({ width: 0 }) }, "wave"));
});

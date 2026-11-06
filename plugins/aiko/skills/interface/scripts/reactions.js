// Reactions like the ones in Mac video calls: clear glass bubbles, "ha" bubbles,
// stars and hearts. They start from a face in a video tile (the default) or from
// any element: stars from a "Publish" button, hearts from a like button,
// a thinking bubble over a chat avatar.
// Plain DOM and requestAnimationFrame. It changes only transform and opacity,
// so a reaction never makes the page lay out again.
//
// Load it any way you like. It has no imports and no exports:
//   <script src="reactions.js"></script>       -> window.AikoReactions
//   import "./reactions.js";                     -> globalThis.AikoReactions
//   require("./reactions.js")                    -> the same object (Node, bundlers)
// It needs reactions.css on the page.
//
//   const layer = AikoReactions.mountLayer(tile);   // tile needs position: relative
//   AikoReactions.playReaction(layer, "laugh", { laughWord: "ха" });
//   const think = AikoReactions.playReaction(layer, "think");
//   ...later: think.stop();                         // the bubble pops
//
//   const page = AikoReactions.mountLayer(document.body, { fixed: true });
//   AikoReactions.playReaction(page, "stars", { from: publishButton });
(function (root) {
  "use strict";

  /** @typedef {"think" | "laugh" | "yes" | "idea" | "stars" | "love"} ReactionKind */
  /** @typedef {{ x: number, y: number }} Anchor  A point as a share of the layer: 0..1. */
  /**
   * @typedef {Object} ReactionOptions
   * @property {Element | { x: number, y: number }} [from]  Where the reaction starts: an element, or a point
   *   in px of the layer. Bubbles start at the top edge of the element, bursts at its centre.
   *   Without it, the reaction starts at the face (anchor).
   * @property {Anchor} [anchor]  The head point when there is no from. Default { x: 0.5, y: 0.24 }:
   *   a face in the middle, near the top of a video tile.
   * @property {number} [size]  With from: the size of a bubble in px. Default: from the element, 36..96,
   *   or from the layer for a point, 44..96.
   * @property {string | (() => string)} [glyph]  Your own SVG or HTML for the glyph of this kind.
   *   A function is called for every item, so each one can get its own gradient id.
   * @property {string} [laughWord]  The word in the laugh bubbles. Default "ha". It is set as text, not HTML.
   * @property {1 | -1} [side]  Bubbles only: 1 puts the bubble right of the head, -1 left.
   * @property {number} [hold]  Bubbles only: ms the bubble floats before it pops.
   * @property {number} [maxMs]  "think" only: it pops after this time if stop() never came. Default 15000.
   * @property {number} [delay]  ms to wait before it shows. stop() before that shows nothing.
   *   For "think", 600 keeps a quick answer from making it blink.
   */
  /** @typedef {{ stop: () => void }} ReactionHandle */

  /** @type {ReactionKind[]} */
  const KINDS = ["think", "laugh", "yes", "idea", "stars", "love"];

  // What starts a reaction, read from the words in a chat or a call: ru, es, en.
  const LAUGH_WORDS = /(?:ха-?){2,}|(?:хе-?){2,}|(?:хи-?){2,}|(?:ja-?){2,}|(?:je-?){2,}|(?:ha-?){2,}|(?:he-?){2,}/i;
  const LOVE_WORDS =
    /спасибо|благодар|нравится|понравил|обожаю|люблю|gracias|me gusta|me encant|te quiero|thank|love it|i like it|i love/i;
  /** The laugh word for a language. Use it as laughWord. */
  const LAUGH_BY_LANG = { ru: "ха", es: "ja", en: "ha" };

  const DEFAULT_ANCHOR = { x: 0.5, y: 0.24 };
  const END = 0.3; // seconds: how long a bubble takes to pop

  const isReduced = () =>
    typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
  const rnd = (a, b) => a + Math.random() * (b - a);
  const clamp01 = (t) => Math.min(1, Math.max(0, t));
  const easeOut = (t) => 1 - Math.pow(1 - t, 3);
  // Overshoot, then settle: the bubbly feel of the Mac reactions.
  const spring = (t) => 1 - Math.cos(t * Math.PI * 2.4) * Math.exp(-t * 6);

  // ---- glyphs: white for bubbles, colour with a light spot for the flying ones ----
  let gid = 0;
  const THUMB = () => {
    const id = `rx-t${gid++}`;
    return `<svg viewBox="0 0 24 24"><defs><linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff"/><stop offset="1" stop-color="#E4E9F2"/></linearGradient></defs><path fill="url(#${id})" d="M1 21h4V9H1v12zm22-11c0-1.1-.9-2-2-2h-6.31l.95-4.57.03-.32c0-.41-.17-.79-.44-1.06L14.17 1 7.59 7.59C7.22 7.95 7 8.45 7 9v10c0 1.1.9 2 2 2h9c.83 0 1.54-.5 1.84-1.22l3.02-7.05c.09-.23.14-.47.14-.73v-2z"/></svg>`;
  };
  const BULB = () =>
    '<svg viewBox="0 0 24 24"><path fill="#fff" d="M9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1zm3-19C8.14 2 5 5.14 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7z"/></svg>';
  const DOTS = () => '<span class="rx-dots"><b></b><b></b><b></b></span>';
  const HEART = () => {
    const id = `rx-h${gid++}`;
    return `<svg viewBox="0 0 24 24"><defs><radialGradient id="${id}" cx=".35" cy=".3" r=".8"><stop offset="0" stop-color="#FF9AA8"/><stop offset=".45" stop-color="#FF3B5C"/><stop offset="1" stop-color="#C8102E"/></radialGradient></defs><path fill="url(#${id})" d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"/><ellipse cx="7.4" cy="7.2" rx="2.6" ry="1.5" fill="#fff" opacity=".55" transform="rotate(-30 7.4 7.2)"/></svg>`;
  };
  const STAR = () => {
    const id = `rx-s${gid++}`;
    return `<svg viewBox="-1 -1 26 26"><defs><radialGradient id="${id}" cx=".4" cy=".3" r=".8"><stop offset="0" stop-color="#FFF1A8"/><stop offset=".5" stop-color="#FFC21A"/><stop offset="1" stop-color="#E08A00"/></radialGradient></defs><path fill="url(#${id})" stroke="url(#${id})" stroke-width="2.6" stroke-linejoin="round" d="M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z"/><ellipse cx="10" cy="8.6" rx="1.6" ry=".9" fill="#fff" opacity=".6" transform="rotate(-25 10 8.6)"/></svg>`;
  };
  /** Default glyphs. Each is a function that returns SVG or HTML. */
  const GLYPHS = { think: DOTS, yes: THUMB, idea: BULB, stars: STAR, love: HEART };

  const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

  /**
   * Where a bubble starts and where it floats, in px of the layer.
   * Pure, so it can be tested without a browser.
   * face true: (x, y) is a head in a video tile; the bubble floats near the top of the tile.
   * face false: (x, y) is the top edge of an element; the bubble floats just above it.
   * @param {{ width: number, height: number, x: number, y: number, side?: 1 | -1, face?: boolean, size?: number }} o
   */
  function bubbleLayout({ width, height, x, y, side = 1, face = true, size }) {
    if (face) {
      const s = size ?? Math.round(Math.min(width, height) * 0.24);
      const hx = x + side * width * 0.06;
      const tx = x + side * width * 0.24 - s / 2;
      const ty = Math.max(height * 0.05, Math.min(34, height * 0.1));
      // dx, dy: the move from the float point back to the start, for the rise.
      return { size: s, hx, hy: y, tx, ty, dx: hx - tx - s / 2, dy: y - ty - s / 2 };
    }
    const s = size ?? Math.round(clamp(Math.min(width, height) * 0.12, 44, 96));
    const tx = clamp(x + side * s * 0.55 - s / 2, 0, Math.max(0, width - s));
    const ty = clamp(y - s * 1.4, 0, Math.max(0, height - s));
    return { size: s, hx: x, hy: y, tx, ty, dx: x - tx - s / 2, dy: y - ty - s / 2 };
  }

  /**
   * The start point of a reaction in px of the layer, and the box that sets its scale.
   * In a video tile the box is the tile. From an element it is a square of at most 360 px,
   * so hearts over a full page stay the size of hearts.
   * @param {DOMRect} layerRect
   * @param {ReactionOptions} options
   * @param {DOMRect | null} fromRect  The rect of options.from when it is an element.
   */
  function originOf(layerRect, options, fromRect) {
    const W = layerRect.width;
    const H = layerRect.height;
    const from = options.from;
    if (!from) {
      const a = { ...DEFAULT_ANCHOR, ...(options.anchor || {}) };
      return { face: true, x: W * a.x, y: H * a.y, top: H * a.y, w: W, h: H, W, H };
    }
    const L = Math.min(W, H, 360);
    if (fromRect) {
      const x = fromRect.left - layerRect.left + fromRect.width / 2;
      // A bubble a bit bigger than the short side of the element: right for an avatar or a button.
      const bubble = Math.round(clamp(Math.min(fromRect.width, fromRect.height) * 1.15, 36, 96));
      return { face: false, x, y: fromRect.top - layerRect.top + fromRect.height / 2, top: fromRect.top - layerRect.top, w: L, h: L, W, H, bubble };
    }
    return { face: false, x: from.x, y: from.y, top: from.y, w: L, h: L, W, H };
  }

  /**
   * One reaction of a kind at a time: two "ha-ha" in one phrase are one laugh.
   * @param {number} ms
   * @param {() => number} [now]  A clock, for tests.
   * @returns {(key: string) => boolean}  true when the reaction may play now.
   */
  function throttle(ms, now = Date.now) {
    const last = new Map();
    return (key) => {
      const t = now();
      if (last.has(key) && t - last.get(key) < ms) return false;
      last.set(key, t);
      return true;
    };
  }

  /**
   * Put a reaction layer into a tile. The tile needs position: relative (or absolute).
   * fixed: true makes a layer over the whole window, for reactions from buttons and cards.
   * @param {HTMLElement} host
   * @param {{ fixed?: boolean }} [o]
   * @returns {HTMLElement}
   */
  function mountLayer(host, o = {}) {
    const cls = o.fixed ? "rx-layer rx-fixed" : "rx-layer";
    const found = host.querySelector(o.fixed ? ":scope > .rx-layer.rx-fixed" : ":scope > .rx-layer:not(.rx-fixed)");
    if (found) return /** @type {HTMLElement} */ (found);
    const layer = document.createElement("div");
    layer.className = cls;
    layer.setAttribute("aria-hidden", "true");
    host.appendChild(layer);
    return layer;
  }

  // ---- the engine ----

  // A run keeps every element and timer of one reaction, so stop() can clean them all.
  function makeRun(layer) {
    const run = {
      layer,
      els: new Set(),
      timers: new Set(),
      onStop: null,
      stopped: false,
      add(cls, html, size) {
        const el = document.createElement("div");
        el.className = cls;
        if (html) el.innerHTML = html;
        el.style.width = el.style.height = `${size}px`;
        el.style.opacity = "0";
        layer.appendChild(el);
        run.els.add(el);
        return el;
      },
      remove(el) {
        el.remove();
        run.els.delete(el);
      },
      later(fn, ms) {
        const id = setTimeout(() => {
          run.timers.delete(id);
          if (!run.stopped && layer.isConnected) fn();
        }, ms);
        run.timers.add(id);
      },
      clear() {
        run.stopped = true;
        run.timers.forEach(clearTimeout);
        run.timers.clear();
        run.els.forEach((el) => el.remove());
        run.els.clear();
      },
    };
    return run;
  }

  // Calls step(t, sec) each frame for ms, then done() or removes the element.
  function animate(run, el, ms, step, done) {
    const t0 = performance.now();
    const tick = (now) => {
      if (run.stopped || !el.isConnected) return;
      const t = clamp01((now - t0) / ms);
      step(t, (now - t0) / 1000);
      if (t < 1) requestAnimationFrame(tick);
      else if (done) done();
      else run.remove(el);
    };
    requestAnimationFrame(tick);
  }

  // Small drops fly out where a bubble popped.
  function pop(run, x, y, size, reduced) {
    if (reduced) return;
    for (let i = 0; i < 9; i++) {
      const d = run.add("rx-drop", "", rnd(4, 8));
      const ang = (i / 9) * Math.PI * 2 + rnd(-0.2, 0.2);
      const dist = size * rnd(0.55, 0.8);
      d.style.left = `${x}px`;
      d.style.top = `${y}px`;
      animate(run, d, 420, (t) => {
        const e = easeOut(t);
        d.style.transform = `translate(${Math.cos(ang) * dist * e}px, ${Math.sin(ang) * dist * e}px) scale(${1 - t * 0.6})`;
        d.style.opacity = String(1 - t);
      });
    }
  }

  // A glass bubble rises from the head (or the top of an element), floats, then pops into drops.
  // hold Infinity: it floats until run.onStop() or maxMs.
  function bubble(run, inner, o) {
    const p = o.origin;
    const g = bubbleLayout({ width: p.W, height: p.H, x: p.x, y: p.top, side: o.side, face: p.face, size: o.size });
    const reduced = isReduced();
    // Two small bubbles lead from the start point to the big one.
    const leads = [0.09, 0.05].map((k, i) => {
      const s = Math.max(6, Math.round((p.face ? Math.min(p.W, p.H) * k : g.size * k * 4) * 0.6));
      const d = run.add("rx-glass", "", s);
      d.style.left = `${g.hx + (g.tx + g.size / 2 - g.hx) * (0.35 - i * 0.2) * (i ? 0.6 : 1)}px`;
      d.style.top = `${g.hy - (g.hy - g.ty - g.size) * (0.35 - i * 0.2)}px`;
      return d;
    });
    const b = run.add("rx-glass rx-bubble", inner, g.size);
    b.style.left = `${g.tx}px`;
    b.style.top = `${g.ty}px`;

    let endAt = Number.isFinite(o.hold) ? ((o.hold + 900) / 1000) * 0.88 : o.maxMs / 1000;
    const t0 = performance.now();
    let gone = false;
    const finish = () => {
      gone = true;
      leads.forEach((d) => run.remove(d));
      pop(run, g.tx + g.size / 2, g.ty + g.size / 2, g.size, reduced);
      run.remove(b);
    };
    run.onStop = () => {
      if (gone) return;
      const sec = (performance.now() - t0) / 1000;
      if (sec < 0.12) {
        gone = true;
        run.clear(); // stopped before it showed: nothing to pop
      } else endAt = Math.min(endAt, sec);
    };
    const tick = (now) => {
      if (gone || run.stopped || !b.isConnected) return;
      const sec = (now - t0) / 1000;
      const end = sec >= endAt ? clamp01((sec - endAt) / END) : 0;
      leads.forEach((d, i) => {
        d.style.opacity = String(clamp01(sec / 0.25) * (1 - end));
        d.style.transform = `scale(${reduced ? 1 : spring(clamp01((sec - i * 0.08) / 0.5))})`;
      });
      const rise = reduced ? 1 : spring(clamp01((sec - 0.12) / 0.7));
      const bob = reduced ? 0 : Math.sin(sec * 2.4) * g.size * 0.03;
      b.style.opacity = String(clamp01((sec - 0.12) / 0.2) * (1 - end));
      b.style.transform = `translate(${(1 - rise) * g.dx}px, ${(1 - rise) * g.dy + bob}px) scale(${Math.max(0.2, rise) * (1 + (reduced ? 0 : end * 0.35))})`;
      if (end >= 1) finish();
      else requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }

  // Things that fly up from a point: hearts from the chest or a like button,
  // "ha" bubbles from the sides of the face. o.box sets the scale, o.from is the start in px.
  // With reduced motion they do not travel: they show a little apart and fade.
  function flyUp(run, make, o) {
    const { layer } = run;
    const { w: W, h: H } = o.box;
    const lift = o.lift || [0.55, 0.85];
    const wide = o.wide ?? 0.18;
    const reduced = isReduced();
    const n = reduced ? Math.ceil(o.n / 3) : o.n;
    for (let i = 0; i < n; i++) {
      run.later(() => {
        const depth = rnd(0, 1); // 0 far, 1 near: near ones are bigger and sharper
        const s = Math.round(Math.min(W, H) * o.size * (0.55 + depth * 0.65));
        const el = run.add(o.glassy ? "rx-glass rx-bubble" : "rx-fly", make(), s);
        const side = i % 2 ? 1 : -1;
        const x0 = o.from.x + (o.aside ? side * rnd(o.aside, o.aside + o.spread) : rnd(-o.spread, o.spread)) * W;
        const y0 = o.from.y;
        const drift = (o.aside ? side * rnd(0.02, wide) : rnd(-wide, wide)) * W;
        const rise = H * rnd(lift[0], lift[1]);
        const sway = reduced ? 0 : rnd(8, 18) * (W / 600);
        const ph = rnd(0, 6);
        const spin = reduced ? 0 : rnd(-14, 14);
        el.style.left = `${x0 - s / 2}px`;
        el.style.top = `${y0 - s / 2}px`;
        // A fixed blur for depth. It is set once and never animated.
        if (!o.glassy) el.style.filter = `blur(${((1 - depth) * 1.6).toFixed(2)}px) drop-shadow(0 6px 10px rgba(120,10,30,.25))`;
        el.style.zIndex = String(Math.round(depth * 10));
        const ms = o.life * rnd(0.85, 1.15) * (1.2 - depth * 0.3);
        animate(
          run,
          el,
          ms,
          (t, sec) => {
            const grow = reduced ? 1 : spring(clamp01(sec / 0.45));
            const e = reduced ? 0.35 : easeOut(t);
            el.style.transform = `translate(${drift * e + Math.sin(sec * 3 + ph) * sway}px, ${-rise * e}px) rotate(${Math.sin(sec * 2.2 + ph) * spin}deg) scale(${grow})`;
            el.style.opacity = String((t < 0.7 ? 1 : 1 - (t - 0.7) / 0.3) * (0.75 + depth * 0.25));
          },
          o.glassy
            ? () => {
                const r = el.getBoundingClientRect();
                const f = layer.getBoundingClientRect();
                pop(run, r.left - f.left + r.width / 2, r.top - f.top + r.height / 2, r.width, reduced);
                run.remove(el);
              }
            : undefined,
        );
      }, i * (reduced ? 0 : rnd(50, 110)));
    }
  }

  // Stars burst out from a point (below the face, or the centre of a button) and fall a little.
  // With reduced motion they show already spread out and fade.
  function stars(run, make, box, from) {
    const { w: W, h: H } = box;
    const reduced = isReduced();
    const n = reduced ? 8 : 18;
    for (let i = 0; i < n; i++) {
      const s = Math.round(Math.min(W, H) * rnd(0.06, 0.13));
      const el = run.add("rx-fly rx-star", make(), s);
      el.style.left = `${from.x - s / 2}px`;
      el.style.top = `${from.y - s / 2}px`;
      const ang = rnd(0, Math.PI * 2);
      const v = Math.min(W, H) * rnd(0.35, 0.6);
      const rot = reduced ? 0 : rnd(-240, 240);
      animate(run, el, rnd(1300, 1800), (t) => {
        const e = reduced ? 0.6 : easeOut(t);
        const fall = reduced ? 0 : H * 0.2 * t * t;
        el.style.transform = `translate(${Math.cos(ang) * v * e}px, ${Math.sin(ang) * v * e + fall}px) rotate(${rot * e}deg) scale(${reduced ? 1 : Math.min(1, t * 6)})`;
        el.style.opacity = String(t < 0.65 ? 1 : 1 - (t - 0.65) / 0.35);
      });
    }
  }

  const glyphOf = (kind, glyph) => {
    if (typeof glyph === "function") return glyph;
    if (typeof glyph === "string") return () => glyph;
    return GLYPHS[kind];
  };

  const laughBubble = (word) => () => {
    const span = document.createElement("span");
    span.className = "rx-ha";
    span.textContent = word;
    return span.outerHTML;
  };

  /**
   * Play one reaction over a layer (an element with the class rx-layer).
   * @param {HTMLElement | null} layer
   * @param {ReactionKind} kind
   * @param {ReactionOptions} [options]
   * @returns {ReactionHandle}  stop() ends it now: a bubble pops, flying items go away.
   */
  function playReaction(layer, kind, options = {}) {
    const none = { stop() {} };
    if (!layer || !KINDS.includes(kind)) return none;
    if (layer.getBoundingClientRect().width === 0) return none;
    const make = glyphOf(kind, options.glyph);
    const run = makeRun(layer);
    const start = () => {
      // Read the rects when the reaction starts, so a delay still finds the element where it is now.
      const lr = layer.getBoundingClientRect();
      const from = options.from;
      const fromRect = from && typeof from.getBoundingClientRect === "function" ? from.getBoundingClientRect() : null;
      const p = originOf(lr, options, fromRect);
      const box = { w: p.w, h: p.h };
      // In a tile the burst points sit below the head: the mouth, the chest. From an element: its centre.
      const at = (dyShare, yShare) => ({ x: p.x, y: p.face ? (yShare ?? (p.y / p.H + dyShare)) * p.H : p.y });
      if (kind === "think")
        bubble(run, make(), { origin: p, side: options.side ?? 1, hold: Infinity, maxMs: options.maxMs ?? 15000, size: options.size ?? p.bubble });
      else if (kind === "yes" || kind === "idea")
        bubble(run, make(), { origin: p, side: options.side ?? (p.face ? -1 : 1), hold: options.hold ?? 1500, size: options.size ?? p.bubble });
      else if (kind === "laugh") {
        const word = options.laughWord ?? "ha";
        const item = typeof options.glyph === "undefined" ? laughBubble(word) : make;
        flyUp(run, item, { box, n: 7, from: at(0.16), size: 0.12, spread: 0.05, life: 1900, glassy: true, lift: [0.15, 0.3], wide: 0.12, aside: 0.17 });
      } else if (kind === "stars") stars(run, make, box, at(0.18));
      else flyUp(run, make, { box, n: 16, from: at(0, 0.95), size: 0.16, spread: p.face ? 0.18 : 0.08, life: 2600 });
    };
    if (options.delay > 0) run.later(start, options.delay);
    else start();
    return {
      stop() {
        if (run.stopped) return;
        if (run.onStop) run.onStop();
        else run.clear();
      },
    };
  }

  const api = { KINDS, GLYPHS, LAUGH_WORDS, LOVE_WORDS, LAUGH_BY_LANG, playReaction, throttle, mountLayer, bubbleLayout, originOf };
  root.AikoReactions = api;
  if (typeof module === "object" && module && module.exports) module.exports = api;
})(typeof globalThis !== "undefined" ? globalThis : this);

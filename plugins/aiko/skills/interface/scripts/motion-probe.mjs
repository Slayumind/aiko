#!/usr/bin/env node
// Proves motion with numbers instead of a feeling.
// Opens a page, waits for an element to appear (or appear again as a new node),
// and reads its opacity and transform at set times after it appeared.
// It also saves a picture of the element at each time and a sheet.html to see them in one view.
//
//   node motion-probe.mjs <url or file> --watch ".selector" [--click ".button"]
//        [--at 50,150,400] [--out dir] [--reduced] [--width 1440 --height 900]
//
// --watch is a CSS selector. --click is a Playwright selector, so ".rx >> nth=4" works there.
// The numbers are read inside the page at the exact times. Pictures are taken from outside
// the page and come a little later; sheet.html shows the real time of each picture.
// Exit code: 0 done; 1 the element never appeared; 2 bad arguments or no Playwright.
// Needs Playwright for Node in the project you run it from: npm i -D playwright

import { mkdirSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const APPEAR_TIMEOUT = 15000;
const MAX_CHILDREN = 6;
const PAD = 12; // px of space around the element in each picture

const USAGE =
  'usage: node motion-probe.mjs <url> --watch ".selector" [--click ".button"] [--at 50,150,400] [--out dir] [--reduced] [--width 1440 --height 900]';

/**
 * Reads the command line. Pure: throws an Error with a clear message on bad input.
 * @param {string[]} argv  Arguments without "node" and the script name.
 */
export function parseArgs(argv) {
  const o = { url: null, watch: null, click: null, at: [50, 150, 400], out: "motion-probe", reduced: false, width: 1440, height: 900 };
  const value = (i, flag) => {
    const v = argv[i + 1];
    if (v === undefined || v.startsWith("--")) throw new Error(`${flag} needs a value`);
    return v;
  };
  const size = (v, flag) => {
    const n = Number(v);
    if (!Number.isInteger(n) || n < 100 || n > 8000) throw new Error(`${flag} must be a whole number of px from 100 to 8000, got "${v}"`);
    return n;
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--reduced") o.reduced = true;
    else if (a === "--watch") o.watch = value(i++, a);
    else if (a === "--click") o.click = value(i++, a);
    else if (a === "--out") o.out = value(i++, a);
    else if (a === "--width") o.width = size(value(i++, a), a);
    else if (a === "--height") o.height = size(value(i++, a), a);
    else if (a === "--at") o.at = parseTimes(value(i++, a));
    else if (a.startsWith("--")) throw new Error(`unknown option ${a}`);
    else if (o.url) throw new Error(`one url only, got "${o.url}" and "${a}"`);
    else o.url = a;
  }
  if (!o.url) throw new Error("give the url or the file of the page");
  if (!o.watch) throw new Error('give --watch ".selector": the element to measure');
  return o;
}

/** "400,50,150" -> [50, 150, 400]. Times are whole ms from 0 to 60000. */
export function parseTimes(text) {
  const parts = text.split(",").map((s) => s.trim()).filter(Boolean);
  if (!parts.length) throw new Error("--at needs times in ms, like 50,150,400");
  const times = parts.map((p) => {
    const n = Number(p);
    if (!Number.isInteger(n) || n < 0 || n > 60000) throw new Error(`--at: "${p}" is not a whole number of ms from 0 to 60000`);
    return n;
  });
  return [...new Set(times)].sort((a, b) => a - b);
}

/** A url stays as it is; a path to a file becomes a file:// url. */
export function toUrl(target, cwd = process.cwd()) {
  return /^[a-z][a-z0-9+.-]*:\/\//i.test(target) || target.startsWith("about:") || target.startsWith("data:")
    ? target
    : pathToFileURL(resolve(cwd, target)).href;
}

/**
 * A computed transform as short words: "x12 y-30 s0.84 r15" or "none".
 * x and y are px, s is the scale, r the rotation in degrees.
 */
export function summarizeTransform(transform) {
  if (!transform || transform === "none") return "none";
  const m = /^matrix\(([^)]+)\)$/.exec(transform);
  if (!m) return transform.startsWith("matrix3d") ? "3d" : transform;
  const [a, b, , , e, f] = m[1].split(",").map(Number);
  const scale = Math.hypot(a, b);
  const rot = (Math.atan2(b, a) * 180) / Math.PI;
  const parts = [];
  if (Math.abs(e) >= 0.5) parts.push(`x${Math.round(e)}`);
  if (Math.abs(f) >= 0.5) parts.push(`y${Math.round(f)}`);
  if (Math.abs(scale - 1) >= 0.005) parts.push(`s${round(scale, 2)}`);
  // At scale 0 the angle means nothing.
  if (scale >= 0.005 && Math.abs(rot) >= 0.5) parts.push(`r${Math.round(rot)}`);
  return parts.length ? parts.join(" ") : "none";
}

const round = (n, digits) => Number(n.toFixed(digits));

/** An opacity as text: "1", "0.42"; "-" when there is none (the element is gone). */
export function formatOpacity(v) {
  if (v === null || v === undefined || v === "") return "-";
  const n = Number(v);
  return Number.isFinite(n) ? String(round(n, 2)) : "-";
}

/**
 * The table that motion-probe prints.
 * @param {{ at: number, connected: boolean, self: { opacity: string, transform: string } | null,
 *           children: { opacity: string, transform: string }[] }[]} samples
 * @returns {string}
 */
export function formatTable(samples) {
  const rows = samples.map((s) => [
    `${s.at} ms`,
    s.connected && s.self ? formatOpacity(s.self.opacity) : "gone",
    s.connected && s.children.length ? s.children.map((c) => formatOpacity(c.opacity)).join(" ") : "-",
    s.connected && s.self ? summarizeTransform(s.self.transform) : "-",
  ]);
  const head = ["time", "opacity", "children opacity", "transform"];
  const widths = head.map((h, i) => Math.max(h.length, ...rows.map((r) => r[i].length)));
  const line = (r) => "  " + r.map((c, i) => (i === r.length - 1 ? c : c.padEnd(widths[i]))).join("  ");
  return [line(head), ...rows.map(line)].join("\n");
}

/**
 * True when nothing changes from the first time to the last:
 * the element was already in its final state at the first time.
 */
export function isSettled(samples) {
  const live = samples.filter((s) => s.connected && s.self);
  if (live.length < 2 || live.length !== samples.length) return false;
  const key = (s) => JSON.stringify([s.self, s.children]);
  return live.every((s) => key(s) === key(live[0]));
}

/** The page with the pictures side by side. Pure. */
export function sheetHtml({ url, watch, reduced, shots }) {
  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);
  const figures = shots
    .map((s) =>
      s.file
        ? `<figure><img src="${esc(s.file)}" alt="${esc(watch)} at ${s.at} ms"><figcaption><b>${s.at} ms</b> · picture at ${s.shotAt} ms · opacity ${esc(s.opacity)}</figcaption></figure>`
        : `<figure class="gone"><div>gone</div><figcaption><b>${s.at} ms</b> · the element was gone</figcaption></figure>`,
    )
    .join("\n");
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Motion probe</title>
<style>
  body { margin: 0; padding: 24px 16px; background: #F3F4F6; color: #1E2939; font: 14px/1.5 system-ui, sans-serif }
  h1 { margin: 0 0 4px; font-size: 18px } p { margin: 0 0 16px; color: #6A7282; overflow-wrap: anywhere }
  .row { display: flex; gap: 16px; overflow-x: auto; align-items: flex-start; padding-bottom: 8px }
  figure { margin: 0; flex: none; display: grid; gap: 6px }
  img { display: block; max-width: 360px; height: auto; border: 1px solid #D1D5DC; border-radius: 8px;
    background: repeating-conic-gradient(#E5E7EB 0 25%, #fff 0 50%) 0 0 / 16px 16px }
  .gone div { width: 120px; height: 80px; display: grid; place-items: center; border: 1px dashed #D1D5DC; border-radius: 8px; color: #6A7282 }
</style></head><body>
<h1>${esc(watch)}</h1>
<p>${esc(url)}${reduced ? " · reduced motion" : ""}</p>
<div class="row">
${figures}
</div>
</body></html>
`;
}

// ---- in the page ----

// Runs before the page loads. Remembers every new node that matches the selector and,
// once armed, measures the first one at each time.
function pageProbe({ selector, at, armed, maxChildren }) {
  const mp = { error: null, armedAt: armed ? 0 : Infinity, target: null, appearedAt: 0, samples: [] };
  window.__motionProbe = mp;
  const read = (el) => {
    const cs = getComputedStyle(el);
    return { opacity: cs.opacity, transform: cs.transform };
  };
  const sample = (when) => {
    const el = mp.target;
    const connected = el.isConnected;
    mp.samples.push({
      at: when,
      real: Math.round(performance.now() - mp.appearedAt),
      connected,
      self: connected ? read(el) : null,
      children: connected ? [...el.children].slice(0, maxChildren).map(read) : [],
    });
  };
  const take = (el, now) => {
    if (mp.target || now < mp.armedAt) return;
    mp.target = el;
    mp.appearedAt = now;
    for (const t of at) setTimeout(() => sample(t), Math.max(0, t - (performance.now() - now)));
  };
  const check = (node, now) => {
    if (node.nodeType !== 1 || mp.target) return;
    try {
      if (node.matches(selector)) return take(node, now);
      const inner = node.querySelector(selector);
      if (inner) take(inner, now);
    } catch (err) {
      mp.error = String(err && err.message ? err.message : err);
    }
  };
  mp.arm = () => {
    mp.armedAt = performance.now();
  };
  new MutationObserver((list) => {
    const now = performance.now();
    for (const m of list) m.addedNodes.forEach((n) => check(n, now));
  }).observe(document, { childList: true, subtree: true });
}

// ---- the run ----

async function loadPlaywright() {
  for (const name of ["playwright", "@playwright/test"]) {
    try {
      return await import(name);
    } catch {}
    try {
      // The project you run it from: its node_modules.
      return createRequire(join(process.cwd(), "noop.js"))(name);
    } catch {}
  }
  return null;
}

async function main(argv) {
  let o;
  try {
    o = parseArgs(argv);
  } catch (err) {
    console.error(`motion-probe: ${err.message}\n${USAGE}`);
    process.exit(2);
  }
  const pw = await loadPlaywright();
  if (!pw) {
    console.error(
      "motion-probe: Playwright for Node is not found.\n" +
        "Install it in the project you run this from: npm i -D playwright && npx playwright install chromium",
    );
    process.exit(2);
  }
  const url = toUrl(o.url);
  const out = resolve(o.out);
  mkdirSync(out, { recursive: true });

  const browser = await pw.chromium.launch();
  try {
    const context = await browser.newContext({
      viewport: { width: o.width, height: o.height },
      reducedMotion: o.reduced ? "reduce" : "no-preference",
    });
    const page = await context.newPage();
    await page.addInitScript(pageProbe, { selector: o.watch, at: o.at, armed: !o.click, maxChildren: MAX_CHILDREN });
    await page.goto(url, { waitUntil: "load" });

    if (o.click) {
      await page.evaluate(() => window.__motionProbe.arm());
      await page.locator(o.click).first().click();
    }
    try {
      await page.waitForFunction(() => window.__motionProbe.target || window.__motionProbe.error, null, {
        timeout: APPEAR_TIMEOUT,
        polling: "raf",
      });
    } catch {
      console.error(`motion-probe: no new element matched ${o.watch} in ${APPEAR_TIMEOUT / 1000} s${o.click ? ` after the click on ${o.click}` : ""}.`);
      process.exitCode = 1;
      return;
    }
    const error = await page.evaluate(() => window.__motionProbe.error);
    if (error) {
      console.error(`motion-probe: --watch is not a valid CSS selector: ${error}`);
      process.exitCode = 2;
      return;
    }

    // Pictures: as close to each time as we can get from outside the page.
    const shots = [];
    for (const [i, t] of o.at.entries()) {
      const since = await page.evaluate(() => performance.now() - window.__motionProbe.appearedAt);
      if (t > since) await page.waitForTimeout(t - since);
      const box = await page.evaluate(() => {
        const mp = window.__motionProbe;
        if (!mp.target.isConnected) return null;
        const r = mp.target.getBoundingClientRect();
        return { x: r.x, y: r.y, width: r.width, height: r.height, at: Math.round(performance.now() - mp.appearedAt) };
      });
      const shot = { at: t, shotAt: box ? box.at : null, file: null, opacity: "" };
      const clip = box && clipOf(box, o.width, o.height);
      if (clip) {
        shot.file = `shot-${String(i + 1).padStart(2, "0")}-${t}ms.png`;
        await page.screenshot({ path: join(out, shot.file), clip });
      }
      shots.push(shot);
    }

    const last = o.at[o.at.length - 1];
    await page.waitForFunction((n) => window.__motionProbe.samples.length >= n, o.at.length, { timeout: last + 5000 });
    const samples = await page.evaluate(() => window.__motionProbe.samples.map(({ at, real, connected, self, children }) => ({ at, real, connected, self, children })));
    samples.sort((a, b) => a.at - b.at);
    for (const s of shots) {
      const m = samples.find((x) => x.at === s.at);
      s.opacity = m && m.self ? formatOpacity(m.self.opacity) : "-";
    }

    writeFileSync(join(out, "sheet.html"), sheetHtml({ url, watch: o.watch, reduced: o.reduced, shots }));
    console.log(`motion-probe: ${o.watch}${o.reduced ? " (reduced motion)" : ""}`);
    console.log(formatTable(samples));
    if (isSettled(samples)) {
      console.log(
        o.reduced
          ? `note: the values at ${o.at[0]} ms are already final. With reduced motion this is what we want.`
          : `note: nothing changes from ${o.at[0]} ms on. The motion is over by then, or there is none.`,
      );
    } else if (o.reduced) {
      console.log("note: values still change with reduced motion. Check that this is a fade, not a move.");
    }
    console.log(`pictures and sheet: ${join(out, "sheet.html")}`);
  } finally {
    await browser.close();
  }
}

// The element with some space around it, kept inside the window.
function clipOf(box, width, height) {
  const x = Math.max(0, Math.floor(box.x - PAD));
  const y = Math.max(0, Math.floor(box.y - PAD));
  const w = Math.min(width, Math.ceil(box.x + box.width + PAD)) - x;
  const h = Math.min(height, Math.ceil(box.y + box.height + PAD)) - y;
  return w > 0 && h > 0 ? { x, y, width: w, height: h } : null;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main(process.argv.slice(2));

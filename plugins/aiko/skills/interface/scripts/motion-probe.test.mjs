// Run: node --test   (from this folder). No browser is started here.
import { test } from "node:test";
import assert from "node:assert/strict";
import { formatTable, isSettled, parseArgs, parseTimes, sheetHtml, summarizeTransform, toUrl } from "./motion-probe.mjs";

test("parseArgs gives defaults", () => {
  const o = parseArgs(["http://localhost:3000", "--watch", ".rx-layer > *"]);
  assert.deepEqual(o, {
    url: "http://localhost:3000",
    watch: ".rx-layer > *",
    click: null,
    at: [50, 150, 400],
    out: "motion-probe",
    reduced: false,
    width: 1440,
    height: 900,
  });
});

test("parseArgs reads every option", () => {
  const o = parseArgs(["page.html", "--watch", ".a", "--click", ".rx >> nth=4", "--at", "400, 50,150,50", "--out", "tmp", "--reduced", "--width", "390", "--height", "844"]);
  assert.equal(o.click, ".rx >> nth=4");
  assert.deepEqual(o.at, [50, 150, 400]);
  assert.equal(o.out, "tmp");
  assert.equal(o.reduced, true);
  assert.equal(o.width, 390);
  assert.equal(o.height, 844);
});

test("parseArgs gives clear errors", () => {
  assert.throws(() => parseArgs(["--watch", ".a"]), /url/);
  assert.throws(() => parseArgs(["page.html"]), /--watch/);
  assert.throws(() => parseArgs(["page.html", "--watch"]), /--watch needs a value/);
  assert.throws(() => parseArgs(["page.html", "--watch", ".a", "--at", "50,soon"]), /"soon"/);
  assert.throws(() => parseArgs(["page.html", "--watch", ".a", "--width", "wide"]), /--width/);
  assert.throws(() => parseArgs(["page.html", "--watch", ".a", "--slow"]), /unknown option --slow/);
  assert.throws(() => parseArgs(["a.html", "b.html", "--watch", ".a"]), /one url/);
});

test("parseTimes sorts and drops repeats", () => {
  assert.deepEqual(parseTimes("0,1000,300,300"), [0, 300, 1000]);
  assert.throws(() => parseTimes(","), /--at needs/);
  assert.throws(() => parseTimes("-5"), /"-5"/);
});

test("toUrl keeps urls and turns paths into file urls", () => {
  assert.equal(toUrl("https://example.com/a"), "https://example.com/a");
  assert.equal(toUrl("file:///C:/a.html"), "file:///C:/a.html");
  assert.match(toUrl("demo.html", process.cwd()), /^file:\/\/.*demo\.html$/);
});

test("summarizeTransform reads a matrix", () => {
  assert.equal(summarizeTransform("none"), "none");
  assert.equal(summarizeTransform("matrix(1, 0, 0, 1, 0, 0)"), "none");
  assert.equal(summarizeTransform("matrix(0.5, 0, 0, 0.5, 12, -30.4)"), "x12 y-30 s0.5");
  // 90 degrees: a = cos, b = sin
  assert.equal(summarizeTransform("matrix(0, 1, -1, 0, 0, 0)"), "r90");
  assert.equal(summarizeTransform("matrix(-0, -0, 0, -0, 0, 0)"), "s0", "no angle at scale 0");
});

const sample = (at, opacity, transform = "none", children = []) => ({
  at,
  connected: true,
  self: { opacity, transform },
  children: children.map((o) => ({ opacity: o, transform: "none" })),
});

test("formatTable shows time, opacity, children and transform", () => {
  const table = formatTable([
    sample(50, "0.123", "matrix(0.4, 0, 0, 0.4, 0, 20)", ["1", "0.5"]),
    sample(400, "1"),
    { at: 2000, connected: false, self: null, children: [] },
  ]);
  const lines = table.split("\n");
  assert.equal(lines.length, 4);
  assert.match(lines[0], /time\s+opacity\s+children opacity\s+transform/);
  assert.match(lines[1], /50 ms\s+0\.12\s+1 0\.5\s+y20 s0\.4/);
  assert.match(lines[2], /400 ms\s+1\s+-\s+none/);
  assert.match(lines[3], /2000 ms\s+gone/);
});

test("isSettled is true only when nothing changes", () => {
  assert.equal(isSettled([sample(50, "1"), sample(400, "1")]), true);
  assert.equal(isSettled([sample(50, "0.2"), sample(400, "1")]), false);
  assert.equal(isSettled([sample(50, "1", "matrix(1, 0, 0, 1, 0, 4)"), sample(400, "1")]), false);
  assert.equal(isSettled([sample(50, "1")]), false);
});

test("sheetHtml lists every picture and escapes the selector", () => {
  const html = sheetHtml({
    url: "file:///a.html",
    watch: ".fx > *",
    reduced: true,
    shots: [
      { at: 50, shotAt: 71, file: "shot-01-50ms.png", opacity: "0.3" },
      { at: 900, shotAt: null, file: null, opacity: "-" },
    ],
  });
  assert.match(html, /<img src="shot-01-50ms\.png"/);
  assert.match(html, /picture at 71 ms/);
  assert.match(html, /the element was gone/);
  assert.match(html, /\.fx &gt; \*/);
  assert.match(html, /reduced motion/);
});

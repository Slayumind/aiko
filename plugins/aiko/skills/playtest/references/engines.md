# Engine recipes

Short starting points. Check the engine version the project uses: names of APIs move between versions.

## Web: three.js, Babylon.js, PixiJS, canvas

**State surface.** In development builds only:

```js
if (import.meta.env.DEV) {
  window.__GAME__ = {
    snapshot: () => ({
      scene: game.scene.name,
      seed: game.seed,
      player: { ...game.player.position },
      loading: game.streaming.pending,
      frameMs: game.stats.lastFrameMs,
      drawCalls: renderer.info.render.calls,
      triangles: renderer.info.render.triangles,
    }),
    loadScene: (name) => game.loadNamedScene(name),
  };
}
```

`renderer.info` is three.js; other engines have their own counters. With `renderer.info.autoReset`
on (the default), the counters describe the last rendered frame.

**Driving it.** Playwright: `page.evaluate(() => window.__GAME__.loadScene("landing"))`, then wait with
`page.waitForFunction(() => !window.__GAME__.snapshot().loading)`. Real controls:
`page.keyboard.down("Space")`, `page.mouse.move(x, y)`.

**Frame times.** Collect `requestAnimationFrame` intervals inside the page and read them once at the
end, not every frame over the protocol. Headless Chromium may throttle or skip the GPU: measure
performance in a headed browser and say so in the report.

## Unity

**State surface.** A static class in a development-only assembly or behind `#if DEVELOPMENT_BUILD ||
UNITY_EDITOR`, with a `Snapshot()` that returns a plain serializable struct.

**Counters.** `Unity.Profiling.ProfilerRecorder`, for example
`ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count")` and
`ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15)` for frame time in
nanoseconds. Some render counters exist only in development builds or the editor.

**Named scenes.** A ScriptableObject per scene (scene asset, seed, spawn point, state) and one
`LoadNamed(string)` entry point that tests and the editor bridge both call.

**Journey tests.** Play Mode tests with the Input System test helpers: a class based on
`InputTestFixture`, `var keyboard = InputSystem.AddDevice<Keyboard>()`, then `Press(keyboard.spaceKey)`
and `yield return null` or `WaitForSeconds` between steps. Assert on positions from the state surface.

**Where to measure.** Editor play mode timings differ from a player build. For performance, measure a
development build and say which one.

## Godot 4

**State surface.** An autoload singleton (for example `DebugState`) with `snapshot() -> Dictionary`,
added only when `OS.is_debug_build()` is true.

**Counters.** `Performance.get_monitor(Performance.TIME_PROCESS)`,
`Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME`, `Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME`,
`Performance.OBJECT_NODE_COUNT`.

**Journey tests.** Real input through `Input.parse_input_event()` with an `InputEventAction` or
`InputEventKey`; a test runner such as GUT or gdUnit4. Logic tests can run with `godot --headless`;
rendering counters need a real renderer.

## Any engine

- Seed every random source from the scene seed. Log the seed with every failure.
- Write snapshots as JSON lines to a file during a run; a small script turns them into the table.
- Keep the scene list and the journey list in the project skill or the project docs, with one line each:
  what it starts with and what it is for.

## Source of the method

The idea of a state surface, named scenes, before and after tables and journey tests comes from
OpenAI's article "How to build games with Astra" (developers.openai.com, 2026). Nothing in it depends on
one model or one engine.

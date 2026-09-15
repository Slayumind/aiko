---
name: aiko-texturing
description: Plan textures for a game from its camera instead of habit - pixels per metre from the screen, when a surface earns a sheet of its own and when it tiles, UVs laid out from world position, guide sheets for hand painting, and measuring texel density face by face. Includes a camera budget calculator and a Blender density checker. Use before unwrapping, when picking a texture size, when a texture looks soft, blurry or visibly repeats, or when preparing sheets for a painter.
---

# aiko texturing

Most texture sizes are chosen by habit: 2048 because the last project used it, 512 px/m because a
tutorial said so. A game with a known camera does not have to guess. **The number of pixels a surface
can ever show on screen is known, and it is usually smaller than instinct says.**

## 1. Density comes from the screen

Ask for the camera: tilt below the horizon, vertical field of view, distance to the point it looks at,
and the screen size you target. Also ask whether the player can zoom or move closer; if yes, use the
closest camera.

Then compute the budget:

```
python "<skill base directory>/scripts/texel_budget.py" --tilt 60 --vfov 30 --distance 27
```

For that camera at 1920 × 1080: about 75 px/m in the middle of the screen, 86 at the near (bottom)
edge, 63 at the far edge. The near edge is the most a texture can ever show. The base directory is shown
when the skill loads.

What the numbers mean:

- A point at depth `d` shows `f / d` pixels per metre across the screen, with
  `f = (screen height / 2) / tan(vfov / 2)`.
- Along the screen's height a surface is squashed: ground by `sin(tilt)`, a vertical wall by
  `cos(tilt)`. At tilt 60 a wall shows half its height. Across the screen both keep the full density.
- Pick the power of two at or just above the near edge for what the player reads closely (128 for the
  camera above). Above that you pay memory and painting time for sharpness nobody sees. Below, the
  texture looks soft.
- A surface seen face on (a floor under a top-down camera, a wall in a side view) may deserve a step
  more than a surface seen at a slant. Decide per role, and write the table down:

| Role | px/m | Why |
|---|---|---|
| walls, columns | 128 | near edge 86; walls show half their height |
| tiling ground | 256 | seen face on, repeats, so a small tile stays sharp |

Numbers from one camera. Compute yours.

## 2. Measure, do not assume

`scripts/texel_check.py` reports pixels per metre face by face inside Blender, with no add-on:

```
density = sqrt(uv_area) * sheet_size / sqrt(world_area)
```

```python
texel_check.report(["wall", "column"], sheet=2048, density=128, hidden=32)
```

A box projection is exact for a face in the plane it projects on and short for a face at a slant, so
expect most of a wall at the target and thin trim a little below. Faces nobody sees can be mapped lower
on purpose (`hidden=`), which frees room on the sheet.

`build_checker()` writes a checker with one-metre cells at your density and coloured cells at the
origin: orange at (0,0), blue along U, green along V. On the model it shows at a glance whether a face
is stretched, mirrored or turned.

## 3. A sheet of its own, or a tile

- **Anything the player reads as a place gets a sheet.** A tile can never carry something that happens
  once: a mark on the floor, a worn doorstep, the centre of an arena.
- **Anything that is just material tiles.** Stone, grass, plaster.
- **A square tile cannot follow a grid that is not square.** Hex cells, for example, repeat over a
  rectangle of `2 × √3` cell radii; no square tile lines up with that.
- **Size decides the rest.** Width in metres × density = pixels. A 20 m floor at 256 px/m is 5 000 px:
  that floor tiles. A 16 m arena at 125 px/m fits a 2048 sheet: it gets one.

## 4. Lay out a sheet from world position

Compute UVs **from where a point is in the world**, never by fitting islands to the current polygons.
Then a later change of shape does not invalidate the sheet, new faces get their coordinates on their
own, and a texture painted a month ago still lands in the same place. Re-unwrapping is running the
script again.

In a Blender to Unity pipeline, compute from the axes Unity will see and keep that rule in one function
(see the `aiko-blender-to-unity` skill). A wrong sign mirrors every sheet, and on symmetric parts nobody
notices for weeks.

Rules that pay for themselves:

- **A role gets one fixed map from world space to pixels.** Variants of a part (a wall with a door, with
  a window, blank) then share one painted sheet.
- **One packing grid per family, not per mesh.** Packing each mesh from an empty grid puts different
  surfaces on the same pixels.
- **Islands of the same size on the same plane share a slot.** Paint it once.
- **What is never seen gets a lower density.** It frees a row of the sheet for what is.
- **Do not nest a recess into the hole it cuts.** The solid variants read the same pixels, so the recess
  could only ever be painted as plain wall. Give it an island of its own.

## 5. Guide sheets for a painter

Per sheet, at the full size and at half and quarter:

- **zones**: every surface filled flat with the colour of its zone. The first coat, and what a magic
  wand selects.
- **uv**: island rims bright, inner edges faint. The top layer to paint against. Keep about 8 px between
  islands.
- **grid**: a line every metre and every quarter metre, numbered. Drop the quarter lines when they come
  closer than about 16 px: they stop being a ruler.
- **preview**: all of it burnt into one opaque sheet. Not for painting: hang it on the model to check
  that everything lands where it should.

A guide that claims to know what the game knows (cell coordinates, a door position) must be computed
with the game's own arithmetic and compared with the mesh UVs. A grid drawn by eye is worse than none.

## 6. A tile seam without an offset filter

A tiling texture has to meet itself at the edges. Most editors have an Offset filter; if the painter's
app has none, shift the image by half a tile in both directions with a script (for example
`numpy.roll` on the pixel array), paint out the cross in the middle, and shift back. Two half shifts
return the original pixel for pixel.

## 7. Before handing a sheet over

1. `texel_check.report(...)` is ok for every mesh.
2. A predictable vertex has the UV you expect, in the engine, not only in Blender.
3. Guides computed from game data were compared with the mesh, and the comparison was printed.
4. The sheets hang on the model, and a frame from the real game camera looks right.

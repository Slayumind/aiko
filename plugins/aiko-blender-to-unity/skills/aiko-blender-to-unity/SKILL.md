---
name: aiko-blender-to-unity
description: Move meshes from Blender into Unity without mirrored, rotated or flat-shaded surprises - the pose a part should arrive in, FBX export and import settings, the measured axis rule for anything computed from coordinates, a gentle blend of normals, cutting shared geometry between modules, and the bpy and bmesh traps that fail silently. Includes export and normals scripts. Use when exporting or re-exporting an FBX for Unity, building modular pieces, or when an imported mesh sits wrong, looks mirrored or loses its shading.
---

# aiko blender to unity

Geometry is authored in Blender and placed by code in Unity. The mesh carries the shape, the code
carries the position. Write down the contract between them once, and check every export against it.

## 1. The contract a part satisfies

Agree on it with the project first and write it in the project's docs. A good default:

- **Orientation in Unity:** length along X, height along Y, thickness along Z. `+Z` points the way the
  placing code aims the part (for example out of a room, when code uses
  `Quaternion.LookRotation(outwardNormal)`).
- **Origin at floor level, centred along the length.** Code sets `localPosition` and never
  `localScale`: the size is the asset's promise, not a multiplier.
- **A part describes its own size.** Colliders and offsets come from `Mesh.bounds`, not from numbers
  copied into a config. When the model changes, nothing else has to follow.

## 2. Export settings (FBX)

**`Apply Transform` on** (`bake_space_transform=True`), `Forward: -Z`, `Up: Y`,
`Apply Scalings: FBX All`, mesh only, no leaf bones, `Smoothing: Edge`.

Without `Apply Transform` the model arrives with a -90° rotation baked into its root, and
`Bake Axis Conversion` in the Unity importer does **not** fix it: it swaps 270° for 89.98° and leaves
the vertices Z-up.

## 3. Import settings (Unity model importer)

`Scale Factor 1`, `Convert Units` on, **`Normals: Import`** (`Calculate` throws custom normals away),
`Material Creation Mode: None` when code assigns materials, cameras and lights off.

## 4. Turn the part, then read the bounds

The export flags alone do not put a part into the contract's pose. Model it in Blender in the pose that
is natural there, and turn a temporary copy before writing the file. `scripts/export_fbx.py` does this
with `turn_z(bm, degrees)`.

**The wrong turn looks right.** -90° and +90° about Z give the same shape, vertex count and screenshot,
but one of them puts the part on the wrong side of its origin. So:

> After every export, read `mesh.bounds` in Unity **before** looking at a screenshot.

Then prove the handedness with one vertex you can predict, for example "a detail on the player's left
in Blender has negative X in Unity".

## 5. Where a vertex lands: use the measured rule

With the settings in section 2, a vertex of the **exported mesh** at Blender `(x, y, z)` lands in Unity
at:

```
Unity = (-x, z, -y)
```

Blender is right-handed and Unity left-handed, so the exporter flips exactly one axis on top of the
axis swap. The shape looks correct either way; the axes do not.

**Anything computed from coordinates (UVs from world position, grids, anchor points) must use this
rule, not one derived on paper.** A sign worked out by hand once mirrored every UV sheet in a project
for weeks: on symmetric parts it was invisible, on a grid painted from game coordinates it was not.
Keep the rule in one function (`unity_of` in `scripts/export_fbx.py`) and check it against one measured
vertex in your project before trusting it.

## 6. Shading: a tenth of the way to smooth

Plain Shade Smooth melts hard-surface parts, flat shading looks like cardboard, and smoothing by angle
often has no useful middle on real geometry (bends are either tiny or large). A blend works:

```
normal = (1 - share) * face normal + share * vertex normal        share = 0.10
```

At 0.10 a normal leans about 4° off its face on a right angle. Planes stay crisp and faces take a soft
fall of light. The value is a taste; try 0.05 to 0.2. `scripts/normals.py` has `blend()` and
`widest_tilt()`.

**Blend on the mesh you export, after every bmesh step.** bmesh drops custom normals, so normals set on
the source model are lost when the export copies, cuts and turns it.

In Unity, check the widest angle between a vertex normal and its triangle: a few degrees means the
blend arrived; zero means the importer recalculated normals; a large angle means the blend never ran.

## 7. Shared geometry: cut it, do not pick an owner

When modules share a piece (a wall between two rooms, a column where three meet), give each module
**its part of the piece**: a half of the wall, a 120° wedge of the column. Code then places parts
without a rule about who owns what, and a boundary between module types runs through the piece.

- **No faces on the cut planes.** They are hidden by the mating part; faces there are wasted polygons
  and depth fights. `bisect(bm, degrees)` cuts without filling.
- Where a neighbour is missing (the edge of a map), a present module places the spare part, and one
  rule decides which one.

## 8. Traps that fail silently

- **Never remove a mesh by name.** Blender may have named the temporary copy `Name.001` and kept the
  original as `Name`. Remove through the object: `bpy.data.meshes.remove(obj.data)`.
- **Object names and mesh names drift apart.** Look parts up by `obj.data.name` or ask for both.
- **Restore the selection and the active object** at the end of a script: the user is working there.
- **An export script never edits the source model.** Cut and turn temporary copies.
- **A modelling script edits it, so it must be idempotent.** Read inputs from something the script does
  not overwrite, and run it twice: the numbers must not move. A one-off that cannot be idempotent must
  refuse to run a second time, by looking for its own marks.
- **Edit Mode:** `mesh.polygons` shows the state from before, and `to_mesh` throws. Use
  `bmesh.from_edit_mesh` / `update_edit_mesh`, and do not switch modes under the user.
- **Blender keeps modules loaded.** A script that imports a helper must `importlib.reload` it, or it
  runs the old code.
- **`face.normal` is stale** until `bm.normal_update()`.
- **`recalc_face_normals` needs a volume.** On a flat open shape it may turn every face down. Say which
  way is up: `bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.normal.z < 0])`.
- **Right after `wm.open_mainfile` there is no selection**, and `bpy.context.selected_objects` raises.

## 9. Check an export in this order

1. Vertex and triangle counts: did the cut remove what it should?
2. `mesh.bounds`: size, and which side of zero the part sits on.
3. One predictable vertex (and its UV, if the part has UVs): bounds cannot catch a mirror.
4. Normals: the widest tilt from section 6.
5. The placing code: rebuild the scene and count parts per slot.
6. Only then a screenshot.

## Scripts

Both run inside Blender. Copy them next to the project's own script or add the skill's `scripts` folder
(shown as the base directory when the skill loads) to `sys.path`.

- `scripts/normals.py`: `blend(mesh, share)`, `widest_tilt(mesh)`.
- `scripts/export_fbx.py`: `turn_z`, `bisect`, `unity_of`, `write_fbx(mesh, path, share)` and
  `export_parts(parts, out_dir)` for a dictionary of parts. The project decides the part names, the cuts
  and the output folder.

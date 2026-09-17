---
name: glb-for-web
description: Turn a Blender model into a .glb that a web page shows correctly with model-viewer or three.js - exporter flags, size and origin, sRGB versus linear colour, when Draco compression pays off, and the silent failure of an unsupported required extension. Includes an offline checker for .glb files. Use when exporting a model for a website, a blog post or a web demo, or when a model uploads fine and the page shows an empty box, a mirrored model or pale colours.
---

# aiko glb for web

Most web 3D viewers are three.js inside: Google's `<model-viewer>`, react-three-fiber, plain
three.js pages. They read **glTF 2.0**, best as one binary `.glb`. Everything below follows from how
they read it.

First ask what shows the model: which player, which version, and whether the site checks uploads
(size limit, allowed extensions). Use those numbers with the checker below instead of the defaults.

## Write the file from a script, not the export window

`scripts/preview_glb.py` holds the export settings as functions. Import it from a project script that
knows what belongs in the model; the module only knows how to write it. It runs inside Blender.

```python
import sys
sys.path.append(r"<skill base directory>/scripts")
import preview_glb
```

The skill's base directory is shown when the skill loads.

It gives you `linear_of`, `flat_material`, `bounds_of`, `shrink`, `write` and `report`. `write` filters
its own arguments against what this Blender declares, so an exporter that renamed a flag drops that
flag and names it, instead of raising halfway through the export.

## glTF axes are not the FBX axes

**Never flip an axis yourself.** glTF is Y up and right-handed; Blender is Z up and right-handed;
`export_yup=True` converts between them and mirrors nothing.

An FBX pipeline for Unity in the same project negates one axis, because Unity is left-handed. Copying
that rule into a glTF path publishes a mirror image, and a mirrored building looks fine until someone
reads the text on it.

## Size and origin are the player's two assumptions

- **1 to 2 Blender units across the widest side.** The player frames any size, but at the extremes the
  ground shadow and the zoom step misbehave. `shrink()` scales about the world origin, not about each
  object's own origin: scaling objects one by one pulls an assembly apart.
- **Origin at the centre of the base.** The camera turns around the origin, and the shadow falls where
  the model meets it. An origin off to one side makes the model swing in a circle instead of spinning.

## Colour is sRGB where you type it and linear where it is stored

glTF keeps `baseColorFactor` in linear light. Every colour a person quotes (a hex, a design token, a
swatch) is sRGB. `#a1a1a1` is `0.631` as typed and **`0.356`** as stored.

Write the hex in the script and let `linear_of` convert it. Handing the exporter `0.631` publishes a
model visibly paler than the colour that was asked for, and the mistake is hard to see because the
model still looks plausible.

## Draco pays off above about a megabyte

Decoders are not free. Draco's wasm decoder is about 250 KB, and a KTX2/Basis transcoder about
570 KB. Players load them only for a file that needs them, so the sum that matters is per model.

A blockout of a thousand triangles with no textures is well under 100 KB: Draco would save a few
kilobytes and add 250. Turn it on above roughly a megabyte, which in practice means as soon as the
model carries real textures. `report()` says when a file has crossed that line.

## The one failure that is silent

A viewer answers a required extension it cannot decode by **drawing nothing at all**: no console
error, no broken-image icon, an empty box.

The check is against `extensionsRequired`, not `extensionsUsed`. "Used" is a hint a loader may ignore;
"required" means the file cannot be read without it. `EXT_meshopt_compression` is the usual culprit:
`gltfpack` writes it, and the page needs a separate meshopt decoder.

`scripts/check_glb.py` answers this and the size question locally, with no Blender and no dependencies
(Python 3.8+):

```
python "<skill base directory>/scripts/check_glb.py" model.glb
python "<skill base directory>/scripts/check_glb.py" model.glb --max-mb 5 --allow EXT_meshopt_compression
```

It reports size, triangles, materials, animations and extensions, rejects a broken or truncated file,
and warns above 150 000 triangles or 4 materials (change with `--triangles` and `--materials`).

## Only Principled BSDF travels, and the light is not yours

Procedural nodes, mixed shaders, noise textures: none of it converts. Bake to textures and feed the
bake into a Principled BSDF, or accept a flat colour the exporter picked.

Most web players light the model with their own environment image. Blender lamps do export, but their
intensities convert only roughly, so a careful Cycles setup is work the reader never sees. Put the look
in the materials.

Budgets that keep a phone happy: about 150 000 triangles, about 4 materials, textures at 2048 px or
less, exported as WebP.

## Before you hand a file over

1. `check_glb.py` says `OK` with the limits of the page that will show it.
2. Size, triangles, materials and required extensions read the way you expected.
3. Load the `.glb` back into a **headless** Blender and render it. That proves the file itself, not the
   scene you exported from: custom normals survived, materials survived, the model is not mirrored and
   not black. Never round-trip in the session the author has open.
4. Open it at <https://modelviewer.dev/editor/>. Same engine as most pages, so what you see there is
   close to what the reader gets. A Blender render looks darker: Blender applies its own view transform.
5. Tell the user where the file is. Uploading it to their site is their step unless they ask otherwise.

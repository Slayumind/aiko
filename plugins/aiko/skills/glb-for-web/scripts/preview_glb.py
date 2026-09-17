"""The export settings a .glb needs to look right in a web player (model-viewer, three.js).

Import this from a project's own assembly script, which knows what to put in the file;
this module only knows how to write it. Blender only - it imports bpy.

    import sys
    sys.path.append(r"<skill base directory>/scripts")
    import preview_glb

Nothing here decides what goes in the model. It decides the four things that are the
same for every model on a web page: which exporter flags, how big, where the origin,
and how a colour is written down.
"""

import os

import bpy
from mathutils import Matrix, Vector

# Web players frame a model of any size, but the ground shadow and the zoom step
# misbehave at the extremes. 1-2 Blender units across is the safe middle.
WIDEST = 2.0

# Draco costs the reader a 250 KB wasm decoder, fetched only by models that need one.
# Below roughly this size the decoder is bigger than everything it saves.
DRACO_WORTH_IT = 1024 * 1024

# A sane size limit for one model on a web page.
MAX_BYTES = 10 * 1024 * 1024


def linear_of(hexadecimal):
    """An sRGB hex string as the linear triple glTF stores.

    glTF keeps baseColorFactor in linear light and Blender keeps Base Color the same
    way, but every colour a person quotes - a hex, a swatch, a CSS token - is sRGB.
    Putting the sRGB number straight into the file publishes a washed out model:
    #a1a1a1 is 0.631 as typed and 0.356 as stored.
    """
    text = hexadecimal.lstrip("#")
    channels = [int(text[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(
        c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
        for c in channels)


def flat_material(name, hexadecimal, roughness=0.85):
    """One Principled BSDF in a flat colour, which is all glTF carries anyway.

    Anything else in a Blender material - procedural nodes, mixed shaders, Musgrave -
    does not travel. It has to be baked to a texture first, or it arrives as a flat
    colour chosen by the exporter rather than by you.
    """
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes["Principled BSDF"]
    principled.inputs["Base Color"].default_value = (*linear_of(hexadecimal), 1.0)
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = 0.0
    return material


def bounds_of(objects):
    """The world space bounding box of a list of objects, as two Vectors."""
    points = [obj.matrix_world @ Vector(c) for obj in objects for c in obj.bound_box]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def shrink(objects, widest=WIDEST):
    """Scale a whole assembly about the world origin and return the factor used.

    About the origin, not about each object's own: scaling objects one by one moves
    their centres apart and takes the model to pieces.

    The origin is also where the player turns the camera and drops the ground shadow,
    so put the model centred on it with its base at z = 0 before calling this.
    """
    low, high = bounds_of(objects)
    size = high - low
    factor = widest / max(size.x, size.y, size.z)

    for obj in objects:
        obj.matrix_world = Matrix.Scale(factor, 4) @ obj.matrix_world

    return factor


def write(objects, path, draco=False):
    """Export `objects` as one .glb and return (path, arguments this Blender ignored).

    Every flag here is a decision, not a default:

    * GLB, never glTF - one self-contained file. A .gltf with sidecar .bin and images
      breaks as soon as one of the files is uploaded, renamed or cached apart.
    * no cameras and no lights - the player frames the model itself and lights it with
      its own neutral studio image. A camera in the file is ignored; Blender lamps
      convert badly and mislead you into lighting for a look nobody sees.
    * normals on - custom split normals are usually the whole shading plan of a
      blockout, and they do survive glTF.
    * apply modifiers on - glTF carries no modifiers, only their result.
    * yup on - Blender is Z up, glTF is Y up, and the exporter converts. Both are right
      handed, so nothing is mirrored. Do not add a flip of your own here; an FBX-for-
      Unity pipeline needs one because Unity is left handed, and copying that rule into
      a glTF path publishes a mirror image.
    """
    for obj in bpy.data.objects:
        obj.select_set(False)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    os.makedirs(os.path.dirname(path), exist_ok=True)

    wanted = {
        "filepath": path,
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": True,
        "export_yup": True,
        "export_normals": True,
        "export_materials": "EXPORT",
        "export_cameras": False,
        "export_lights": False,
        "export_animations": False,
        "export_extras": False,
        "export_image_format": "NONE",
        "export_draco_mesh_compression_enable": bool(draco),
    }

    # The glTF operator renames arguments between Blender versions. Filtering by what
    # this build actually declares means an old or new Blender drops one setting instead
    # of raising halfway through the export - and says which one it dropped.
    known = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
    settings = {name: value for name, value in wanted.items() if name in known}
    ignored = sorted(set(wanted) - set(settings))

    bpy.ops.export_scene.gltf(**settings)
    return path, ignored


def report(path, objects, extra=""):
    """Print the numbers worth reading back before handing a file over."""
    size = os.path.getsize(path)
    triangles = sum(
        sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in objects)
    materials = {m.name for obj in objects for m in obj.data.materials if m}

    print(f"wrote {path} {size:,} bytes")
    print(f"parts {len(objects)} triangles {triangles:,} materials {len(materials)}")
    if size > MAX_BYTES:
        print(f"TOO BIG over {MAX_BYTES:,} bytes, too heavy for one model on a page")
    elif size > DRACO_WORTH_IT:
        print("over a megabyte - Draco would now save more than its decoder costs")
    if extra:
        print(extra)

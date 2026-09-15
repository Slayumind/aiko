"""Measure texel density face by face, and make a checker texture to look at it.

Blender only - it imports bpy. Nothing here changes geometry: it may write one
PNG and one material, and it prints numbers. Import it from a project script:

    import importlib, sys
    sys.path.insert(0, r"<skill base directory>/scripts")
    import texel_check
    importlib.reload(texel_check)

    texel_check.report(["wall", "column"], sheet=2048, density=128, hidden=32)
    path = texel_check.build_checker(r"C:\\path\\to\\guides", sheet=2048, density=128)
    texel_check.checker_material(path)   # then assign the "checker" material

Density is measured without any add-on:

    density = sqrt(uv_area) * sheet_size / sqrt(world_area)
"""

import math
import os
import struct
import zlib

import bpy


# ---------------------------------------------------------------- png writing

def write_png(path, width, height, pixels):
    """Write RGBA bytes as a PNG. Row 0 of `pixels` is the bottom row.

    Blender's own image saving runs the values through colour management, which
    a guide image does not want: the byte written is the byte meant.
    """
    stride = width * 4
    raw = bytearray()
    for y in range(height - 1, -1, -1):
        raw.append(0)
        raw.extend(pixels[y * stride:(y + 1) * stride])

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    blob = b"\x89PNG\r\n\x1a\n"
    blob += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    blob += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    blob += chunk(b"IEND", b"")

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(blob)
    return path


def fill_rect(pixels, width, x0, y0, x1, y1, rgba):
    colour = bytes(rgba)
    for y in range(max(0, y0), y1):
        row = y * width
        for x in range(max(0, x0), x1):
            i = (row + x) * 4
            pixels[i:i + 4] = colour


# ------------------------------------------------------------------- checker

def build_checker(out_dir, sheet=2048, density=128):
    """A checker whose cell is exactly one metre at `density` pixels per metre.

    Three coloured cells at the origin say which way is up and whether an
    island is mirrored: orange at (0,0), blue one metre along U, green one metre
    along V. Thin lines mark every quarter metre.
    """
    cell = density
    quarter = max(1, cell // 4)
    light = (154, 154, 154, 255)
    dark = (110, 110, 110, 255)
    line = (70, 70, 70, 255)
    edge = (40, 40, 40, 255)
    marks = {(0, 0): (255, 154, 77, 255),
             (1, 0): (110, 138, 168, 255),
             (0, 1): (74, 90, 58, 255)}

    pixels = bytearray(bytes(dark) * sheet * sheet)
    cells = sheet // cell
    for cy in range(cells):
        for cx in range(cells):
            colour = marks.get((cx, cy), light if (cx + cy) % 2 == 0 else dark)
            fill_rect(pixels, sheet, cx * cell, cy * cell, (cx + 1) * cell, (cy + 1) * cell, colour)

    for step in range(0, sheet, quarter):
        colour = edge if step % cell == 0 else line
        fill_rect(pixels, sheet, step, 0, step + 1, sheet, colour)
        fill_rect(pixels, sheet, 0, step, sheet, step + 1, colour)

    return write_png(os.path.join(out_dir, "checker_%d.png" % density), sheet, sheet, pixels)


def checker_material(path, name="checker"):
    """A material that shows the checker with nearest-pixel sampling."""
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()

    output = tree.nodes.new("ShaderNodeOutputMaterial")
    shader = tree.nodes.new("ShaderNodeBsdfDiffuse")
    texture = tree.nodes.new("ShaderNodeTexImage")

    image = bpy.data.images.get(os.path.basename(path))
    if image is None:
        image = bpy.data.images.load(path)
    else:
        image.filepath = path
        image.reload()
    texture.image = image
    texture.interpolation = "Closest"

    tree.links.new(texture.outputs["Color"], shader.inputs["Color"])
    tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


# -------------------------------------------------------------- measurement

def density_of(mesh, sheet=2048):
    """Pixels per metre for every polygon of `mesh`, or None without UVs."""
    if not mesh.uv_layers:
        return None
    uvs = mesh.uv_layers[0].data
    out = []
    for polygon in mesh.polygons:
        loops = [uvs[i].uv for i in polygon.loop_indices]
        area = 0.0
        for i, uv in enumerate(loops):
            nxt = loops[(i + 1) % len(loops)]
            area += uv.x * nxt.y - nxt.x * uv.y
        area = abs(area) * 0.5
        if area <= 0.0 or polygon.area <= 0.0:
            out.append(0.0)
        else:
            out.append(math.sqrt(area) * sheet / math.sqrt(polygon.area))
    return out


def report(mesh_names, sheet=2048, density=128, hidden=None, tolerance=0.01):
    """Print min, max and the worst miss per mesh.

    `hidden` is a second density that also counts as right, for faces mapped
    lower on purpose because nobody sees them. `tolerance` is a share of the
    target. Returns True when every mesh is within it.
    """
    targets = [density] + ([hidden] if hidden else [])
    all_ok = True
    for name in mesh_names:
        mesh = bpy.data.meshes.get(name)
        if mesh is None:
            print("%-24s MISSING (look it up by obj.data.name)" % name)
            all_ok = False
            continue
        values = density_of(mesh, sheet)
        alive = [v for v in (values or []) if v > 0.0]
        if not alive:
            print("%-24s no UVs" % name)
            all_ok = False
            continue

        worst = max(min(abs(v - t) for t in targets) for v in alive)
        low = sum(1 for v in alive if hidden and abs(v - hidden) <= hidden * tolerance)
        ok = worst <= density * tolerance
        all_ok &= ok
        print("%-24s faces %4d  min %7.1f  max %7.1f  at hidden %3d  off by %6.1f  %s" % (
            name, len(alive), min(alive), max(alive), low, worst, "ok" if ok else "OFF"))
    return all_ok

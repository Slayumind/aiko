"""Check a .glb before a web page tries to show it.

Plain Python 3.8+, no Blender and no dependencies:

    python check_glb.py model.glb
    python check_glb.py model.glb --max-mb 5 --allow EXT_meshopt_compression

Why bother. Because of the one failure that is silent: model-viewer and three.js answer a
required extension they cannot decode by drawing nothing at all - no console error, no
broken image, just an empty box. This script says so before anyone uploads the file.

If the site that shows the model checks uploads itself, copy its limits here with the
options rather than guessing.
"""

import argparse
import json
import os
import struct
import sys

# Extensions that model-viewer and three.js decode once the page gives them the decoders
# (Draco, KTX2/Basis). EXT_meshopt_compression is left out on purpose: it needs a separate
# decoder many pages do not load. Allow it with --allow when yours does.
SUPPORTED = (
    "KHR_draco_mesh_compression",
    "KHR_texture_basisu",
    "EXT_texture_webp",
    "KHR_texture_transform",
    "KHR_mesh_quantization",
    "KHR_lights_punctual",
    "KHR_materials_unlit",
    "KHR_materials_emissive_strength",
    "KHR_materials_ior",
    "KHR_materials_specular",
    "KHR_materials_clearcoat",
    "KHR_materials_sheen",
    "KHR_materials_transmission",
    "KHR_materials_volume",
    "KHR_materials_iridescence",
    "KHR_materials_anisotropy",
    "KHR_materials_variants",
    "KHR_materials_dispersion",
)

# A sane default for one model on a web page. Change it with --max-mb.
MAX_MB = 10.0

GLB_MAGIC = 0x46546C67       # "glTF", little endian
GLB_CHUNK_JSON = 0x4E4F534A  # "JSON"
HEADER_BYTES = 12
CHUNK_HEADER_BYTES = 8

# Budgets that keep a phone happy. A file over them still loads; it is only a warning.
TRIANGLE_BUDGET = 150_000
MATERIAL_BUDGET = 4


def read_json_chunk(blob):
    """The glTF JSON out of a .glb, or a reason it is not one."""
    if len(blob) < HEADER_BYTES + CHUNK_HEADER_BYTES:
        return None, "shorter than a glTF header"

    magic, version, declared = struct.unpack_from("<III", blob, 0)
    if magic != GLB_MAGIC:
        return None, "does not start with the glTF magic - this is not a .glb"
    if version != 2:
        return None, f"is glTF version {version}, and web players read 2"

    # Declared length may be shorter than the file (some tools pad the tail) but never
    # longer: that means the upload was truncated.
    if declared > len(blob):
        return None, f"claims {declared} bytes but holds {len(blob)} - truncated"

    length, kind = struct.unpack_from("<II", blob, HEADER_BYTES)
    start = HEADER_BYTES + CHUNK_HEADER_BYTES
    if kind != GLB_CHUNK_JSON:
        return None, "first chunk is not JSON"
    if length == 0 or start + length > len(blob):
        return None, "JSON chunk runs past the end of the file"

    try:
        gltf = json.loads(blob[start:start + length].decode("utf-8"))
    except (UnicodeDecodeError, ValueError) as bad:
        return None, f"JSON chunk does not parse: {bad}"

    if not isinstance(gltf, dict):
        return None, "JSON chunk is not an object"

    return gltf, None


def triangles_in(gltf):
    """Triangles across every primitive, counted from the accessors."""
    accessors = gltf.get("accessors") or []
    total = 0
    for mesh in gltf.get("meshes") or []:
        for primitive in mesh.get("primitives") or []:
            # Mode 4 is TRIANGLES, and it is the default when the key is absent.
            if primitive.get("mode", 4) != 4:
                continue
            index = primitive.get("indices")
            if index is None:
                index = (primitive.get("attributes") or {}).get("POSITION")
            if index is None or index >= len(accessors):
                continue
            total += (accessors[index].get("count") or 0) // 3
    return total


def report(path, max_mb=MAX_MB, allowed=SUPPORTED, triangle_budget=TRIANGLE_BUDGET, material_budget=MATERIAL_BUDGET):
    size = os.path.getsize(path)
    max_bytes = int(max_mb * 1024 * 1024)
    with open(path, "rb") as handle:
        blob = handle.read()

    gltf, bad = read_json_chunk(blob)
    if gltf is None:
        print(f"REJECTED  {os.path.basename(path)} {bad}")
        return 1

    required = [n for n in gltf.get("extensionsRequired") or [] if isinstance(n, str)]
    used = [n for n in gltf.get("extensionsUsed") or [] if isinstance(n, str)]
    unsupported = [n for n in required if n not in allowed]

    meshes = len(gltf.get("meshes") or [])
    materials = len(gltf.get("materials") or [])
    images = len(gltf.get("images") or [])
    animations = [
        clip.get("name") or f"Animation {i + 1}"
        for i, clip in enumerate(gltf.get("animations") or [])
    ]
    triangles = triangles_in(gltf)

    print(f"file        {os.path.basename(path)}")
    print(f"size        {size:,} bytes ({size / 1024 / 1024:.2f} MB of {max_mb:g})")
    print(f"generator   {(gltf.get('asset') or {}).get('generator', '?')}")
    print(f"meshes      {meshes}")
    print(f"materials   {materials}")
    print(f"images      {images}")
    print(f"triangles   {triangles:,}")
    print(f"animations  {', '.join(animations) if animations else 'none'}")
    print(f"required    {', '.join(required) if required else 'none'}")
    print(f"used        {', '.join(used) if used else 'none'}")

    problems = []
    if size > max_bytes:
        problems.append(f"over the {max_mb:g} MB limit by {size - max_bytes:,} bytes")
    for name in unsupported:
        problems.append(f"needs {name}, which the player cannot decode")

    warnings = []
    if triangles > triangle_budget:
        warnings.append(f"{triangles:,} triangles is over the {triangle_budget:,} budget")
    if materials > material_budget:
        warnings.append(f"{materials} materials is over the {material_budget} budget")

    print()
    for warning in warnings:
        print(f"WARNING   {warning}")
    if problems:
        for problem in problems:
            print(f"REJECTED  {problem}")
        return 1

    print("OK        a web player can show this file")
    return 0


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    parser.add_argument("path", help="the .glb to check")
    parser.add_argument("--max-mb", type=float, default=MAX_MB, help=f"size limit, default {MAX_MB:g}")
    parser.add_argument("--allow", action="append", default=[], metavar="EXTENSION",
                        help="a required extension your player can decode on top of the default list")
    parser.add_argument("--triangles", type=int, default=TRIANGLE_BUDGET, help="triangle budget for a warning")
    parser.add_argument("--materials", type=int, default=MATERIAL_BUDGET, help="material budget for a warning")
    args = parser.parse_args(argv[1:])

    if not os.path.isfile(args.path):
        print(f"no such file: {args.path}")
        return 2
    return report(args.path, args.max_mb, SUPPORTED + tuple(args.allow), args.triangles, args.materials)


if __name__ == "__main__":
    sys.exit(main(sys.argv))

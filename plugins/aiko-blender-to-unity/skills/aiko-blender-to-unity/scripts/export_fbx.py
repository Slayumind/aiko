"""Export parts of a Blender model as FBX files for Unity.

Blender only - it imports bpy and bmesh. Import it from the project's own
script, which knows the part names, the cuts and the output folder:

    import importlib, sys
    sys.path.insert(0, r"<skill base directory>/scripts")
    import export_fbx
    importlib.reload(export_fbx)   # Blender keeps modules loaded between runs

    export_fbx.export_parts({
        "Wall": {"meshes": ["wall"], "cuts": [0.0]},
        "Column": {"meshes": ["column-base", "column-shaft"], "cuts": [150.0, -150.0]},
        "Floor": {"meshes": ["floor"]},
    }, out_dir=r"C:\\path\\to\\Unity\\Assets\\Models")

Each part is copied from its source meshes onto a temporary mesh, cut, turned
into the pose Unity expects, blended and written. The source model is never
modified, and the selection is put back at the end.

Always read mesh.bounds in Unity afterwards. A screenshot cannot tell a correct
part from a mirrored one.
"""

import importlib
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in dir() else None
if HERE and HERE not in sys.path:
    sys.path.insert(0, HERE)

import normals
importlib.reload(normals)

# The turn that takes a part from the pose it is modelled in to the pose the
# contract asks for. -90 degrees about Z suits a part modelled with its length
# along Y. Check the sign with mesh.bounds: +90 gives the same shape on the
# wrong side of the origin.
TURN_Z = -90.0


def turn_z(bm, degrees=TURN_Z):
    """Rotate every vertex about the Z axis through the origin."""
    angle = math.radians(degrees)
    cos, sin = math.cos(angle), math.sin(angle)
    for vert in bm.verts:
        x, y = vert.co.x, vert.co.y
        vert.co.x, vert.co.y = x * cos - y * sin, x * sin + y * cos


def bisect(bm, degrees):
    """Drop everything on the far side of a vertical plane through the origin.

    `degrees` is the direction of the plane's normal in the XY plane; the side
    it points to is removed. No fill: a cut plane is hidden by the part that
    mates with it, and a face there is both wasted polygons and a depth fight.
    """
    angle = math.radians(degrees)
    bmesh.ops.bisect_plane(
        bm,
        geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        plane_co=(0.0, 0.0, 0.0),
        plane_no=(math.cos(angle), math.sin(angle), 0.0),
        clear_outer=True,
        clear_inner=False,
    )


def unity_of(point):
    """Where a vertex of the exported mesh lands in Unity, as (x, y, z).

    With the settings in write_fbx, Blender (x, y, z) arrives at Unity
    (-x, z, -y): the axes swap for Y-up and the exporter flips one axis for
    handedness. Check it once against a measured vertex in your project, then
    use only this function for anything computed from coordinates.
    """
    return -point[0], point[2], -point[1]


def part_mesh(source_names, cuts=(), turn=TURN_Z):
    """A temporary mesh from one or more source meshes, cut and turned."""
    bm = bmesh.new()
    for name in source_names:
        bm.from_mesh(bpy.data.meshes[name])
    for degrees in cuts:
        bisect(bm, degrees)
    turn_z(bm, turn)
    mesh = bpy.data.meshes.new("export")
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def write_fbx(mesh, path, share=normals.SHARE):
    """Write one mesh as FBX, with the blend of normals set on it here.

    The normals go on this mesh, not on the source: the part went through
    bmesh, and bmesh drops custom normals.
    """
    if share is not None:
        normals.blend(mesh, share)

    part = bpy.data.objects.new(os.path.splitext(os.path.basename(path))[0], mesh)
    bpy.context.scene.collection.objects.link(part)
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    part.select_set(True)
    bpy.context.view_layer.objects.active = part

    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        object_types={"MESH"},
        use_mesh_modifiers=False,
        mesh_smooth_type="EDGE",
        use_triangles=False,
        add_leaf_bones=False,
        path_mode="COPY",
    )

    tilt = normals.widest_tilt(mesh) if share is not None else 0.0
    print("%-24s verts %5d polys %5d  widest tilt %.2f deg" % (
        os.path.basename(path), len(mesh.vertices), len(mesh.polygons), tilt))

    # Through the object, never by name: the name may belong to another mesh.
    bpy.data.objects.remove(part)
    bpy.data.meshes.remove(mesh)


def export_parts(parts, out_dir, turn=TURN_Z, share=normals.SHARE):
    """Export a dictionary of parts: file name -> {"meshes": [...], "cuts": [...]}."""
    try:
        was_selected = list(bpy.context.selected_objects)
    except AttributeError:
        # Right after opening a file there is no selection to read.
        was_selected = []
    was_active = bpy.context.view_layer.objects.active

    try:
        for name, part in parts.items():
            mesh = part_mesh(part["meshes"], part.get("cuts", ()), turn)
            write_fbx(mesh, os.path.join(out_dir, name + ".fbx"), share)
    finally:
        for obj in bpy.context.scene.objects:
            obj.select_set(obj in was_selected)
        bpy.context.view_layer.objects.active = was_active

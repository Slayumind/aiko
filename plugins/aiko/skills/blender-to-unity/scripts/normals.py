"""Soften the shading a little, without melting the shape.

Every corner of every face gets a normal that is mostly the face's own and
partly the average of the faces around that vertex:

    normal = (1 - share) * face normal  +  share * vertex normal

At 0 that is flat shading, at 1 it is plain Shade Smooth. At 0.10 a normal
leans about four degrees off its face on a right angle, a little more where
three faces meet: enough to take the cardboard edge off a plane, not enough to
blur it into its neighbour.

These are custom normals, and bmesh does not carry them. Set them on the mesh
that actually gets written, after every bmesh step.

Blender only - it imports bpy.
"""

import math

import bpy

SHARE = 0.10


def blend(mesh, share=SHARE):
    """Write custom normals on `mesh`: flat, pulled `share` of the way to smooth."""
    polygons = mesh.polygons
    polygons.foreach_set("use_smooth", [True] * len(polygons))
    sharp = mesh.attributes.get("sharp_edge")
    if sharp is not None:
        mesh.attributes.remove(sharp)
    mesh.update()

    vertex_normals = [v.normal.copy() for v in mesh.vertices]
    loops = mesh.loops
    corners = [None] * len(loops)
    for polygon in polygons:
        flat = polygon.normal
        for index in polygon.loop_indices:
            smooth = vertex_normals[loops[index].vertex_index]
            corners[index] = (flat * (1.0 - share) + smooth * share).normalized()

    mesh.normals_split_custom_set(corners)
    mesh.update()
    return corners


def widest_tilt(mesh):
    """The largest angle, in degrees, between a corner normal and its face."""
    corner = mesh.corner_normals
    worst = 0.0
    for polygon in mesh.polygons:
        for index in polygon.loop_indices:
            cosine = max(-1.0, min(1.0, corner[index].vector.dot(polygon.normal)))
            worst = max(worst, math.degrees(math.acos(cosine)))
    return worst

"""Tests for check_glb.py. Run from this folder: python -m unittest"""

import contextlib
import io
import json
import os
import struct
import tempfile
import unittest

import check_glb


def glb(gltf, declared_extra=0):
    """A minimal .glb with the given JSON and no binary chunk."""
    text = json.dumps(gltf).encode("utf-8")
    text += b" " * (-len(text) % 4)
    body = struct.pack("<II", len(text), check_glb.GLB_CHUNK_JSON) + text
    header = struct.pack("<III", check_glb.GLB_MAGIC, 2, 12 + len(body) + declared_extra)
    return header + body


def triangle_mesh(count):
    return {
        "asset": {"version": "2.0", "generator": "test"},
        "accessors": [{"count": count * 3}],
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}],
    }


class CheckGlbTests(unittest.TestCase):
    def run_on(self, blob, *args):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "model.glb")
            with open(path, "wb") as handle:
                handle.write(blob)
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = check_glb.main(["check_glb.py", path, *args])
            return code, out.getvalue()

    def test_a_plain_model_passes_and_counts_triangles(self):
        code, out = self.run_on(glb(triangle_mesh(12)))
        self.assertEqual(0, code)
        self.assertIn("triangles   12", out)
        self.assertIn("OK", out)

    def test_a_required_extension_no_player_decodes_is_rejected(self):
        model = triangle_mesh(1) | {"extensionsRequired": ["EXT_meshopt_compression"]}
        code, out = self.run_on(glb(model))
        self.assertEqual(1, code)
        self.assertIn("EXT_meshopt_compression", out)

    def test_an_extension_can_be_allowed_for_a_player_that_decodes_it(self):
        model = triangle_mesh(1) | {"extensionsRequired": ["EXT_meshopt_compression"]}
        code, _ = self.run_on(glb(model), "--allow", "EXT_meshopt_compression")
        self.assertEqual(0, code)

    def test_used_but_not_required_extensions_are_fine(self):
        model = triangle_mesh(1) | {"extensionsUsed": ["EXT_meshopt_compression"]}
        code, _ = self.run_on(glb(model))
        self.assertEqual(0, code)

    def test_the_size_limit_is_an_option(self):
        code, out = self.run_on(glb(triangle_mesh(1)), "--max-mb", "0.0001")
        self.assertEqual(1, code)
        self.assertIn("over the 0.0001 MB limit", out)

    def test_a_file_that_is_not_a_glb_or_is_cut_short_is_rejected(self):
        self.assertEqual(1, self.run_on(b"not a glb at all, just text")[0])
        self.assertEqual(1, self.run_on(glb(triangle_mesh(1), declared_extra=100))[0])

    def test_budgets_warn_but_do_not_reject(self):
        code, out = self.run_on(glb(triangle_mesh(20)), "--triangles", "10")
        self.assertEqual(0, code)
        self.assertIn("WARNING", out)


if __name__ == "__main__":
    unittest.main()

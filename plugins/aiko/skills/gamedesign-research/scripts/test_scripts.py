"""Tests for the offline parts of the scripts. Run from this folder: python -m unittest"""

import io
import tempfile
import unittest
from pathlib import Path

import fetch_image
import wiki_fetch

try:
    import PIL  # noqa: F401

    HAVE_PILLOW = True
except ImportError:
    HAVE_PILLOW = False


class ReadLimitedTests(unittest.TestCase):
    def test_a_body_under_the_limit_is_read_whole(self):
        self.assertEqual(b"x" * 100_000, fetch_image.read_limited(io.BytesIO(b"x" * 100_000), limit=200_000))

    def test_a_body_over_the_limit_is_refused_before_it_is_all_in_memory(self):
        with self.assertRaises(fetch_image.TooBig):
            fetch_image.read_limited(io.BytesIO(b"x" * 300_000), limit=200_000)


class ArgumentTests(unittest.TestCase):
    def test_something_that_is_not_a_url_is_refused_without_a_request(self):
        self.assertEqual(1, fetch_image.main(["file:///etc/passwd", "out.png"]))

    def test_both_scripts_say_who_is_asking(self):
        for agent in (fetch_image.USER_AGENT, wiki_fetch.USER_AGENT):
            self.assertIn("github.com/Slayumind/aiko", agent)


@unittest.skipUnless(HAVE_PILLOW, "Pillow is not installed")
class ImageTests(unittest.TestCase):
    def test_an_html_page_is_not_an_image(self):
        self.assertIsNone(fetch_image.open_image(b"<!doctype html><title>Just a moment...</title>"))

    def test_a_wide_image_gets_a_jpeg_web_copy_1200_px_wide(self):
        from PIL import Image

        buffer = io.BytesIO()
        Image.new("RGBA", (2400, 600), (10, 200, 125, 255)).save(buffer, "PNG")
        image = fetch_image.open_image(buffer.getvalue())

        with tempfile.TemporaryDirectory() as folder:
            shot = fetch_image.web_copy(image, Path(folder), "wide")
            with Image.open(shot) as saved:
                self.assertEqual((1200, 300), saved.size)
                self.assertEqual("JPEG", saved.format)


if __name__ == "__main__":
    unittest.main()

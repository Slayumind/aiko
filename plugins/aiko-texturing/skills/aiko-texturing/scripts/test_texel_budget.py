"""Tests for texel_budget.py. Run from this folder: python -m unittest"""

import contextlib
import io
import unittest

import texel_budget


class TexelBudgetTests(unittest.TestCase):
    def test_the_middle_of_the_screen_is_focal_length_over_distance(self):
        numbers = texel_budget.budget(tilt=60, vfov=30, distance=27)
        self.assertAlmostEqual(74.64, numbers["centre"], places=2)

    def test_the_bottom_edge_is_closer_and_sharper_than_the_top(self):
        numbers = texel_budget.budget(tilt=60, vfov=30, distance=27)
        self.assertAlmostEqual(86.19, numbers["near"], places=2)
        self.assertAlmostEqual(63.09, numbers["far"], places=2)
        self.assertEqual(128, numbers["power_of_two"])

    def test_a_camera_looking_straight_down_sees_the_same_depth_in_the_middle(self):
        numbers = texel_budget.budget(tilt=90, vfov=30, distance=10, height=1080)
        self.assertAlmostEqual(1080 / 2 / 0.26795 / 10, numbers["centre"], places=1)
        self.assertAlmostEqual(0.0, numbers["wall_squash"], places=6)

    def test_a_ray_above_the_horizon_has_no_ground(self):
        self.assertIsNone(texel_budget.ground_density(tilt=10, vfov=40, distance=5, height=1080, offset=-20))

    def test_a_bigger_screen_means_more_pixels_per_metre(self):
        small = texel_budget.budget(60, 30, 27, height=1080)["centre"]
        large = texel_budget.budget(60, 30, 27, height=2160)["centre"]
        self.assertAlmostEqual(2 * small, large, places=6)

    def test_bad_arguments_exit_with_2(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(2, texel_budget.main(["x", "--tilt", "0", "--vfov", "30", "--distance", "27"]))
            self.assertEqual(0, texel_budget.main(["x", "--tilt", "60", "--vfov", "30", "--distance", "27"]))


if __name__ == "__main__":
    unittest.main()

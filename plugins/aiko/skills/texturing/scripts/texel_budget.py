"""How many pixels per metre a surface can ever show, from the camera.

Plain Python 3.8+, no dependencies:

    python texel_budget.py --tilt 60 --vfov 30 --distance 27
    python texel_budget.py --tilt 60 --vfov 30 --distance 27 --width 2560 --height 1440

The camera looks down at `tilt` degrees below the horizon, from `distance`
metres to the ground point in the middle of the screen, with a vertical field
of view of `vfov` degrees. A pinhole camera shows a point at depth d (along the
view direction) with f / d pixels per metre across the screen, where
f = (height / 2) / tan(vfov / 2). The closest ground the camera sees is at the
bottom edge of the frame, so that is where a texture can look sharpest.

A surface seen at a slant loses part of its size on screen along one axis:
ground by sin(tilt), a vertical wall by cos(tilt). Across the screen both keep
the full density, so the budget for a texture is the larger of the two numbers.
"""

import argparse
import math
import sys


def focal_pixels(height, vfov):
    return (height / 2.0) / math.tan(math.radians(vfov / 2.0))


def ground_density(tilt, vfov, distance, height, offset):
    """Pixels per metre across the screen for a ground point.

    `offset` is the angle of the ray from the middle of the screen, in degrees,
    positive towards the bottom edge. Returns None when the ray misses the
    ground (it points at or above the horizon).
    """
    below = tilt + offset
    if below <= 0.0:
        return None
    camera_height = distance * math.sin(math.radians(tilt))
    along_ray = camera_height / math.sin(math.radians(below))
    depth = along_ray * math.cos(math.radians(offset))
    return focal_pixels(height, vfov) / depth


def budget(tilt, vfov, distance, width=1920, height=1080):
    """The numbers a texel density choice rests on, as a dictionary."""
    half = vfov / 2.0
    centre = ground_density(tilt, vfov, distance, height, 0.0)
    near = ground_density(tilt, vfov, distance, height, half)
    far = ground_density(tilt, vfov, distance, height, -half)
    return {
        "centre": centre,
        "near": near,
        "far": far,
        "ground_squash": math.sin(math.radians(tilt)),
        "wall_squash": math.cos(math.radians(tilt)),
        "power_of_two": 2 ** math.ceil(math.log2(near)) if near else None,
    }


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    parser.add_argument("--tilt", type=float, required=True, help="degrees below the horizon")
    parser.add_argument("--vfov", type=float, required=True, help="vertical field of view, degrees")
    parser.add_argument("--distance", type=float, required=True, help="metres to the ground point in the middle")
    parser.add_argument("--width", type=int, default=1920, help="screen width in pixels")
    parser.add_argument("--height", type=int, default=1080, help="screen height in pixels")
    args = parser.parse_args(argv[1:])

    if not 0.0 < args.tilt <= 90.0 or not 0.0 < args.vfov < 180.0 or args.distance <= 0.0:
        print("tilt must be in (0, 90], vfov in (0, 180), distance above 0")
        return 2

    numbers = budget(args.tilt, args.vfov, args.distance, args.width, args.height)
    print(f"centre of the screen   {numbers['centre']:.1f} px/m")
    print(f"near edge (sharpest)   {numbers['near']:.1f} px/m")
    if numbers["far"] is None:
        print("far edge               above the horizon")
    else:
        print(f"far edge               {numbers['far']:.1f} px/m")
    print(f"ground is squashed to  {numbers['ground_squash']:.2f} along the screen's height")
    print(f"walls are squashed to  {numbers['wall_squash']:.2f} along the screen's height")
    print(f"a power of two that covers the near edge: {numbers['power_of_two']} px/m")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

#!/usr/bin/env python3
"""Download one image for a design review and check that it really is an image.

Usage:
    python fetch_image.py URL OUT [--referer PAGE_URL] [--shots SHOTS_DIR]

OUT is the original, for example refs/dragon-age-1.png.
With --shots, a web copy is also written to SHOTS_DIR/<name>.jpg:
RGB JPEG, at most 1200 px wide, quality 84, progressive.

Needs Python 3.10+ and Pillow (pip install pillow). Exits with 1 when the answer
is not an image (for example an HTML error or a bot check page) or is bigger
than 20 MB. Take another source then.
"""

import argparse
import io
import sys
import urllib.request
from pathlib import Path

USER_AGENT = "aiko-gamedesign-research/1.0 (design research; https://github.com/Slayumind/aiko)"
MAX_WIDTH = 1200
MAX_BYTES = 20 * 1024 * 1024


class TooBig(Exception):
    pass


def read_limited(stream, limit: int = MAX_BYTES) -> bytes:
    """Read a response body, refusing to hold more than `limit` bytes."""
    chunks = []
    total = 0
    while True:
        chunk = stream.read(64 * 1024)
        if not chunk:
            return b"".join(chunks)
        total += len(chunk)
        if total > limit:
            raise TooBig(f"more than {limit // (1024 * 1024)} MB")
        chunks.append(chunk)


def download(url: str, referer: str | None) -> bytes:
    headers = {"User-Agent": USER_AGENT, "Accept": "image/avif,image/webp,image/png,image/jpeg,*/*"}
    if referer:
        headers["Referer"] = referer
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as answer:
        declared = answer.headers.get("Content-Length")
        if declared and declared.isdigit() and int(declared) > MAX_BYTES:
            raise TooBig(f"{int(declared) // (1024 * 1024)} MB declared")
        return read_limited(answer)


def open_image(data: bytes):
    """A loaded Pillow image, or None when the bytes are not an image."""
    from PIL import Image

    try:
        image = Image.open(io.BytesIO(data))
        image.load()
        return image
    except Exception:
        return None


def web_copy(image, shots: Path, stem: str) -> Path:
    from PIL import Image

    web = image.convert("RGB")
    if web.width > MAX_WIDTH:
        web = web.resize((MAX_WIDTH, round(web.height * MAX_WIDTH / web.width)), Image.LANCZOS)
    shot = shots / (stem + ".jpg")
    shot.parent.mkdir(parents=True, exist_ok=True)
    web.save(shot, "JPEG", quality=84, progressive=True, optimize=True)
    return shot


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("url")
    parser.add_argument("out")
    parser.add_argument("--referer")
    parser.add_argument("--shots")
    args = parser.parse_args(argv)

    if not args.url.startswith(("http://", "https://")):
        print(f"NOT A URL: {args.url!r}", file=sys.stderr)
        return 1
    try:
        import PIL  # noqa: F401
    except ImportError:
        print("Pillow is missing: pip install pillow", file=sys.stderr)
        return 1

    try:
        data = download(args.url, args.referer)
    except TooBig as big:
        print(f"TOO BIG: {args.url} is {big}", file=sys.stderr)
        return 1

    image = open_image(data)
    if image is None:
        print(f"NOT AN IMAGE: {args.url} starts with {data[:80]!r}", file=sys.stderr)
        return 1

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(data)
    print(f"saved {out} {image.width}x{image.height}")

    if args.shots:
        shot = web_copy(image, Path(args.shots), out.stem)
        print(f"shot  {shot} {image.width}x{image.height} -> {shot.stat().st_size // 1024} KB")
    return 0


if __name__ == "__main__":
    sys.exit(main())

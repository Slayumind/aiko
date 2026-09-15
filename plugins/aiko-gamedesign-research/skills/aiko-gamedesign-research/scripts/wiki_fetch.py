#!/usr/bin/env python3
"""Read a MediaWiki page (Fandom, wiki.gg, official wikis) through the site API.

Usage:
    python wiki_fetch.py page HOST "Page title" OUT.txt     page text and the list of its images
    python wiki_fetch.py files HOST "File1.png" "File2.jpg"  full URLs and sizes of image files

HOST is the wiki host, for example dragonage.fandom.com. Some wikis keep the API at /w/api.php;
pass --api /w/api.php then. In Git Bash on Windows set MSYS_NO_PATHCONV=1 first, or Git Bash
turns /w/api.php into a Windows path. Needs Python 3.8+ and only the standard library.
"""

import argparse
import json
import sys
import urllib.parse
import urllib.request
from pathlib import Path

USER_AGENT = "aiko-gamedesign-research/1.0 (design research; https://github.com/Slayumind/aiko)"


def call(host: str, api: str, params: dict) -> dict:
    url = f"https://{host}{api}?{urllib.parse.urlencode({**params, 'format': 'json'})}"
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": USER_AGENT}), timeout=30) as answer:
        return json.load(answer)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["page", "files"])
    parser.add_argument("host")
    parser.add_argument("items", nargs="+")
    parser.add_argument("--api", default="/api.php")
    args = parser.parse_args()

    if args.mode == "page":
        if len(args.items) != 2:
            parser.error('page mode needs "Page title" and OUT.txt')
        title, out = args.items
        data = call(args.host, args.api, {"action": "parse", "page": title, "prop": "wikitext|images", "redirects": 1})
        if "error" in data:
            print(data["error"].get("info", data["error"]), file=sys.stderr)
            return 1
        text = data["parse"]["wikitext"]["*"]
        images = data["parse"].get("images", [])
        path = Path(out)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text + "\n\nIMAGES: " + ", ".join(images), encoding="utf-8")
        print(f"saved {out}: {len(text)} characters, {len(images)} images")
        return 0

    titles = "|".join("File:" + name for name in args.items)
    data = call(args.host, args.api, {"action": "query", "titles": titles, "prop": "imageinfo", "iiprop": "url|size"})
    for page in data["query"]["pages"].values():
        info = (page.get("imageinfo") or [{}])[0]
        print(page["title"], info.get("url"), info.get("width"), info.get("height"))
    return 0


if __name__ == "__main__":
    sys.exit(main())

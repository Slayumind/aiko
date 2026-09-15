# Images

## Pipeline

1. Download the original to `refs/<slug>-<n>.<ext>` with `scripts/fetch_image.py`. The script
   checks that the answer is really an image and not an HTML error page.
2. Open the file and look at it. Keep it only if it shows the mechanic's interface.
3. Re-encode into `shots/<slug>-<n>.jpg`: RGB JPEG, at most 1200 px wide, quality about 84,
   progressive. `scripts/fetch_image.py --shots` does both steps.
4. Put width and height on every `<img>`, `loading="lazy"`, real alt text, a caption.
5. Ship `shots/` as files next to the page (artifact files, or a folder), never as base64 inside
   the page and never hot-linked: hosts block hot links and pages grow too large with base64.
   The script refuses answers over 20 MB.

Budget: about 30-40 images and 3-5 MB for a tier-1 set.

## Sources that usually work

| Source | How |
|---|---|
| Steam store screenshots | the store page lists full-size image URLs; no login needed |
| MediaWiki wikis (Fandom, wiki.gg, official wikis) | `scripts/wiki_fetch.py` lists page images and their file URLs; Fandom's image host needs a `Referer` header with the wiki page URL |
| Wikimedia Commons | file pages carry licence information — prefer these when a licence matters |
| Developer blogs and dev diaries | often the best interface shots, with the designer's own explanation |
| Papers and official manuals (PDF) | render the page and crop the figure |
| Article sites | fine for a single image; credit the article in the caption |

If no image exists, render the source page in a headless browser and crop the interface. If that
fails too, say so in the hand-off and do not draw a fake screenshot.

## Do not

- Do not use sites whose terms forbid AI use or scraping (for example gameuidatabase.com).
- Do not log in, pass age or family-view gates, or work around bot checks. Take another source.
- Do not use box art, key art or logos in place of the mechanic.

## Rights

Screenshots belong to their owners. Embed them for private study, keep the artifact private,
and name the source of every image. Say this to the user once before publishing.

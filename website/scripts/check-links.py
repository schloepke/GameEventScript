#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Check the generated site's internal links and anchors without network access."""

from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urljoin, urlsplit
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts'))
from website_assets import remove_metadata, validate_static_assets

DIST = ROOT / "artifacts/website/dist"
ORIGIN = "https://gameeventscript.org"


class Page(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.ids = set()
        self.links = []
        self.feed(source)

    def handle_starttag(self, tag, attributes):
        attrs = dict(attributes)
        # Canonical metadata is not a navigation link (notably on 404 pages).
        if tag == "link" and attrs.get("rel") == "canonical":
            return
        if "id" in attrs:
            self.ids.add(attrs["id"])
        for attribute in ("href", "src"):
            if attrs.get(attribute):
                self.links.append(attrs[attribute])


if '--clean-metadata' in sys.argv:
    # Finder may create new metadata while Astro generates the output.
    remove_metadata(DIST)
validate_static_assets(DIST)
pages = {path: Page(path.read_text()) for path in DIST.rglob("*.html")}
if not pages:
    raise SystemExit("No built HTML pages found.")
failures = []
brand_files = {path.relative_to(DIST / 'brand').as_posix() for path in (DIST / 'brand').rglob('*') if path.is_file()}
if brand_files != {'logo.svg'}:
    failures.append(f"Public brand assets must contain only logo.svg: {sorted(brand_files)}")
for path in DIST.rglob('*'):
    if path.is_file() and (path.name == 'GameEventScript-Brand.zip' or path.name == 'brand-board.svg'):
        failures.append(f"Private brand export in public output: {path.relative_to(DIST)}")
for path, page in pages.items():
    source = "/" + path.relative_to(DIST).as_posix()
    for href in page.links:
        url = urlsplit(urljoin(ORIGIN + source, href))
        if url.scheme not in ("http", "https") or url.netloc != "gameeventscript.org":
            continue
        target = DIST / unquote(url.path).lstrip("/")
        if target.is_dir():
            target /= "index.html"
        if not target.is_file():
            failures.append(f"{source}: missing {href}")
        elif url.fragment and target in pages and unquote(url.fragment) not in pages[target].ids:
            failures.append(f"{source}: missing anchor {href}")
if failures:
    raise SystemExit("\n".join(sorted(set(failures))))
print(f"Internal links, assets and anchors verified in {len(pages)} HTML pages.")

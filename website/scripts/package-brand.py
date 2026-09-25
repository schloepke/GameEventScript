# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Export the Pixel-Duo brand from its SVG master and package the assets."""

from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import argparse
import xml.etree.ElementTree as ET
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts'))
from website_assets import is_metadata

SOURCE = ROOT / "website/brand/pixel-duo"
OUTPUT = ROOT / "artifacts/website/brand/pixel-duo"
NS = "http://www.w3.org/2000/svg"
ET.register_namespace("", NS)
HEADER = "<!-- Copyright 2026 Stephan Schlöpke -->\n<!-- SPDX-License-Identifier: Apache-2.0 -->\n"


def element(name, attributes=None):
    return ET.Element(f"{{{NS}}}{name}", attributes or {})


def serialize(root):
    return ET.tostring(root, encoding="unicode")


def small_variant(master):
    result = deepcopy(master)
    for group in result.iter():
        for child in list(group):
            if child.get("data-detail") == "particles":
                group.remove(child)
    return result


def mono_variant(master, colour):
    result = deepcopy(master)
    for child in list(result):
        if child.tag == f"{{{NS}}}defs":
            result.remove(child)
    for node in result.iter():
        if node.get("fill", "").startswith("url("):
            node.set("fill", colour)
    return result


def tile_variant(master):
    result = element("svg", {"viewBox": "0 0 64 64", "role": "img", "aria-label": "Game Event Script"})
    result.append(element("rect", {"width": "64", "height": "64", "rx": "14", "fill": "#10203c"}))
    content = deepcopy(master)
    content.set("x", "4")
    content.set("y", "4")
    content.set("width", "56")
    content.set("height", "56")
    result.append(content)
    return result


def wordmark_variant(master, colour):
    result = element("svg", {"viewBox": "0 0 480 80", "role": "img", "aria-label": "Game Event Script"})
    content = deepcopy(master)
    content.set("x", "0")
    content.set("y", "8")
    content.set("width", "64")
    content.set("height", "64")
    result.append(content)
    text = element("text", {"x": "84", "y": "51", "fill": colour, "font-family": "system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif", "font-size": "32", "font-weight": "750", "letter-spacing": "-.8"})
    text.text = "Game Event Script"
    result.append(text)
    return result


def board_symbol(name, master):
    result = deepcopy(master)
    # Each embedded variant owns unique gradient/title identifiers.
    ids = {node.get("id"): f"{name}-{node.get('id')}" for node in result.iter() if node.get("id")}
    for node in result.iter():
        for attribute, value in list(node.attrib.items()):
            if attribute == "id":
                node.set(attribute, ids[value])
            elif attribute == "aria-labelledby":
                node.set(attribute, " ".join(ids.get(part, part) for part in value.split()))
            else:
                for old, new in ids.items():
                    value = value.replace(f"url(#{old})", f"url(#{new})")
                node.set(attribute, value)
    result.tag = f"{{{NS}}}g"
    result.attrib.clear()
    result.set("id", name)
    return serialize(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--site", action="store_true", help="Export only the logo and favicon used by the public website.")
    args = parser.parse_args()
    master = ET.parse(SOURCE / "mark.svg").getroot()
    small = small_variant(master)
    if args.site:
        public = ROOT / "artifacts/website/public"
        (public / "brand").mkdir(parents=True, exist_ok=True)
        (public / "brand/logo.svg").write_text(HEADER + serialize(master) + "\n")
        (public / "favicon-pixel-duo.svg").write_text(HEADER + serialize(tile_variant(small)) + "\n")
        print("Prepared public Pixel-Duo logo and favicon only.")
        return
    OUTPUT.mkdir(parents=True, exist_ok=True)
    variants = {
        "mark": master,
        "mark-small": small,
        "mark-mono": mono_variant(master, "#10203c"),
        "mark-inverse": mono_variant(master, "#ffffff"),
        "icon": tile_variant(master),
        "icon-small": tile_variant(small),
        "wordmark": wordmark_variant(master, "#10203c"),
        "wordmark-inverse": wordmark_variant(master, "#edf3fc"),
    }
    for name, variant in variants.items():
        (OUTPUT / f"{name}.svg").write_text(HEADER + serialize(variant) + "\n")
    definitions = "".join(board_symbol(name, variant) for name, variant in variants.items())
    board = (SOURCE / "board-template.svg").read_text().replace("<!-- BRAND_DEFINITIONS -->", definitions)
    ET.fromstring(board)
    (OUTPUT / "brand-board.svg").write_text(board)
    for filename in ("README.md", "tokens.css", "nuget-icon.png"):
        (OUTPUT / filename).write_bytes((SOURCE / filename).read_bytes())
    (OUTPUT / "LICENSE").write_bytes((ROOT / "LICENSE").read_bytes())
    with ZipFile(OUTPUT / "GameEventScript-Brand.zip", "w") as archive:
        for file in sorted(OUTPUT.iterdir()):
            if file.suffix == ".zip" or is_metadata(file.relative_to(OUTPUT)):
                continue
            info = ZipInfo(f"GameEventScript-Brand/{file.name}", (2026, 1, 1, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            info.compress_type = ZIP_DEFLATED
            archive.writestr(info, file.read_bytes())
    print("Prepared Pixel-Duo brand board, SVG variants, guide, tokens and ZIP.")


if __name__ == "__main__":
    main()

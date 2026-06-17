#!/usr/bin/env python3
"""
Extract Wedding Witch's image assets (Texture2D + Sprite) from the Unity build to PNG.

Wedding Witch is an IL2CPP build: assets live in the packed `data.unity3d` plus a set of
Addressable bundles under StreamingAssets/aa/. UnityPy decodes textures (incl. BC7/DXT atlases)
and crops Sprites out of their atlas straight to PIL images. We enumerate *every* asset file under
the game-data dir so nothing is missed. Output goes to a gitignored dir (regenerate any time).

Usage:  .venv/bin/python tools/extract_images.py [GAME_DATA_DIR] [--sprites|--textures] [--out DIR]
        default GAME_DATA_DIR = the installed Wedding Witch_Data ; default extracts both.
"""
import os
import sys

import UnityPy

DEFAULT_DATA = "/home/jpn/.steam/debian-installation/steamapps/common/Wedding Witch/Wedding Witch_Data"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def parse_args():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = [a for a in sys.argv[1:] if a.startswith("--")]
    data = args[0] if args else DEFAULT_DATA
    out = os.path.join(ROOT, "ref", "images")
    if "--out" in flags:
        i = sys.argv.index("--out")
        out = sys.argv[i + 1]
    want = {"Sprite", "Texture2D"}
    if "--sprites" in flags:
        want = {"Sprite"}
    if "--textures" in flags:
        want = {"Texture2D"}
    return data, out, want


def safe(name):
    return "".join(c if c.isalnum() or c in " ._-" else "_" for c in (name or "")).strip() or "unnamed"


def main():
    data, out, want = parse_args()
    counts, seen, skips = {"Sprite": 0, "Texture2D": 0}, {}, 0
    # Load the WHOLE dir as one env: covers data.unity3d + every Addressable bundle, and resolves
    # the .resource sidecars textures stream from.
    env = UnityPy.load(data)
    for o in env.objects:
        if o.type.name not in want:
            continue
        sub = "sprites" if o.type.name == "Sprite" else "textures"
        dst_dir = os.path.join(out, sub)
        name = "?"
        try:
            d = o.read()
            img = d.image
            name = safe(getattr(d, "m_Name", ""))
            key = (sub, name)
            seen[key] = seen.get(key, 0) + 1
            if seen[key] > 1:                       # disambiguate duplicate asset names
                name = f"{name}_{o.path_id}"
            os.makedirs(dst_dir, exist_ok=True)
            img.save(os.path.join(dst_dir, name + ".png"))
            counts[o.type.name] += 1
        except Exception as e:
            skips += 1
            print(f"  skip {o.type.name} {name}: {e}", file=sys.stderr)
    print(f"extracted {counts['Sprite']} sprites + {counts['Texture2D']} textures "
          f"({skips} skipped) -> {os.path.relpath(out, ROOT)}/")


if __name__ == "__main__":
    main()

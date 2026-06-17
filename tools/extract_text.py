#!/usr/bin/env python3
"""
Extract Wedding Witch's text: every TextAsset plus the Unity Localization string tables.

Wedding Witch is an IL2CPP build, so MonoBehaviours ship NO type tree and their fields can't be read
cleanly. As in the Fakutori dialogue extractor we fall back to the raw serialized bytes and pull out
the 4-byte-length-prefixed, 4-aligned UTF-8 strings.

Text systems found in this game (see ref/text/_README.txt for the full write-up):
  * Unity Localization (com.unity.localization): String Tables + Asset Tables, shipped as
    Addressable bundles under StreamingAssets/aa/. Each String Table MonoBehaviour is one table
    (e.g. Dialog_en, StartAdventure_en, LastBattle_en, Victory_en, UI_en, Achievement_en, ...) for
    one locale. 13 locales (en, ja, ko, zh-Hans, zh-TW, fr, de, es, it, pl, pt-BR, ru, tr).
  * A bespoke Dialog / DialogManager / DialogParser / DialogViewer / SpineDialogController set of
    MonoScripts drives the in-game dialogue UI, but the actual dialogue *strings* live in the
    localization String Tables above (the Dialog_<locale> / StartAdventure_<locale> / LastBattle_
    <locale> tables). TextMesh Pro + Febucci TextAnimator render it.

This dumps, per locale, each String Table as a readable .txt (m_Name header + the text values in
serialized order) and a combined localization.json keyed by locale -> table -> [values]. TextAssets
go to ref/text/textassets/ verbatim.

Usage:  .venv/bin/python tools/extract_text.py [GAME_DATA_DIR] [--out DIR]
"""
import json
import os
import struct
import sys

import UnityPy

DEFAULT_DATA = "/home/jpn/.steam/debian-installation/steamapps/common/Wedding Witch/Wedding Witch_Data"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# MonoScript class-name fragments that hint at a text / dialogue / localization system.
TEXT_CLASS_HINTS = ("local", "dialog", "yarn", "narrat", "story", "ink", "nanin",
                    "csv", "translat", "conversa", "subtitle", "stringtable")

# The 13 Unity Localization locale codes this build ships (a String Table is named <Table>_<code>).
LOCALES = {"en", "ja", "ko", "zh-Hans", "zh-TW", "fr", "de", "es", "it", "pl", "pt-BR", "ru", "tr"}


def parse_args():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = [a for a in sys.argv[1:] if a.startswith("--")]
    data = args[0] if args else DEFAULT_DATA
    out = os.path.join(ROOT, "ref", "text")
    if "--out" in flags:
        out = sys.argv[sys.argv.index("--out") + 1]
    return data, out


def safe(name):
    return "".join(c if c.isalnum() or c in " ._-" else "_" for c in (name or "")).strip() or "unnamed"


def length_prefixed_strings(b, maxlen=20000):
    """Yield every 4-byte-length-prefixed, 4-aligned UTF-8 string in serialized MonoBehaviour bytes."""
    out, i, n = [], 0, len(b)
    while i + 4 <= n:
        L = struct.unpack_from("<I", b, i)[0]
        if 0 <= L <= maxlen and i + 4 + L <= n:
            s = b[i + 4:i + 4 + L]
            try:
                t = s.decode("utf-8")
                if t == "" or all(ord(c) >= 32 or c in "\n\t\r" for c in t):
                    out.append(t)
                    i += 4 + ((L + 3) & ~3)
                    continue
            except UnicodeDecodeError:
                pass
        i += 1
    return out


def locale_of(table_name):
    """`Dialog_en` -> `en`, `UI_zh-Hans` -> `zh-Hans`. Falls back to 'misc'."""
    if "_" in table_name:
        return table_name.rsplit("_", 1)[1]
    return "misc"


def is_string_table(table_name, strings):
    """A Localization String Table is named <Table>_<locale> where <locale> is a known locale code,
    and carries real text values. This excludes the many other underscore-named MonoBehaviours in
    data.unity3d (SkeletonData, Atlas, Floor1, ...)."""
    return "_" in table_name and locale_of(table_name) in LOCALES and len(strings) > 2


def main():
    data, out = parse_args()
    text_dir = os.path.join(out, "textassets")
    loc_dir = os.path.join(out, "localization")
    os.makedirs(out, exist_ok=True)

    text_assets, loc = 0, {}            # loc[locale][table] = [values]
    text_seen = {}                      # dedupe duplicate TextAsset names
    class_hits = set()
    skips = 0

    # Load the WHOLE dir as one env: covers data.unity3d + every Addressable bundle (the localization
    # string tables live in the StreamingAssets/aa bundles).
    env = UnityPy.load(data)
    if True:
        for o in env.objects:
            tn = o.type.name
            if tn == "TextAsset":
                name = "?"
                try:
                    d = o.read()
                    name = safe(getattr(d, "m_Name", "")) or str(o.path_id)
                    script = d.m_Script
                    raw = script.encode("utf-8", "surrogateescape") if isinstance(script, str) else bytes(script)
                    os.makedirs(text_dir, exist_ok=True)
                    ext = ".txt"
                    head = raw[:64].lstrip()
                    if head[:1] == b"{" or head[:1] == b"[":
                        ext = ".json"
                    elif head[:5] == b"<?xml" or head[:1] == b"<":
                        ext = ".xml"
                    text_seen[name] = text_seen.get(name, 0) + 1
                    if text_seen[name] > 1:            # disambiguate duplicate asset names
                        name = f"{name}_{o.path_id}"
                    with open(os.path.join(text_dir, name + ext), "wb") as fh:
                        fh.write(raw)
                    text_assets += 1
                except Exception as e:
                    skips += 1
                    print(f"  skip TextAsset {name}: {e}", file=sys.stderr)
            elif tn == "MonoScript":
                try:
                    d = o.read()
                    cn = getattr(d, "m_ClassName", "") or ""
                    ns = getattr(d, "m_Namespace", "") or ""
                    full = f"{ns}.{cn}" if ns else cn
                    if any(h in full.lower() for h in TEXT_CLASS_HINTS):
                        class_hits.add(full)
                except Exception:
                    pass
            elif tn == "MonoBehaviour":
                try:
                    name = safe(getattr(o.read(), "m_Name", "")) or ""
                except Exception:
                    name = ""
                if not name:
                    continue
                raw = o.get_raw_data()
                ss = length_prefixed_strings(raw)
                if not is_string_table(name, ss):
                    continue
                loc_code = locale_of(name)
                # drop the table-name header and bare locale codes; keep the real text values
                values = [t for t in ss if t and t != name and t != loc_code]
                loc.setdefault(loc_code, {})[name] = values

    # write per-locale readable dumps
    for loc_code, tables in sorted(loc.items()):
        d = os.path.join(loc_dir, safe(loc_code))
        os.makedirs(d, exist_ok=True)
        for table, values in sorted(tables.items()):
            with open(os.path.join(d, safe(table) + ".txt"), "w") as fh:
                fh.write(f"# {table}  ({len(values)} strings, serialized order)\n\n")
                for v in values:
                    fh.write(v + "\n")

    # combined JSON (compact-ish but readable)
    if loc:
        with open(os.path.join(out, "localization.json"), "w") as fh:
            json.dump(loc, fh, ensure_ascii=False, indent=1)

    write_readme(out, loc, class_hits, text_assets)

    n_tables = sum(len(t) for t in loc.values())
    print(f"extracted {text_assets} TextAssets, {len(loc)} locales / {n_tables} string tables "
          f"({skips} skipped) -> {os.path.relpath(out, ROOT)}/")


def write_readme(out, loc, class_hits, text_assets):
    en_tables = sorted(loc.get("en", {}).keys())
    lines = []
    lines.append("Wedding Witch — text / localization systems")
    lines.append("=" * 44)
    lines.append("")
    lines.append("Build: Unity IL2CPP. MonoBehaviours ship no type tree, so fields were recovered")
    lines.append("from raw serialized bytes (4-byte length-prefixed, 4-aligned UTF-8 strings).")
    lines.append("")
    lines.append("PRIMARY TEXT SYSTEM: Unity Localization (com.unity.localization)")
    lines.append("-" * 60)
    lines.append("  Shipped as Addressable bundles under StreamingAssets/aa/StandaloneLinux64/ :")
    lines.append("    localization-string-tables-<language>(<code>)_assets_all.bundle   <- ALL UI + dialogue text")
    lines.append("    localization-asset-tables-<language>(<code>)_assets_all.bundle    <- localized sprites/fonts refs")
    lines.append("    localization-assets-<language>_assets_all.bundle                  <- localized fonts/materials")
    lines.append("    localization-locales_assets_all.bundle                            <- the 13 Locale definitions")
    lines.append("")
    lines.append(f"  Locales ({len(loc)}): " + ", ".join(sorted(loc.keys())))
    lines.append("")
    lines.append("  Each String Table is one MonoBehaviour named <Table>_<locale>. Tables (en):")
    for t in en_tables:
        n = len(loc["en"][t])
        lines.append(f"    {t:24s} {n:4d} strings")
    lines.append("")
    lines.append("DIALOGUE")
    lines.append("-" * 60)
    lines.append("  In-game dialogue is rendered by a bespoke MonoBehaviour set (no Yarn/Ink/Naninovel):")
    lines.append("    Dialog, DialogManager, DialogParser, DialogViewer, SpineDialogController")
    lines.append("    (Spine skeletal portraits, TextMesh Pro + Febucci TextAnimator for the typewriter FX).")
    lines.append("  The dialogue *strings* themselves live in the Localization String Tables above —")
    lines.append("  chiefly the Dialog_<locale>, StartAdventure_<locale>, LastBattle_<locale> and")
    lines.append("  Victory_<locale> tables. See localization/<locale>/*.txt and localization.json.")
    lines.append("")
    lines.append(f"  text-system MonoScript class names detected ({len(class_hits)}):")
    for c in sorted(class_hits):
        lines.append(f"    {c}")
    lines.append("")
    lines.append("OTHER")
    lines.append("-" * 60)
    lines.append(f"  TextAsset objects dumped verbatim to textassets/ : {text_assets}")
    lines.append("  Save data uses Easy Save 3 (ES3*) — runtime only, not shipped as assets.")
    lines.append("")
    with open(os.path.join(out, "_README.txt"), "w") as fh:
        fh.write("\n".join(lines))


if __name__ == "__main__":
    main()

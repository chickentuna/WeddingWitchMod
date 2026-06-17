#!/usr/bin/env python3
"""
Extract Wedding Witch's audio (Unity AudioClips) from the game build to FLAC (lossless).

Wedding Witch is an IL2CPP build: AudioClips live in `data.unity3d`, with their sample data streaming
from the sharedassets0.resource / sharedassets2.resource sidecars (FSB5 / Vorbis). We load the whole
game-data dir as ONE UnityPy environment so those .resource sidecars resolve as dependencies — load
data.unity3d on its own and the streamed samples come back empty. UnityPy decodes each clip to PCM
WAV via AudioClip.samples (a dict name->wav bytes); we transcode to FLAC with ffmpeg by default for a
lossless, ~half-size archive.

Music vs SFX: clip names here are not reliably tagged, so we split by name hints AND duration —
clips over ~20 s (or matching a music hint) go to music/, the rest to sfx/. Output is a gitignored
dir. Any clip UnityPy still fails to decode has its raw payload dumped to ref/audio/raw/ so nothing
is lost.

Usage:  .venv/bin/python tools/extract_audio.py [GAME_DATA_DIR] [--wav] [--out DIR]
  --wav   keep raw WAV, skip the FLAC transcode
  default GAME_DATA_DIR = the installed Wedding Witch_Data ; default format FLAC.
"""
import os
import struct
import subprocess
import sys

import UnityPy
from UnityPy.helpers.ResourceReader import get_resource_data

DEFAULT_DATA = "/home/jpn/.steam/debian-installation/steamapps/common/Wedding Witch/Wedding Witch_Data"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# A clip is "music" if it is long or its name hints at music/ambience; everything else is sfx.
MUSIC_HINTS = ("bgm", "music", "theme", "song", "loop", "ambient", "title", "menu",
               "stage", "boss", "ending", "ost", "track")
MUSIC_MIN_SECONDS = 20.0


def parse_args():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    flags = [a for a in sys.argv[1:] if a.startswith("--")]
    data = args[0] if args else DEFAULT_DATA
    out = os.path.join(ROOT, "ref", "audio")
    if "--out" in flags:
        out = sys.argv[sys.argv.index("--out") + 1]
    return data, out, ("--wav" not in flags)


def wav_seconds(wav_bytes):
    try:
        i, n = 12, len(wav_bytes)
        byte_rate, data_size = None, None
        while i + 8 <= n:
            cid = wav_bytes[i:i + 4]
            csz = struct.unpack_from("<I", wav_bytes, i + 4)[0]
            if cid == b"fmt ":
                byte_rate = struct.unpack_from("<I", wav_bytes, i + 8 + 8)[0]
            elif cid == b"data":
                data_size = csz
                break
            i += 8 + csz + (csz & 1)
        if byte_rate and data_size:
            return data_size / byte_rate
    except Exception:
        pass
    return 0.0


def is_music(name, seconds):
    n = name.lower()
    return seconds >= MUSIC_MIN_SECONDS or any(h in n for h in MUSIC_HINTS)


def raw_audio_bytes(clip):
    if getattr(clip, "m_AudioData", None):
        return bytes(clip.m_AudioData)
    r = getattr(clip, "m_Resource", None)
    if r is not None:
        return bytes(get_resource_data(r.m_Source, clip.object_reader.assets_file, r.m_Offset, r.m_Size))
    return b""


def transcode(wav_path):
    out = wav_path.rsplit(".", 1)[0] + ".flac"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path, out], check=True)
    os.remove(wav_path)


def main():
    data, out, do_flac = parse_args()
    counts, seen, raw_dumped, skips = {"music": 0, "sfx": 0}, {}, 0, 0

    # Load the WHOLE dir as one env so .resource sidecars resolve (covers data.unity3d + all bundles).
    env = UnityPy.load(data)

    for o in env.objects:
        if o.type.name != "AudioClip":
            continue
        name = "?"
        try:
            d = o.read()
            name = getattr(d, "m_Name", "?")
            samples = d.samples
        except Exception as e:
            # fallback: dump whatever raw payload we can so nothing is silently lost
            try:
                raw = raw_audio_bytes(d)
                if raw:
                    raw_dir = os.path.join(out, "raw")
                    os.makedirs(raw_dir, exist_ok=True)
                    with open(os.path.join(raw_dir, name + ".fsb"), "wb") as fh:
                        fh.write(raw)
                    raw_dumped += 1
                    print(f"  raw-dump AudioClip {name}: {e}", file=sys.stderr)
                else:
                    skips += 1
                    print(f"  skip AudioClip {name}: {e}", file=sys.stderr)
            except Exception as e2:
                skips += 1
                print(f"  skip AudioClip {name}: {e} / {e2}", file=sys.stderr)
            continue

        for sample_name, wav in samples.items():        # usually one sample per clip
            base = os.path.splitext(sample_name)[0]      # UnityPy keys carry a .wav suffix
            cat = "music" if is_music(base, wav_seconds(wav)) else "sfx"
            dst_dir = os.path.join(out, cat)
            os.makedirs(dst_dir, exist_ok=True)
            seen[base] = seen.get(base, 0) + 1
            fname = base if seen[base] == 1 else f"{base}_{o.path_id}"
            wav_path = os.path.join(dst_dir, fname + ".wav")
            try:
                with open(wav_path, "wb") as fh:
                    fh.write(wav)
                if do_flac:
                    transcode(wav_path)
                counts[cat] += 1
            except Exception as e:
                skips += 1
                print(f"  skip sample {base}: {e}", file=sys.stderr)

    fmt = "flac" if do_flac else "wav"
    extra = f", {raw_dumped} raw-dumped" if raw_dumped else ""
    print(f"extracted {counts['music']} music + {counts['sfx']} sfx ({fmt}{extra}, {skips} skipped) "
          f"-> {os.path.relpath(out, ROOT)}/")


if __name__ == "__main__":
    main()

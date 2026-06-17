# WeddingWitchMod

Reverse-engineering / modding workspace for the Unity game **Wedding Witch**.

Unlike a Mono Unity game (e.g. Fakutori), Wedding Witch is an **IL2CPP** build: the C# was
compiled to native code (`GameAssembly.so` + `il2cpp_data/Metadata/global-metadata.dat`,
metadata **v31**). There is no `Assembly-CSharp.dll` to decompile, so the code view is a
**structural dump** (signatures + native offsets, no method bodies) rather than real source.

Game install (Steam):
`/home/jpn/.steam/debian-installation/steamapps/common/Wedding Witch`

## Code: IL2CPP dump (`.decomp/`, gitignored)

Produced with **Il2CppDumper v6.7.46** (net7 build, run on the net8 runtime):

```sh
cd /tmp/il2cppdumper   # download: github.com/Perfare/Il2CppDumper releases
echo "" | DOTNET_ROLL_FORWARD=LatestMajor dotnet Il2CppDumper.dll \
  "$GAME/GameAssembly.so" \
  "$GAME/Wedding Witch_Data/il2cpp_data/Metadata/global-metadata.dat" \
  "$PWD_OF_THIS_REPO/.decomp"
```

(It exits 134 *after* finishing — a harmless `Console.ReadKey` on redirected stdin.)

Outputs in `.decomp/`:
- **`dump.cs`** — 30 MB / ~864k lines, **13,651 types across 314 namespaces**. Every class /
  method / field / enum / property with signatures, field byte-offsets (`// 0x20`), attributes,
  and per-method native `RVA/Offset/VA`. **Method bodies are empty** — that's the IL2CPP ceiling.
  `grep` this file the way you'd grep Fakutori's decompile.
- `DummyDll/` — ~70 reconstructed assemblies (`Assembly-CSharp.dll`, etc.) to compile a mod against.
- `il2cpp.h`, `script.json` — for loading native offsets into Ghidra/IDA (helper scripts ship with
  Il2CppDumper) if you ever need actual method *logic*. Cpp2IL is the alternative for that.
- `decompiled/` — bonus ILSpy decompile of the DummyDll stubs (game-only, ~40k lines, signatures).

Key game classes: `GameManager`, `GameDataManager`, `WaveManager`, `MissionManager`,
`PotionManager`, `BattleUIManager`, `InventoryItem`, `WitchInput`, and the
`EnemyController`/`BossController`/`LastBoss_*` hierarchy.

### Modding note
IL2CPP modding is **not** the Mono+Harmony setup Fakutori uses. Use **BepInEx 6 (IL2CPP)** with
**Il2CppInterop**: running the game once with BepInEx 6 generates interop assemblies with full type
info; you write plugins against those (Harmony works via the IL2CPP shim). The `DummyDll/` here is
useful for exploring the API ahead of that.

## Assets: `ref/` (media gitignored, text tracked)

UnityPy extractors in `tools/`. **Important:** they load the whole `Wedding Witch_Data` *directory*
as one environment, not just `data.unity3d` — otherwise the streamed `sharedassets*.resource`
sidecars don't resolve (audio comes back empty) and the Addressable bundles under
`StreamingAssets/aa/` are missed.

```sh
.venv/bin/python tools/extract_images.py   # -> ref/images/{textures,sprites}/  (PNG)
.venv/bin/python tools/extract_audio.py     # -> ref/audio/{music,sfx}/  (FLAC; --wav to skip transcode)
.venv/bin/python tools/extract_text.py      # -> ref/text/  (TextAssets + localization string tables)
```

Extracted (this build): **774 textures + 483 sprites** (265 MB), **158 AudioClips** → 26 music +
132 sfx (197 MB), **56 TextAssets** (Spine skeletons/atlases) + **13 locales × 11 string tables**.

### Text / localization
Wedding Witch uses **Unity Localization** (`com.unity.localization`) — String/Asset Tables shipped
as Addressable bundles, **13 locales** (en, ja, ko, zh-Hans, zh-TW, fr, de, es, it, pl, pt-BR, ru,
tr). Dialogue is rendered by a bespoke `Dialog`/`DialogManager`/`DialogParser`/`SpineDialogController`
set (Spine portraits + TextMesh Pro + Febucci TextAnimator), but the **strings live in the String
Tables** — mainly `Dialog_<locale>`, `StartAdventure_<locale>`, `LastBattle_<locale>`,
`Victory_<locale>`. Because it's IL2CPP (no MonoBehaviour type tree), tables are read via raw
length-prefixed-UTF-8 byte scanning. See `ref/text/_README.txt`.

## Layout
```
.decomp/   IL2CPP dump + DummyDll + decompiled stubs   (gitignored — regenerate as above)
.venv/     Python venv with UnityPy 1.25.0             (gitignored)
tools/     extract_images.py / extract_audio.py / extract_text.py
ref/text/  TextAssets + localization tables            (tracked)
ref/images,ref/audio/  extracted media                 (gitignored — regenerate via tools/)
```

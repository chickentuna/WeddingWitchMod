# WeddingWitchMod

Reverse-engineering / modding workspace for the Unity game **Wedding Witch**.

Wedding Witch is a Unity **Mono** build (6000.3.19f1): the game code ships as
`Wedding Witch_Data/Managed/Assembly-CSharp.dll` and decompiles to real method bodies.

Game install (Steam):
`/home/jpn/.steam/debian-installation/steamapps/common/Wedding Witch`

## Code: decompile (`.decomp/`, gitignored)

```sh
DOTNET_ROLL_FORWARD=LatestMajor ilspycmd -p -o .decomp/mono \
  "$GAME/Wedding Witch_Data/Managed/Assembly-CSharp.dll"
DOTNET_ROLL_FORWARD=LatestMajor ilspycmd -o .decomp/firstpass \
  "$GAME/Wedding Witch_Data/Managed/Assembly-CSharp-firstpass.dll"   # ES3 (Easy Save 3)
```

`.decomp/mono/` is 395 files, one per type. Key classes: `GameManager`, `GameDataManager`,
`MissionManager`, `RoadMapCanvas`, `PotionManager`, `BattleUIManager`, `EnchantManager`,
`GlobalStat`, `UpgradeWitchCanvas`/`UpgradeItem`/`UpgradeData`, and the
`EnemyController`/`BossController`/`LastBoss_*` hierarchy. Persistence is **ES3** into
`GameData.es3`, `AchivementState.es3` and `Config.es3`.

ScriptableObject *contents* are not in the assembly. They come out of the asset bundles with
UnityPy, which needs a `TypeTreeGenerator` fed from the `Managed` folder because the build ships
no type trees for its own MonoBehaviours:

```py
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
gen = TypeTreeGenerator("6000.3.19f1")
gen.load_local_dll_folder(GAME + "/Wedding Witch_Data/Managed")
env = UnityPy.load(GAME + "/Wedding Witch_Data/data.unity3d")
env.typetree_generator = gen
```

## Archipelago mod (`src/`)

BepInEx 6 **Unity.Mono** plugin. See [docs/archipelago.md](docs/archipelago.md).

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
`Victory_<locale>`. The build ships no MonoBehaviour type trees, so tables are read via raw
length-prefixed-UTF-8 byte scanning. See `ref/text/_README.txt`.

## Layout
```
.decomp/   ILSpy decompile of the game assemblies       (gitignored — regenerate as above)
.venv/     Python venv with UnityPy 1.25.0             (gitignored)
tools/     extract_images.py / extract_audio.py / extract_text.py
ref/text/  TextAssets + localization tables            (tracked)
ref/images,ref/audio/  extracted media                 (gitignored — regenerate via tools/)
```

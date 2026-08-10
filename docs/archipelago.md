# Archipelago integration

Wedding Witch is a Unity **Mono** build (`Wedding Witch_Data/Managed/Assembly-CSharp.dll`), so the
mod is **BepInEx 6 Unity.Mono + HarmonyX**. Which flavour of BepInEx is needed follows the game
build, not the operating system: `BepInEx-Unity.Mono-win-x64` on Windows and
`BepInEx-Unity.Mono-linux-x64` on Linux, with the same plugin DLL on both.

## The two halves
- **Client mod** (`src/`, this repo) — the BepInEx plugin. Closes the shop, applies received
  upgrades, sends checks.
- **apworld** (`worlds/wedding_witch/` in the Archipelago checkout) — defines the item and location
  names and ids. Both sides name the same things, and `UpgradeCatalog.cs` / `Locations.cs` have
  counterparts in `constants.py` / `locations.py` that must not drift.

## Items — the meta-upgrade shop, closed
`ShopPatch` blocks `UpgradeItem.OnClick` and `UpgradeWitchCanvas.ResetUpgrade` unconditionally,
session or not. All 17 upgrades, 73 levels between them, are Archipelago items instead; the panel
still opens and now reads as a display of what the multiworld has sent.

An upgrade's runtime identity is `UpgradeData.loadKey`, which is the asset name (`base.name`), and
every consumer reads it the same way — `ES3.Load<int>(loadKey, "GameData.es3")`:

| Consumer | Reads |
|---|---|
| `GlobalStat.ApplyUpgrade` | the 14 combat stats |
| `PlayerMagnet` | Witch's Doll (absorb radius) |
| `EnchantManager` | Witch's Bag (+1 level-up option) |
| `RoadMapCanvas` | Eye Of The Beholder (rewards shown on the map) |
| `UpgradeItem.Awake` | the level the panel displays |

So `SaveDataPatch` intercepts that one read rather than each consumer. It also diverts the whole of
`GameData.es3` into a per-room store (`ApProfile`) while connected, which is what keeps the
multiworld's levels out of the save played unconnected. `Config.es3` and `AchivementState.es3` are
left alone.

Levels deliberately take effect from the next run: `GlobalStat.ApplyUpgrade` accumulates into
instance fields that only reset when the scene reloads, so calling it again mid-run applies
everything twice.

## Locations — 82 by default
| Source | Hook | Count |
|---|---|---|
| Potion drunk | `PotionManager.EatPotion1/2/3`, stage = `RoadMapCanvas.currentFloor` | 54, or 18 |
| Boss cleared | `MissionManager.AdventureComplete`, form = `PotionManager.bodyState` | 8 |
| Shelf bought | `UpgradeItem.OnClick` on a cloned row | `shop_locations`, 20 by default |

Filler arrives as `Gift: <something>`. The mod matches that prefix rather than carrying
the apworld's list of them — it does nothing with a gift but recognise it, so duplicating
three dozen names would only be one more thing to drift. See `ApItems.IsFiller`.

Clearing an ordinary stage is not a check: the potion screen it leads to is, so the clear would
double every run. The boss is the exception, since it offers no potion.

Only Nightmare's boss is split by form — `Locations.PerFormBoss`, matching `PER_FORM_BOSS` in the
apworld. The shorter runs rarely offer enough of one potion type to transform, so asking for six
forms there would ask for something the run does not reliably allow.

The boss hook sits on an iterator method on purpose: Harmony patches the stub that builds the
state machine, so the postfix fires at `StartCoroutine(...)` — the moment the mission is judged
complete, ahead of the treasure and the fanfare.

Normal has 5 stages, Hard 6, Nightmare 7 (`RoadMapCanvas.Start` → `MissionManager.maxMissionCount`);
the boss sits on a further node past the last floor. `Difficulty` also has `Hell` and `Impossible`,
but `SelectDifficultyCanvas` hardcodes `hellMode.interactable = false`, so neither is reachable and
neither carries locations.

Each stage offers three potions and you drink one, so its other two slots want another run through
that stage. `PotionManager.GetRandomEnchantList` always fills three.

## Goal
Clear the Nightmare boss once in each of the six witch forms. The mod sends it — generation only
knows the point past which that run is a fair ask, since nothing on the AP side can observe a form.

## Build & run
1. Install BepInEx 6 Unity.Mono into the game folder and launch once. On Linux, Steam launch
   options `./run_bepinex.sh %command%`, and `chmod +x run_bepinex.sh` — the zip does not carry
   the bit.

   Two lines in the log are expected and harmless on Unity 6000.3: `cant resolve internal call
   to "UnityEngine.UnityLogWriter::WriteStringToUnityLog"`, which only costs the BepInEx-to-Unity
   log bridge, and Mono's `your mono runtime and class libraries are out of sync`.
2. `src/lib/` holds the DLLs the build references: BepInEx's four, and `Assembly-CSharp`,
   `Assembly-CSharp-firstpass` (ES3) and the `UnityEngine.*` modules out of the game's `Managed`
   folder. Gitignored — copy them from an install.
3. `dotnet build src/`. With `GameDir` set in `src/WeddingWitchArchipelago.csproj.user` it deploys
   into `BepInEx/plugins/` on its own.

## Reading the game
`ilspycmd -p -o .decomp/mono "$GAME/Wedding Witch_Data/Managed/Assembly-CSharp.dll"` gives real
method bodies. ScriptableObject contents — the upgrade `cost` arrays that set each max level — are
not in the assembly; they come out of the asset bundles with UnityPy plus a `TypeTreeGenerator`
loaded from the `Managed` folder, since the build ships no type trees for its own MonoBehaviours.

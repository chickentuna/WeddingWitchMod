# Archipelago integration — upgrades (first slice)

Wedding Witch is Unity **IL2CPP** (2022.3.61f1). Modding is **BepInEx 6 IL2CPP + Il2CppInterop +
HarmonyX** (not Mono/Harmony). This first slice maps the persistent meta-shop **upgrades** to
Archipelago: buying an upgrade level = an AP *location* check; a received AP *item* grants that level.

## The two halves
- **Client mod** (`src/`, this repo) — the BepInEx plugin. Hooks the shop, sends checks, applies items.
- **apworld** (`apworld/wedding_witch/`) — the Python world AP needs to generate seeds. It defines the
  item/location names+ids and must use the **same id scheme** as the client.

## How the game's upgrade system works (from `.decomp/dump.cs`)
The persistent meta-shop (NOT the in-run `BattleUIManager.LevelUp`, which is per-run leveling):

| Thing | Where |
|---|---|
| Shop controller (singleton) | `UpgradeWitchCanvas.instance`, field `List<UpgradeItem> upgradeItems` |
| One upgrade row | `UpgradeItem` — field `UpgradeData researchData`, `int level` |
| Purchase commit (**location hook**) | `UpgradeItem.Upgrade()` (virtual, RVA `0x2420960`) |
| Upgrade data | `UpgradeData : ScriptableObject` — getters `loadKey`, `nameKey`, `maxLevel`, `int[] cost` |
| Persistence | `WitchCandy.Save.Save.instance.GetInt/SetInt(loadKey, level)` → `SaveData.dat` |
| Recompute effects | `GlobalStat.instance.ApplyUpgrade()` (RVA `0x23FA300`) |

Upgrades are **leveled** (`0..maxLevel`), **17** of them (14 `GlobalStat` combat stats + 3 specials).
Runtime identity = `UpgradeData.loadKey` (a save key string). Those strings are asset-derived and
**not in the dump**, so they must be captured once in game (below).

## Id scheme (the client↔apworld contract)
Defined in `src/Game/UpgradeCatalog.cs` and mirrored in `apworld/wedding_witch/__init__.py`:
- `BASE = 8_451_000`, `SPAN = 32`, `ItemBase = BASE + 100_000`
- slug order = `UpgradeCatalog.All` (append-only; index drives ids)
- location `"<name> Lv N"` → `BASE + index*SPAN + N`
- item `"Progressive <name>"` → `ItemBase + index` (one copy per level)

Keep the slug order and per-upgrade `MaxLevel` **identical** on both sides.

## Build & run workflow
1. **Generate interop** (once): launch the game with BepInEx active — `./run_bepinex.sh` in the game
   folder, or Steam launch options `./run_bepinex.sh %command%`. This creates
   `…/Wedding Witch/BepInEx/interop/Assembly-CSharp.dll`, which the csproj references. The build
   **cannot** resolve game types (`UpgradeItem`, `GlobalStat`, …) until this exists.
2. **Capture the upgrade catalog** (once): in game, open the upgrade shop, click the **Dump Upgrades**
   debug button (top-left). The BepInEx log lists every `loadKey` / `nameKey` / `maxLevel`. Fill:
   - `UpgradeCatalog.LoadKeyToSlug` (loadKey → slug) and `UpgradeCatalog.MaxLevel` in C#, and
   - `MAX_LEVEL` in the apworld. Until filled, purchases log `UNMAPPED loadKey=…` and grants no-op.
3. **Build**: `dotnet build src/` (Debug) → outputs to `…/Wedding Witch/BepInEx/plugins/`. The compiler
   validates every game member access against the interop (same "build is the validator" rule as the
   Fakutori notes). If interop Unity types clash with the `UnityEngine.Modules` NuGet, drop that NuGet
   and reference the interop `UnityEngine.*.dll`.
4. **Test**: run the game, the on-screen panel shows AP connect fields; connect to a server generated
   from the apworld, buy an upgrade → location check; receive a "Progressive <name>" → level applied.

## Design notes / decisions to revisit
- Checks fire on the player's own purchase (`Upgrade()` postfix). Items raise the persisted level via
  `Save.SetInt` + `GlobalStat.ApplyUpgrade()` — see `SyncGrantsToSave()`.
- Not yet gated: currently the player can still buy with in-game currency regardless of AP items. A
  stricter rando would lock purchases behind received items (logic decision).
- The 3 "special" upgrades (Witch's Bag / Eye Of The Beholder / Witch's Doll) share the same
  shop/persist flow but their *effects* live outside `GlobalStat`; `ApplyUpgrade()` may not refresh
  them mid-run — verify in game.
- Next slices to consider as locations/items: achievements, flower unlocks, mission/wave milestones.

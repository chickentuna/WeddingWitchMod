using System.Collections.Generic;
using HarmonyLib;
using WeddingWitchArchipelago.Game;
using Save = WitchCandy.Save.Save;

// NOTE (IL2CPP): the game types below (UpgradeItem, UpgradeWitchCanvas, UpgradeData, GlobalStat)
// are global-namespace types from the generated interop Assembly-CSharp.dll. If the build can't find
// them, Il2CppInterop emitted global types under the `Il2Cpp` namespace — add `using Il2Cpp;` here.
// The compiler is the validator (same as the Fakutori workflow): build once interop exists, then fix
// any namespace/member names it flags against .decomp/dump.cs.

namespace WeddingWitchArchipelago.Patches;

/// <summary>
/// Bridges the Wedding Witch meta-upgrade shop to Archipelago:
///   * buying an upgrade level (UpgradeItem.Upgrade) -> send that level's AP location check
///   * a received AP item -> raise the persisted level so the power actually applies
/// See <see cref="UpgradeCatalog"/> for the id scheme and the one-time loadKey capture step.
/// </summary>
[HarmonyPatch]
public static class UpgradeShopPatches
{
    // Levels the server has granted us, by slug. Applied to the save whenever the shop/save is live.
    public static readonly Dictionary<string, int> GrantedLevels = new();

    /// A purchase committed in-game -> check the matching AP location.
    [HarmonyPatch(typeof(UpgradeItem), nameof(UpgradeItem.Upgrade))]
    [HarmonyPostfix]
    public static void OnUpgradePurchased(UpgradeItem __instance)
    {
        try
        {
            var data = __instance.researchData;
            if (data == null) return;
            string loadKey = data.loadKey;
            int level = __instance.level; // already incremented by Upgrade()

            if (!UpgradeCatalog.LoadKeyToSlug.TryGetValue(loadKey, out var slug))
            {
                Plugin.BepinLogger.LogWarning(
                    $"[Upgrade] purchased UNMAPPED loadKey='{loadKey}' (level {level}). " +
                    "Add it to UpgradeCatalog.LoadKeyToSlug (press the dump key to list all).");
                return;
            }

            long loc = UpgradeCatalog.LocationId(slug, level);
            Plugin.BepinLogger.LogInfo($"[Upgrade] bought {slug} Lv{level} -> AP location {loc}");
            Plugin.ArchipelagoClient.SendCheck(loc);
        }
        catch (System.Exception e) { Plugin.BepinLogger.LogError($"[Upgrade] purchase hook failed: {e}"); }
    }

    /// Push server-granted levels into the save whenever the shop becomes active.
    [HarmonyPatch(typeof(UpgradeWitchCanvas), "OnEnable")]
    [HarmonyPostfix]
    public static void OnShopOpened() => SyncGrantsToSave();

    /// Server granted us a progressive upgrade item -> remember the new level and apply it.
    public static void OnReceivedUpgradeItem(long itemId)
    {
        if (!UpgradeCatalog.TrySlugForItemId(itemId, out var slug)) return;
        GrantedLevels.TryGetValue(slug, out var lvl);
        GrantedLevels[slug] = lvl + 1; // progressive: each copy = +1 level
        Plugin.BepinLogger.LogInfo($"[Upgrade] AP granted {slug} -> level {GrantedLevels[slug]}");
        SyncGrantsToSave();
    }

    /// Writes GrantedLevels into the persistent save and recomputes combat stats. Safe to call often.
    public static void SyncGrantsToSave()
    {
        try
        {
            var save = Save.instance;
            var shop = UpgradeWitchCanvas.instance;
            if (save == null || shop == null || GrantedLevels.Count == 0) return;

            var items = shop.upgradeItems;
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var data = item?.researchData;
                if (data == null) continue;
                string loadKey = data.loadKey;
                if (!UpgradeCatalog.LoadKeyToSlug.TryGetValue(loadKey, out var slug)) continue;
                if (!GrantedLevels.TryGetValue(slug, out var want)) continue;

                int capped = System.Math.Min(want, data.maxLevel);
                if (save.GetInt(loadKey) < capped)
                {
                    save.SetInt(loadKey, capped);
                    item.SetValue(); // refresh the row's displayed level/cost
                    changed = true;
                }
            }
            if (changed) GlobalStat.instance?.ApplyUpgrade(); // recompute multipliers
        }
        catch (System.Exception e) { Plugin.BepinLogger.LogError($"[Upgrade] sync failed: {e}"); }
    }

    /// One-time capture helper: logs every shop upgrade's loadKey/nameKey/maxLevel/cost so you can
    /// fill UpgradeCatalog.LoadKeyToSlug and MaxLevel. Bind to a debug key (see Plugin).
    public static void DumpCatalog()
    {
        var shop = UpgradeWitchCanvas.instance;
        if (shop == null) { Plugin.BepinLogger.LogWarning("[Upgrade] open the upgrade shop first, then dump."); return; }
        var items = shop.upgradeItems;
        Plugin.BepinLogger.LogInfo($"[Upgrade] === {items.Count} upgrades in UpgradeWitchCanvas ===");
        for (int i = 0; i < items.Count; i++)
        {
            var d = items[i]?.researchData;
            if (d == null) continue;
            Plugin.BepinLogger.LogInfo($"  [{i}] loadKey='{d.loadKey}' nameKey='{d.nameKey}' maxLevel={d.maxLevel}");
        }
        Plugin.BepinLogger.LogInfo("[Upgrade] map each loadKey -> a slug in UpgradeCatalog.All and fill MaxLevel.");
    }
}

using System.Collections.Generic;

namespace WeddingWitchArchipelago.Game;

/// <summary>
/// The canonical list of Wedding Witch meta-shop upgrades and the deterministic Archipelago
/// location/item id scheme. THIS FILE IS THE CONTRACT WITH THE apworld — the Python world
/// (apworld/wedding_witch) must use the identical BASE, SPAN, slug order, and per-upgrade MaxLevel.
///
/// Upgrades are LEVELED (0..MaxLevel). We model each level as its own AP *location*
/// ("&lt;name&gt; Lv N", checked when the player buys that level in-game) and each AP *item* as a
/// progressive "+1 level" for that upgrade. The shop is `UpgradeWitchCanvas`; one purchase commit
/// is `UpgradeItem.Upgrade()`; levels persist in WitchCandy.Save.Save under UpgradeData.loadKey.
///
/// Runtime identity of an upgrade = its UpgradeData.loadKey (a save-key string). Those strings are
/// asset-derived and NOT in the IL2CPP dump, so <see cref="LoadKeyToSlug"/> is filled from a one-time
/// in-game capture (press the dump key — see UpgradeShopPatches.DumpCatalog). Until it's filled the
/// mod still runs and logs every unmapped loadKey so you can complete the table.
/// </summary>
public static class UpgradeCatalog
{
    // AP id space. Arbitrary but must match the apworld. 32 levels reserved per upgrade.
    public const long Base = 8_451_000;
    public const int Span = 32;
    public const long ItemBase = Base + 100_000;

    public readonly struct Upgrade
    {
        public readonly string Slug;       // stable internal id (used for ordering -> ids)
        public readonly string Display;    // English shop name
        public Upgrade(string slug, string display) { Slug = slug; Display = display; }
    }

    /// Ordered list — INDEX IS PART OF THE ID FORMULA, never reorder; append only.
    /// 1-14 are GlobalStat combat stats; 15-17 are the "special" upgrades (handled outside GlobalStat).
    public static readonly Upgrade[] All =
    {
        new("health",        "Witch's Hat"),         // Max Health
        new("attack",        "Witch's Orb"),         // Attack Power
        new("attackSpeed",   "Witch's Gloves"),      // Attack Speed
        new("radius",        "Witch's Ring"),        // Attack Radius
        new("lifespan",      "Witch's Watch"),       // Lifespan
        new("castAmount",    "Phantasm Mirror"),     // Cast Amount
        new("moveSpeed",     "Witch's Shoes"),       // Movement Speed
        new("hitCount",      "Witch's Necklace"),    // Spell Hit Count
        new("critRate",      "Witch's Earrings"),    // Critical Rate
        new("critDamage",    "Magical Optics"),      // Critical Damage
        new("dodge",         "Cape of Protection"),  // Dodge Rate
        new("regen",         "Witch's Bracelet"),    // Health regen / 10s
        new("exp",           "Book of Hitomi"),      // EXP gained
        new("gold",          "Maneki Neko"),         // Gold earned
        new("levelUpOption", "Witch's Bag"),         // +1 level-up option
        new("rewardCount",   "Eye Of The Beholder"), // +rewards shown on map
        new("absorbRadius",  "Witch's Doll"),        // item absorption radius
    };

    /// TODO (one-time capture): fill from the in-game dump — maps UpgradeData.loadKey -> Slug above.
    /// e.g. { "HealthUpgrade", "health" }, ... Run the game, open the upgrade shop, press the dump key.
    public static readonly Dictionary<string, string> LoadKeyToSlug = new();

    /// TODO (one-time capture): real MaxLevel per slug (UpgradeData.maxLevel == cost.Length).
    /// Placeholder 5 each until captured; the apworld must use the same numbers.
    public static readonly Dictionary<string, int> MaxLevel = new();

    static readonly Dictionary<string, int> slugIndex = BuildIndex();
    static Dictionary<string, int> BuildIndex()
    {
        var d = new Dictionary<string, int>();
        for (int i = 0; i < All.Length; i++) d[All[i].Slug] = i;
        return d;
    }

    public static int MaxLevelOf(string slug) => MaxLevel.TryGetValue(slug, out var m) ? m : 5;
    public static bool TryIndex(string slug, out int i) => slugIndex.TryGetValue(slug, out i);

    public static long LocationId(string slug, int level) =>
        slugIndex.TryGetValue(slug, out var i) ? Base + (long)i * Span + level : -1;

    public static long ItemId(string slug) =>
        slugIndex.TryGetValue(slug, out var i) ? ItemBase + i : -1;

    public static string LocationName(string slug, int level) =>
        $"{DisplayOf(slug)} Lv{level}";

    public static string ItemName(string slug) => $"Progressive {DisplayOf(slug)}";

    public static string DisplayOf(string slug) =>
        slugIndex.TryGetValue(slug, out var i) ? All[i].Display : slug;

    /// reverse: item id -> slug (for applying a received item)
    public static bool TrySlugForItemId(long itemId, out string slug)
    {
        slug = null;
        long idx = itemId - ItemBase;
        if (idx < 0 || idx >= All.Length) return false;
        slug = All[(int)idx].Slug;
        return true;
    }
}

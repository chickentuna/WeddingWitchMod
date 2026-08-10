using System.Collections.Generic;

namespace WeddingWitchArchipelago;

/// The meta-shop upgrades, and the Archipelago item name each one arrives under.
///
/// THIS IS A CONTRACT WITH THE APWORLD — the load keys, display names, max levels
/// and their order all have counterparts in worlds/wedding_witch/constants.py and
/// must not drift.
///
/// A load key is the UpgradeData asset name (UpgradeData.loadKey returns
/// `base.name`), which is the key every consumer reads out of GameData.es3:
/// GlobalStat.ApplyUpgrade, PlayerMagnet, EnchantManager, RoadMapCanvas and
/// UpgradeItem itself. Intercepting that read is how a received item becomes a
/// level — see SaveDataPatch.
public static class UpgradeCatalog
{
    public readonly struct Upgrade
    {
        public readonly string LoadKey;
        public readonly string Display;
        public readonly int MaxLevel;

        public Upgrade(string loadKey, string display, int maxLevel)
        {
            LoadKey = loadKey;
            Display = display;
            MaxLevel = maxLevel;
        }

        /// An upgrade is named after the thing itself. Several copies of one raise its
        /// level, but the shop calls it Witch's Hat and so does the multiworld.
        public string ItemName => Display;
    }

    public static readonly Upgrade[] All =
    {
        new Upgrade("Upgrade_WitchHat", "Witch's Hat", 5),
        new Upgrade("Upgrade_Orb", "Witch's Orb", 5),
        new Upgrade("Upgrade_Glove", "Witch's Gloves", 5),
        new Upgrade("Upgrade_Ring", "Witch's Ring", 5),
        new Upgrade("Upgrade_Watch", "Witch's Watch", 5),
        new Upgrade("Upgrade_Mirror", "Phantasm Mirror", 1),
        new Upgrade("Upgrade_Shoes", "Witch's Shoes", 3),
        new Upgrade("Upgrade_Necklace", "Witch's Necklace", 3),
        new Upgrade("Upgrade_Earring", "Witch's Earrings", 5),
        new Upgrade("Upgrade_Monocle", "Magical Optics", 5),
        new Upgrade("Upgrade_Cape", "Cape of Protection", 5),
        new Upgrade("Upgrade_Bracelet", "Witch's Bracelet", 5),
        new Upgrade("Upgrade_HitomiBook", "Book of Hitomi", 5),
        new Upgrade("Upgrade_Maneki", "Maneki Neko", 5),
        new Upgrade("Upgrade_Bag", "Witch's Bag", 1),
        new Upgrade("Upgrade_EyeOfBeholder", "Eye Of The Beholder", 5),
        new Upgrade("Upgrade_WitchDoll", "Witch's Doll", 5),
    };

    private static readonly Dictionary<string, Upgrade> ByItemName = BuildItemNameIndex();
    private static readonly HashSet<string> LoadKeys = BuildLoadKeys();

    private static Dictionary<string, Upgrade> BuildItemNameIndex()
    {
        var index = new Dictionary<string, Upgrade>();
        foreach (var upgrade in All) index[upgrade.ItemName] = upgrade;
        return index;
    }

    private static HashSet<string> BuildLoadKeys()
    {
        var keys = new HashSet<string>();
        foreach (var upgrade in All) keys.Add(upgrade.LoadKey);
        return keys;
    }

    public static bool IsUpgradeKey(string loadKey) => LoadKeys.Contains(loadKey);

    public static bool TryResolve(string itemName, out Upgrade upgrade) =>
        ByItemName.TryGetValue(itemName, out upgrade);

    public static int MaxLevelOf(string loadKey)
    {
        foreach (var upgrade in All)
            if (upgrade.LoadKey == loadKey)
                return upgrade.MaxLevel;
        return 0;
    }
}

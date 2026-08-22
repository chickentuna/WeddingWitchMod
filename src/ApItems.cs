using System.Collections.Generic;

namespace WeddingWitchArchipelago;

/// Every Archipelago item name the mod handles. Upgrade names come from
/// <see cref="UpgradeCatalog"/>; the rest are here.
public static class ApItems
{
    /// One copy per difficulty past Normal. See DifficultyPatch.
    public const string ProgressiveDifficulty = "Progressive Difficulty";

    /// Grants Love Coins, kept outside <see cref="GiftPrefix"/> so it is acted on rather
    /// than ignored. Matched by name, so the apworld's GOLD_ITEM has to read the same.
    public const string Gold = "+90 Coins";
    public const int GoldPerItem = 90;

    /// Filler is a wedding gift. The mod only has to recognise one, so it matches the
    /// prefix rather than carrying the names.
    public const string GiftPrefix = "Gift: ";

    public static bool IsFiller(string itemName) =>
        itemName != null && itemName.StartsWith(GiftPrefix);

    /// Distinct names the mod knows, not pool copies. Gifts are excluded, being matched
    /// by prefix instead.
    public static IEnumerable<string> AllNames()
    {
        yield return ProgressiveDifficulty;
        yield return Gold;
        foreach (var upgrade in UpgradeCatalog.All) yield return upgrade.ItemName;
    }
}

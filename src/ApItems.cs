using System.Collections.Generic;

namespace WeddingWitchArchipelago;

/// Every Archipelago item name the mod answers to.
///
/// The upgrades come from <see cref="UpgradeCatalog"/>; the rest are here. All of it is
/// a contract with worlds/wedding_witch/items.py — a name that drifts is an item the mod
/// silently ignores.
public static class ApItems
{
    /// One copy per difficulty past Normal. See DifficultyPatch.
    public const string ProgressiveDifficulty = "Progressive Difficulty";

    /// Love Coins, which the hijacked shop spends. Deliberately outside the Gift prefix:
    /// the mod ignores anything wearing that, and this has to be acted on.
    public const string Gold = "+100 Gold";
    public const int GoldPerItem = 100;

    /// Filler is a wedding gift — the apworld names three dozen of them, all prefixed
    /// like this. Matching the prefix rather than the list is deliberate: the mod does
    /// nothing with a gift but recognise it, and carrying the names on both sides would
    /// only be one more thing to drift.
    public const string GiftPrefix = "Gift: ";

    public static bool IsFiller(string itemName) =>
        itemName != null && itemName.StartsWith(GiftPrefix);

    /// Distinct names the mod knows by name, not pool copies — for cross-checking
    /// against the apworld. Gifts are excluded, being matched by prefix instead.
    public static IEnumerable<string> AllNames()
    {
        yield return ProgressiveDifficulty;
        yield return Gold;
        foreach (var upgrade in UpgradeCatalog.All) yield return upgrade.ItemName;
    }
}

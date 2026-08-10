using System.Collections.Generic;

namespace WeddingWitchArchipelago;

/// Every Archipelago item name the mod answers to, in one place.
///
/// The upgrades come from <see cref="UpgradeCatalog"/>; these are the two that are
/// not upgrades. All of it is a contract with worlds/wedding_witch/items.py — a name
/// that drifts is an item the mod silently ignores.
public static class ApItems
{
    public const string Filler = "Wedding Gift";

    /// One copy per difficulty past Normal. See DifficultyPatch.
    public const string ProgressiveDifficulty = "Progressive Difficulty";

    /// Distinct names, not pool copies — for cross-checking against the apworld.
    public static IEnumerable<string> AllNames()
    {
        yield return Filler;
        yield return ProgressiveDifficulty;
        foreach (var upgrade in UpgradeCatalog.All) yield return upgrade.ItemName;
    }
}

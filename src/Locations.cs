using System.Collections.Generic;

namespace WeddingWitchArchipelago;

/// Location naming, and the two enum-to-name tables the checks are keyed by.
///
/// These strings are the contract with the apworld — see
/// worlds/wedding_witch/locations.py and constants.py.
public static class Locations
{
    /// Playable difficulties. Hell and Impossible are in the Difficulty enum but
    /// SelectDifficultyCanvas hardcodes `hellMode.interactable = false`, so neither
    /// can be reached and neither carries locations.
    private static readonly Dictionary<Difficulty, string> DifficultyNames =
        new Dictionary<Difficulty, string>
        {
            { Difficulty.Normal, "Normal" },
            { Difficulty.Hard, "Hard" },
            { Difficulty.Nightmare, "Nightmare" },
        };

    /// BodyState.Normal is the untransformed witch and carries no location.
    private static readonly Dictionary<BodyState, string> FormNames =
        new Dictionary<BodyState, string>
        {
            { BodyState.BigBreast, "Big Breasts" },
            { BodyState.SmallBreast, "Small Breasts" },
            { BodyState.Corruption, "Corruption" },
            { BodyState.Beast, "Beast" },
            { BodyState.Muscle, "Muscle" },
            { BodyState.Hip, "Hips" },
        };

    /// Stages per difficulty, as RoadMapCanvas.Start sets MissionManager
    /// .maxMissionCount. The boss sits past the last floor and is not one of these.
    private static readonly Dictionary<Difficulty, int> StageCounts =
        new Dictionary<Difficulty, int>
        {
            { Difficulty.Normal, 5 },
            { Difficulty.Hard, 6 },
            { Difficulty.Nightmare, 7 },
        };

    /// PotionManager.GetRandomEnchantList fills Mathf.Min(possibleEnchant.Count, 3)
    /// slots, and the possible list is never short of three.
    public const int PotionSlots = 3;

    public static string Stage(Difficulty difficulty, int stage)
    {
        if (!DifficultyNames.TryGetValue(difficulty, out var name)) return null;
        if (stage < 1 || stage > StageCounts[difficulty]) return null;
        return name + " - Stage " + stage + " Cleared";
    }

    /// The single check a potion screen pays out, however the player picks. This is
    /// the shape with Potion Slot Locations off.
    public static string Potion(Difficulty difficulty, int stage)
    {
        var prefix = StagePrefix(difficulty, stage);
        return prefix == null ? null : prefix + " Potion";
    }

    /// One of the three potions offered after a stage, keyed by the slot it sat in
    /// rather than by which potion it was — the pool is rolled at random, so the potion
    /// itself is nothing the apworld could name. This is the shape with Potion Slot
    /// Locations on.
    public static string Potion(Difficulty difficulty, int stage, int slot)
    {
        if (slot < 1 || slot > PotionSlots) return null;
        var prefix = StagePrefix(difficulty, stage);
        return prefix == null ? null : prefix + " Potion " + slot;
    }

    /// A shelf in the hijacked witch's shop. How many of them a seed stocks is the
    /// caller's to know.
    public static string ShopSlot(int slot) => slot >= 1 ? "Shop Slot " + slot : null;

    private static string StagePrefix(Difficulty difficulty, int stage)
    {
        if (!DifficultyNames.TryGetValue(difficulty, out var name)) return null;
        if (stage < 1 || stage > StageCounts[difficulty]) return null;
        return name + " - Stage " + stage;
    }

    public static string Boss(Difficulty difficulty, BodyState form)
    {
        if (!DifficultyNames.TryGetValue(difficulty, out var name)) return null;
        if (!FormNames.TryGetValue(form, out var formName)) return null;
        return name + " - Boss Cleared as " + formName;
    }

    /// Everything whose contents are shown to the player before they claim it, for the
    /// connect-time scout: the potion screens and the shop's shelves.
    public static IEnumerable<string> AllScoutable(bool potionSlots, int shopSlots)
    {
        foreach (var difficulty in StageCounts.Keys)
        {
            for (var stage = 1; stage <= StageCounts[difficulty]; stage++)
            {
                if (!potionSlots)
                {
                    yield return Potion(difficulty, stage);
                    continue;
                }
                for (var slot = 1; slot <= PotionSlots; slot++)
                    yield return Potion(difficulty, stage, slot);
            }
        }

        for (var slot = 1; slot <= shopSlots; slot++)
            yield return ShopSlot(slot);
    }

    /// Every name any seed could use, whatever its options — for cross-checking
    /// against the apworld's id table.
    public static IEnumerable<string> AllNames(int shopSlotMax)
    {
        foreach (var difficulty in StageCounts.Keys)
        {
            for (var stage = 1; stage <= StageCounts[difficulty]; stage++)
            {
                yield return Stage(difficulty, stage);
                yield return Potion(difficulty, stage);
                for (var slot = 1; slot <= PotionSlots; slot++)
                    yield return Potion(difficulty, stage, slot);
            }
            foreach (var location in AllBossForms(difficulty)) yield return location;
        }

        for (var slot = 1; slot <= shopSlotMax; slot++) yield return ShopSlot(slot);
    }

    /// Every boss-form location of a difficulty, which is what the goal asks for.
    public static IEnumerable<string> AllBossForms(Difficulty difficulty)
    {
        foreach (var form in FormNames.Keys)
        {
            var location = Boss(difficulty, form);
            if (location != null) yield return location;
        }
    }

    public static bool TryParseDifficulty(string name, out Difficulty difficulty)
    {
        foreach (var pair in DifficultyNames)
        {
            if (pair.Value != name) continue;
            difficulty = pair.Key;
            return true;
        }
        difficulty = Difficulty.Nightmare;
        return false;
    }
}

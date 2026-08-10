using HarmonyLib;

namespace WeddingWitchArchipelago;

/// The run's two kinds of check.
///
/// Both hooks sit on iterator methods, which is deliberate: Harmony patches the stub
/// that builds the state machine, so the postfix runs at `StartCoroutine(...)` — the
/// moment the mission is judged complete, before the three seconds of treasure and
/// fanfare.
[HarmonyPatch]
public static class MissionPatch
{
    /// RoadMapCanvas.ExecuteNode increments currentFloor as the stage begins, so by
    /// the time it completes the counter already reads the 1-based stage just
    /// cleared. It stops at the floor count, which is where the boss node sits.
    [HarmonyPatch(typeof(MissionManager), nameof(MissionManager.MissionComplete))]
    [HarmonyPostfix]
    public static void OnStageCleared() =>
        ApState.SendCheck(Locations.Stage(BattleUIManager.difficulty,
                                          RoadMapCanvas.currentFloor));

    /// The last boss is down, which is a check for the form the witch finished in.
    /// Winning as the untransformed witch is a legitimate way to end a run and
    /// simply carries no location.
    [HarmonyPatch(typeof(MissionManager), nameof(MissionManager.AdventureComplete))]
    [HarmonyPostfix]
    public static void OnBossCleared() =>
        ApState.SendCheck(Locations.Boss(BattleUIManager.difficulty,
                                         PotionManager.bodyState));
}

/// The potion offered at the end of a stage.
///
/// Drinking it is the check. With Potion Slot Locations on the three slots are three
/// separate checks, so a visit yields one and the other two want another run through
/// that stage; with it off the screen pays out once whichever you pick. Either way the
/// potion is still drunk and still does what it always did — nothing about the run
/// changes.
///
/// The potion screen belongs to MissionComplete, which runs before RoadMapCanvas
/// moves on, so currentFloor still reads the stage that produced it. The boss goes
/// through AdventureComplete and offers no potion at all.
[HarmonyPatch]
public static class PotionPatch
{
    [HarmonyPatch(typeof(PotionManager), nameof(PotionManager.EatPotion1))]
    [HarmonyPostfix]
    public static void OnFirstSlotDrunk() => SendSlot(1);

    [HarmonyPatch(typeof(PotionManager), nameof(PotionManager.EatPotion2))]
    [HarmonyPostfix]
    public static void OnSecondSlotDrunk() => SendSlot(2);

    [HarmonyPatch(typeof(PotionManager), nameof(PotionManager.EatPotion3))]
    [HarmonyPostfix]
    public static void OnThirdSlotDrunk() => SendSlot(3);

    private static void SendSlot(int slot)
    {
        var difficulty = BattleUIManager.difficulty;
        var stage = RoadMapCanvas.currentFloor;
        ApState.SendCheck(ApState.Settings.PotionSlotLocations
            ? Locations.Potion(difficulty, stage, slot)
            : Locations.Potion(difficulty, stage));
    }
}

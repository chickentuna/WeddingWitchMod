using System.Collections.Generic;
using System.Linq;
using WeddingWitchArchipelago.Archipelago;

namespace WeddingWitchArchipelago;

/// The single thing the game patches talk to. Everything above the network layer
/// works in location and item names; only ArchipelagoClient sees ids.
public static class ApState
{
    private static readonly HashSet<string> Checked = new HashSet<string>();

    private static ArchipelagoClient client;

    public static bool Connected => client?.Connected == true;

    public static SlotSettings Settings =>
        client?.Connected == true ? client.Settings : SlotSettings.Defaults();

    // ---- session lifecycle ----------------------------------------------

    public static void AdoptSession(ArchipelagoClient session,
                                    IEnumerable<string> alreadyChecked)
    {
        client = session;

        Checked.Clear();
        DifficultiesGranted = 0;
        if (alreadyChecked != null)
            foreach (var location in alreadyChecked) Checked.Add(location);

        // The item stream is replayed in full on every connect, so levels are
        // rebuilt from it rather than trusted from the last run. Anything the
        // server has since taken back therefore goes away instead of lingering.
        foreach (var upgrade in UpgradeCatalog.All)
            ApProfile.Remove(ApProfile.GameDataFile, upgrade.LoadKey);

        Plugin.Logger.LogInfo($"Session adopted — {Checked.Count} locations already checked");
    }

    public static void ReleaseSession()
    {
        client = null;
        ApProfile.Leave();
    }

    // ---- items ----------------------------------------------------------

    /// A received upgrade raises the level the game reads out of GameData.es3.
    ///
    /// Deliberately not followed by GlobalStat.ApplyUpgrade: that method accumulates
    /// into instance fields that only reset when the scene reloads, so calling it a
    /// second time applies every upgrade twice. Levels take effect from the next run,
    /// which is also when a permanent upgrade would normally start mattering.
    public static void ReceiveItem(string itemName)
    {
        if (itemName == ApItems.Filler) return;

        if (itemName == ApItems.ProgressiveDifficulty)
        {
            DifficultiesGranted++;
            Plugin.Logger.LogInfo($"[ap] difficulties unlocked: {DifficultiesGranted}");
            RequestReload();
            return;
        }

        if (!UpgradeCatalog.TryResolve(itemName, out var upgrade))
        {
            Plugin.Logger.LogWarning(
                $"Received '{itemName}' but no upgrade matches it — "
                + "mod and apworld item names have drifted");
            return;
        }

        var level = LevelOf(upgrade.LoadKey);
        if (level >= upgrade.MaxLevel)
        {
            Plugin.Logger.LogWarning(
                $"Received '{itemName}' beyond its {upgrade.MaxLevel} levels — ignored");
            return;
        }

        level++;
        ApProfile.Set(ApProfile.GameDataFile, upgrade.LoadKey, level.ToString());
        Plugin.Logger.LogInfo($"[ap] {upgrade.Display} is now level {level}/{upgrade.MaxLevel}");
        RequestReload();
    }

    /// How many difficulties past Normal the multiworld has opened. Rebuilt from the
    /// item stream on every connect, so it needs no save of its own.
    public static int DifficultiesGranted { get; private set; }

    /// Items and socket closures arrive on a network thread, and everything that
    /// reacts to them touches Unity objects. Plugin.Update picks this up.
    public static void RequestReload() => reloadPending = true;

    public static bool TakeReloadRequest()
    {
        if (!reloadPending) return false;
        reloadPending = false;
        return true;
    }

    private static volatile bool reloadPending;

    public static int LevelOf(string loadKey) =>
        ApProfile.TryGet(ApProfile.GameDataFile, loadKey, out var stored)
        && int.TryParse(stored, out var level)
            ? level
            : 0;

    // ---- locations ------------------------------------------------------

    public static bool IsChecked(string location) => Checked.Contains(location);

    public static void SendCheck(string location)
    {
        if (location == null || !Checked.Add(location)) return;
        Plugin.Logger.LogInfo($"[ap] CHECK {location}");
        client?.SendCheck(location);
        CheckGoal();
    }

    // ---- goal -----------------------------------------------------------

    public static bool GoalComplete { get; private set; }

    /// The goal is one winning run per form asked for — the boss of the goal
    /// difficulty beaten in that many different witch forms.
    private static void CheckGoal()
    {
        if (GoalComplete || !Connected) return;

        if (!Locations.TryParseDifficulty(Settings.GoalDifficulty, out var difficulty))
        {
            Plugin.Logger.LogError(
                $"Seed asks for goal difficulty '{Settings.GoalDifficulty}', which is not playable");
            return;
        }

        var wanted = Settings.GoalForms;
        var done = Locations.AllBossForms(difficulty).Count(Checked.Contains);
        if (done < wanted)
        {
            Plugin.Logger.LogInfo($"[ap] goal progress {done}/{wanted} forms");
            return;
        }

        GoalComplete = true;
        Plugin.Logger.LogInfo(
            $"[ap] GOAL COMPLETE — {Settings.GoalDifficulty} beaten in {done} forms");
        client.SendGoal();
    }
}

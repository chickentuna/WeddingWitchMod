using System.Collections;
using UnityEngine;

namespace WeddingWitchArchipelago;

/// A key that wins the mission you are in, for testing the Archipelago integration
/// without playing six full runs to reach the end of one.
///
/// Off unless EnableCheats is set in the config, because a key that clears a stage
/// outright would otherwise let anyone empty a multiworld's locations in a minute.
///
/// It does not skip to the end. It satisfies whatever the mission's own coroutine is
/// waiting on and lets the game finish normally, so the treasure, the potion screen,
/// the map and every check fire in the order a real run would produce — which is the
/// only thing worth testing.
public class DebugCheats : MonoBehaviour
{
    private Coroutine forcing;

    private void Update()
    {
        if (!Plugin.EnableCheats.Value) return;
        if (!Input.GetKeyDown(Plugin.WinMissionKey.Value)) return;
        WinCurrentMission();
    }

    private void WinCurrentMission()
    {
        var mission = MissionManager.instance;
        var battle = BattleUIManager.instance;

        if (mission == null || battle == null || !battle.isInBattle)
        {
            Plugin.Logger.LogWarning("[cheat] not in a mission — nothing to win");
            return;
        }

        // The last boss has no objective counter to satisfy; killing it is the
        // objective, and LastBossKilled is what its death calls.
        if (mission.currentMission == SlotType.LastBoss)
        {
            Plugin.Logger.LogInfo(
                $"[cheat] finishing the last boss as {PotionManager.bodyState}");
            mission.LastBossKilled();
            return;
        }

        if (forcing != null) return;
        Plugin.Logger.LogInfo($"[cheat] completing {mission.currentMission} on "
                              + $"{BattleUIManager.difficulty} stage {RoadMapCanvas.currentFloor}");
        forcing = StartCoroutine(ForceObjectives());
    }

    /// Held at zero until the battle actually ends rather than set once: KillElite
    /// waits on a timer and only then on a kill count, so a single write would satisfy
    /// the first wait and leave the mission stuck on the second.
    private IEnumerator ForceObjectives()
    {
        // The mission's checks poll on a one-second beat, so this outlives a couple of
        // those without hanging around if something goes wrong.
        var deadline = Time.unscaledTime + 15f;

        while (BattleUIManager.instance != null
               && BattleUIManager.instance.isInBattle
               && Time.unscaledTime < deadline)
        {
            MissionManager.remainKillCount = 0;
            MissionManager.survivalTime = 0;
            MissionManager.remainEliteKillCount = 0;
            if (SealBreakProgress.instance != null) SealBreakProgress.instance.isFull = true;
            yield return null;
        }

        forcing = null;
    }
}

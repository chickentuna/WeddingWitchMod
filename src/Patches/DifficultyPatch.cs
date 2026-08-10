using HarmonyLib;

namespace WeddingWitchArchipelago;

/// Which difficulties can be started.
///
/// Vanilla opens Hard once NormalClearCount is above zero and Nightmare once
/// HardClearCount is, which would let a save that has already finished the game walk
/// straight into Nightmare on turn one. Connected, the ladder is Progressive
/// Difficulty items instead, so the multiworld decides how deep the seed goes.
///
/// Unconnected this does nothing and the clear counts still rule — installing the mod
/// closes the shop, but it does not otherwise take the game away from you.
///
/// Only the buttons' interactable flag is set, which is enough:
/// StartHardGameButtonPressed and its siblings each re-check it before starting.
[HarmonyPatch]
public static class DifficultyPatch
{
    [HarmonyPatch(typeof(SelectDifficultyCanvas), "OnEnable")]
    [HarmonyPostfix]
    public static void OnScreenShown(SelectDifficultyCanvas __instance)
    {
        Apply();
        DifficultyProgress.Refresh(__instance);
    }

    /// A difficulty arriving while the screen is open has to unlock it there and then.
    public static void Apply()
    {
        var screen = SelectDifficultyCanvas.instance;
        if (screen == null || !ApState.Connected) return;

        Set(screen.hardMode, screen.hardModeLock, ApState.DifficultiesGranted >= 1);
        Set(screen.nightmareMode, screen.nightmareModeLock, ApState.DifficultiesGranted >= 2);

        // Hell stays shut. SelectDifficultyCanvas hardcodes it off in vanilla and the
        // apworld has no locations for it.
        Set(screen.hellMode, screen.hellModeLock, false);
    }

    private static void Set(UnityEngine.UI.Button button, UnityEngine.UI.Image padlock,
                            bool unlocked)
    {
        if (button != null) button.interactable = unlocked;
        if (padlock != null) padlock.gameObject.SetActive(!unlocked);
    }
}

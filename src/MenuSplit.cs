using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeddingWitchArchipelago;

/// Splits the menu's Upgrades button into Upgrades and Shop, both opening the same
/// panel filtered to one kind of row.
///
/// The panel is the game's own, not a second one: it already scrolls, styles rows and
/// handles pad navigation, and a shelf is a clone of an upgrade row anyway. All this
/// changes is which rows are active.
///
/// Nothing here can be reached by patching a method — the menu button's onClick is
/// wired in the scene straight to SetActive on the panel, with no game code in
/// between. So the button is found by that wiring: the one whose persistent onClick
/// target is the panel's GameObject. That holds in any language, unlike matching the
/// label.
public static class MenuSplit
{
    public enum Panel { Upgrades, Shop }

    public static Panel Showing { get; private set; } = Panel.Shop;

    private const string ShopButtonName = "ApShopButton";

    /// Off unless a session is live: with no shelves to sell, a second button would
    /// open an empty panel.
    ///
    /// Safe to call repeatedly, and it needs to be: the scene can load before the
    /// connection or after it, and whichever comes second is what makes the buttons
    /// appear. Doing it only on scene load left a late connection with a vanilla menu
    /// and no way back short of relaunching.
    public static void Install(MonoBehaviour host)
    {
        if (host != null) host.StartCoroutine(InstallWhenReady());
    }

    /// The immediate attempt, for when the connection is what arrived second and the menu
    /// is already sitting there built.
    public static void InstallNow()
    {
        if (!ApState.Connected || Installed) return;
        TryInstall();
    }

    private static bool Installed;

    private static IEnumerator InstallWhenReady()
    {
        // The menu builds over the first frames of the scene, and the panel it points
        // at starts inactive, so nothing is findable immediately.
        yield return null;
        yield return new WaitForSeconds(1f);

        if (!ApState.Connected || Installed) yield break;
        TryInstall();
    }

    private static void TryInstall()
    {

        var panel = FindPanel();
        if (panel == null) return;

        var upgrades = FindOpener(panel.gameObject);
        if (upgrades == null)
        {
            Plugin.Logger.LogWarning(
                "[menu] no button opens the upgrade panel — leaving the menu alone");
            return;
        }

        if (upgrades.transform.parent != null
            && upgrades.transform.parent.Find(ShopButtonName) != null)
        {
            Installed = true;
            return;
        }

        Clone(upgrades);
        Installed = true;
    }

    /// The buttons live and die with the scene, so a reload has to build them again.
    public static void Forget() => Installed = false;

    /// The panel is inactive in the menu, so its static instance is still null and
    /// only the all-objects lookup finds it.
    private static UpgradeWitchCanvas FindPanel()
    {
        if (UpgradeWitchCanvas.instance != null) return UpgradeWitchCanvas.instance;

        foreach (var candidate in Resources.FindObjectsOfTypeAll<UpgradeWitchCanvas>())
            if (candidate.gameObject.scene.IsValid())
                return candidate;
        return null;
    }

    /// The button whose in-scene onClick acts on the panel — and that is not itself
    /// inside the panel.
    ///
    /// The panel's own Back button targets it too, with SetActive(false). UnityEvent
    /// exposes a persistent call's target and method name but not its argument, so true
    /// cannot be told from false that way; what separates them is that the opener sits
    /// out in the menu and the closer sits in the panel.
    private static Button FindOpener(GameObject panel)
    {
        Button opener = null;

        foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!button.gameObject.scene.IsValid()) continue;

            var targetsPanel = false;
            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (ReferenceEquals(button.onClick.GetPersistentTarget(i), panel))
                    targetsPanel = true;
            if (!targetsPanel) continue;

            var inside = button.transform.IsChildOf(panel.transform);
            Plugin.Logger.LogInfo(
                $"[menu] '{button.name}' acts on the panel ({(inside ? "inside it" : "in the menu")})");
            if (!inside && opener == null) opener = button;
        }

        return opener;
    }

    private static void Clone(Button upgrades)
    {
        var shop = UnityEngine.Object.Instantiate(upgrades.gameObject, upgrades.transform.parent);
        shop.name = ShopButtonName;
        shop.transform.SetSiblingIndex(upgrades.transform.GetSiblingIndex() + 1);

        // Shop wears the Archipelago logo, because Archipelago is what it now sells;
        // Upgrades wears an inventory icon, because it lists what you already hold. White
        // rather than colour on the logo: the menu art is flat and dark.
        //
        // Only the clone is relabelled. The original's label is already the right word
        // and already localized; rewriting it would mean tearing out its localization.
        var shopButton = shop.GetComponent<Button>();
        Relabel(shop, "Shop");
        Reicon(shopButton, ApSprite.GetWhite());
        Reicon(upgrades, ApSprite.GetInventory());

        // The in-scene SetActive(true) is a persistent listener and so runs before
        // anything added here — which is fine, because the mode is applied again after
        // it. Both buttons get one, so whichever was pressed last decides the filter.
        if (shopButton != null) shopButton.onClick.AddListener(() => Show(Panel.Shop));
        upgrades.onClick.AddListener(() => Show(Panel.Upgrades));

        Plugin.Logger.LogInfo($"[menu] added a Shop button beside {upgrades.name}");
    }

    /// Sets the clone's label, permanently.
    ///
    /// TranslateThis is what kept putting "Upgrade" back: its OnEnable re-reads the text
    /// from the UI string table keyed on its own GameObject name, so it runs again every
    /// time the menu is shown and undoes anything written here. Removing it is what makes
    /// the new label stick — and it costs the clone nothing, since a hand-written English
    /// label has no table entry to be translated from.
    private static void Relabel(GameObject button, string text)
    {
        foreach (var translator in button.GetComponentsInChildren<TranslateThis>(true))
            UnityEngine.Object.Destroy(translator);

        var labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var label in labels) label.text = text;
        Plugin.Logger.LogInfo(
            $"[menu] labelled {button.name} '{text}' across {labels.Length} text object(s)");
    }

    /// The Button's own graphic is its background; the icon is a separate child Image.
    private static void Reicon(Button button, Sprite sprite)
    {
        if (button == null || sprite == null) return;

        foreach (var image in button.GetComponentsInChildren<Image>(true))
        {
            if (ReferenceEquals(image, button.image)) continue;
            Plugin.Logger.LogInfo(
                $"[menu] icon '{image.name}' was '{(image.sprite != null ? image.sprite.name : "none")}'");
            image.sprite = sprite;
            image.color = Color.white;
            // The replacements are not all the shape of the art they stand in for, and the
            // icon rect belongs to the menu's layout rather than to us.
            image.preserveAspect = true;
            return;
        }

        Plugin.Logger.LogWarning($"[menu] {button.name} has no child Image to use as an icon");
    }

    // ---- the filter -----------------------------------------------------

    private static void Show(Panel panel)
    {
        Showing = panel;
        Apply();
    }

    /// Called from the panel's OnEnable as well as the buttons, so it does not matter
    /// which of the two runs first.
    public static void Apply()
    {
        var panel = UpgradeWitchCanvas.instance;
        if (panel == null || !ApState.Connected) return;

        GameObject focus = null;
        foreach (var row in panel.GetComponentsInChildren<UpgradeItem>(true))
        {
            var wanted = ShopPatch.IsShelf(row) == (Showing == Panel.Shop);
            row.gameObject.SetActive(wanted);
            if (wanted && focus == null) focus = row.gameObject;
        }

        if (focus == null) return;

        // OnEnable has already selected the field's old value by now, and the scroll
        // view chases whatever is selected — so the selection is moved, not just the
        // field.
        panel.firstSelect = focus;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                focus, new BaseEventData(EventSystem.current));
    }
}

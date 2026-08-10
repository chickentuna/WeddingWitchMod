using System.Collections.Generic;
using DarkTonic.MasterAudio;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using WeddingWitchArchipelago.Archipelago;

namespace WeddingWitchArchipelago;

/// Turns the witch's upgrade shop into a stall selling Archipelago checks.
///
/// Two things happen to it. The 17 upgrade rows stop being buyable — every level comes
/// from the multiworld now, and a level bought with Love Coins is a level the seed did
/// not place — but they stay on the shelf as a read-only account of what has arrived,
/// since nothing else in the game shows your permanent upgrades. Below them go the AP
/// shelves, which is what Love Coins are for once the upgrades are gone.
///
/// Blocking purchases is unconditional, session or not. The AP shelves only exist
/// while connected.
[HarmonyPatch]
public static class ShopPatch
{
    /// The first shelf costs this, and each one after is 15% more. The curve is the
    /// mod's own — the apworld only decides how many shelves there are.
    private const int FirstPrice = 150;
    private const float PriceGrowth = 1.15f;

    /// The logo at its own pixel size dwarfs an upgrade's icon.
    private const float IconScale = 0.5f;

    /// Which AP shelf a row is, for the rows that are shelves. Rebuilt whenever the
    /// shop is opened, since the rows are recreated with the scene.
    private static readonly Dictionary<UpgradeItem, int> Shelves =
        new Dictionary<UpgradeItem, int>();

    public static int PriceOf(int slot)
    {
        var price = FirstPrice * Mathf.Pow(PriceGrowth, slot - 1);
        return Mathf.RoundToInt(price);
    }

    // ---- purchases ------------------------------------------------------

    /// The one entry point for clicking a row, so it is where both behaviours live.
    [HarmonyPatch(typeof(UpgradeItem), nameof(UpgradeItem.OnClick))]
    [HarmonyPrefix]
    public static bool OnRowClicked(UpgradeItem __instance)
    {
        if (Shelves.TryGetValue(__instance, out var slot)) BuyShelf(__instance, slot);
        return false;
    }

    private static void BuyShelf(UpgradeItem row, int slot)
    {
        if (slot > ApState.Settings.ShopLocations) return;
        var location = Locations.ShopSlot(slot);
        if (location == null || ApState.IsChecked(location)) return;

        var price = PriceOf(slot);
        if (Coin.GetCoin() < price)
        {
            MasterAudio.PlaySound("Cancel");
            return;
        }

        Coin.UpdateCoin(-price);
        ApState.SendCheck(location);
        MasterAudio.PlaySound("Purchase");
        Repaint(row, slot);
    }

    /// Refunding every level would zero what Archipelago granted, and nothing was
    /// spent on upgrades to refund in the first place.
    [HarmonyPatch(typeof(UpgradeWitchCanvas), nameof(UpgradeWitchCanvas.ResetUpgrade))]
    [HarmonyPrefix]
    public static bool BlockRefund() => false;

    [HarmonyPatch(typeof(UpgradeWitchCanvas), nameof(UpgradeWitchCanvas.UpdateResetButton))]
    [HarmonyPostfix]
    public static void HideResetButton(UpgradeWitchCanvas __instance) =>
        __instance.resetButton.gameObject.SetActive(false);

    // ---- the shelves ----------------------------------------------------

    /// UpgradeWitchCanvas.OnEnable collects its rows by GetComponentsInChildren, so
    /// the shelves have to exist by the time it runs — hence a prefix. They are cloned
    /// from an upgrade row, which is what keeps them looking like the game.
    [HarmonyPatch(typeof(UpgradeWitchCanvas), "OnEnable")]
    [HarmonyPrefix]
    public static void BuildShelves(UpgradeWitchCanvas __instance)
    {
        Shelves.Clear();
        if (!ApState.Connected) return;

        var rows = __instance.GetComponentsInChildren<UpgradeItem>(includeInactive: true);

        // Never a shelf: cloning one would copy a row we have already painted, and
        // after the reorder below rows[0] is a shelf.
        UpgradeItem template = null;
        var existing = 0;
        foreach (var row in rows)
        {
            if (IsShelf(row)) existing++;
            else if (template == null) template = row;
        }
        if (template == null) return;

        var parent = template.transform.parent;
        var wanted = ApState.Settings.ShopLocations;

        // Clones survive between openings of the panel while the scene lives, so only
        // the shortfall is built.
        for (var slot = existing + 1; slot <= wanted; slot++)
        {
            var clone = Object.Instantiate(template.gameObject, parent);
            clone.name = ShelfName + slot;

            // Registered before anything else touches it. Instantiate has already run
            // the clone's Awake, which wrote the template upgrade's name straight onto
            // the row, and Start will write its description a frame later — neither goes
            // through SetValue, so both need a repaint that only a registered shelf gets.
            var row = clone.GetComponent<UpgradeItem>();
            if (row == null) continue;
            Shelves[row] = slot;
            Repaint(row, slot);
        }

        Adopt(rows);
        Reorder();
    }

    /// After the shelves exist and after the vanilla OnEnable has made its own
    /// selection, so the menu's filter has the last word on what is showing.
    [HarmonyPatch(typeof(UpgradeWitchCanvas), "OnEnable")]
    [HarmonyPostfix]
    public static void ApplyMenuFilter() => MenuSplit.Apply();

    private const string ShelfName = "ApShelf";

    public static bool IsShelf(UpgradeItem row) => row.name.StartsWith(ShelfName);

    /// Shelves built on an earlier opening of the panel are still in the hierarchy but
    /// no longer in the table, since it is cleared each time the shop opens.
    private static void Adopt(UpgradeItem[] rows)
    {
        foreach (var row in rows)
        {
            if (!IsShelf(row) || Shelves.ContainsKey(row)) continue;
            if (!int.TryParse(row.name.Substring(ShelfName.Length), out var slot)) continue;
            Shelves[row] = slot;
            Repaint(row, slot);
        }
    }

    /// Shelves to the top, in slot order, with the read-only upgrades below them.
    /// Applied ascending because each move shifts the siblings after it.
    private static List<UpgradeItem> Reorder()
    {
        var ordered = new List<KeyValuePair<UpgradeItem, int>>(Shelves);
        ordered.Sort((left, right) => left.Value.CompareTo(right.Value));

        var rows = new List<UpgradeItem>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Key.transform.SetSiblingIndex(i);
            rows.Add(ordered[i].Key);
        }
        return rows;
    }

    // ---- painting -------------------------------------------------------

    /// SetValue owns the level and cost text; Start owns the description. Both have to
    /// be followed, or a shelf keeps whichever upgrade it was cloned from in the parts
    /// the other one doesn't write.
    ///
    /// Two methods rather than two [HarmonyPatch] attributes on one: stacked attributes
    /// merge into a single target, so that would quietly patch only the last named.
    [HarmonyPatch(typeof(UpgradeItem), "SetValue")]
    [HarmonyPostfix]
    public static void AfterSetValue(UpgradeItem __instance) => RepaintIfShelf(__instance);

    [HarmonyPatch(typeof(UpgradeItem), "Start")]
    [HarmonyPostfix]
    public static void AfterStart(UpgradeItem __instance) => RepaintIfShelf(__instance);

    private static void RepaintIfShelf(UpgradeItem row)
    {
        if (Shelves.TryGetValue(row, out var slot))
        {
            Repaint(row, slot);
            return;
        }

        // An upgrade row is a read-out, not an offer. Leaving a price on it only
        // invites a click that does nothing. The level beside it is the part worth
        // keeping.
        if (row.cost == null) return;
        row.cost.text = "";
        SetCoinVisible(row, false);
    }

    /// The coin beside the price, which a blanked price leaves stranded.
    ///
    /// It is not a field on UpgradeItem, and picking it by position does not work: it
    /// shares the row as a parent with the row's own frame, an Image carrying the Slot
    /// sprite. Hiding by parent takes the frame with it. So it is picked by its sprite.
    ///
    /// Set rather than only cleared, because a shelf is cloned from an upgrade row and
    /// would otherwise inherit a coin that had already been hidden.
    private static void SetCoinVisible(UpgradeItem row, bool visible)
    {
        foreach (Transform child in row.transform)
        {
            var image = child.GetComponent<Image>();
            if (image == null || image.sprite == null || image.sprite.name != "Coin") continue;
            image.enabled = visible;
            return;
        }
    }

    /// The level pips belong to an upgrade's 0..max ladder, which a shelf has no
    /// equivalent of.
    [HarmonyPatch(typeof(UpgradeItem), nameof(UpgradeItem.SetLvSlot))]
    [HarmonyPostfix]
    public static void OnPipsPainted(UpgradeItem __instance)
    {
        if (!Shelves.ContainsKey(__instance)) return;
        foreach (var pip in __instance.lvSlotList)
            if (pip != null) pip.gameObject.SetActive(false);
    }

    private static void Repaint(UpgradeItem row, int slot)
    {
        var location = Locations.ShopSlot(slot);
        if (location == null) return;

        var sold = ApState.IsChecked(location);

        if (row.upgradeIcon != null)
        {
            var icon = ApSprite.Get();
            if (icon != null)
            {
                row.upgradeIcon.sprite = icon;
                // Sized off the sprite rather than by scaling what is already there:
                // Repaint runs on every refresh, and halving a size in place compounds.
                row.upgradeIcon.rectTransform.sizeDelta =
                    new Vector2(icon.rect.width, icon.rect.height) * IconScale;
            }
            row.upgradeIcon.color = sold ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        var known = ScoutCache.TryGet(location, out var entry);
        if (row.itemName != null)
            row.itemName.text = known ? entry.ItemName : "Archipelago Item";
        if (row.description != null)
            row.description.text = known ? entry.Receiver + " · " + entry.Kind : location;
        if (row.cost != null)
            row.cost.text = sold ? "SOLD" : PriceOf(slot).ToString();
        SetCoinVisible(row, !sold);
        if (row.curruntLevel != null)
            row.curruntLevel.text = "";
        if (row.maxLevel != null)
            row.maxLevel.text = "";
    }

    // ---- the upgrade rows -----------------------------------------------

    /// UpgradeItem reads its level once, in Awake, so a level arriving while the panel
    /// is open would not show until the scene reloaded.
    public static void RefreshOpenShop()
    {
        var shop = UpgradeWitchCanvas.instance;
        if (shop == null) return;

        foreach (var row in shop.GetComponentsInChildren<UpgradeItem>(includeInactive: true))
        {
            if (row == null || row.researchData == null) continue;

            if (Shelves.TryGetValue(row, out var slot))
            {
                Repaint(row, slot);
                continue;
            }

            row.level = ES3.KeyExists(row.researchData.loadKey, ApProfile.GameDataFile)
                ? ES3.Load<int>(row.researchData.loadKey, ApProfile.GameDataFile)
                : 0;
            Traverse.Create(row).Method("SetValue").GetValue();
            row.SetLvSlot();
        }
    }
}

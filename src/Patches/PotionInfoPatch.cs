using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WeddingWitchArchipelago.Archipelago;

namespace WeddingWitchArchipelago;

/// Names what an unclaimed potion slot is holding for the multiworld.
///
/// The potion itself is unchanged — drinking it is the check, and the banner is only
/// there so the choice between three slots can be made on what they pay out rather
/// than blind. A slot already drunk in this seed shows nothing, which is also how you
/// tell at a glance which of a stage's three you still owe a visit.
[HarmonyPatch]
public static class PotionInfoPatch
{
    /// PotionCanvas.OnEnable calls this to fill the three slots, so it is where the
    /// banners are (re)built. RoadMapCanvas.currentFloor still reads the stage that
    /// produced the screen — see MissionPatch.
    [HarmonyPatch(typeof(PotionManager), nameof(PotionManager.GetRandomEnchantList),
                  new[] { typeof(System.Collections.Generic.List<Potion>) })]
    [HarmonyPostfix]
    public static void OnSlotsRolled(PotionManager __instance)
    {
        var slots = __instance.EnchantSlots;
        if (slots == null) return;

        // With Potion Slot Locations off the whole screen is one check, so it gets one
        // banner. Under the middle card, where it reads as belonging to the screen
        // rather than to a card.
        var perSlot = ApState.Settings.PotionSlotLocations;
        var single = slots.Length / 2;

        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            string location = null;
            if (perSlot)
                location = Locations.Potion(BattleUIManager.difficulty,
                                           RoadMapCanvas.currentFloor, i + 1);
            else if (i == single)
                location = Locations.Potion(BattleUIManager.difficulty,
                                           RoadMapCanvas.currentFloor);
            ApPotionBanner.Attach(slots[i], location);
        }
    }
}

/// The banner. Built from bare uGUI rather than a prefab so the mod stays one file,
/// and parented to the slot so it follows the card's own show/hide and animation.
public class ApPotionBanner : MonoBehaviour
{
    private const float Height = 64f;

    /// Sits under the card. Above it is where the potion's own name and level hearts
    /// already are, and this would cover them.
    private const float Offset = -8f;

    private Image icon;
    private TextMeshProUGUI label;

    public static void Attach(PotionSlotBehaviour slot, string location)
    {
        var banner = slot.GetComponentInChildren<ApPotionBanner>(includeInactive: true)
                     ?? Build(slot);
        if (banner == null) return;

        banner.Show(location, slot);
    }

    private static ApPotionBanner Build(PotionSlotBehaviour slot)
    {
        var parent = slot.transform as RectTransform;
        if (parent == null) return null;

        var root = new GameObject("ApPotionBanner", typeof(RectTransform));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, Offset);
        rect.sizeDelta = new Vector2(0f, Height);

        var backdrop = root.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.62f);
        backdrop.raycastTarget = false;

        var banner = root.AddComponent<ApPotionBanner>();
        banner.icon = BuildIcon(rect);
        banner.label = BuildLabel(rect, slot);
        return banner;
    }

    private static Image BuildIcon(RectTransform parent)
    {
        var node = new GameObject("Icon", typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(6f, 0f);
        rect.sizeDelta = new Vector2(Height - 12f, Height - 12f);

        var image = node.AddComponent<Image>();
        image.sprite = ApSprite.Get();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    /// Borrows the slot's own font so the banner reads as part of the card rather
    /// than as an overlay.
    private static TextMeshProUGUI BuildLabel(RectTransform parent, PotionSlotBehaviour slot)
    {
        var node = new GameObject("Label", typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, worldPositionStays: false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(Height, 4f);
        rect.offsetMax = new Vector2(-6f, -4f);

        var text = node.AddComponent<TextMeshProUGUI>();
        if (slot.EnchantNames != null) text.font = slot.EnchantNames.font;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 26f;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        return text;
    }

    private void Show(string location, PotionSlotBehaviour slot)
    {
        if (location == null || !ApState.Connected || ApState.IsChecked(location))
        {
            gameObject.SetActive(false);
            return;
        }

        // The scout is one batch at connect and can still be in flight, so the
        // banner has to be able to say "something is here" without knowing what.
        if (ScoutCache.TryGet(location, out var entry))
        {
            label.text = $"<b>{entry.ItemName}</b>\n{entry.Receiver} · {entry.Kind}";
        }
        else
        {
            label.text = "<b>Archipelago Item</b>";
        }

        if (icon.sprite == null) icon.sprite = ApSprite.Get();
        gameObject.SetActive(true);
    }
}

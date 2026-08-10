using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeddingWitchArchipelago;

/// Two readouts on the new-game screen: a checked/total count under each difficulty, and a
/// row of witch forms across the bottom showing which have beaten the goal difficulty.
///
/// Without them a difficulty gives no sign of being worth revisiting — its potion screens
/// are finite but nothing says how many are left — and the goal's six separate wins had no
/// record outside the Archipelago log.
///
/// The forms sit apart from the difficulty buttons because they belong to the goal rather
/// than to any one difficulty, and the goal is whichever the seed named.
///
/// Counts are only what a difficulty itself holds — shop shelves belong to none of them.
public static class DifficultyProgress
{
    private const string LabelName = "ApProgressLabel";
    private const string TextName = "ApProgressText";
    private const string GoalName = "ApGoalForms";
    private const float RowHeight = 28f;
    private const float IconSize = 56f;
    private const float IconGap = 8f;

    /// The buttons are found by the handler they call rather than by name or label.
    /// SelectDifficultyCanvas exposes Button fields for hard, nightmare and hell but not
    /// for normal — that one has no padlock, so the game never needed a reference to it.
    /// Every one of them does have an in-scene onClick naming its own start method.
    private static readonly (string Method, Difficulty Difficulty)[] Starters =
    {
        ("StartNormalGameButtonPressed", Difficulty.Normal),
        ("StartHardGameButtonPressed", Difficulty.Hard),
        ("StartNightmareGameButtonPressed", Difficulty.Nightmare),
    };

    private static readonly BodyState[] Forms =
    {
        BodyState.BigBreast, BodyState.SmallBreast, BodyState.Corruption,
        BodyState.Beast, BodyState.Muscle, BodyState.Hip,
    };

    public static void Refresh(SelectDifficultyCanvas screen)
    {
        if (screen == null || !ApState.Connected) return;

        var potionSlots = ApState.Settings.PotionSlotLocations;
        var icons = FormIcons();

        foreach (var starter in Starters)
        {
            var button = FindStarter(screen, starter.Method);
            if (button == null)
            {
                Plugin.Logger.LogWarning($"[difficulty] no button calls {starter.Method}");
                continue;
            }

            var row = Row(button, screen);
            var locations = Locations.AllForDifficulty(starter.Difficulty, potionSlots).ToList();
            var done = locations.Count(ApState.IsChecked);

            row.Find(TextName).GetComponent<TextMeshProUGUI>().text =
                $"{done}/{locations.Count} checks";
        }

        PaintGoal(screen, icons);
    }

    /// The goal's forms, under the button of the difficulty they belong to — which is the
    /// seed's to name, not this screen's.
    private static void PaintGoal(SelectDifficultyCanvas screen,
                                  Dictionary<BodyState, Sprite> icons)
    {
        if (!Locations.TryParseDifficulty(ApState.Settings.GoalDifficulty, out var difficulty))
            return;

        var method = Starters.FirstOrDefault(s => s.Difficulty == difficulty).Method;
        if (method == null) return;

        var button = FindStarter(screen, method);
        if (button == null) return;

        var row = button.transform.Find(GoalName) as RectTransform ?? BuildGoal(button, screen);
        var wanted = ApState.Settings.GoalForms;
        var done = 0;

        for (var i = 0; i < Forms.Length; i++)
        {
            var image = row.Find($"{GoalName}{i}")?.GetComponent<Image>();
            if (image == null) continue;

            // Re-assigned every refresh rather than only when built: the menu these come
            // from need not have been loaded the first time this screen appeared.
            if (icons.TryGetValue(Forms[i], out var sprite)) image.sprite = sprite;
            image.enabled = image.sprite != null;

            var location = Locations.Boss(difficulty, Forms[i]);
            var won = location != null && ApState.IsChecked(location);
            if (won) done++;
            image.color = won ? Color.white : new Color(1f, 1f, 1f, 0.22f);
        }

        var caption = row.Find(TextName)?.GetComponent<TextMeshProUGUI>();
        if (caption != null)
            caption.text = $"Goal — {ApState.Settings.GoalDifficulty} as {done}/{wanted} forms";
    }

    private static RectTransform BuildGoal(Button button, SelectDifficultyCanvas screen)
    {
        var width = Forms.Length * IconSize + (Forms.Length - 1) * IconGap;

        // Below the button and below its check count, centred on it: the forms belong to that
        // difficulty's boss, so they read as part of it.
        var row = New(GoalName, button.transform);
        row.anchorMin = new Vector2(0.5f, 0f);
        row.anchorMax = new Vector2(0.5f, 0f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -(RowHeight + 6f));
        row.sizeDelta = new Vector2(width, RowHeight + IconSize);

        var caption = New(TextName, row);
        caption.anchorMin = new Vector2(0f, 1f);
        caption.anchorMax = new Vector2(1f, 1f);
        caption.pivot = new Vector2(0.5f, 1f);
        caption.anchoredPosition = Vector2.zero;
        caption.sizeDelta = new Vector2(0f, RowHeight);

        var text = caption.gameObject.AddComponent<TextMeshProUGUI>();
        if (screen.stageCounts != null) text.font = screen.stageCounts.font;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 22f;
        text.raycastTarget = false;

        for (var i = 0; i < Forms.Length; i++)
        {
            var slot = New($"{GoalName}{i}", row);
            slot.anchorMin = new Vector2(0f, 0f);
            slot.anchorMax = new Vector2(0f, 0f);
            slot.pivot = new Vector2(0f, 0f);
            slot.anchoredPosition = new Vector2(i * (IconSize + IconGap), 0f);
            slot.sizeDelta = new Vector2(IconSize, IconSize);

            var image = slot.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        return row;
    }

    /// The form art the main menu itself uses, off the six Images SelectModeBehaviour
    /// colours in to show which forms have been unlocked. Read from the live objects rather
    /// than loaded by path, because that screen is what these icons are borrowed from and
    /// they should stay identical to it.
    private static Dictionary<BodyState, Sprite> FormIcons()
    {
        var icons = new Dictionary<BodyState, Sprite>();

        var menu = Resources.FindObjectsOfTypeAll<SelectModeBehaviour>()
            .FirstOrDefault(candidate => candidate.gameObject.scene.IsValid());
        if (menu == null)
        {
            Plugin.Logger.LogWarning("[difficulty] no SelectModeBehaviour — form icons unavailable");
            return icons;
        }

        Add(icons, BodyState.BigBreast, menu.BigBreastSlot);
        Add(icons, BodyState.SmallBreast, menu.SmallBreastSlot);
        Add(icons, BodyState.Corruption, menu.CorruptionSlot);
        Add(icons, BodyState.Beast, menu.BeastSlot);
        Add(icons, BodyState.Muscle, menu.Muscle);
        Add(icons, BodyState.Hip, menu.HipSlot);
        return icons;
    }

    private static void Add(Dictionary<BodyState, Sprite> icons, BodyState form, Image source)
    {
        if (source != null && source.sprite != null) icons[form] = source.sprite;
    }

    private static Button FindStarter(SelectDifficultyCanvas screen, string method)
    {
        foreach (var button in screen.GetComponentsInChildren<Button>(true))
            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (ReferenceEquals(button.onClick.GetPersistentTarget(i), screen)
                    && button.onClick.GetPersistentMethodName(i) == method)
                    return button;
        return null;
    }

    /// Built once per button and reused, since the screen is re-shown rather than rebuilt.
    private static RectTransform Row(Button button, SelectDifficultyCanvas screen)
    {
        var existing = button.transform.Find(LabelName) as RectTransform;
        if (existing != null) return existing;

        // Hung below the button rather than inside it, so nothing covers the art.
        var row = New(LabelName, button.transform);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = new Vector2(0f, -4f);
        row.sizeDelta = new Vector2(0f, RowHeight);

        var textRect = New(TextName, row);
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(4f, 0f);
        textRect.offsetMax = new Vector2(-4f, 0f);

        var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        // The screen's own stage-count text, for the face rather than the colour: that one
        // is a dark red that disappears against the art behind these buttons.
        if (screen.stageCounts != null) text.font = screen.stageCounts.font;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 20f;
        text.raycastTarget = false;

        return row;
    }

    private static RectTransform New(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, worldPositionStays: false);
        return rect;
    }
}

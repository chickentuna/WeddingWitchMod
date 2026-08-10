using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeddingWitchArchipelago;

/// The connection panel, built from the game's own uGUI.
///
/// It was IMGUI first, and IMGUI could not be made to work here: the window drew, buttons
/// lit on hover, and clicks and keystrokes went nowhere, while a focused IMGUI text field
/// swallowed the keyboard so the game stopped responding too. Wedding Witch drives its UI
/// with the new Input System, and IMGUI is served by the legacy one.
///
/// uGUI sidesteps the whole question by being what the game already uses — the same Canvas
/// scaling, the same EventSystem, the same input module. It also works with a gamepad,
/// which IMGUI never would.
public class ConnectionPanel : MonoBehaviour
{
    private const float Width = 560f;
    private const float RowHeight = 46f;

    private GameObject root;
    private TMP_InputField host, port, slot, password;
    private TextMeshProUGUI status;
    private Button connect;
    private bool autoShown;

    private bool Visible => root != null && root.activeSelf;

    private void Update()
    {
        if (Input.GetKeyDown(Plugin.ConnectionUIKey.Value)) Toggle();

        // Only on the menu, never over the intro. The intro activates its skip button two
        // seconds in and selects it, and this window is centred — opening on top of it put
        // the panel over the very thing you press to skip, and took the selection that a
        // keypress would have reached.
        if (autoShown || SceneManager.GetActiveScene().name != "Main") return;
        if (Time.timeSinceLevelLoad < 1f) return;

        autoShown = true;
        if (!ApState.Connected) Show();
    }

    private void Toggle()
    {
        if (Visible) Hide();
        else Show();
    }

    private void Show()
    {
        if (root == null) Build();
        if (root == null) return;

        host.text = Plugin.Host.Value;
        port.text = Plugin.Port.Value.ToString();
        slot.text = Plugin.SlotName.Value;
        password.text = Plugin.Password.Value;
        SetStatus(ApState.Connected
            ? $"Connected — beat {ApState.Settings.GoalDifficulty} in "
              + $"{ApState.Settings.GoalForms} form(s)"
            : "", isError: false);

        root.SetActive(true);

        // Selected explicitly so a gamepad or the keyboard can reach the fields at all;
        // uGUI navigates from whatever the EventSystem has, and that is the game's menu.
        //
        // Whatever was selected is handed back on close. Without that the game is left with
        // nothing focused — the intro selects its skip button once, two seconds in, and never
        // again, so taking that selection away broke skipping the video for good.
        if (EventSystem.current != null)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(host.gameObject);
        }
    }

    private void Hide()
    {
        if (root != null) root.SetActive(false);

        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(
            previousSelection != null && previousSelection.activeInHierarchy
                ? previousSelection
                : null);
        previousSelection = null;
    }

    private GameObject previousSelection;

    // ---- the panel ------------------------------------------------------

    private void Build()
    {
        var font = SceneFont();
        if (font == null)
        {
            Plugin.Logger.LogError("[ui] no TMP font in the scene — cannot build the panel");
            return;
        }

        // Its own Canvas, above the game's, and only raycasting while it is open — a
        // permanent full-screen raycaster would eat clicks meant for the menu.
        root = new GameObject("ApConnectionPanel");
        root.transform.SetParent(transform, worldPositionStays: false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        root.AddComponent<GraphicRaycaster>();

        // Dims what is behind without swallowing it. Raycasting here made the panel modal,
        // and since it opens itself two seconds in — during the intro — that ate the click
        // that skips the video.
        var backdrop = Rect("Backdrop", root.transform);
        Stretch(backdrop);
        var shade = backdrop.gameObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.55f);
        shade.raycastTarget = false;

        var window = Rect("Window", root.transform);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(Width, RowHeight * 9f);
        var frame = window.gameObject.AddComponent<Image>();
        frame.color = new Color(0.09f, 0.05f, 0.10f, 0.98f);

        var y = -RowHeight * 0.5f;
        Label(window, font, "Archipelago", y, 34f, TextAlignmentOptions.Center);
        y -= RowHeight;

        host = Field(window, font, "Server", ref y);
        port = Field(window, font, "Port", ref y);
        slot = Field(window, font, "Slot name", ref y);
        password = Field(window, font, "Password", ref y);

        y -= 6f;
        connect = Action(window, font, "Connect", new Vector2(-Width * 0.22f, y), Connect);
        Action(window, font, "Close", new Vector2(Width * 0.22f, y), Hide);
        y -= RowHeight;

        status = Label(window, font, "", y, 22f, TextAlignmentOptions.Center);
        y -= RowHeight * 0.8f;

        var hint = Label(window, font, $"Press {Plugin.ConnectionUIKey.Value} to toggle",
                         y, 20f, TextAlignmentOptions.Center);
        hint.color = new Color(1f, 1f, 1f, 0.5f);

        // Chained so the keyboard walks the fields in reading order and reaches the button.
        Chain(host, port, slot, password);
        root.SetActive(false);
    }

    /// The game's own font, so the panel reads as part of it rather than as Arial.
    private static TMP_FontAsset SceneFont() =>
        Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
            .Where(text => text != null && text.font != null)
            .Select(text => text.font)
            .FirstOrDefault();

    private TMP_InputField Field(RectTransform parent, TMP_FontAsset font, string caption,
                                 ref float y)
    {
        Label(parent, font, caption, y, 24f, TextAlignmentOptions.Left, indent: 24f,
              width: 190f);

        var box = Rect($"{caption}Field", parent);
        box.anchorMin = box.anchorMax = new Vector2(0f, 1f);
        box.pivot = new Vector2(0f, 0.5f);
        box.anchoredPosition = new Vector2(220f, y);
        box.sizeDelta = new Vector2(Width - 250f, RowHeight - 12f);
        var background = box.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.14f);

        // TMP_InputField needs its parts wired by hand: a masked viewport, the text it
        // edits and a placeholder. Left unset it renders nothing and takes no input.
        var viewport = Rect("Text Area", box);
        Stretch(viewport, 8f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var text = Label(viewport, font, "", 0f, 24f, TextAlignmentOptions.Left);
        Stretch((RectTransform)text.transform);
        var hint = Label(viewport, font, "", 0f, 24f, TextAlignmentOptions.Left);
        Stretch((RectTransform)hint.transform);
        hint.color = new Color(1f, 1f, 1f, 0.35f);

        var field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = hint;
        field.fontAsset = font;
        field.pointSize = 24f;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.caretWidth = 2;
        field.customCaretColor = true;
        field.caretColor = Color.white;
        field.selectionColor = new Color(1f, 1f, 1f, 0.3f);
        field.targetGraphic = background;

        y -= RowHeight;
        return field;
    }

    private Button Action(RectTransform parent, TMP_FontAsset font, string caption,
                          Vector2 offset, UnityEngine.Events.UnityAction onClick)
    {
        var box = Rect($"{caption}Button", parent);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = offset;
        box.sizeDelta = new Vector2(Width * 0.36f, RowHeight - 10f);

        var background = box.gameObject.AddComponent<Image>();
        background.color = new Color(0.72f, 0.16f, 0.34f, 1f);

        var label = Label(box, font, caption, 0f, 26f, TextAlignmentOptions.Center);
        Stretch((RectTransform)label.transform);

        var button = box.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(onClick);
        return button;
    }

    private static TextMeshProUGUI Label(RectTransform parent, TMP_FontAsset font, string value,
                                         float y, float size, TextAlignmentOptions align,
                                         float indent = 0f, float width = 0f)
    {
        var node = Rect(value.Length > 0 ? value : "Text", parent);
        node.anchorMin = node.anchorMax = new Vector2(0f, 1f);
        node.pivot = new Vector2(0f, 0.5f);
        node.anchoredPosition = new Vector2(indent, y);
        node.sizeDelta = new Vector2(width > 0f ? width : Width, RowHeight - 12f);

        var text = node.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }

    private static void Chain(params TMP_InputField[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            var navigation = fields[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            if (i > 0) navigation.selectOnUp = fields[i - 1];
            if (i < fields.Length - 1) navigation.selectOnDown = fields[i + 1];
            fields[i].navigation = navigation;
        }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, worldPositionStays: false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    // ---- connecting -----------------------------------------------------

    private void Connect()
    {
        if (string.IsNullOrWhiteSpace(slot.text))
        {
            SetStatus("Slot name is required.", isError: true);
            return;
        }

        if (!int.TryParse(port.text, out var portNumber) || portNumber <= 0 || portNumber > 65535)
        {
            SetStatus("Port must be a number between 1 and 65535.", isError: true);
            return;
        }

        // Remembered for next launch, so this only has to be typed once.
        Plugin.Host.Value = host.text;
        Plugin.Port.Value = portNumber;
        Plugin.SlotName.Value = slot.text;
        Plugin.Password.Value = password.text;

        SetStatus("Connecting…", isError: false);
        var error = Plugin.Client.Connect(host.text, portNumber, slot.text, password.text);
        if (error != null)
        {
            SetStatus(error, isError: true);
            return;
        }

        SetStatus("Connected.", isError: false);
        Hide();
    }

    private void SetStatus(string message, bool isError)
    {
        if (status != null)
        {
            status.text = message;
            status.color = isError ? new Color(1f, 0.5f, 0.5f) : Color.white;
        }
        if (isError) Plugin.Logger.LogWarning($"[ui] {message}");
    }
}

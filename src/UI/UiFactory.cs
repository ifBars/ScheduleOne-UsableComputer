using System;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Font = Il2CppTMPro.TMP_FontAsset;
using S1Alignment = Il2CppTMPro.TextAlignmentOptions;
using S1FontStyles = Il2CppTMPro.FontStyles;
using S1Overflow = Il2CppTMPro.TextOverflowModes;
using S1Wrapping = Il2CppTMPro.TextWrappingModes;
#elif MONOMELON
using TMPro;
using S1Text = TMPro.TextMeshProUGUI;
using S1Input = TMPro.TMP_InputField;
using S1Font = TMPro.TMP_FontAsset;
using S1Alignment = TMPro.TextAlignmentOptions;
using S1FontStyles = TMPro.FontStyles;
using S1Overflow = TMPro.TextOverflowModes;
using S1Wrapping = TMPro.TextWrappingModes;
#endif

namespace UsableComputer.UI;

internal static class UiFactory
{
    private sealed class Palette
    {
        internal Palette(
            Color desktopBackground,
            Color surface,
            Color surfaceRaised,
            Color surfaceInset,
            Color accent,
            Color accentHighlight,
            Color titleBar,
            Color titleBarDark,
            Color taskbar,
            Color taskbarButton,
            Color startGreen,
            Color textPrimary,
            Color textMuted,
            Color textOnAccent,
            Color success,
            Color danger)
        {
            DesktopBackground = desktopBackground;
            Surface = surface;
            SurfaceRaised = surfaceRaised;
            SurfaceInset = surfaceInset;
            Accent = accent;
            AccentHighlight = accentHighlight;
            TitleBar = titleBar;
            TitleBarDark = titleBarDark;
            Taskbar = taskbar;
            TaskbarButton = taskbarButton;
            StartGreen = startGreen;
            TextPrimary = textPrimary;
            TextMuted = textMuted;
            TextOnAccent = textOnAccent;
            Success = success;
            Danger = danger;
        }

        internal Color DesktopBackground { get; }
        internal Color Surface { get; }
        internal Color SurfaceRaised { get; }
        internal Color SurfaceInset { get; }
        internal Color Accent { get; }
        internal Color AccentHighlight { get; }
        internal Color TitleBar { get; }
        internal Color TitleBarDark { get; }
        internal Color Taskbar { get; }
        internal Color TaskbarButton { get; }
        internal Color StartGreen { get; }
        internal Color TextPrimary { get; }
        internal Color TextMuted { get; }
        internal Color TextOnAccent { get; }
        internal Color Success { get; }
        internal Color Danger { get; }
    }

    private static readonly Palette LightPalette = new(
        new Color(0.24f, 0.55f, 0.79f, 1f),
        new Color(0.86f, 0.85f, 0.77f, 1f),
        new Color(0.96f, 0.95f, 0.88f, 1f),
        new Color(0.99f, 0.99f, 0.97f, 1f),
        new Color(0.08f, 0.31f, 0.78f, 1f),
        new Color(0.22f, 0.49f, 0.91f, 1f),
        new Color(0.05f, 0.29f, 0.75f, 1f),
        new Color(0.03f, 0.19f, 0.56f, 1f),
        new Color(0.04f, 0.24f, 0.66f, 0.99f),
        new Color(0.15f, 0.39f, 0.82f, 1f),
        new Color(0.22f, 0.57f, 0.18f, 1f),
        new Color(0.09f, 0.12f, 0.19f, 1f),
        new Color(0.28f, 0.31f, 0.37f, 1f),
        Color.white,
        new Color(0.05f, 0.45f, 0.12f, 1f),
        new Color(0.75f, 0.12f, 0.14f, 1f));

    private static readonly Palette DarkPalette = new(
        new Color(0.07f, 0.10f, 0.16f, 1f),
        new Color(0.16f, 0.18f, 0.22f, 1f),
        new Color(0.20f, 0.22f, 0.27f, 1f),
        new Color(0.11f, 0.13f, 0.17f, 1f),
        new Color(0.12f, 0.36f, 0.78f, 1f),
        new Color(0.22f, 0.48f, 0.88f, 1f),
        new Color(0.04f, 0.18f, 0.46f, 1f),
        new Color(0.02f, 0.10f, 0.28f, 1f),
        new Color(0.03f, 0.14f, 0.35f, 0.99f),
        new Color(0.10f, 0.27f, 0.58f, 1f),
        new Color(0.16f, 0.43f, 0.15f, 1f),
        new Color(0.92f, 0.94f, 0.98f, 1f),
        new Color(0.64f, 0.68f, 0.75f, 1f),
        Color.white,
        new Color(0.38f, 0.82f, 0.48f, 1f),
        new Color(0.68f, 0.10f, 0.13f, 1f));

    private static Palette Current => GetPalette(PreferencesStore.Appearance.Theme);

    internal static Color DesktopBackground => Current.DesktopBackground;
    internal static Color Surface => Current.Surface;
    internal static Color SurfaceRaised => Current.SurfaceRaised;
    internal static Color SurfaceInset => Current.SurfaceInset;
    internal static Color Accent => Current.Accent;
    internal static Color AccentHighlight => Current.AccentHighlight;
    internal static Color TitleBar => Current.TitleBar;
    internal static Color TitleBarDark => Current.TitleBarDark;
    internal static Color Taskbar => Current.Taskbar;
    internal static Color TaskbarButton => Current.TaskbarButton;
    internal static Color StartGreen => Current.StartGreen;
    internal static Color TextPrimary => Current.TextPrimary;
    internal static Color TextMuted => Current.TextMuted;
    internal static Color TextOnAccent => Current.TextOnAccent;
    internal static Color Success => Current.Success;
    internal static Color Danger => Current.Danger;

    private static S1Font? _font;

    internal static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<RectTransform>();
        gameObject.AddComponent<Image>();
        gameObject.GetComponent<Image>().color = color;
        return gameObject;
    }

    internal static S1Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color color,
        S1Alignment alignment = S1Alignment.Left,
        bool bold = false)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.AddComponent<RectTransform>();
        var label = gameObject.AddComponent<S1Text>();
        label.font = ResolveFont();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = bold ? S1FontStyles.Bold : S1FontStyles.Normal;
        label.textWrappingMode = S1Wrapping.Normal;
        label.overflowMode = S1Overflow.Overflow;
        label.raycastTarget = false;
        return label;
    }

    internal static Button CreateButton(
        Transform parent,
        string name,
        string labelText,
        Color color,
        out S1Text label)
    {
        var gameObject = CreatePanel(parent, name, color);
        var image = gameObject.GetComponent<Image>();
        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.14f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.45f);
        button.colors = colors;

        var shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
        var highlight = gameObject.AddComponent<Outline>();
        highlight.effectColor = new Color(1f, 1f, 1f, 0.34f);
        highlight.effectDistance = new Vector2(-1f, 1f);
        highlight.useGraphicAlpha = true;

        label = CreateText(
            gameObject.transform,
            "Label",
            labelText,
            16f,
            GetButtonTextColor(color),
            S1Alignment.Center);
        Stretch(label.rectTransform, new Vector2(6f, 3f));
        return button;
    }

    internal static S1Input CreateInputField(
        Transform parent,
        string name,
        string initialText,
        string placeholderText,
        bool multiline,
        out S1Text textComponent)
    {
        var gameObject = CreatePanel(parent, name, SurfaceInset);
        gameObject.SetActive(false);
        var input = gameObject.AddComponent<S1Input>();
        input.targetGraphic = gameObject.GetComponent<Image>();

        GameObject viewportObject = new GameObject("TextViewport");
        viewportObject.transform.SetParent(gameObject.transform, false);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        Stretch(viewportRect, new Vector2(10f, 8f));
        viewportObject.AddComponent<RectMask2D>();

        textComponent = CreateText(
            viewportObject.transform,
            "Text",
            initialText,
            17f,
            TextPrimary,
            S1Alignment.TopLeft);
        Stretch(textComponent.rectTransform, Vector2.zero);
        textComponent.textWrappingMode = multiline ? S1Wrapping.Normal : S1Wrapping.NoWrap;

        S1Text placeholder = CreateText(
            viewportObject.transform,
            "Placeholder",
            placeholderText,
            17f,
            TextMuted,
            S1Alignment.TopLeft);
        Stretch(placeholder.rectTransform, Vector2.zero);
        placeholder.fontStyle = S1FontStyles.Italic;

        input.textViewport = viewportRect;
        input.textComponent = textComponent;
        input.placeholder = placeholder;
        input.lineType = multiline
            ? S1Input.LineType.MultiLineNewline
            : S1Input.LineType.SingleLine;
        input.text = initialText;
        input.richText = false;
        input.resetOnDeActivation = false;
        input.caretWidth = 2;
        gameObject.SetActive(true);
        return input;
    }

    internal static void Stretch(RectTransform rectTransform, Vector2 padding)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = padding;
        rectTransform.offsetMax = -padding;
    }

    internal static void SetRect(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    internal static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    internal static void ApplyTheme(GameObject root, DesktopTheme previousTheme, DesktopTheme nextTheme)
    {
        Palette previous = GetPalette(previousTheme);
        Palette next = GetPalette(nextTheme);

        foreach (Image image in root.GetComponentsInChildren<Image>(includeInactive: true))
            image.color = MapColor(image.color, previous, next);

        foreach (S1Text text in root.GetComponentsInChildren<S1Text>(includeInactive: true))
            text.color = MapColor(text.color, previous, next);

        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(includeInactive: true))
        {
            ColorBlock colors = selectable.colors;
            Color normal = MapColor(colors.normalColor, previous, next);
            if (!Approximately(normal, colors.normalColor))
            {
                colors.normalColor = normal;
                colors.highlightedColor = Color.Lerp(normal, Color.white, 0.16f);
                colors.pressedColor = Color.Lerp(normal, Color.black, 0.14f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
            }
            else
            {
                colors.highlightedColor = MapColor(colors.highlightedColor, previous, next);
                colors.pressedColor = MapColor(colors.pressedColor, previous, next);
                colors.selectedColor = MapColor(colors.selectedColor, previous, next);
                colors.disabledColor = MapColor(colors.disabledColor, previous, next);
            }
            selectable.colors = colors;
        }
    }

    private static Palette GetPalette(DesktopTheme theme) =>
        theme == DesktopTheme.Dark ? DarkPalette : LightPalette;

    private static Color MapColor(Color color, Palette previous, Palette next)
    {
        Color[] from =
        {
            previous.DesktopBackground, previous.Surface, previous.SurfaceRaised, previous.SurfaceInset,
            previous.Accent, previous.AccentHighlight, previous.TitleBar, previous.TitleBarDark,
            previous.Taskbar, previous.TaskbarButton, previous.StartGreen, previous.TextPrimary,
            previous.TextMuted, previous.TextOnAccent, previous.Success, previous.Danger,
        };
        Color[] to =
        {
            next.DesktopBackground, next.Surface, next.SurfaceRaised, next.SurfaceInset,
            next.Accent, next.AccentHighlight, next.TitleBar, next.TitleBarDark,
            next.Taskbar, next.TaskbarButton, next.StartGreen, next.TextPrimary,
            next.TextMuted, next.TextOnAccent, next.Success, next.Danger,
        };
        for (int index = 0; index < from.Length; index++)
        {
            if (Approximately(color, from[index]))
                return to[index];
        }

        return color;
    }

    private static bool Approximately(Color left, Color right) =>
        Mathf.Abs(left.r - right.r) < 0.002f &&
        Mathf.Abs(left.g - right.g) < 0.002f &&
        Mathf.Abs(left.b - right.b) < 0.002f &&
        Mathf.Abs(left.a - right.a) < 0.002f;

    private static S1Font? ResolveFont()
    {
        if (_font != null)
            return _font;

        S1Font[] fonts = Resources.FindObjectsOfTypeAll<S1Font>();
        foreach (S1Font font in fonts)
        {
            if (font != null && font.characterLookupTable != null && font.characterLookupTable.Count > 0)
            {
                _font = font;
                break;
            }
        }

        return _font;
    }

    private static Color GetButtonTextColor(Color background)
    {
        float luminance = (background.r * 0.2126f) + (background.g * 0.7152f) + (background.b * 0.0722f);
        return luminance < 0.58f ? TextOnAccent : TextPrimary;
    }
}

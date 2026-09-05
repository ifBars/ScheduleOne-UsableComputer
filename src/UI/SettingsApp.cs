using UsableComputer.API;
using UsableComputer.Logic;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class SettingsApp : IDesktopAppSession
{
    private readonly S1Text _selection;

    internal SettingsApp(DesktopAppContext context)
    {
        S1Text heading = UiFactory.CreateText(
            context.Container,
            "Heading",
            "Display Properties",
            21f,
            UiFactory.TextPrimary,
            GetLeftAlignment(),
            bold: true);
        SetTopRect(heading.rectTransform, 32f, -12f);

        S1Text description = UiFactory.CreateText(
            context.Container,
            "Description",
            "Choose window colors, a background, and your desktop icon layout.",
            13f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        SetTopRect(description.rectTransform, 28f, -46f);

        CreateSectionLabel(context.Container, "Color scheme", -80f);
        CreateChoice(context, "Light", 12f, -108f, () => PreferencesStore.SetTheme(DesktopTheme.Light));
        CreateChoice(context, "Dark", 142f, -108f, () => PreferencesStore.SetTheme(DesktopTheme.Dark));

        CreateSectionLabel(context.Container, "Desktop background", -146f);
        CreateChoice(context, "Rolling hills", 12f, -174f, () => PreferencesStore.SetWallpaper(WallpaperStyle.RollingHills));
        CreateChoice(context, "Blue sky", 142f, -174f, () => PreferencesStore.SetWallpaper(WallpaperStyle.BlueSky));
        CreateChoice(context, "Twilight", 272f, -174f, () => PreferencesStore.SetWallpaper(WallpaperStyle.Twilight));

        CreateSectionLabel(context.Container, "Desktop icons", -210f);
        CreateChoice(context, "Small", 12f, -238f, () => PreferencesStore.SetIconSize(DesktopIconSize.Small));
        CreateChoice(context, "Medium", 142f, -238f, () => PreferencesStore.SetIconSize(DesktopIconSize.Medium));
        CreateChoice(context, "Large", 272f, -238f, () => PreferencesStore.SetIconSize(DesktopIconSize.Large));
        CreateChoice(context, "Arrange by name", 12f, -270f, () => PreferencesStore.ArrangeIcons(DesktopIconOrder.Name));
        CreateChoice(context, "Folders first", 142f, -270f, () => PreferencesStore.ArrangeIcons(DesktopIconOrder.Kind));

        _selection = UiFactory.CreateText(
            context.Container,
            "CurrentSelection",
            string.Empty,
            13f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        _selection.rectTransform.anchorMin = new Vector2(0f, 0f);
        _selection.rectTransform.anchorMax = new Vector2(1f, 0f);
        _selection.rectTransform.pivot = new Vector2(0.5f, 0f);
        _selection.rectTransform.sizeDelta = new Vector2(-24f, 18f);
        _selection.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        PreferencesStore.AppearanceChanged += OnAppearanceChanged;
        PreferencesStore.IconLayoutChanged += RefreshSelection;
        RefreshSelection();
    }

    public void OnOpened() => RefreshSelection();

    public void OnClosed()
    {
    }

    public void OnTick()
    {
    }

    public void Dispose()
    {
        PreferencesStore.AppearanceChanged -= OnAppearanceChanged;
        PreferencesStore.IconLayoutChanged -= RefreshSelection;
    }

    private void OnAppearanceChanged(DesktopAppearance previous, DesktopAppearance next) => RefreshSelection();

    private void CreateChoice(DesktopAppContext context, string label, float x, float y, System.Action action)
    {
        Button button = UiFactory.CreateButton(
            context.Container,
            $"Choice_{label}",
            label,
            UiFactory.SurfaceRaised,
            out S1Text choiceLabel);
        choiceLabel.fontSize = 13f;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(116f, 30f);
        rect.anchoredPosition = new Vector2(x, y);
        context.Bind(button, () =>
        {
            action();
            RefreshSelection();
        });
    }

    private static void CreateSectionLabel(Transform parent, string text, float y)
    {
        S1Text label = UiFactory.CreateText(
            parent,
            $"Section_{text}",
            text,
            15f,
            UiFactory.TextPrimary,
            GetLeftAlignment(),
            bold: true);
        SetTopRect(label.rectTransform, 26f, y);
    }

    private void RefreshSelection()
    {
        DesktopAppearance appearance = PreferencesStore.Appearance;
        _selection.text = $"{appearance.Theme} · {FormatWallpaper(appearance.Wallpaper)} · {PreferencesStore.IconSize} icons · {PreferencesStore.IconOrder} order";
        _selection.color = UiFactory.TextMuted;
    }

    private static string FormatWallpaper(WallpaperStyle style) => style switch
    {
        WallpaperStyle.RollingHills => "Rolling hills",
        WallpaperStyle.BlueSky => "Blue sky",
        _ => "Twilight",
    };

    private static void SetTopRect(RectTransform rect, float height, float y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-24f, height);
        rect.anchoredPosition = new Vector2(0f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
#else
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
#endif
}

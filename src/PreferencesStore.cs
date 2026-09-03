using MelonLoader;
using System;
using UsableComputer.UI;

namespace UsableComputer;

internal static class PreferencesStore
{
    private static MelonPreferences_Entry<string>? _globalNote;
    private static MelonPreferences_Entry<string>? _theme;
    private static MelonPreferences_Entry<string>? _wallpaper;

    internal static event Action<DesktopAppearance, DesktopAppearance>? AppearanceChanged;

    internal static string Note => _globalNote?.Value ?? string.Empty;

    internal static DesktopAppearance Appearance => new(
        Parse(_theme?.Value, DesktopTheme.Light),
        Parse(_wallpaper?.Value, WallpaperStyle.RollingHills));

    internal static void Initialize()
    {
        if (_globalNote != null)
            return;

        MelonPreferences_Category category = MelonPreferences.CreateCategory(Constants.PreferenceCategory);
        _globalNote = category.CreateEntry(
            Constants.NotePreferenceKey,
            string.Empty,
            "A global note shared by every usable computer.");
        _theme = category.CreateEntry(
            Constants.ThemePreferenceKey,
            DesktopTheme.Light.ToString(),
            "Usable Computer desktop color theme.");
        _wallpaper = category.CreateEntry(
            Constants.WallpaperPreferenceKey,
            WallpaperStyle.RollingHills.ToString(),
            "Usable Computer runtime wallpaper style.");
    }

    internal static void SetNote(string value)
    {
        if (_globalNote == null)
            Initialize();

        _globalNote!.Value = value ?? string.Empty;
        MelonPreferences.Save();
    }

    internal static void SetTheme(DesktopTheme theme)
    {
        EnsureInitialized();
        DesktopAppearance previous = Appearance;
        if (previous.Theme == theme)
            return;

        _theme!.Value = theme.ToString();
        SaveAppearance(previous);
    }

    internal static void SetWallpaper(WallpaperStyle wallpaper)
    {
        EnsureInitialized();
        DesktopAppearance previous = Appearance;
        if (previous.Wallpaper == wallpaper)
            return;

        _wallpaper!.Value = wallpaper.ToString();
        SaveAppearance(previous);
    }

    private static void SaveAppearance(DesktopAppearance previous)
    {
        MelonPreferences.Save();
        AppearanceChanged?.Invoke(previous, Appearance);
    }

    private static void EnsureInitialized()
    {
        if (_globalNote == null)
            Initialize();
    }

    private static T Parse<T>(string? value, T fallback)
        where T : struct, Enum
    {
        return Enum.TryParse(value, ignoreCase: true, out T parsed) ? parsed : fallback;
    }
}

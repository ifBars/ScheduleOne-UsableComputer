namespace UsableComputer.UI;

internal enum DesktopTheme
{
    Light,
    Dark,
}

internal enum WallpaperStyle
{
    RollingHills,
    BlueSky,
    Twilight,
}

internal readonly struct DesktopAppearance
{
    internal DesktopAppearance(DesktopTheme theme, WallpaperStyle wallpaper)
    {
        Theme = theme;
        Wallpaper = wallpaper;
    }

    internal DesktopTheme Theme { get; }
    internal WallpaperStyle Wallpaper { get; }
}

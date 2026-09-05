namespace UsableComputer;

internal static class Constants
{
    internal const string ModName = "Usable Computer";
    internal const string ModVersion = "1.0.0";
    internal const string ModAuthor = "Bars";
    internal const string ItemId = "usable_computer";
    internal const string DonorItemId = "launderingstation";
    internal const string DonorBuiltRootName = "LaunderingStation_Built";
    internal const string InteractionMessage = "Use computer";
    internal const float InteractionRange = 5f;
    internal const string InteractionTokenPrefix = "UsableComputer:";
    internal const string StateHostName = "UsableComputer_State";
    internal const string ModelRootName = "UsableComputer_RuntimeModel";
    internal const string CameraAnchorName = "UsableComputer_CameraAnchor";
    internal const string ScreenAnchorName = "UsableComputer_ScreenAnchor";
    internal const string CanvasName = "UsableComputer_DesktopCanvas";
    internal const string NotesAppId = "notes";
    internal const string FilesAppId = "files";
    internal const string CalculatorAppId = "calculator";
    internal const string AboutAppId = "about";
    internal const string JournalAppId = "journal";
    internal const string ProductManagerAppId = "product-manager";
    internal const string AppStudioAppId = "app-studio";
    internal const string SettingsAppId = "settings";
    internal const string DoomAppId = "doom";
    internal const string NestedGameAppId = "schedule-one";
    internal const string PreferenceCategory = "UsableComputer";
    internal const string NotePreferenceKey = "GlobalNote";
    internal const string ThemePreferenceKey = "DesktopTheme";
    internal const string WallpaperPreferenceKey = "DesktopWallpaper";
    internal const string IconSizePreferenceKey = "DesktopIconSize";
    internal const string IconOrderPreferenceKey = "DesktopIconOrder";
    internal const string HardwareShop = "Handy Hank's Hardware";
    internal const string DanHardwareShop = "Dan's Hardware";
    internal const int UiLayer = 5;
    internal const int CanvasSortingOrder = 320;
    internal const float CameraTransitionSeconds = 0.15f;

#if IL2CPPMELON
    internal const string RuntimeName = "IL2CPP";
#else
    internal const string RuntimeName = "Mono";
#endif
}

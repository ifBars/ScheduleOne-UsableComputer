namespace UsableComputer.Tests.Shared;

/// <summary>
/// Full names of Usable Computer types that the live smoke mods resolve by reflection.
/// A stale name still compiles and only fails inside a game run, so update this file whenever a type moves.
/// </summary>
internal static class ModTypeNames
{
    internal const string Constants = "UsableComputer.Constants";
    internal const string PreferencesStore = "UsableComputer.PreferencesStore";
    internal const string DisplayProfile = "UsableComputer.DisplayProfile";
    internal const string DesktopAppRegistry = "UsableComputer.API.DesktopAppRegistry";
    internal const string DesktopShell = "UsableComputer.UI.DesktopShell";
    internal const string VirtualFileSystemService = "UsableComputer.FileSystem.VirtualFileSystemService";
    internal const string UsableComputerController = "UsableComputer.Runtime.UsableComputerController";
    internal const string NativeComputerModelFactory = "UsableComputer.Content.NativeComputerModelFactory";
    internal const string ComputerContentRegistrar = "UsableComputer.Content.ComputerContentRegistrar";
    internal const string BankReportsService = "UsableComputer.Reports.BankReportsService";
    internal const string DealersNativeAdapter = "UsableComputer.Native.DealersNativeAdapter";
    internal const string DeliveriesNativeAdapter = "UsableComputer.Native.DeliveriesNativeAdapter";
    internal const string EggRunNativeAdapter = "UsableComputer.Native.EggRunNativeAdapter";
    internal const string NoodleNativeAdapter = "UsableComputer.Native.NoodleNativeAdapter";
    internal const string DoomWadLocator = "UsableComputer.Doom.DoomWadLocator";
    internal const string LuaAppManager = "UsableComputer.Scripting.LuaAppManager";
    internal const string LuaStorageService = "UsableComputer.Scripting.LuaStorageService";
    internal const string ExternalAppDesktopSession = "UsableComputer.Bridge.ExternalAppDesktopSession";
    internal const string S1ApiDesktopBridge = "UsableComputer.Bridge.S1ApiDesktopBridge";
}

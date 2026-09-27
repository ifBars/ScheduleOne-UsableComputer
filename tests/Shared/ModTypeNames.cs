namespace UsableComputer.Tests.Shared;

/// <summary>
/// Full names of Usable Computer types that the live smoke mods resolve by reflection.
/// A stale name still compiles and only fails inside a game run, so update this file whenever a type moves.
/// </summary>
internal static class ModTypeNames
{
    internal const string Constants = "UsableComputer.Constants";
    internal const string PreferencesStore = "UsableComputer.Shell.PreferencesStore";
    internal const string DisplayProfile = "UsableComputer.Hardware.DisplayProfile";
    internal const string DesktopAppRegistry = "UsableComputer.API.DesktopAppRegistry";
    internal const string DesktopShell = "UsableComputer.Shell.DesktopShell";
    internal const string VirtualFileSystemService = "UsableComputer.FileSystem.VirtualFileSystemService";
    internal const string UsableComputerController = "UsableComputer.Hardware.UsableComputerController";
    internal const string NativeComputerModelFactory = "UsableComputer.Hardware.NativeComputerModelFactory";
    internal const string ComputerContentRegistrar = "UsableComputer.Hardware.ComputerContentRegistrar";
    internal const string BankReportsService = "UsableComputer.Apps.Reports.BankReportsService";
    internal const string DealersNativeAdapter = "UsableComputer.Apps.Dealers.DealersNativeAdapter";
    internal const string DeliveriesNativeAdapter = "UsableComputer.Apps.Deliveries.DeliveriesNativeAdapter";
    internal const string EggRunNativeAdapter = "UsableComputer.Apps.Games.EggRun.EggRunNativeAdapter";
    internal const string NoodleNativeAdapter = "UsableComputer.Apps.Games.Noodle.NoodleNativeAdapter";
    internal const string DoomWadLocator = "UsableComputer.Apps.Games.Doom.DoomWadLocator";
    internal const string LuaAppManager = "UsableComputer.Subsystems.Lua.LuaAppManager";
    internal const string LuaStorageService = "UsableComputer.Subsystems.Lua.LuaStorageService";
    internal const string ExternalAppDesktopSession = "UsableComputer.Subsystems.S1Api.ExternalAppDesktopSession";
    internal const string S1ApiDesktopBridge = "UsableComputer.Subsystems.S1Api.S1ApiDesktopBridge";
}

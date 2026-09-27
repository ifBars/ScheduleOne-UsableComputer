using S1API.Internal.Abstraction;
using S1API.Saveables;

namespace UsableComputer.Subsystems.Lua;

public sealed class UsableComputerLuaStorageSave : Saveable
{
    private static UsableComputerLuaStorageSave? _instance;
    [SaveableField("lua-storage")]
    private LuaStorageSnapshot _snapshot = new();
    public UsableComputerLuaStorageSave() => _instance = this;
    internal static void Capture(LuaStorageSnapshot snapshot)
    {
        if (_instance != null) _instance._snapshot = snapshot;
    }
    protected override void OnCreated() => LuaStorageService.Load(_snapshot);
    protected override void OnLoaded() => LuaStorageService.Load(_snapshot);
}

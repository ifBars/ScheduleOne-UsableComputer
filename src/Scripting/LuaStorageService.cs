using System;
using MoonSharp.Interpreter;
using UsableComputer.Persistence;

#if IL2CPPMELON
using NativeMoney = Il2CppScheduleOne.Money.MoneyManager;
#else
using NativeMoney = ScheduleOne.Money.MoneyManager;
#endif

namespace UsableComputer.Scripting;

internal static class LuaStorageService
{
    private static LuaStorageBook _book = new();
    private static bool _loaded;
    private static bool _active;
    internal static void Prepare() { Stop(); _loaded = false; _book = new(); }
    internal static void Start() => _active = true;
    internal static void Stop() => _active = false;
    internal static void Load(LuaStorageSnapshot snapshot)
    {
        _loaded = false;
        try { _book = new LuaStorageBook(snapshot); _loaded = true; }
        catch (ArgumentException error) { MelonLoader.MelonLogger.Warning($"[Usable Computer] {error.Message}"); }
    }
    internal static string? Get(string app, string key)
    {
        RequireReady();
        try { return _book.Get(app, key); }
        catch (ArgumentException error) { throw new ScriptRuntimeException(error.Message); }
    }
    internal static void Set(string app, string key, string value)
    {
        RequireReady();
        try { _book.Set(app, key, value); }
        catch (ArgumentException error) { throw new ScriptRuntimeException(error.Message); }
        UsableComputerLuaStorageSave.Capture(_book.Snapshot());
    }
    internal static void Delete(string app, string key)
    {
        RequireReady();
        try { _book.Delete(app, key); }
        catch (ArgumentException error) { throw new ScriptRuntimeException(error.Message); }
        UsableComputerLuaStorageSave.Capture(_book.Snapshot());
    }
    private static void RequireReady()
    {
        if (!_active || !_loaded || !NativeMoney.InstanceExists || NativeMoney.Instance == null || !NativeMoney.Instance.IsServerInitialized)
            throw new ScriptRuntimeException("Lua storage requires a loaded host save. Multiplayer client storage is not available.");
    }
}

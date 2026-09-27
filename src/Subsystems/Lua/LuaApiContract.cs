using System;
using MoonSharp.Interpreter;

namespace UsableComputer.Subsystems.Lua;

internal static class LuaApiContract
{
    internal const int Version = 1;
    internal static void Validate(Table definition)
    {
        DynValue version = definition.Get("api_version");
        if (!version.IsNil() && (version.Type != DataType.Number || version.Number != Version))
            throw new ScriptRuntimeException("Unsupported api_version. This installation supports Lua API v1; omit api_version for legacy v1 apps.");
    }

    internal static void BindStorage(Script script, Table computer, Func<string> app,
        Func<string, string, string?> get, Action<string, string, string> set, Action<string, string> delete)
    {
        computer.Set("api_version", DynValue.NewNumber(Version));
        var storage = new Table(script);
        storage.Set("get", DynValue.NewCallback((_, args) =>
        {
            string? value = get(app(), String(args, 0));
            return value == null ? DynValue.Nil : DynValue.NewString(value);
        }));
        storage.Set("set", DynValue.NewCallback((_, args) =>
        {
            set(app(), String(args, 0), String(args, 1));
            return DynValue.Nil;
        }));
        storage.Set("delete", DynValue.NewCallback((_, args) =>
        {
            delete(app(), String(args, 0));
            return DynValue.Nil;
        }));
        computer.Set("storage", DynValue.NewTable(storage));
    }

    private static string String(CallbackArguments args, int index)
    {
        if (args.Count <= index || args[index].Type != DataType.String)
            throw new ScriptRuntimeException("Storage keys and values must be strings.");
        return args[index].String;
    }
}

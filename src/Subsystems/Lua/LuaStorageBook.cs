using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsableComputer.Subsystems.Lua;

internal sealed class LuaStorageSnapshot
{
    public int Version { get; set; } = 1;
    public List<LuaStorageEntry> Entries { get; set; } = new();
}

internal sealed class LuaStorageEntry
{
    public string App { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

internal sealed class LuaStorageBook
{
    private readonly Dictionary<string, Dictionary<string, string>> _apps = new(StringComparer.Ordinal);
    internal const int MaximumAppBytes = 32768;
    internal const int MaximumTotalBytes = 524288;
    private int _totalBytes;

    internal LuaStorageBook(LuaStorageSnapshot? snapshot = null)
    {
        if (snapshot == null) return;
        if (snapshot.Version != 1) throw new ArgumentException("Unsupported Lua storage version; storage is disabled.");
        if (snapshot.Entries == null || snapshot.Entries.Count > 4096) throw new ArgumentException("Invalid Lua storage entries.");
        foreach (LuaStorageEntry entry in snapshot.Entries)
        {
            if (entry == null) throw new ArgumentException("Invalid Lua storage entry.");
            if (Get(entry.App, entry.Key) != null) throw new ArgumentException("Duplicate Lua storage key.");
            Set(entry.App, entry.Key, entry.Value);
        }
    }

    internal string? Get(string app, string key)
    {
        ValidateName(app); ValidateName(key);
        return _apps.TryGetValue(app, out var values) && values.TryGetValue(key, out string? value) ? value : null;
    }

    internal void Set(string app, string key, string value)
    {
        ValidateName(app); ValidateName(key);
        if (value == null || value.Length > 8192 || Encoding.UTF8.GetByteCount(value) > 8192)
            throw new ArgumentException("Storage values are limited to 8192 UTF-8 bytes.");
        bool exists = _apps.TryGetValue(app, out var values);
        values ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (!exists && _apps.Count >= 64) throw new ArgumentException("The save has reached its 64-app storage limit.");
        bool replacing = values.TryGetValue(key, out string? old);
        if (!replacing && values.Count >= 64) throw new ArgumentException("An app may store at most 64 keys.");
        int oldBytes = replacing ? Bytes(key, old!) : 0;
        int delta = Bytes(key, value) - oldBytes;
        int appBytes = values.Sum(pair => Bytes(pair.Key, pair.Value));
        if (appBytes + delta > MaximumAppBytes || _totalBytes + delta > MaximumTotalBytes)
            throw new ArgumentException("Storage quota exceeded (32 KiB per app, 512 KiB per save).");
        values[key] = value;
        _apps[app] = values;
        _totalBytes += delta;
    }

    internal void Delete(string app, string key)
    {
        ValidateName(app); ValidateName(key);
        if (!_apps.TryGetValue(app, out var values) || !values.TryGetValue(key, out string? old)) return;
        _totalBytes -= Bytes(key, old);
        values.Remove(key);
        if (values.Count == 0) _apps.Remove(app);
    }

    internal LuaStorageSnapshot Snapshot() => new()
    {
        Entries = _apps.OrderBy(app => app.Key, StringComparer.Ordinal).SelectMany(app => app.Value.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new LuaStorageEntry { App = app.Key, Key = pair.Key, Value = pair.Value })).ToList()
    };

    private static int Bytes(string key, string value) => Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value);
    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 64 || name.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '-' || c == '_')))
            throw new ArgumentException("Storage names must be 1-64 letters, numbers, dots, hyphens, or underscores.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace UsableComputer.API;

/// <summary>
/// Unity-free storage and ordering contract used by the public desktop registry.
/// </summary>
internal sealed class DesktopAppRegistryStore<T>
    where T : class
{
    private readonly object _sync = new();
    private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);

    internal event Action? Changed;

    internal IReadOnlyList<T> GetAll(Func<T, string> getId)
    {
        if (getId == null)
            throw new ArgumentNullException(nameof(getId));

        lock (_sync)
        {
            return _items.Values
                .OrderBy(getId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    internal void Register(string id, T item)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An app id is required.", nameof(id));
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        lock (_sync)
        {
            if (_items.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"A desktop app with id '{id}' is already registered.");
            }

            _items.Add(id, item);
        }

        Changed?.Invoke();
    }

    internal bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An app id is required.", nameof(id));

        bool removed;
        lock (_sync)
            removed = _items.Remove(id);

        if (removed)
            Changed?.Invoke();

        return removed;
    }

    internal bool TryGet(string id, out T item)
    {
        lock (_sync)
            return _items.TryGetValue(id, out item!);
    }
}

using System;
using System.Collections.Generic;
using S1API.ExternalHosting;
using UsableComputer.API;
using UnityEngine;

namespace UsableComputer.Bridge;

/// <summary>
/// Mirrors explicitly hostable S1API apps into the desktop registry.
/// Catalog events are applied on the next game update to avoid reentering S1API initialization.
/// </summary>
internal static class S1ApiDesktopBridge
{
    private static readonly Dictionary<string, BridgeEntry> Entries = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ReportedDiagnostics = new(StringComparer.Ordinal);
    private static bool _started;
    private static bool _dirty;
    private static bool _suspended;

    internal static void Start()
    {
        if (_started)
            return;

        _started = true;
        _dirty = true;
        _suspended = true;
        ExternalAppCatalog.Changed += MarkDirty;
        DesktopAppRegistry.Changed += MarkDirty;
    }

    internal static void Update()
    {
        if (!_started || _suspended || !_dirty)
            return;

        _dirty = false;
        Reconcile();
    }

    internal static void ClearForSceneChange()
    {
        _suspended = true;
        RemoveAll();
        _dirty = true;
    }

    internal static void ResumeForSave()
    {
        if (!_started)
            return;
        _suspended = false;
        _dirty = true;
    }

    internal static void Stop()
    {
        if (!_started)
            return;

        _started = false;
        ExternalAppCatalog.Changed -= MarkDirty;
        DesktopAppRegistry.Changed -= MarkDirty;
        RemoveAll();
        ReportedDiagnostics.Clear();
        _dirty = false;
        _suspended = false;
    }

    private static void MarkDirty() => _dirty = true;

    private static void Reconcile()
    {
        foreach (ExternalAppDiagnostic diagnostic in ExternalAppCatalog.GetDiagnostics())
        {
            Report($"unsupported:{diagnostic.Family}:{diagnostic.TypeName}",
                $"S1API {diagnostic.Family} app '{diagnostic.TypeName}' is unavailable on the computer: {diagnostic.Message}");
        }

        IReadOnlyList<ExternalAppRegistration> catalog = ExternalAppCatalog.GetAll();
        var desired = new Dictionary<string, ExternalAppRegistration>(StringComparer.Ordinal);
        foreach (ExternalAppRegistration registration in catalog)
        {
            try
            {
                if (!registration.Host.AllowExternalHosting)
                    continue;
                if (!desired.TryAdd(registration.Id, registration))
                    Report($"collision:{registration.Id}",
                        $"S1API external app identity '{registration.Id}' occurs more than once.");
            }
            catch (Exception exception)
            {
                Report($"eligibility:{registration.Id}",
                    $"Could not check external app '{registration.Id}': {exception.Message}");
            }
        }

        foreach (KeyValuePair<string, BridgeEntry> entry in new List<KeyValuePair<string, BridgeEntry>>(Entries))
        {
            if (!desired.TryGetValue(entry.Key, out ExternalAppRegistration? current) ||
                !ReferenceEquals(entry.Value.Registration, current))
            {
                Remove(entry.Key, entry.Value);
            }
        }

        foreach (ExternalAppRegistration registration in desired.Values)
        {
            if (Entries.ContainsKey(registration.Id))
                continue;
            if (DesktopAppRegistry.TryGet(registration.Id, out _))
            {
                Report($"desktop-collision:{registration.Id}",
                    $"External app '{registration.Id}' conflicts with a desktop app. The existing app keeps its ID.");
                continue;
            }

            ExternalAppRegistration source = registration;
            var descriptor = new DesktopAppDescriptor(
                source.Id,
                source.Title,
                string.Empty,
                new Vector2(700f, 470f),
                Vector2.zero,
                context => new ExternalAppDesktopSession(source, context),
                source.ResolveIcon);
            try
            {
                DesktopAppRegistry.Register(descriptor);
                Entries.Add(source.Id, new BridgeEntry(source, descriptor));
            }
            catch (Exception exception)
            {
                Report($"register:{source.Id}",
                    $"Could not register external app '{source.Id}': {exception.Message}");
            }
        }
    }

    private static void RemoveAll()
    {
        foreach (KeyValuePair<string, BridgeEntry> entry in new List<KeyValuePair<string, BridgeEntry>>(Entries))
            Remove(entry.Key, entry.Value);
    }

    private static void Remove(string id, BridgeEntry entry)
    {
        Entries.Remove(id);
        if (DesktopAppRegistry.TryGet(id, out DesktopAppDescriptor current) &&
            ReferenceEquals(current, entry.Descriptor))
        {
            // Unregistering notifies each desktop shell, which closes active sessions.
            DesktopAppRegistry.Unregister(id);
        }
    }

    private static void Report(string key, string message)
    {
        if (ReportedDiagnostics.Add(key))
            MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] {message}");
    }

    private sealed class BridgeEntry
    {
        internal BridgeEntry(ExternalAppRegistration registration, DesktopAppDescriptor descriptor)
        {
            Registration = registration;
            Descriptor = descriptor;
        }

        internal ExternalAppRegistration Registration { get; }
        internal DesktopAppDescriptor Descriptor { get; }
    }
}

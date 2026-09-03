using System;
using System.Collections.Generic;

namespace UsableComputer.API;

/// <summary>
/// Process-local registry for apps available in every placed Usable Computer.
/// </summary>
public static class DesktopAppRegistry
{
    private static readonly DesktopAppRegistryStore<DesktopAppDescriptor> Apps = new();

    static DesktopAppRegistry()
    {
        Apps.Changed += NotifyChanged;
    }

    /// <summary>
    /// Raised after a registration or unregistration. Handlers should be short and main-thread safe.
    /// </summary>
    public static event Action? Changed;

    public static IReadOnlyList<DesktopAppDescriptor> GetAll()
    {
        return Apps.GetAll(app => app.Id);
    }

    public static void Register(DesktopAppDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        Apps.Register(descriptor.Id, descriptor);
    }

    public static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An app id is required.", nameof(id));

        return Apps.Unregister(id);
    }

    internal static bool TryGet(string id, out DesktopAppDescriptor descriptor)
    {
        return Apps.TryGet(id, out descriptor!);
    }

    private static void NotifyChanged()
    {
        Delegate[] handlers = Changed?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Desktop app registry listener failed: {exception.Message}");
            }
        }
    }
}

using System;

namespace UsableComputer.API;

public static class DesktopDriverAppExtensions
{
    /// <summary>Registers a desktop app and removes that exact registration when the owning driver stops.</summary>
    public static void RegisterApp(this DesktopDriverContext context, DesktopAppDescriptor app)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        context.EnsureActive();
        DesktopAppRegistry.Register(app);
        void RemoveOwnedApp()
        {
            if (DesktopAppRegistry.TryGet(app.Id, out DesktopAppDescriptor current) && ReferenceEquals(current, app))
                DesktopAppRegistry.Unregister(app.Id);
        }
        try { context.RegisterCleanup(RemoveOwnedApp); }
        catch { RemoveOwnedApp(); throw; }
    }
}

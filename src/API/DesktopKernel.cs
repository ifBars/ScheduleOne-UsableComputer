using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.Kernel;

namespace UsableComputer.API;

/// <summary>Game-thread entry point for trusted drivers. Services use bounded text protocols, not CLR object sharing.</summary>
public static class DesktopKernel
{
    public const int ApiVersion = 1;
    public const string ClockFormatterService = "desktop.clock.format.v1";
    public const string AppOpenedEvent = "desktop.app.opened.v1";
    public const string SaveLoadedEvent = "game.save.loaded.v1";
    public const string SaveLeavingEvent = "game.save.leaving.v1";
    private static readonly DesktopKernelRuntime Runtime = new();
    private static readonly HashSet<string> AutoStart = new(StringComparer.Ordinal);
    private static bool _booted;
    private static int _threadId;

    public static IReadOnlyList<DesktopDriverInfo> GetDrivers() { CheckThread(); return Runtime.GetDrivers(); }
    public static void Register(DesktopDriverDescriptor driver, bool autoStart = true)
    {
        CheckThread(); Runtime.Register(driver);
        if (autoStart) { AutoStart.Add(driver.Id); if (_booted) Runtime.Start(driver.Id); }
    }
    public static bool Unregister(string id) { CheckThread(); bool removed = Runtime.Unregister(id); if (removed) AutoStart.Remove(id); return removed; }
    public static void Start(string id) { CheckReady(); Runtime.Start(id); }
    public static void Stop(string id) { CheckThread(); Runtime.Stop(id); }
    public static void Restart(string id) { CheckReady(); Runtime.Restart(id); }
    public static bool TryCall(string serviceId, string request, out string response)
    { CheckThread(); return Runtime.TryCall(serviceId, request, out response); }
    public static void Publish(string eventId, string payload) { CheckThread(); Runtime.Publish(eventId, payload); }

    internal static void Boot()
    {
        CheckThread(); if (_booted) return; _booted = true;
        foreach (string id in AutoStart.ToArray()) Runtime.Start(id);
    }
    internal static void Tick(float deltaTime) { if (_booted) Runtime.Tick(deltaTime); }
    internal static void Shutdown()
    {
        CheckThread(); Runtime.Shutdown(); AutoStart.Clear(); _booted = false;
    }
    internal static void SaveLoaded() => Publish(SaveLoadedEvent, "");
    internal static void SaveLeaving() => Publish(SaveLeavingEvent, "");
    private static void CheckReady() { CheckThread(); if (!_booted) throw new InvalidOperationException("The desktop kernel has not booted yet."); }
    private static void CheckThread()
    {
        int current = Environment.CurrentManagedThreadId;
        if (_threadId == 0) _threadId = current;
        if (_threadId != current) throw new InvalidOperationException("Desktop kernel APIs must run on the game thread.");
    }
}

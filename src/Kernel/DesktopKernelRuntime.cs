using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.API;

namespace UsableComputer.Kernel;

/// <summary>Unity-free driver lifecycle and dispatch. Owned by one game-thread facade.</summary>
internal sealed class DesktopKernelRuntime
{
    private sealed class Driver
    {
        internal DesktopDriverDescriptor Descriptor = null!;
        internal DesktopDriverState State;
        internal IDesktopDriver? Instance;
        internal DesktopDriverContext? Context;
        internal string? Error;
    }
    private readonly Dictionary<string, Driver> _drivers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DesktopDriverContext Owner, Func<string, string> Handler)> _services = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(DesktopDriverContext Owner, Action<string> Handler)>> _subscriptions = new(StringComparer.Ordinal);
    private int _callbackDepth;
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    internal const int MaximumPayloadLength = 16384;

    internal IReadOnlyList<DesktopDriverInfo> GetDrivers() => _drivers.Values.OrderBy(driver => driver.Descriptor.Id, StringComparer.Ordinal)
        .Select(driver => new DesktopDriverInfo(driver.Descriptor.Id, driver.Descriptor.Title, driver.State, driver.Error,
            _services.Where(service => ReferenceEquals(service.Value.Owner, driver.Context)).Select(service => service.Key).OrderBy(id => id, StringComparer.Ordinal).ToArray())).ToArray();

    internal void Register(DesktopDriverDescriptor descriptor)
    {
        EnsureLifecycleAllowed();
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        if (_drivers.ContainsKey(descriptor.Id)) throw new InvalidOperationException($"Driver '{descriptor.Id}' is already registered.");
        _drivers.Add(descriptor.Id, new Driver { Descriptor = descriptor });
    }
    internal void Start(string id)
    {
        EnsureLifecycleAllowed();
        Driver driver = Find(id);
        if (driver.State == DesktopDriverState.Running) return;
        driver.Error = null; driver.State = DesktopDriverState.Starting;
        driver.Context = new DesktopDriverContext(this, id);
        try
        {
            Callback(() =>
            {
                driver.Instance = driver.Descriptor.CreateDriver() ?? throw new InvalidOperationException("Driver factory returned null.");
                driver.Instance.Start(driver.Context);
            });
            driver.State = DesktopDriverState.Running;
        }
        catch (Exception exception) { Fault(driver, exception); }
    }
    internal void Stop(string id)
    {
        EnsureLifecycleAllowed();
        Driver driver = Find(id);
        Cleanup(driver);
        driver.State = DesktopDriverState.Stopped;
    }
    internal void Restart(string id) { Stop(id); Start(id); }
    internal bool Unregister(string id)
    {
        EnsureLifecycleAllowed();
        if (!_drivers.ContainsKey(id)) return false;
        Stop(id); return _drivers.Remove(id);
    }
    internal void Shutdown()
    {
        EnsureLifecycleAllowed();
        foreach (string id in _drivers.Keys.Reverse().ToArray()) Stop(id);
        _drivers.Clear();
    }
    internal void Tick(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        foreach (Driver driver in _drivers.Values.ToArray())
        {
            if (driver.State != DesktopDriverState.Running) continue;
            try { Callback(() => driver.Instance!.Tick(deltaTime)); }
            catch (Exception exception) { Fault(driver, exception); }
        }
    }
    internal void Provide(DesktopDriverContext owner, string id, Func<string, string> handler)
    {
        ValidateId(id);
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (_services.ContainsKey(id)) throw new InvalidOperationException($"Service '{id}' is already provided.");
        _services.Add(id, (owner, handler));
        owner.RegisterCleanup(() => _services.Remove(id));
    }
    internal void Subscribe(DesktopDriverContext owner, string eventId, Action<string> handler)
    {
        ValidateId(eventId);
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        if (!_subscriptions.TryGetValue(eventId, out var listeners))
            _subscriptions.Add(eventId, listeners = new());
        var registration = (owner, handler);
        listeners.Add(registration);
        owner.RegisterCleanup(() => { listeners.Remove(registration); if (listeners.Count == 0) _subscriptions.Remove(eventId); });
    }
    internal bool TryCall(string id, string request, out string response)
    {
        ValidateId(id); ValidatePayload(request); response = "";
        if (!_services.TryGetValue(id, out var service) || !IsRunning(service.Owner)) return false;
        try
        {
            string result = "";
            Callback(() => result = service.Handler(request));
            ValidatePayload(result);
            if (!IsRunning(service.Owner)) return false;
            response = result; return true;
        }
        catch (Exception exception) { Fault(Find(service.Owner.DriverId), exception); return false; }
    }
    internal void Publish(string id, string payload)
    {
        ValidateId(id); ValidatePayload(payload);
        if (!_subscriptions.TryGetValue(id, out var listeners)) return;
        foreach (var subscriber in listeners.ToArray())
        {
            if (!IsRunning(subscriber.Owner)) continue;
            try { Callback(() => subscriber.Handler(payload)); }
            catch (Exception exception) { Fault(Find(subscriber.Owner.DriverId), exception); }
        }
    }
    private bool IsRunning(DesktopDriverContext context) => _drivers.TryGetValue(context.DriverId, out Driver? driver)
        && ReferenceEquals(driver.Context, context) && driver.State == DesktopDriverState.Running;
    private Driver Find(string id) => _drivers.TryGetValue(id, out Driver? driver) ? driver : throw new KeyNotFoundException($"Driver '{id}' is not registered.");
    private void Fault(Driver driver, Exception exception)
    {
        driver.Error = exception.GetType().Name + ": " + exception.Message;
        driver.State = DesktopDriverState.Faulted;
        Cleanup(driver);
    }
    private void Cleanup(Driver driver)
    {
        var instance = driver.Instance; var context = driver.Context;
        driver.Instance = null; driver.Context = null;
        void Error(Exception exception) => driver.Error = (driver.Error == null ? "" : driver.Error + "\n") + "Cleanup: " + exception.Message;
        // Revoke service access before user cleanup can call back into the kernel.
        _callbackDepth++;
        try { context?.Revoke(Error); } finally { _callbackDepth--; }
        if (instance != null)
            try { Callback(instance.Dispose); } catch (Exception exception) { Error(exception); }
    }
    private void Callback(Action callback)
    {
        if (_callbackDepth >= 8) throw new InvalidOperationException("Kernel callback recursion limit reached.");
        _callbackDepth++;
        try { callback(); } finally { _callbackDepth--; }
    }
    private void EnsureLifecycleAllowed()
    {
        EnsureThread();
        if (_callbackDepth != 0) throw new InvalidOperationException("Driver lifecycle changes must run outside driver callbacks.");
    }
    internal void EnsureThread()
    {
        if (Environment.CurrentManagedThreadId != _threadId) throw new InvalidOperationException("Driver contexts must run on the game thread.");
    }
    internal static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 96 || !id.Contains('.') || id[0] == '.' || id[id.Length - 1] == '.' || id.Contains(".."))
            throw new ArgumentException("Use a namespaced ID such as author.service.v1.", nameof(id));
        foreach (char c in id)
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '-' && c != '_')
                throw new ArgumentException("IDs use lowercase letters, digits, dots, hyphens, and underscores.", nameof(id));
    }
    private static void ValidatePayload(string payload)
    {
        if (payload == null || payload.Length > MaximumPayloadLength) throw new ArgumentException("Kernel payloads are limited to 16384 characters.");
    }
}

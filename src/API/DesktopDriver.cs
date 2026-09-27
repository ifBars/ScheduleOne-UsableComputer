using System;
using System.Collections.Generic;
using UsableComputer.Kernel;

namespace UsableComputer.API;

/// <summary>A trusted, process-wide service module. All callbacks run on the game thread.</summary>
public interface IDesktopDriver : IDisposable
{
    void Start(DesktopDriverContext context);
    void Tick(float deltaTime);
}

public enum DesktopDriverState { Stopped, Starting, Running, Faulted }

public sealed class DesktopDriverDescriptor
{
    public DesktopDriverDescriptor(string id, string title, Func<IDesktopDriver> createDriver, int apiVersion = 1)
    {
        DesktopKernelRuntime.ValidateId(id);
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A driver title is required.", nameof(title));
        if (apiVersion != 1) throw new ArgumentOutOfRangeException(nameof(apiVersion), "This kernel supports driver API version 1.");
        Id = id; Title = title.Trim(); CreateDriver = createDriver ?? throw new ArgumentNullException(nameof(createDriver));
    }
    public string Id { get; }
    public string Title { get; }
    public Func<IDesktopDriver> CreateDriver { get; }
}

/// <summary>A detached driver status snapshot.</summary>
public sealed class DesktopDriverInfo
{
    internal DesktopDriverInfo(string id, string title, DesktopDriverState state, string? error, string[] services)
    { Id = id; Title = title; State = state; LastError = error; Services = Array.AsReadOnly(services); }
    public string Id { get; }
    public string Title { get; }
    public DesktopDriverState State { get; }
    public string? LastError { get; }
    public IReadOnlyList<string> Services { get; }
}

/// <summary>Resources registered through this context are revoked when this driver instance stops.</summary>
public sealed class DesktopDriverContext
{
    private readonly DesktopKernelRuntime _kernel;
    private readonly List<Action> _cleanup = new();
    private bool _revoked;
    internal DesktopDriverContext(DesktopKernelRuntime kernel, string driverId) { _kernel = kernel; DriverId = driverId; }
    public string DriverId { get; }

    public void RegisterCleanup(Action cleanup)
    {
        EnsureActive(); _cleanup.Add(cleanup ?? throw new ArgumentNullException(nameof(cleanup)));
    }
    /// <summary>Provides a versioned, namespaced text service. The provider owns its request/response schema.</summary>
    public void ProvideService(string id, Func<string, string> handler)
    {
        EnsureActive(); _kernel.Provide(this, id, handler);
    }
    public void Subscribe(string eventId, Action<string> handler)
    {
        EnsureActive(); _kernel.Subscribe(this, eventId, handler);
    }
    public void Publish(string eventId, string payload) { EnsureActive(); _kernel.Publish(eventId, payload); }
    public bool TryCall(string serviceId, string request, out string response) { EnsureActive(); return _kernel.TryCall(serviceId, request, out response); }
    internal void EnsureActive() { _kernel.EnsureThread(); if (_revoked) throw new ObjectDisposedException(nameof(DesktopDriverContext)); }
    internal void Revoke(Action<Exception> reportError)
    {
        if (_revoked) return;
        _revoked = true;
        for (int i = _cleanup.Count - 1; i >= 0; i--)
            try { _cleanup[i](); } catch (Exception exception) { reportError(exception); }
        _cleanup.Clear();
    }
}

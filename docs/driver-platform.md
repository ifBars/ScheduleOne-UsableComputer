# Drivers and the desktop kernel

Driver API v1 provides live background services and desktop customization for C# mods. Reference the matching Usable Computer assembly and register a driver from your mod. Open **System Monitor** on the computer to inspect services, stop or start drivers, restart them, and read faults.

## Lifecycle

The kernel is shared across computers. A computer's [Shutdown and Restart](computer-power.md) controls close its app windows and change its power screen; they do not stop shared drivers. Use System Monitor for driver lifecycle operations.

A player can open System Monitor to see running drivers, their services, and any fault. They can stop, start, or restart a driver without leaving the save. Mod authors can build background tools that keep working while no computer window is open and share services with desktop apps. A driver can customize desktop behavior through supported hooks rather than patching private shell objects.

Drivers registered with `autoStart: true` start when the kernel boots, or immediately when registered after boot. A descriptor's factory must create a fresh instance for each start. Driver registration and enabled state are process-local; restarting the game uses the registering mod's defaults.

- A versioned public driver descriptor and lifecycle: register, start, tick, stop, unregister. Registration can occur before or after the kernel starts. Tick runs on the game thread and uses unscaled time.
- A driver context owns service registrations, event subscriptions, desktop apps, and cleanup actions. Stopping or faulting the driver removes those resources in reverse order. A failed start rolls back partial registration.
- Namespaced service and event IDs reject collisions. Consumers receive an explicit unavailable result when a driver stops. No stale service reference should silently continue after its owner unloads.
- Driver failures become visible state and diagnostics. A bad callback should not prevent other drivers from ticking or cleaning up. A faulted driver requires an explicit restart.
- Save transitions are explicit lifecycle events. Process-wide driver registration is distinct from save-owned game data. Drivers must not retain native scene objects across a transition.
- System Monitor provides live state, services, fault details, and start/stop/restart controls.
- The [session-clock example](../examples/DriverSample) replaces the taskbar clock with elapsed session time and provides a statistics service. Stop it to restore the game clock, or restart it to reset its state.

```csharp
DesktopKernel.Register(new DesktopDriverDescriptor(
    "my-mod.tools", "My tools", () => new MyDriver()));

sealed class MyDriver : IDesktopDriver
{
    public void Start(DesktopDriverContext context)
    {
        context.ProvideService("my-mod.echo.v1", request => request);
        context.Subscribe(DesktopKernel.SaveLeavingEvent, _ => ClearSceneReferences());
        context.RegisterCleanup(ReleaseOwnedResources);
        // context.RegisterApp(descriptor) owns and removes that exact app registration.
    }
    public void Tick(float deltaTime) { }
    public void Dispose() { }
    private void ClearSceneReferences() { }
    private void ReleaseOwnedResources() { }
}
```

Call `DesktopKernel.Unregister(id)` when your mod unloads. All APIs run on the game thread. Start/stop/restart/register/unregister must run outside driver callbacks; reentrant lifecycle changes fault the calling driver instead of mutating a driver mid-callback. System Monitor invokes lifecycle operations outside callbacks.

Cleanup actions run in reverse registration order, followed by the driver's `Dispose`. The context is revoked first: do not use it from cleanup or `Dispose`. One failed cleanup does not skip the rest. Resources acquired without registering cleanup remain your mod's responsibility.

## Services and events

Services have one provider. IDs use lowercase letters, digits, dots, hyphens, and underscores, with a namespace such as `my-mod.stats.v1`. Duplicate IDs fail rather than replacing another driver. App code calls `DesktopKernel.TryCall(id, request, out response)`; false means the service is unavailable or its provider faulted. Requests and responses are strings limited to 16,384 characters. Define and version your own schema, for example JSON. A null or oversized response faults the provider. No provider objects are handed to callers, so a stopped provider cannot leave a stale callable handle.

Events use the same ID and payload limits. Call `context.Subscribe` for automatically owned subscriptions. `Publish` uses a listener snapshot, skips stopped owners, and continues to healthy listeners after one fails. Nested dispatch is limited to eight callbacks.

| Contract | Payload / result |
| --- | --- |
| `DesktopKernel.SaveLoadedEvent` | Empty payload after the game save loads. |
| `DesktopKernel.SaveLeavingEvent` | Empty payload before a scene transition; discard native scene references. |
| `DesktopKernel.AppOpenedEvent` | App ID when the desktop creates a window. Focusing an existing window is not another open. |
| `DesktopKernel.ClockFormatterService` | Request is the native formatted game time. Return one line of 1–12 characters to replace the taskbar clock. Invalid formatting falls back to game time. The hook is called during desktop updates, so keep it cheap. |

These are interoperability conventions among trusted mods, not authenticated messages. One driver may provide the clock formatter at a time. Unloading its provider immediately restores the default clock.

Build the example with `dotnet build examples/DriverSample -c Mono` or `-c Il2cpp`, then install the matching sample DLL alongside Usable Computer in `Mods`. The example does not require separate image assets or game assembly redistribution.

## Boundaries

C# drivers are trusted MelonLoader mods with full game-process access; the kernel is a lifecycle and interoperability layer, not an isolation boundary. The first implementation does not promise unloading a .NET assembly, interrupting an infinite C# loop, or undoing arbitrary native mutations.

Lua keeps its existing sandbox and execution limits. Drivers may eventually expose explicit Lua capabilities, but registering a service must not automatically grant arbitrary CLR objects or methods to scripts. Virtual filesystem mounts need their own path, capacity, save, and teardown contract before they are exposed.

## Verification

Run `dotnet run --project tests/UsableComputer.KernelVerifier` for lifecycle, rollback, duplicate services, revocation, callback faults, reentrant changes, and repeated restart. `Run-VfsSmoke.ps1 -Drivers` exercises a real driver through System Monitor, clock customization and restoration, service calls, owned-app cleanup, and fault isolation in a disposable save. Build and run both runtime configurations; inspect the actual CRT captures.

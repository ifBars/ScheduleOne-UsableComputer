using UsableComputer.API;
using UsableComputer.Kernel;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid input accepted."); }
var kernel = new DesktopKernelRuntime();
var cleanup = new List<int>();
DesktopDriverContext? retired = null;
int events = 0, starts = 0, ticks = 0;
kernel.Register(new DesktopDriverDescriptor("test.main", "Main", () => new Driver(context =>
{
    starts++; retired = context;
    context.RegisterCleanup(() => cleanup.Add(1));
    context.RegisterCleanup(() => cleanup.Add(2));
    context.ProvideService("test.echo.v1", request => "echo:" + request);
    context.Subscribe("test.event", _ => events++);
}, _ => ticks++)));
kernel.Start("test.main"); kernel.Start("test.main");
Check(starts == 1 && kernel.GetDrivers()[0].State == DesktopDriverState.Running, "Start is not idempotent.");
bool wrongThreadRejected = Task.Run(() =>
{
    try { retired!.ProvideService("test.thread", _ => ""); return false; }
    catch (InvalidOperationException) { return true; }
}).GetAwaiter().GetResult();
Check(wrongThreadRejected, "Driver context accepted a worker-thread mutation.");
Check(kernel.TryCall("test.echo.v1", "hello", out string response) && response == "echo:hello", "Service dispatch failed.");
kernel.Publish("test.event", ""); kernel.Tick(0.1f);
Check(events == 1 && ticks == 1, "Event or background tick failed.");
var snapshot = kernel.GetDrivers()[0];
kernel.Stop("test.main");
Check(cleanup.SequenceEqual(new[] { 2, 1 }), "Cleanup order changed.");
Check(!kernel.TryCall("test.echo.v1", "", out _), "Stopped service still callable.");
kernel.Publish("test.event", ""); Check(events == 1, "Stopped subscription still active.");
Check(snapshot.Services.Count == 1 && kernel.GetDrivers()[0].Services.Count == 0, "Snapshot aliases live service state.");
bool revoked = false;
try { retired!.ProvideService("test.leak", _ => ""); } catch (ObjectDisposedException) { revoked = true; }
Check(revoked, "Retired driver context can register resources.");
for (int i = 0; i < 5; i++) kernel.Restart("test.main");
kernel.Publish("test.event", ""); Check(events == 2, "Restart accumulated subscriptions.");

kernel.Register(new DesktopDriverDescriptor("test.fail", "Failed start", () => new Driver(context =>
{
    context.ProvideService("test.partial", _ => "leak");
    throw new InvalidOperationException("start failed");
})));
kernel.Start("test.fail");
Check(kernel.GetDrivers().Single(d => d.Id == "test.fail").State == DesktopDriverState.Faulted && !kernel.TryCall("test.partial", "", out _), "Failed start did not roll back.");
kernel.Register(new DesktopDriverDescriptor("test.collision", "Collision", () => new Driver(context => context.ProvideService("test.echo.v1", _ => "wrong"))));
kernel.Start("test.collision");
Check(kernel.TryCall("test.echo.v1", "ok", out response) && response == "echo:ok", "Collision replaced another driver's service.");

kernel.Register(new DesktopDriverDescriptor("test.tick", "Bad tick", () => new Driver(_ => { }, _ => throw new Exception("tick failed"))));
kernel.Start("test.tick"); kernel.Tick(1);
Check(kernel.GetDrivers().Single(d => d.Id == "test.tick").State == DesktopDriverState.Faulted && ticks == 2, "Tick failure affected healthy drivers.");
kernel.Register(new DesktopDriverDescriptor("test.reentrant", "Reentrant", () => new Driver(_ => kernel.Stop("test.main"))));
kernel.Start("test.reentrant");
Check(kernel.GetDrivers().Single(d => d.Id == "test.reentrant").State == DesktopDriverState.Faulted && kernel.TryCall("test.echo.v1", "", out _), "Reentrant lifecycle corrupted another driver.");
kernel.Register(new DesktopDriverDescriptor("test.service", "Bad service", () => new Driver(context =>
{
    context.ProvideService("test.throw", _ => throw new Exception("service failed"));
    context.Subscribe("test.event", _ => throw new Exception("stale listener"));
})));
kernel.Start("test.service");
Check(!kernel.TryCall("test.throw", "", out _) && kernel.GetDrivers().Single(d => d.Id == "test.service").State == DesktopDriverState.Faulted, "Service failure did not fault its owner.");
kernel.Register(new DesktopDriverDescriptor("test.cleanup", "Bad cleanup", () => new Driver(context =>
{
    context.RegisterCleanup(() => cleanup.Add(3));
    context.RegisterCleanup(() => throw new Exception("cleanup failed"));
})));
kernel.Start("test.cleanup"); kernel.Stop("test.cleanup");
Check(cleanup.Last() == 3 && kernel.GetDrivers().Single(d => d.Id == "test.cleanup").LastError!.Contains("cleanup failed"), "Cleanup failure prevented remaining cleanup.");
foreach (string id in new[] { "", "name", ".", ".name", "name.", "test..name", "Test.name", "test/name" })
    Reject(() => new DesktopDriverDescriptor(id, "Invalid", () => new Driver(_ => { })));
Reject(() => new DesktopDriverDescriptor("test.version", "Version", () => new Driver(_ => { }), 2));
Reject(() => kernel.TryCall("test.echo.v1", new string('x', 16385), out _));
kernel.Shutdown(); Check(kernel.GetDrivers().Count == 0 && !kernel.TryCall("test.echo.v1", "", out _), "Shutdown left driver resources.");
Console.WriteLine($"PASS | Kernel verifier | {assertions} assertions");

file sealed class Driver(Action<DesktopDriverContext> start, Action<float>? tick = null) : IDesktopDriver
{
    public void Start(DesktopDriverContext context) => start(context);
    public void Tick(float deltaTime) => tick?.Invoke(deltaTime);
    public void Dispose() { }
}

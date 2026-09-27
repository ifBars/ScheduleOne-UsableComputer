using System.Collections;
using System.Reflection;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#else
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunDriversScenario()
    {
        int ticks = 0, events = 0, cleanup = 0;
        const string id = "smoke.kernel";
        try
        {
            CloseAllWindows();
            DesktopKernel.Register(new DesktopDriverDescriptor(id, "Smoke clock driver", () => new SmokeDriver(context =>
            {
                context.ProvideService(DesktopKernel.ClockFormatterService, _ => "DRIVER");
                context.ProvideService("smoke.echo.v1", request => request);
                context.Subscribe(DesktopKernel.AppOpenedEvent, _ => events++);
                context.RegisterCleanup(() => cleanup++);
                context.RegisterApp(new DesktopAppDescriptor("smoke-owned-app", "Driver owned app", "", new Vector2(320, 220), Vector2.zero,
                    _ => new EmptyDriverApp()));
            }, _ => ticks++)));
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "system-monitor" });
        }
        catch (Exception exception) { Fail("Driver setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(1.2f);
        try
        {
            Require(ticks > 0 && events == 1, "Driver did not receive background ticks or the app-open event.");
            Require(ReadDesktopClock() == "DRIVER", "Driver did not customize the taskbar clock.");
            Require(DesktopKernel.TryCall("smoke.echo.v1", "hello", out string echo) && echo == "hello", "Driver service did not respond.");
        }
        catch (Exception exception) { Fail("Driver service failed", Unwrap(exception)); yield break; }
        string screenshot = Path.Combine(_outputDirectory, "drivers-running.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        int ticksBeforeClose = ticks;
        try { _controller!.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_controller, null); }
        catch (Exception exception) { Fail("Driver background setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.5f);
        try
        {
            Require(ticks > ticksBeforeClose, "Driver stopped ticking when the computer closed.");
            _controller!.GetType().GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_controller, null);
        }
        catch (Exception exception) { Fail("Driver background tick failed", Unwrap(exception)); yield break; }
        yield return null;
        try { GameObject.Find("DriverToggle_" + id).GetComponent<Button>().onClick.Invoke(); }
        catch (Exception exception) { Fail("Driver stop UI failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            Require(cleanup == 1 && !DesktopKernel.TryCall("smoke.echo.v1", "", out _), "Stopped driver retained resources.");
            Require(!DesktopAppRegistry.GetAll().Any(app => app.Id == "smoke-owned-app"), "Owned app survived driver stop.");
            Require(ReadDesktopClock() != "DRIVER", "Stopping driver did not restore the native clock.");
            GameObject.Find("DriverRestart_" + id).GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Driver restart failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            Require(ReadDesktopClock() == "DRIVER" && cleanup == 1, "Restart did not create a clean driver.");
            DesktopKernel.Register(new DesktopDriverDescriptor("smoke.fault", "Faulting test driver", () => new SmokeDriver(
                context => context.ProvideService("smoke.fault.v1", _ => "unavailable after fault"), _ => throw new Exception("Expected smoke fault"))));
        }
        catch (Exception exception) { Fail("Driver fault setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            var fault = DesktopKernel.GetDrivers().Single(driver => driver.Id == "smoke.fault");
            Require(fault.State == DesktopDriverState.Faulted && fault.LastError!.Contains("Expected smoke fault"), "Driver fault was not recorded.");
            Require(!DesktopKernel.TryCall("smoke.fault.v1", "", out _) && ReadDesktopClock() == "DRIVER", "Driver fault affected other services.");
        }
        catch (Exception exception) { Fail("Driver isolation failed", Unwrap(exception)); yield break; }
        screenshot = Path.Combine(_outputDirectory, "drivers-fault.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            DesktopKernel.Unregister("smoke.fault"); DesktopKernel.Unregister(id);
            Require(cleanup == 2, "Restart cleanup did not run exactly once per driver instance.");
            LoggerInstance.Msg($"[UsableComputerDriversSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Services=True ClockHook=True StopRestart=True OwnedAppCleanup=True FaultIsolation=True");
        }
        catch (Exception exception) { Fail("Driver cleanup failed", Unwrap(exception)); }
    }
    private string ReadDesktopClock() => ((S1Text)_desktop!.GetType().GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!).text;
    private sealed class SmokeDriver(Action<DesktopDriverContext> start, Action<float> tick) : IDesktopDriver
    {
        public void Start(DesktopDriverContext context) => start(context);
        public void Tick(float deltaTime) => tick(deltaTime);
        public void Dispose() { }
    }
    private sealed class EmptyDriverApp : IDesktopAppSession
    {
        public void OnOpened() { } public void OnClosed() { } public void OnTick() { } public void Dispose() { }
    }
}

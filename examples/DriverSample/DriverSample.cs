using System.Globalization;
using MelonLoader;
using UsableComputer.API;

[assembly: MelonInfo(typeof(UsableComputer.DriverSample.Mod), "Usable Computer Driver Sample", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.DriverSample;

public sealed class Mod : MelonMod
{
    private const string DriverId = "sample.session-clock";
    public override void OnInitializeMelon() => DesktopKernel.Register(new DesktopDriverDescriptor(
        DriverId, "Session clock", () => new SessionClockDriver()));
    public override void OnDeinitializeMelon() => DesktopKernel.Unregister(DriverId);
}

/// <summary>Replaces the taskbar clock with session elapsed time; stop it in System Monitor to restore game time.</summary>
internal sealed class SessionClockDriver : IDesktopDriver
{
    private float _seconds;
    private int _openedApps;

    public void Start(DesktopDriverContext context)
    {
        context.ProvideService(DesktopKernel.ClockFormatterService, _ =>
        {
            int seconds = (int)Math.Min(_seconds, 359999);
            return $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        });
        context.ProvideService("sample.session.stats.v1", _ =>
            "{\"seconds\":" + _seconds.ToString("F1", CultureInfo.InvariantCulture) + ",\"appsOpened\":" + _openedApps + "}");
        context.Subscribe(DesktopKernel.AppOpenedEvent, _ => _openedApps++);
        context.Subscribe(DesktopKernel.SaveLoadedEvent, _ => { _seconds = 0; _openedApps = 0; });
    }
    public void Tick(float deltaTime) => _seconds += deltaTime;
    public void Dispose() { }
}

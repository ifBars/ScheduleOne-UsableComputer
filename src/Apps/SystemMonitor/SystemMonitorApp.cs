using System;
using System.Linq;
using UsableComputer.API;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using S1Alignment = Il2CppTMPro.TextAlignmentOptions;
#else
using S1Text = TMPro.TextMeshProUGUI;
using S1Alignment = TMPro.TextAlignmentOptions;
#endif

namespace UsableComputer.Apps.SystemMonitor;

internal sealed class SystemMonitorApp : IDesktopAppSession
{
    private readonly RectTransform _rows;
    private readonly S1Text _status;
    private UiListenerRegistry _listeners = new();
    private string _key = "";
    private float _nextRefresh;
    private bool _disposed;

    internal SystemMonitorApp(DesktopAppContext context)
    {
        Text(context.Container, "KernelHeading", "System Monitor", 22, 12, 8, 640, 34);
        Text(context.Container, "KernelDescription", "Drivers provide background services and customize the desktop.", 14, 12, 46, 640, 28);
        var viewport = UiFactory.CreatePanel(context.Container, "DriverViewport", UiFactory.SurfaceInset);
        var viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12, 56); viewportRect.offsetMax = new Vector2(-12, -84);
        viewport.AddComponent<RectMask2D>();
        var content = new GameObject("DriverRows"); content.transform.SetParent(viewport.transform, false);
        _rows = content.AddComponent<RectTransform>(); _rows.anchorMin = new Vector2(0, 1); _rows.anchorMax = Vector2.one;
        _rows.pivot = new Vector2(0.5f, 1); _rows.sizeDelta = Vector2.zero;
        var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewportRect; scroll.content = _rows;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
        _status = Text(context.Container, "KernelStatus", "Driver API v1. C# drivers are trusted game mods.", 12, 12, 0, 640, 38);
        _status.rectTransform.anchorMin = Vector2.zero; _status.rectTransform.anchorMax = new Vector2(1, 0);
        _status.rectTransform.pivot = Vector2.zero; _status.rectTransform.offsetMin = new Vector2(12, 8); _status.rectTransform.offsetMax = new Vector2(-12, 46);
        UiFactory.SetLayerRecursively(context.Container.gameObject, Constants.UiLayer);
    }
    public void OnOpened() => Refresh();
    public void OnClosed() { }
    public void OnTick() { if (!_disposed && Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 0.5f; Refresh(); } }
    public void Dispose() { if (_disposed) return; _disposed = true; _listeners.Dispose(); }

    private void Refresh()
    {
        var drivers = DesktopKernel.GetDrivers();
        string key = string.Join("|", drivers.Select(d => d.Id + d.State + d.LastError + string.Join(",", d.Services)));
        if (key == _key && _rows.childCount > 0) return;
        _key = key; _listeners.Dispose(); _listeners = new();
        for (int i = _rows.childCount - 1; i >= 0; i--)
        { var child = _rows.GetChild(i); child.SetParent(null, false); UnityEngine.Object.Destroy(child.gameObject); }
        _rows.sizeDelta = new Vector2(0, Math.Max(80, drivers.Count * 132));
        if (drivers.Count == 0) Text(_rows, "NoDrivers", "No drivers installed. Add a mod using the driver API to get started.", 15, 10, 12, 610, 55);
        for (int i = 0; i < drivers.Count; i++)
        {
            DesktopDriverInfo driver = drivers[i];
            var panel = UiFactory.CreatePanel(_rows, "Driver_" + driver.Id, UiFactory.SurfaceRaised);
            RectTransform rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(0, 128); rect.anchoredPosition = new Vector2(0, -i * 132);
            Text(panel.transform, "DriverTitle", driver.Title + " - " + driver.State, 16, 10, 4, 370, 28);
            Text(panel.transform, "DriverIdentity", driver.Id, 12, 10, 33, 610, 22);
            Text(panel.transform, "DriverServices", driver.Services.Count == 0 ? "No active services" : string.Join(", ", driver.Services), 12, 10, 57, 610, 26);
            S1Text error = Text(panel.transform, "DriverError", driver.LastError ?? "", 12, 10, 84, 610, 40);
            error.color = UiFactory.Danger;
            bool running = driver.State == DesktopDriverState.Running;
            AddAction(panel.transform, "DriverToggle_" + driver.Id, running ? "Stop" : "Start", 400,
                () => { if (running) DesktopKernel.Stop(driver.Id); else DesktopKernel.Start(driver.Id); });
            AddAction(panel.transform, "DriverRestart_" + driver.Id, "Restart", 512, () => DesktopKernel.Restart(driver.Id));
        }
        UiFactory.SetLayerRecursively(_rows.gameObject, Constants.UiLayer);
    }
    private void AddAction(Transform parent, string name, string title, float x, Action action)
    {
        Button button = UiFactory.CreateButton(parent, name, title, UiFactory.SurfaceRaised, out _);
        Top(button.GetComponent<RectTransform>(), x, 4, 108, 30);
        _listeners.Add(() =>
        {
            try { action(); _status.text = "Driver state updated."; }
            catch (Exception exception) { _status.text = exception.Message; }
            Refresh();
        }, button.onClick);
    }
    private static S1Text Text(Transform parent, string name, string text, float size, float x, float y, float width, float height)
    {
        S1Text label = UiFactory.CreateText(parent, name, text, size, UiFactory.TextPrimary, S1Alignment.MidlineLeft);
        label.richText = false; Top(label.rectTransform, x, y, width, height); return label;
    }
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y); }
}

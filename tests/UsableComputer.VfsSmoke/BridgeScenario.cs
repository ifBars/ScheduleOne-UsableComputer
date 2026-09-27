using System.Collections;
using System.Reflection;
using S1API.ExternalHosting;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using NativePhone = Il2CppScheduleOne.UI.Phone.Phone;
#else
using NativePhone = ScheduleOne.UI.Phone.Phone;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunBridgeScenario()
    {
        SmokePhoneHost? phone = null;
        SmokeTvHost? tv = null;
        SmokePhoneHost? optedOut = null;
        SmokePhoneHost? collision = null;
        string? collisionId = null;
        try
        {
            CloseAllWindows();
            optedOut = new SmokePhoneHost(false);
            optedOut.RegisterForSmoke();
            Require(!ExternalAppCatalog.GetAll().Any(entry => ReferenceEquals(entry.Host, optedOut)),
                "An opted-out phone app entered the external catalog.");

            phone = new SmokePhoneHost(true);
            tv = new SmokeTvHost(true);
            phone.RegisterForSmoke();
            tv.RegisterForSmoke();
            Require(ExternalAppCatalog.GetAll().Any(entry => ReferenceEquals(entry.Host, phone)),
                "A late phone registration did not enter the external catalog.");
            Require(ExternalAppCatalog.GetAll().Any(entry => ReferenceEquals(entry.Host, tv)),
                "A late TV registration did not enter the external catalog.");
        }
        catch (Exception exception) { Fail("S1API bridge registration failed", Unwrap(exception)); yield break; }

        yield return new WaitForSecondsRealtime(0.25f);
        string phoneId, tvId;
        GameObject? originalPhoneApp = NativePhone.ActiveApp;
        try
        {
            phoneId = ExternalAppCatalog.GetAll().Single(entry => ReferenceEquals(entry.Host, phone)).Id;
            tvId = ExternalAppCatalog.GetAll().Single(entry => ReferenceEquals(entry.Host, tv)).Id;
            Require(DesktopAppRegistry.GetAll().Any(app => app.Id == phoneId),
                "A late phone app did not appear on the desktop.");
            Require(DesktopAppRegistry.GetAll().Any(app => app.Id == tvId),
                "A late TV app did not appear on the desktop.");
            InvokeBridge("ClearForSceneChange");
            InvokeBridge("Update");
            Require(!DesktopAppRegistry.GetAll().Any(app => app.Id == phoneId || app.Id == tvId),
                "Suspended bridge reintroduced stale apps before save load.");
            InvokeBridge("ResumeForSave");
            InvokeBridge("Update");
            Require(DesktopAppRegistry.GetAll().Any(app => app.Id == phoneId) &&
                DesktopAppRegistry.GetAll().Any(app => app.Id == tvId),
                "Bridge did not reconcile registrations after save load.");
            OpenBridgedApp(phoneId);
            OpenBridgedApp(tvId);
            Require(phone!.Session?.OpenCount == 1 && tv!.Session?.OpenCount == 1,
                "Independent phone and TV sessions did not open.");
            Require(NativePhone.ActiveApp == originalPhoneApp && !tv.IsOpen,
                "Opening a desktop session changed the original phone or TV state.");
        }
        catch (Exception exception) { Fail("S1API bridge session failed", Unwrap(exception)); yield break; }

        yield return new WaitForSecondsRealtime(0.25f);
        string screenshot = Path.Combine(_outputDirectory, "bridge-running.png");
        try
        {
            Require(phone!.Session!.TickCount > 0 && tv!.Session!.TickCount > 0,
                "Independent sessions did not receive desktop ticks.");
            Require(phone.Session!.Root.GetComponentsInChildren<Transform>(true)
                    .All(child => child.gameObject.layer == 5) &&
                tv.Session!.Root.GetComponentsInChildren<Transform>(true)
                    .All(child => child.gameObject.layer == 5),
                "External session content retained a layer outside the desktop UI camera.");
            ScreenCapture.CaptureScreenshot(screenshot);
        }
        catch (Exception exception) { Fail("S1API bridge render check failed", Unwrap(exception)); yield break; }
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            phone!.UnregisterForSmoke();
            tv!.UnregisterForSmoke();
        }
        catch (Exception exception) { Fail("S1API bridge removal setup failed", Unwrap(exception)); yield break; }

        yield return new WaitForSecondsRealtime(0.25f);
        try
        {
            Require(!DesktopAppRegistry.GetAll().Any(app => app.Id == phoneId || app.Id == tvId),
                "Removed S1API apps survived in the desktop registry.");
            Require(phone!.Session!.CloseCount == 1 && phone.Session.DisposeCount == 1 &&
                tv!.Session!.CloseCount == 1 && tv.Session.DisposeCount == 1,
                "Removing an S1API app did not close and dispose its independent session.");
            Require(GameObject.Find("Window_" + phoneId) == null && GameObject.Find("Window_" + tvId) == null,
                "Removed S1API apps retained desktop windows.");
            Require(NativePhone.ActiveApp == originalPhoneApp && !tv.IsOpen,
                "Removing desktop sessions changed original device state.");

            // Reserve the same identity manually, then verify the bridge reports and preserves it.
            var probe = new SmokePhoneHost(true);
            probe.RegisterForSmoke();
            collisionId = ExternalAppCatalog.GetAll().Single(entry => ReferenceEquals(entry.Host, probe)).Id;
            probe.UnregisterForSmoke();
            DesktopAppRegistry.Register(new DesktopAppDescriptor(collisionId, "Reserved identity", "",
                new Vector2(300f, 220f), Vector2.zero, _ => new EmptyDriverApp()));
            collision = new SmokePhoneHost(true);
            collision.RegisterForSmoke();
        }
        catch (Exception exception) { Fail("S1API bridge collision setup failed", Unwrap(exception)); yield break; }

        yield return new WaitForSecondsRealtime(0.25f);
        try
        {
            Require(DesktopAppRegistry.GetAll().Single(app => app.Id == collisionId).Title == "Reserved identity",
                "The bridge overwrote an existing desktop app identity.");
            Require(collision!.Session == null, "A colliding S1API app created a desktop session.");
            DesktopAppRegistry.Unregister(collisionId);
            InvokeBridge("Update");
            Require(DesktopAppRegistry.GetAll().Single(app => app.Id == collisionId).Title == "Bridge smoke phone",
                "The bridge did not recover when the conflicting desktop app was removed.");
        }
        catch (Exception exception) { Fail("S1API bridge collision failed", Unwrap(exception)); }
        finally
        {
            collision?.UnregisterForSmoke();
            if (collisionId != null) DesktopAppRegistry.Unregister(collisionId);
            optedOut?.UnregisterForSmoke();
        }
        if (_completed) yield break;

        SmokePhoneHost? faulty = null;
        try
        {
            faulty = new SmokePhoneHost(true, failOpen: true);
            faulty.RegisterForSmoke();
        }
        catch (Exception exception) { Fail("S1API malformed session setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.25f);
        try
        {
            string faultyId = ExternalAppCatalog.GetAll().Single(entry => ReferenceEquals(entry.Host, faulty)).Id;
            OpenBridgedApp("notes");
            DesktopAppContext context = GetBridgeTestContext("notes");
            ExternalAppRegistration registration = ExternalAppCatalog.GetAll()
                .Single(entry => entry.Id == faultyId);
            Type adapterType = typeof(DesktopAppRegistry).Assembly
                .GetType("UsableComputer.Bridge.ExternalAppDesktopSession")!;
            var adapter = (IDesktopAppSession)Activator.CreateInstance(adapterType,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { registration, context }, null)!;
            try
            {
                AssertExpectedFault(adapter);
            }
            finally
            {
                adapter.Dispose();
            }
            Require(faulty!.Session?.CloseCount == 1 && faulty.Session.DisposeCount == 1,
                "An app whose Open failed retained its independent session.");
            Require(NativePhone.ActiveApp == originalPhoneApp,
                "A malformed desktop session changed the original phone state.");
            CloseAllWindows();
            LoggerInstance.Msg($"[UsableComputerBridgeSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} " +
                "Phone=True TV=True LateRegistration=True Removal=True OptOut=True Collision=True " +
                "SuspendedScene=True SourceState=True SessionCleanup=True FaultCleanup=True");
        }
        catch (Exception exception) { Fail("S1API malformed session cleanup failed", Unwrap(exception)); }
        finally { faulty?.UnregisterForSmoke(); }
    }

    private void OpenBridgedApp(string appId) => _desktop!.GetType()
        .GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(_desktop, new object[] { appId });

    private static void InvokeBridge(string method)
    {
        Type bridge = typeof(DesktopAppRegistry).Assembly.GetType("UsableComputer.Bridge.S1ApiDesktopBridge")
            ?? throw new InvalidOperationException("S1API desktop bridge type is unavailable.");
        bridge.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
    }

    private DesktopAppContext GetBridgeTestContext(string appId)
    {
        object manager = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_desktop)!;
        IEnumerable windows = (IEnumerable)manager.GetType()
            .GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        foreach (object window in windows)
        {
            string id = (string)window.GetType().GetProperty("AppId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
            if (id == appId)
                return (DesktopAppContext)window.GetType()
                    .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        }
        throw new InvalidOperationException($"Missing test context for '{appId}'.");
    }

    private static void AssertExpectedFault(IDesktopAppSession adapter)
    {
        try
        {
            adapter.OnOpened();
            throw new InvalidOperationException("The malformed external session did not fail on Open.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "Expected bridge smoke open failure.")
        {
        }
    }

    private sealed class SmokePhoneHost : S1API.PhoneApp.PhoneApp, IExternalAppHost
    {
        private readonly bool _failOpen;
        public SmokePhoneHost(bool enabled, bool failOpen = false)
        {
            AllowExternalHosting = enabled;
            _failOpen = failOpen;
        }
        public bool AllowExternalHosting { get; }
        public SmokeExternalSession? Session { get; private set; }
        protected override string AppName => "bridge-smoke-phone";
        protected override string AppTitle => "Bridge smoke phone";
        protected override string IconLabel => "Bridge";
        protected override string IconFileName => string.Empty;
        protected override void OnCreatedUI(GameObject container) { }
        public void RegisterForSmoke() => base.OnCreated();
        public void UnregisterForSmoke() => base.OnDestroyed();
        public IExternalAppSession CreateExternalSession(GameObject container, Action requestClose) =>
            Session = new SmokeExternalSession(container, _failOpen);
    }

    private sealed class SmokeTvHost : S1API.TVApp.TVApp, IExternalAppHost
    {
        public SmokeTvHost(bool enabled) { AllowExternalHosting = enabled; }
        public bool AllowExternalHosting { get; }
        public SmokeExternalSession? Session { get; private set; }
        protected override string AppName => "bridge-smoke-tv";
        protected override string AppTitle => "Bridge smoke TV";
        protected override Sprite Icon => null!;
        protected override void OnCreatedUI(GameObject container) { }
        public void RegisterForSmoke() => base.OnCreated();
        public void UnregisterForSmoke() => base.OnDestroyed();
        public IExternalAppSession CreateExternalSession(GameObject container, Action requestClose) =>
            Session = new SmokeExternalSession(container);
    }

    private sealed class SmokeExternalSession : IExternalAppSession
    {
        private readonly GameObject _root;
        private readonly bool _failOpen;
        private bool _dynamicAdded;
        public SmokeExternalSession(GameObject container, bool failOpen = false)
        {
            _failOpen = failOpen;
            _root = new GameObject("BridgeSmokeContent");
            _root.transform.SetParent(container.transform, false);
            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _root.AddComponent<Image>().color = new Color(0.08f, 0.28f, 0.52f, 1f);
        }
        public GameObject Root => _root;
        public int OpenCount { get; private set; }
        public int TickCount { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }
        public void Open()
        {
            OpenCount++;
            if (_failOpen) throw new InvalidOperationException("Expected bridge smoke open failure.");
        }
        public void Tick()
        {
            TickCount++;
            if (_dynamicAdded) return;
            _dynamicAdded = true;
            var marker = new GameObject("BridgeDynamicMarker");
            marker.transform.SetParent(_root.transform, false);
            RectTransform rect = marker.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80f, 80f);
            marker.AddComponent<Image>().color = new Color(0.96f, 0.71f, 0.18f, 1f);
        }
        public void Close() => CloseCount++;
        public void Dispose()
        {
            DisposeCount++;
            UnityEngine.Object.Destroy(_root);
        }
    }
}

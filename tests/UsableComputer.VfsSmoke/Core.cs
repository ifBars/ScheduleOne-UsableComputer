using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

#if IL2CPP
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using Il2CppInterop.Runtime;
using S1BuildableDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1DateTime = Il2CppSystem.DateTime;
using S1DateTimeData = Il2CppScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using S1MetaData = Il2CppScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = Il2CppScheduleOne.Persistence.SaveInfo;
using S1SaveManager = Il2CppScheduleOne.Persistence.SaveManager;
using S1Registry = Il2CppScheduleOne.Registry;
using S1MoneyManager = Il2CppScheduleOne.Money.MoneyManager;
using S1DeliveryManager = Il2CppScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = Il2CppScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = Il2CppScheduleOne.Delivery.DeliveryReceipt;
using S1Property = Il2CppScheduleOne.Property.Property;
using S1ItemPair = Il2CppScheduleOne.DevUtilities.StringIntPair;
#else
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
using S1BuildableDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1DateTime = System.DateTime;
using S1DateTimeData = ScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
using S1MetaData = ScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = ScheduleOne.Persistence.SaveInfo;
using S1SaveManager = ScheduleOne.Persistence.SaveManager;
using S1Registry = ScheduleOne.Registry;
using S1MoneyManager = ScheduleOne.Money.MoneyManager;
using S1DeliveryManager = ScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = ScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = ScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = ScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = ScheduleOne.Delivery.DeliveryReceipt;
using S1Property = ScheduleOne.Property.Property;
using S1ItemPair = ScheduleOne.DevUtilities.StringIntPair;
#endif

[assembly: MelonInfo(
    typeof(UsableComputer.VfsSmoke.Core),
    "Usable Computer VFS Smoke",
    "1.0.0",
    "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.VfsSmoke;

public sealed partial class Core : MelonMod
{
    private const string FolderName = "Smoke Workspace";
    private const string NotesAppId = "notes";

    private bool _enabled;
    private bool _started;
    private bool _completed;
    private string _phase = string.Empty;
    private string _outputDirectory = string.Empty;
    private string _savePath = string.Empty;
    private IDisposable? _desktop;
    private GameObject? _screenAnchor;
    private GameObject? _computerModel;
    private bool _iconLayout;
    private bool _textFiles;
    private bool _reports;
    private bool _deliveries;
    private bool _desktopUx;
    private bool _appIcons;
    private bool _dealers;
    private bool _drivers;
    private bool _power;
    private bool _products;
    private bool _studio;
    private bool _luaStorage;
    private bool _tvInventory;
    private bool _noodle;
    private bool _bridge;
    private bool _eggRun;
    private bool _doomRuntime;
    private bool _nestedRuntime;
    private bool _laptop;
    private const string TextFileName = "Shopping list.txt";
    private const string NoteText = "Shopping list\nMilk\nCoffee\nSave a copy for tomorrow.";
    private IDisposable? _controller;

    public override void OnUpdate()
    {
        _controller?.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_controller, new object[] { Time.unscaledTime });
    }

    public override void OnInitializeMelon()
    {
        string[] args = Environment.GetCommandLineArgs();
        _enabled = Array.IndexOf(args, "--usable-computer-vfs-smoke") >= 0;
        _iconLayout = Array.IndexOf(args, "--usable-computer-icon-layout-smoke") >= 0;
        _textFiles = Array.IndexOf(args, "--usable-computer-text-files-smoke") >= 0;
        _reports = Array.IndexOf(args, "--usable-computer-reports-smoke") >= 0;
        _deliveries = Array.IndexOf(args, "--usable-computer-deliveries-smoke") >= 0;
        _desktopUx = Array.IndexOf(args, "--usable-computer-desktop-ux-smoke") >= 0;
        _appIcons = Array.IndexOf(args, "--usable-computer-app-icons-smoke") >= 0;
        _dealers = Array.IndexOf(args, "--usable-computer-dealers-smoke") >= 0;
        _drivers = Array.IndexOf(args, "--usable-computer-drivers-smoke") >= 0;
        _power = Array.IndexOf(args, "--usable-computer-power-smoke") >= 0;
        _products = Array.IndexOf(args, "--usable-computer-products-smoke") >= 0;
        _studio = Array.IndexOf(args, "--usable-computer-studio-smoke") >= 0;
        _luaStorage = Array.IndexOf(args, "--usable-computer-lua-storage-smoke") >= 0;
        _tvInventory = Array.IndexOf(args, "--usable-computer-tv-inventory-smoke") >= 0;
        _noodle = Array.IndexOf(args, "--usable-computer-noodle-smoke") >= 0;
        _bridge = Array.IndexOf(args, "--usable-computer-bridge-smoke") >= 0;
        _eggRun = Array.IndexOf(args, "--usable-computer-egg-run-smoke") >= 0;
        _doomRuntime = Array.IndexOf(args, "--usable-computer-doom-runtime-smoke") >= 0;
        _nestedRuntime = Array.IndexOf(args, "--usable-computer-nested-runtime-smoke") >= 0;
        _laptop = Array.IndexOf(args, "--usable-computer-laptop-smoke") >= 0;
        if (!_enabled)
            return;

        _phase = GetArgument(args, "--usable-computer-vfs-smoke-phase");
        _outputDirectory = GetArgument(args, "--usable-computer-vfs-smoke-dir");
        _savePath = GetArgument(args, "--usable-computer-vfs-smoke-save");
        Directory.CreateDirectory(_outputDirectory);
        LoggerInstance.Msg($"[UsableComputerVfsSmoke] Enabled Phase={_phase} Save={_savePath}");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!_enabled || _completed)
            return;

        if (sceneName == "Menu" && !_started)
        {
            _started = true;
            MelonCoroutines.Start(StartFixture());
        }
        else if (sceneName == "Main")
        {
            MelonCoroutines.Start(RunScenario());
        }
    }

    public override void OnDeinitializeMelon() => CleanupFixture();

    private void CleanupFixture()
    {
        _controller?.Dispose();
        _controller = null;
        _desktop?.Dispose();
        _desktop = null;
        if (_computerModel != null)
            UnityEngine.Object.Destroy(_computerModel);
        _computerModel = null;
        _screenAnchor = null;
    }

    private IEnumerator StartFixture()
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while ((S1LoadManager.Instance == null || S1SaveManager.Instance == null || S1LoadManager.Instance.IsLoading) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        try
        {
            Require(S1LoadManager.Instance != null, "LoadManager was unavailable in Menu.");
            Require(S1SaveManager.Instance != null, "SaveManager was unavailable in Menu.");
            var saveInfo = new S1SaveInfo(
                _savePath,
                -1,
                "Usable Computer VFS Smoke",
                GetNow(),
                GetNow(),
                0f,
                Application.version,
                new S1MetaData(
                    (S1DateTimeData?)null,
                    (S1DateTimeData?)null,
                    Application.version,
                    Application.version,
                    playTutorial: false));
            S1LoadManager.Instance!.StartGame(saveInfo, allowLoadStacking: false, allowSaveBackup: false);
        }
        catch (Exception exception)
        {
            Fail("Could not start the disposable save", exception);
        }
    }

    private IEnumerator RunScenario()
    {
        float deadline = Time.realtimeSinceStartup + 90f;
        while ((S1LoadManager.Instance == null ||
                S1LoadManager.Instance.IsLoading ||
                !S1LoadManager.Instance.IsGameLoaded ||
                !S1LoadManager.Instance.IsInGameScene ||
                Camera.main == null) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (S1LoadManager.Instance == null ||
            S1LoadManager.Instance.IsLoading ||
            !S1LoadManager.Instance.IsGameLoaded ||
            !S1LoadManager.Instance.IsInGameScene ||
            Camera.main == null)
        {
            Fail("Normal gameplay did not become ready", new TimeoutException("Main readiness timed out."));
            yield break;
        }

        yield return new WaitForSecondsRealtime(1f);

        string folderId;
        try
        {
            folderId = string.Equals(_phase, "seed", StringComparison.Ordinal)
                ? SeedVirtualFileSystem()
                : VerifyReloadedVirtualFileSystem();
            OpenFolderUi(folderId);
        }
        catch (Exception exception)
        {
            Fail("Virtual filesystem scenario failed", Unwrap(exception));
            yield break;
        }

        Screen.SetResolution(1280, 720, false);
        yield return new WaitForSecondsRealtime(2f);

        if (_laptop)
        {
            yield return RunLaptopDisplayScenario();
            if (_completed) yield break;
        }

        if (_desktopUx)
        {
            OpenSettings();
            IEnumerator scenario = UsableComputer.DisplaySmoke.DesktopInteractionScenario.Run(
                GameObject.Find("UsableComputer_DesktopCanvas"), Camera.main!, _outputDirectory,
                verifyIconPosition: true, expectSavedIconPosition: _phase == "reload");
            while (true)
            {
                object? current;
                try
                {
                    if (!scenario.MoveNext()) break;
                    current = scenario.Current;
                }
                catch (Exception exception)
                {
                    Fail("Desktop UX scenario failed", Unwrap(exception));
                    yield break;
                }
                yield return current;
            }
            _desktop!.GetType().GetMethod("OpenFolder", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { folderId });
        }

        if (_iconLayout)
        {
            yield return RunIconLayoutScenario();
            if (_completed) yield break;
        }

        if (_textFiles)
        {
            yield return RunTextFilesScenario(folderId);
            if (_completed) yield break;
        }

        if (_reports)
        {
            yield return RunReportsScenario();
            if (_completed) yield break;
        }

        if (_noodle)
        {
            yield return RunNoodleScenario();
            if (_completed) yield break;
        }

        if (_eggRun)
        {
            yield return RunEggRunScenario();
            if (_completed) yield break;
        }

        if (_tvInventory)
        {
            yield return RunTvInventoryScenario();
            if (_completed) yield break;
        }

        if (_bridge)
        {
            yield return RunBridgeScenario();
            if (_completed) yield break;
        }

        if (_doomRuntime)
        {
            yield return RunDoomRuntimeScenario();
            if (_completed) yield break;
        }

        if (_nestedRuntime)
        {
            yield return RunNestedGameRuntimeScenario();
            if (_completed) yield break;
        }

        if (_luaStorage)
        {
            yield return RunLuaStorageScenario();
            if (_completed) yield break;
        }

        if (_studio)
        {
            yield return RunStudioScenario();
            if (_completed) yield break;
        }

        if (_products)
        {
            yield return RunProductsScenario();
            if (_completed) yield break;
        }

        if (_power)
        {
            yield return RunPowerScenario();
            if (_completed) yield break;
        }

        if (_drivers)
        {
            yield return RunDriversScenario();
            if (_completed) yield break;
        }

        if (_dealers)
        {
            yield return RunDealersScenario();
            if (_completed) yield break;
        }

        if (_appIcons)
        {
            yield return RunAppIconsScenario();
            if (_completed) yield break;
        }

        if (_deliveries)
        {
            yield return RunDeliveriesScenario();
            if (_completed) yield break;
        }

        string screenshotPath = Path.Combine(_outputDirectory, $"vfs-{_phase}.png");
        ScreenCapture.CaptureScreenshot(screenshotPath);
        deadline = Time.realtimeSinceStartup + 10f;
        while ((!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
        {
            Fail("Screenshot was not written", new IOException(screenshotPath));
            yield break;
        }

        try
        {
            if (string.Equals(_phase, "reload", StringComparison.Ordinal))
                CleanVirtualFileSystem(folderId);
            S1SaveManager.Instance.Save();
        }
        catch (Exception exception)
        {
            Fail("Save request failed", Unwrap(exception));
            yield break;
        }

        bool sawSave = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawSave |= S1SaveManager.Instance.IsSaving;
            if (sawSave && !S1SaveManager.Instance.IsSaving)
                break;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);
        try
        {
            string persistedPath = Path.Combine(
                _savePath,
                "Modded",
                "Saveables",
                "UsableComputerFileSystemSave",
                "virtual-filesystem.json");
            Require(File.Exists(persistedPath), "The S1API saveable file was not created.");
            string json = File.ReadAllText(persistedPath);
            if (_reports)
            {
                string reportsPath = Path.Combine(_savePath, "Modded", "Saveables", "UsableComputerReportsSave", "bank-reports.json");
                Require(File.Exists(reportsPath), "Bank reports were not persisted.");
                string reportsJson = File.ReadAllText(reportsPath);
                Require(reportsJson.Contains("Reports smoke receipt"), "Bank report receipt was not persisted.");
            }
            bool containsFolder = json.Contains(FolderName, StringComparison.Ordinal);
            if (string.Equals(_phase, "seed", StringComparison.Ordinal))
                Require(containsFolder, "The seeded folder was not persisted.");
            else
                Require(!containsFolder, "The cleanup folder remained in persisted data.");

            Pass(screenshotPath, sawSave, persistedPath);
        }
        catch (Exception exception)
        {
            Fail("Persisted save validation failed", Unwrap(exception));
        }
    }

    private string SeedVirtualFileSystem()
    {
        object service = GetServiceType();
        Require(FindChild(service, "desktop", FolderName, targetId: null) == null, "Seed folder already exists.");
        object folder = Invoke(service, "CreateDirectory", "desktop", FolderName)!;
        string folderId = ReadString(folder, "Id");
        object notes = FindShortcutRecursive(service, "desktop", NotesAppId)
            ?? throw new InvalidOperationException("Notes shortcut was not found.");
        Invoke(service, "Move", ReadString(notes, "Id"), folderId);
        Require(FindChild(service, folderId, name: null, NotesAppId) != null, "Notes shortcut did not move into the folder.");
        return folderId;
    }

    private IEnumerator RunIconLayoutScenario()
    {
        Type preferences = GetUsableComputerAssembly().GetType("UsableComputer.PreferencesStore", true)!;
        if (_phase == "reload")
        {
            try
            {
                Require(preferences.GetProperty("IconSize", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString() == "Large", "Icon size did not survive process reload.");
                Require(preferences.GetProperty("IconOrder", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!.ToString() == "Name", "Icon order did not survive process reload.");
            }
            catch (Exception exception)
            {
                Fail("Icon preferences reload failed", Unwrap(exception));
                yield break;
            }
        }

        foreach (string theme in new[] { "Light", "Dark" })
        {
            foreach (string size in new[] { "Small", "Medium", "Large" })
            {
                try
                {
                    OpenSettings();
                    ClickChoice(theme);
                    ClickChoice(size);
                    ClickChoice("Folders first");
                    ValidateIconLayout(size, foldersFirst: true);
                    ClickChoice("Arrange by name");
                    ValidateIconLayout(size, foldersFirst: false);
                }
                catch (Exception exception)
                {
                    Fail($"Icon layout {theme}/{size} failed", Unwrap(exception));
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.3f);
                if (size == "Large")
                {
                    string settingsPath = Path.Combine(_outputDirectory, $"settings-{theme}.png");
                    ScreenCapture.CaptureScreenshot(settingsPath);
                    yield return WaitForCapture(settingsPath);
                    if (_completed) yield break;
                }
                CloseAllWindows();
                yield return new WaitForSecondsRealtime(0.3f);
                string path = Path.Combine(_outputDirectory, $"icons-{theme}-{size}.png");
                ScreenCapture.CaptureScreenshot(path);
                yield return WaitForCapture(path);
                if (_completed) yield break;
            }
            try
            {
                _desktop!.GetType().GetMethod("ToggleStartMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, null);
                GameObject menu = GameObject.Find("StartMenu")!;
                var scroll = menu.transform.Find("ProgramsViewport").GetComponent<UnityEngine.UI.ScrollRect>();
                Require(scroll.vertical && !scroll.horizontal, "Programs list is not vertically scrollable.");
                Require(menu.GetComponent<RectTransform>().sizeDelta.y + 50f <= 536f, "Start menu exceeds desktop.");
                Require(scroll.content.rect.height > scroll.viewport.rect.height, "Built-in programs do not exercise overflow.");
                Canvas.ForceUpdateCanvases();
                scroll.verticalScrollbar.value = 1f;
                scroll.verticalScrollbar.value = 0f;
            }
            catch (Exception exception)
            {
                Fail("Scrollable Start menu failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            string menuPath = Path.Combine(_outputDirectory, $"start-menu-{theme}.png");
            ScreenCapture.CaptureScreenshot(menuPath);
            yield return WaitForCapture(menuPath);
            if (_completed) yield break;
            try
            {
                var scroll = GameObject.Find("StartMenu").transform.Find("ProgramsViewport").GetComponent<UnityEngine.UI.ScrollRect>();
                Vector3[] corners = GetWorldCorners(scroll.content.GetChild(scroll.content.childCount - 1).GetComponent<RectTransform>());
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = scroll.viewport.InverseTransformPoint(corner);
                    Rect viewport = scroll.viewport.rect;
                    Require(point.x >= viewport.xMin - 0.1f && point.x <= viewport.xMax + 0.1f && point.y >= viewport.yMin - 0.1f && point.y <= viewport.yMax + 0.1f,
                        $"Last program is clipped after scrolling: point={point} viewport={viewport} offset={scroll.content.anchoredPosition} normalized={scroll.verticalNormalizedPosition}.");
                }
                Require(GameObject.Find("Start_Settings").activeInHierarchy && GameObject.Find("Start_Shut down").activeInHierarchy && GameObject.Find("Start_Restart").activeInHierarchy, "Footer actions disappeared while scrolling.");
                GameObject.Find("Start_Notes").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                RequireWindow("notes");
                CloseAllWindows();
            }
            catch (Exception exception)
            {
                Fail("Start menu scrolled action failed", Unwrap(exception));
                yield break;
            }
        }
        yield return ValidateDesktopOverflow();
        if (_completed) yield break;
        foreach (Vector2 resolution in new[] { new Vector2(1280, 720), new Vector2(1024, 768), new Vector2(800, 900), new Vector2(1920, 1080), new Vector2(1600, 675) })
        {
            Screen.SetResolution((int)resolution.x, (int)resolution.y, false);
            yield return new WaitForSecondsRealtime(1f);
            string viewPath = Path.Combine(_outputDirectory, $"view-{Screen.width}x{Screen.height}.png");
            ScreenCapture.CaptureScreenshot(viewPath);
            yield return WaitForCapture(viewPath);
            if (_completed) yield break;
            try
            {
                RectTransform canvas = GameObject.Find("UsableComputer_DesktopCanvas").GetComponent<RectTransform>();
                Vector3[] corners = GetWorldCorners(canvas);
                Vector3 center = Camera.main!.WorldToViewportPoint(canvas.position);
                LoggerInstance.Msg($"[UsableComputerViewSmoke] Resolution={Screen.width}x{Screen.height} Aspect={Camera.main.aspect} Center={center} FOV={Camera.main.fieldOfView}");
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = Camera.main.WorldToViewportPoint(corner);
                    Require(point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f,
                        $"Desktop clipped at {Screen.width}x{Screen.height}: {point}");
                }
                Require(Math.Abs(center.x - 0.5f) < 0.04f && Math.Abs(center.y - 0.5f) < 0.04f, "Desktop is off-center.");
            }
            catch (Exception exception)
            {
                Fail("Resolution camera fit failed", Unwrap(exception));
                yield break;
            }
        }
        Screen.SetResolution(1280, 720, false);
        yield return new WaitForSecondsRealtime(1f);
        LoggerInstance.Msg($"[UsableComputerIconLayoutSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Sizes=3 Themes=2 Order=Name,Kind Reload={_phase == "reload"}");
    }

    private IEnumerator ValidateDesktopOverflow()
    {
        object service = GetServiceType();
        var ids = new List<string>();
        try
        {
            for (int index = 0; index < 30; index++)
                ids.Add(ReadString(Invoke(service, "CreateDirectory", "desktop", $"ZZZ overflow folder with a long label {index:00}")!, "Id"));
            Canvas.ForceUpdateCanvases();
            var scroll = GameObject.Find("DesktopIconViewport").GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontalScrollbar.value = 0f;
            scroll.horizontalScrollbar.value = 1f;
            yield return new WaitForSecondsRealtime(0.3f);
            try
            {
                Require(scroll.horizontalScrollbar.gameObject.activeInHierarchy, "Overflow scrollbar is hidden.");
                Require(scroll.content.anchoredPosition.x < -1f, "Desktop did not scroll horizontally.");
                Transform last = scroll.content.GetChild(scroll.content.childCount - 1);
                Require(last.name == "DesktopIcon_" + ids[ids.Count - 1], "Overflow fixture is not the last sorted icon.");
                Vector3[] corners = GetWorldCorners(last.GetComponent<RectTransform>());
                foreach (Vector3 corner in corners)
                {
                    Vector3 point = scroll.viewport.InverseTransformPoint(corner);
                    Rect viewport = scroll.viewport.rect;
                    Require(point.x >= viewport.xMin - 0.1f && point.x <= viewport.xMax + 0.1f, "Last desktop icon is unreachable.");
                }
                var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                    clickCount = 2
                };
                last.GetComponent<UnityEngine.EventSystems.EventTrigger>().OnPointerClick(pointer);
                RequireWindow("files");
                CloseAllWindows();
            }
            catch (Exception exception)
            {
                Fail("Desktop overflow failed", Unwrap(exception));
            }
        }
        finally
        {
            foreach (string id in ids)
                Invoke(service, "Delete", id);
        }
    }

    private static Vector3[] GetWorldCorners(RectTransform rect)
    {
#if IL2CPP
        // Native writes must be read back from the IL2CPP array, not its managed input copy.
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        rect.GetWorldCorners(corners);
        return corners.ToArray();
#else
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners;
#endif
    }

    private IEnumerator WaitForCapture(string path)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            Fail("Icon screenshot was not written", new IOException(path));
    }

    private void OpenSettings()
    {
        CloseAllWindows();
        _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_desktop, new object[] { "settings" });
    }

    private void RequireWindow(string appId)
    {
        object windows = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        Require((bool)windows.GetType().GetMethod("HasWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(windows, new object[] { appId })!,
            $"App {appId} did not finish opening.");
    }

    private void CloseAllWindows()
    {
        object windows = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        string[] ids = ((IEnumerable)windows.GetType().GetMethod("GetAppIds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(windows, null)!).Cast<string>().ToArray();
        foreach (string id in ids)
            windows.GetType().GetMethod("CloseWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(windows, new object[] { id });
    }

    private static void ClickChoice(string label)
    {
        GameObject button = GameObject.Find($"Choice_{label}") ?? throw new InvalidOperationException($"Missing choice {label}.");
        button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    }

    private void ValidateIconLayout(string size, bool foldersFirst)
    {
        GameObject root = (GameObject)_desktop!.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        RectTransform icons = root.transform.Find("DesktopIconViewport/DesktopIcons").GetComponent<RectTransform>();
        Require(icons.childCount > 0, "No desktop icons were created.");
        float expectedSize = size == "Small" ? 32f : size == "Large" ? 56f : 42f;
        var nodes = GetChildren(GetServiceType(), "desktop").Cast<object>()
            .OrderBy(node => foldersFirst && ReadString(node, "Kind") != "Directory" ? 1 : 0)
            .ThenBy(node => ReadString(node, "Name"), StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => ReadString(node, "Id"), StringComparer.Ordinal).ToArray();
        Require(icons.childCount == nodes.Length, "Desktop omitted filesystem nodes.");
        for (int index = 0; index < icons.childCount; index++)
        {
            Transform child = icons.GetChild(index);
            Require(child.name == $"DesktopIcon_{ReadString(nodes[index], "Id")}", "Auto-arrange ordering differs from saved preference.");
            RectTransform frame = child.Find("IconFrame").GetComponent<RectTransform>();
            Require(Math.Abs(frame.sizeDelta.x - expectedSize) < 0.01f, $"Incorrect {size} icon size.");
            Require(child.GetComponent<UnityEngine.UI.Button>().interactable, "Icon cannot be clicked.");
            RectTransform rect = child.GetComponent<RectTransform>();
            Require(-rect.anchoredPosition.y + rect.sizeDelta.y <= 464f, "Icon overlaps taskbar/scrollbar.");
            Require(rect.anchoredPosition.x + rect.sizeDelta.x / 2f <= icons.sizeDelta.x, "Icon is beyond scroll content.");
        }
    }

    private IEnumerator RunTextFilesScenario(string folderId)
    {
        object service = GetServiceType();
        string fileId;
        try
        {
            if (_phase == "seed")
            {
                _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(_desktop, new object[] { NotesAppId });
                GameObject.Find("NoteInput").GetComponent<S1Input>().text = NoteText;
                GameObject.Find("NotePath").GetComponent<S1Input>().text = "/Desktop/" + TextFileName;
                ClickNotesButton("Save");
                object file = FindChild(service, "desktop", TextFileName, null)
                    ?? throw new InvalidOperationException("Notes Save did not create a text file.");
                fileId = ReadString(file, "Id");
                Invoke(service, "Move", fileId, folderId);
                ClickNotesButton("Save");
                Require(GameObject.Find("NotePath").GetComponent<S1Input>().text == $"/Desktop/{FolderName}/{TextFileName}",
                    "Notes did not follow the moved file.");
            }
            else
                fileId = ReadString(FindChild(service, folderId, TextFileName, null)!, "Id");

            // Exercise the Files app's document routing into Notes.
            object filesSession = GetAppSession("files");
            filesSession.GetType().GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(filesSession, new object[] { fileId });
            Require(GameObject.Find("NoteInput").GetComponent<S1Input>().text == NoteText,
                "File Explorer did not open the saved text in Notes.");

            GameObject.Find("NoteInput").GetComponent<S1Input>().text = "Draft retained across window close";
            object manager = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
            manager.GetType().GetMethod("CloseWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(manager, new object[] { NotesAppId });
        }
        catch (Exception exception)
        {
            Fail("Text file creation or opening failed", Unwrap(exception));
            yield break;
        }
        yield return null;
        try
        {
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { NotesAppId });
            Require(GameObject.Find("NoteInput").GetComponent<S1Input>().text == "Draft retained across window close",
                "Closing Notes lost its draft.");
            _desktop.GetType().GetMethod("OpenNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { fileId });
            Require(GameObject.Find("NoteInput").GetComponent<S1Input>().text == "Draft retained across window close",
                "Opening a file discarded dirty text.");
            Require(GameObject.Find("NoteStatus").GetComponent<S1Text>().text.Contains("Save your draft"),
                "Dirty file switching did not explain how to continue.");
            ClickNotesButton("Discard");
            _desktop.GetType().GetMethod("OpenNode", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { fileId });
            Require(GameObject.Find("NoteInput").GetComponent<S1Input>().text == NoteText,
                "Desktop did not reopen the document.");
            Require((string)Invoke(service, "ReadText", fileId)! == NoteText, "Live text content differs from saved note.");
        }
        catch (Exception exception)
        {
            Fail("Notes draft preservation failed", Unwrap(exception));
            yield break;
        }

        Type preferences = GetUsableComputerAssembly().GetType("UsableComputer.PreferencesStore", true)!;
        MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (string theme in new[] { "Dark", "Light" })
        {
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, theme) });
            yield return new WaitForSecondsRealtime(0.5f);
            string capture = Path.Combine(_outputDirectory, $"notes-{theme}.png");
            ScreenCapture.CaptureScreenshot(capture);
            yield return WaitForCapture(capture);
            if (_completed) yield break;
        }
        LoggerInstance.Msg($"[UsableComputerTextFilesSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Save=True Move=True Open=True Draft=True Themes=2");
    }

    private IEnumerator RunReportsScenario()
    {
        Type service = GetUsableComputerAssembly().GetType("UsableComputer.Reports.BankReportsService", true)!;
        try
        {
            Invoke(service, "Flush");
            Require((bool)service.GetProperty("Available", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!, "Report service unavailable.");
            Require((bool)service.GetProperty("IsHost", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!, "Host report service did not recognize host.");
            if (_phase == "seed")
            {
                S1MoneyManager.Instance.CreateOnlineTransaction("Reports smoke receipt", 123.45f, 1, "");
                S1MoneyManager.Instance.CreateOnlineTransaction("Reports smoke expense", -23.45f, 1, "");
                S1MoneyManager.Instance.CreateOnlineTransaction("A long delivery receipt description that should be shortened before reaching the amount column on the computer", -0.01f, 1, "");
            }
        }
        catch (Exception exception)
        {
            Fail("Reports setup failed", Unwrap(exception));
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        try
        {
            Invoke(service, "Flush");
            object book = service.GetProperty("Book", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            object[] days = ((IEnumerable)book.GetType().GetProperty("Days", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(book)!).Cast<object>().ToArray();
            object[] entries = days.SelectMany(day => ((IEnumerable)day.GetType().GetProperty("Entries")!.GetValue(day)!).Cast<object>()).ToArray();
            Require(entries.Count(entry => ReadString(entry, "Name") == "Reports smoke receipt") == 1,
                "Receipt missing or duplicated after observation/reload.");
            Require(entries.Count(entry => ReadString(entry, "Name") == "Reports smoke expense") == 1,
                "Expense missing or duplicated after observation/reload.");
            object receipt = entries.Single(entry => ReadString(entry, "Name") == "Reports smoke receipt");
            Require((decimal)receipt.GetType().GetProperty("Amount")!.GetValue(receipt)! == 123.45m, "Receipt amount mismatch.");
        }
        catch (Exception exception)
        {
            Fail("Reports data failed", Unwrap(exception));
            yield break;
        }

        foreach (string theme in new[] { "Light", "Dark" })
        {
            try
            {
                OpenSettings();
                ClickChoice(theme);
                CloseAllWindows();
                _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(_desktop, new object[] { "bank-reports" });
                RequireWindow("bank-reports");
            }
            catch (Exception exception)
            {
                Fail($"Reports {theme} window failed", Unwrap(exception));
                yield break;
            }
            // Unity destroys the previous window at the end of the frame.
            yield return null;
            try
            {
                GameObject.Find("ReportExport").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(GameObject.Find("ReportStatus").GetComponent<S1Text>().text.StartsWith("Saved Bank report"), "Report export failed.");
                Require(GameObject.Find("ReportTotals").GetComponent<S1Text>().text.Contains("123.45"), "Report totals were not rendered.");
                object files = GetServiceType();
                object[] exported = ((IEnumerable)Invoke(files, "GetChildren", "desktop")!).Cast<object>()
                    .Where(entry => ReadString(entry, "Name").StartsWith("Bank report - Day ")).ToArray();
                Require(exported.Length >= (theme == "Dark" ? 2 : 1), "Report export overwrote an earlier file.");
                string contents = (string)Invoke(files, "ReadText", ReadString(exported.Last(), "Id"))!;
                Require(contents.Contains("Reports smoke receipt") && contents.Contains("not profit"), "Export did not contain the report.");
            }
            catch (Exception exception)
            {
                Fail($"Reports {theme} UI failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            string screenshot = Path.Combine(_outputDirectory, $"reports-{theme}.png");
            ScreenCapture.CaptureScreenshot(screenshot);
            yield return WaitForCapture(screenshot);
            if (_completed) yield break;
            try
            {
                GameObject.Find("ReportPeriod").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(GameObject.Find("ReportCoverage").GetComponent<S1Text>().text.Contains("days observed"), "Summary coverage was not rendered.");
                Require(GameObject.Find("ReportTotals").GetComponent<S1Text>().text.Contains("123.45"), "Summary totals were not rendered.");
                GameObject.Find("ReportExport").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(GameObject.Find("ReportStatus").GetComponent<S1Text>().text.StartsWith("Saved Bank summary"), "Summary export failed.");
                object[] summaries = ((IEnumerable)Invoke(GetServiceType(), "GetChildren", "desktop")!).Cast<object>()
                    .Where(entry => ReadString(entry, "Name").StartsWith("Bank summary - Days ")).ToArray();
                Require(summaries.Length >= (theme == "Dark" ? 2 : 1), "Summary export overwrote an earlier file.");
                string summary = (string)Invoke(GetServiceType(), "ReadText", ReadString(summaries.Last(), "Id"))!;
                Require(summary.Contains("days observed") && summary.Contains("not profit"), "Summary export lost coverage.");
            }
            catch (Exception exception)
            {
                Fail($"Reports {theme} summary failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            screenshot = Path.Combine(_outputDirectory, $"report-summary-{theme}.png");
            ScreenCapture.CaptureScreenshot(screenshot);
            yield return WaitForCapture(screenshot);
            if (_completed) yield break;
        }
        LoggerInstance.Msg($"[UsableComputerReportsSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} NativeReceipts=True NoDuplicates=True Export=True Themes=2");
    }

    private object GetAppSession(string appId)
    {
        object manager = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        IEnumerable windows = (IEnumerable)manager.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        foreach (object window in windows)
            if ((string)window.GetType().GetProperty("AppId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)! == appId)
                return window.GetType().GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        throw new InvalidOperationException($"No window for {appId}.");
    }

    private static void ClickNotesButton(string name)
    {
        Transform button = GameObject.Find("Window_notes").transform.Find("Content/" + name);
        Require(button != null, $"Notes button {name} is missing.");
        button!.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
    }

    private string VerifyReloadedVirtualFileSystem()
    {
        object service = GetServiceType();
        object folder = FindChild(service, "desktop", FolderName, targetId: null)
            ?? throw new InvalidOperationException("Persisted folder was not restored after process reload.");
        string folderId = ReadString(folder, "Id");
        Require(FindChild(service, folderId, name: null, NotesAppId) != null, "Persisted Notes shortcut was not restored inside the folder.");
        if (_textFiles)
        {
            object file = FindChild(service, folderId, TextFileName, null)
                ?? throw new InvalidOperationException("Text file was not restored after process reload.");
            Require((string)Invoke(service, "ReadText", ReadString(file, "Id"))! == NoteText,
                "Persisted text content changed after process reload.");
        }
        return folderId;
    }

    private void CleanVirtualFileSystem(string folderId)
    {
        object service = GetServiceType();
        object notes = FindChild(service, folderId, name: null, NotesAppId)
            ?? throw new InvalidOperationException("Notes shortcut disappeared before cleanup.");
        Invoke(service, "Move", ReadString(notes, "Id"), "desktop");
        if (_textFiles)
        {
            object file = FindChild(service, folderId, TextFileName, null)!;
            Invoke(service, "Delete", ReadString(file, "Id"));
        }
        Invoke(service, "Delete", folderId);
    }

    private void OpenFolderUi(string folderId)
    {
        Assembly assembly = GetUsableComputerAssembly();
        Type shellType = assembly.GetType("UsableComputer.UI.DesktopShell", throwOnError: true)!;
        Camera camera = Camera.main!;
        _computerModel = CreateComputerModel(assembly);
        Transform cameraAnchor = FindDescendant(_computerModel.transform, "UsableComputer_CameraAnchor")
            ?? throw new InvalidOperationException("The fixture computer is missing its camera anchor.");
        _screenAnchor = FindDescendant(_computerModel.transform, "UsableComputer_ScreenAnchor")?.gameObject
            ?? throw new InvalidOperationException("The fixture computer is missing its screen anchor.");

        // Exercise the real controller, including camera transitions and resize handling.
        _computerModel.transform.rotation = camera.transform.rotation * Quaternion.Inverse(cameraAnchor.localRotation);
        _computerModel.transform.position = camera.transform.position -
            (_computerModel.transform.rotation * Vector3.Scale(cameraAnchor.localPosition, _computerModel.transform.localScale));
        _computerModel.SetActive(true);
        _computerModel.name = "FurnitureVisual";
        Type controllerType = assembly.GetType("UsableComputer.Runtime.UsableComputerController", true)!;
        _controller = (IDisposable)Activator.CreateInstance(controllerType, BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { _computerModel }, null)!;
        controllerType.GetMethod("TryInitialize", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_controller, null);
        controllerType.GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_controller, null);
        Require((bool)controllerType.GetProperty("IsOpen", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_controller)!, "Real controller did not open.");
        object shell = controllerType.GetField("_desktop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_controller)!;
        GameObject desktopRoot = (GameObject)(shellType
            .GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(shell) ?? throw new InvalidOperationException("Desktop root was not created."));
        bool hasNativeScheduleOneLogo = false;
        foreach (UnityEngine.UI.Image image in desktopRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.sprite != null &&
                string.Equals(image.sprite.name, "S1 Logo Whiteout small", StringComparison.Ordinal))
            {
                hasNativeScheduleOneLogo = true;
                break;
            }
        }
        Require(hasNativeScheduleOneLogo, "The desktop did not use the game-owned Schedule I logo sprite.");
        shellType.GetMethod("OpenFolder", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(shell, new object[] { folderId });
        _desktop = (IDisposable)shell;
    }

    private GameObject CreateComputerModel(Assembly assembly)
    {
        object donorItem = S1Registry.GetItem("launderingstation")
            ?? throw new InvalidOperationException("The laundering station donor is unavailable.");
#if IL2CPP
        S1BuildableDefinition donor = ((Il2CppSystem.Object)donorItem).TryCast<S1BuildableDefinition>()
            ?? throw new InvalidOperationException("The laundering station donor is not buildable.");
#else
        S1BuildableDefinition donor = donorItem as S1BuildableDefinition
            ?? throw new InvalidOperationException("The laundering station donor is not buildable.");
#endif
        GameObject donorObject = donor.BuiltItem.gameObject;

        Type factoryType = assembly.GetType("UsableComputer.Content.NativeComputerModelFactory", throwOnError: true)!;
        if (_laptop)
        {
            object laptopItem = S1Registry.GetItem("usable_laptop")
                ?? throw new InvalidOperationException("The laptop item was not registered before loading.");
#if IL2CPP
            S1BuildableDefinition laptopDefinition = ((Il2CppSystem.Object)laptopItem).TryCast<S1BuildableDefinition>()
                ?? throw new InvalidOperationException("The laptop item is not a buildable definition.");
#else
            S1BuildableDefinition laptopDefinition = laptopItem as S1BuildableDefinition
                ?? throw new InvalidOperationException("The laptop item is not a buildable definition.");
#endif
            Require(laptopDefinition.BuiltItem != null && laptopDefinition.StoredItem != null,
                "The laptop is missing its placed or inventory prefab.");
            Type registrar = assembly.GetType("UsableComputer.Content.ComputerContentRegistrar", true)!;
            GameObject source = (GameObject)(registrar.GetMethod("FindNativeLaptop", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null) ?? throw new InvalidOperationException("The native laptop was not found."));
            MethodInfo createLaptop = factoryType.GetMethod("CreateLaptop", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (GameObject)(createLaptop.Invoke(null, new object[] { donorObject, source })
                ?? throw new InvalidOperationException("The physical laptop model could not be created."));
        }

        MethodInfo create = factoryType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (GameObject)(create.Invoke(null, new object[] { donorObject })
            ?? throw new InvalidOperationException("The physical computer model could not be created."));
    }

    private static Transform? FindDescendant(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            Transform? match = FindDescendant(child, name);
            if (match != null)
                return match;
        }
        return null;
    }

    private static object GetServiceType()
    {
        return GetUsableComputerAssembly().GetType(
            "UsableComputer.FileSystem.VirtualFileSystemService",
            throwOnError: true)!;
    }

    private static Assembly GetUsableComputerAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                   assembly.GetName().Name?.StartsWith("UsableComputer_", StringComparison.Ordinal) == true)
               ?? throw new InvalidOperationException("The Usable Computer assembly is not loaded.");
    }

    private static object? FindShortcutRecursive(object service, string directoryId, string targetId)
    {
        foreach (object node in GetChildren(service, directoryId))
        {
            string kind = ReadValue(node, "Kind")?.ToString() ?? string.Empty;
            if (kind == "AppShortcut" && string.Equals(ReadString(node, "TargetId"), targetId, StringComparison.Ordinal))
                return node;
            if (kind == "Directory")
            {
                object? found = FindShortcutRecursive(service, ReadString(node, "Id"), targetId);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private static object? FindChild(object service, string parentId, string? name, string? targetId)
    {
        foreach (object node in GetChildren(service, parentId))
        {
            if ((name == null || string.Equals(ReadString(node, "Name"), name, StringComparison.Ordinal)) &&
                (targetId == null || string.Equals(ReadString(node, "TargetId"), targetId, StringComparison.Ordinal)))
            {
                return node;
            }
        }
        return null;
    }

    private static IEnumerable GetChildren(object service, string parentId)
    {
        return (IEnumerable)(Invoke(service, "GetChildren", parentId)
            ?? throw new InvalidOperationException("GetChildren returned null."));
    }

    private static object? Invoke(object service, string methodName, params object[] arguments)
    {
        return ((Type)service).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments);
    }

    private static object? ReadValue(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.GetValue(target);
    }

    private static string ReadString(object target, string propertyName)
    {
        return ReadValue(target, propertyName)?.ToString() ?? string.Empty;
    }

    private void Pass(string screenshotPath, bool sawSave, string persistedPath)
    {
#if IL2CPP
        const string runtime = "Il2cpp";
#else
        const string runtime = "Mono";
#endif
        _completed = true;
        string result =
            $"PASS|Runtime={runtime}|Phase={_phase}|Scene={SceneManager.GetActiveScene().name}|" +
            $"Reloaded={string.Equals(_phase, "reload", StringComparison.Ordinal)}|SaveObserved={sawSave}|" +
            $"Persisted=True|Laptop={_laptop}|Screenshot={screenshotPath}|SaveFile={persistedPath}";
        File.WriteAllText(Path.Combine(_outputDirectory, "result.txt"), result);
        LoggerInstance.Msg($"[UsableComputerVfsSmoke] {result}");
        CleanupFixture();
        Application.Quit();
    }

    private void Fail(string message, Exception exception)
    {
        if (_completed)
            return;

        _completed = true;
        string result = $"FAIL|Runtime={ConstantsRuntime()}|Phase={_phase}|Reason={message}|Exception={exception.GetType().Name}:{exception.Message}";
        File.WriteAllText(Path.Combine(_outputDirectory, "result.txt"), result);
        LoggerInstance.Error($"[UsableComputerVfsSmoke] {result}");
        LoggerInstance.Error(exception.ToString());
        CleanupFixture();
        Application.Quit();
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is TargetInvocationException invocation
            ? invocation.InnerException ?? exception
            : exception;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string GetArgument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException($"Missing required argument '{name}'.");
        return args[index + 1];
    }

    private static S1DateTime GetNow()
    {
        return S1DateTime.Now;
    }

    private static string ConstantsRuntime()
    {
#if IL2CPP
        return "Il2cpp";
#else
        return "Mono";
#endif
    }
}

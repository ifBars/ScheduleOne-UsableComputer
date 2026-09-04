using System.Collections;
using System.Reflection;
using MelonLoader;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;
using ScheduleOne.Persistence.Datas;
using UnityEngine;
using UnityEngine.SceneManagement;
using S1PlayerCamera = ScheduleOne.PlayerScripts.PlayerCamera;
using S1CameraSingleton = ScheduleOne.DevUtilities.PlayerSingleton<ScheduleOne.PlayerScripts.PlayerCamera>;
using S1NativeTimeManager = ScheduleOne.GameTime.TimeManager;
using S1Registry = ScheduleOne.Registry;

[assembly: MelonInfo(
    typeof(UsableComputer.DisplaySmoke.Core),
    "Usable Computer Display Smoke",
    "1.0.0",
    "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.DisplaySmoke;

public sealed class Core : MelonMod
{
    private bool _enabled;
    private bool _started;
    private bool _completed;
    private string _outputDirectory = string.Empty;
    private string _savePath = string.Empty;
    private IDisposable? _desktop;
    private GameObject? _computerModel;
    private S1PlayerCamera? _playerCamera;
    private bool _cameraOverridden;
    private bool _fovOverridden;

    public override void OnInitializeMelon()
    {
        string[] args = Environment.GetCommandLineArgs();
        _enabled = Array.IndexOf(args, "--usable-computer-display-smoke") >= 0;
        if (!_enabled)
            return;

        _outputDirectory = GetArgument(args, "--usable-computer-display-smoke-dir");
        _savePath = GetArgument(args, "--usable-computer-display-smoke-save");
        Directory.CreateDirectory(_outputDirectory);
        LoggerInstance.Msg($"[UsableComputerDisplaySmoke] Enabled Save={_savePath}");
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

    public override void OnDeinitializeMelon()
    {
        if (_fovOverridden && _playerCamera != null)
            _playerCamera.StopFOVOverride(0f);
        if (_cameraOverridden && _playerCamera != null)
            _playerCamera.StopTransformOverride(0f);
        _fovOverridden = false;
        _cameraOverridden = false;
        _playerCamera = null;
        _desktop?.Dispose();
        _desktop = null;
        if (_computerModel != null)
            UnityEngine.Object.Destroy(_computerModel);
        _computerModel = null;
    }

    private IEnumerator StartFixture()
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while ((LoadManager.Instance == null || SaveManager.Instance == null || LoadManager.Instance.IsLoading) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        try
        {
            Require(LoadManager.Instance != null, "LoadManager was unavailable in Menu.");
            Require(SaveManager.Instance != null, "SaveManager was unavailable in Menu.");
            var now = DateTime.Now;
            var saveInfo = new SaveInfo(
                _savePath,
                -1,
                "Usable Computer Display Smoke",
                now,
                now,
                0f,
                Application.version,
                new MetaData(
                    (DateTimeData?)null,
                    (DateTimeData?)null,
                    Application.version,
                    Application.version,
                    playTutorial: false));
            LoadManager.Instance!.StartGame(saveInfo, allowLoadStacking: false, allowSaveBackup: false);
        }
        catch (Exception exception)
        {
            Fail("Could not start the disposable save", exception);
        }
    }

    private IEnumerator RunScenario()
    {
        float deadline = Time.realtimeSinceStartup + 90f;
        while ((LoadManager.Instance == null ||
                LoadManager.Instance.IsLoading ||
                !LoadManager.Instance.IsGameLoaded ||
                !LoadManager.Instance.IsInGameScene ||
                Camera.main == null) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (LoadManager.Instance == null ||
            LoadManager.Instance.IsLoading ||
            !LoadManager.Instance.IsGameLoaded ||
            !LoadManager.Instance.IsInGameScene ||
            Camera.main == null)
        {
            Fail("Normal gameplay did not become ready", new TimeoutException("Main readiness timed out."));
            yield break;
        }

        yield return new WaitForSecondsRealtime(1f);

        float scale;
        float fieldOfView;
        float donorWidth;
        float modelWidth;
        int rendererCount;
        int colliderCount;
        string clockText;
        try
        {
            Assembly assembly = GetUsableComputerAssembly();
            Type profile = assembly.GetType("UsableComputer.DisplayProfile", throwOnError: true)!;
            scale = ReadConstant<float>(profile, "ModelScale");
            fieldOfView = ReadConstant<float>(profile, "InteractionFieldOfView");
            Require(Math.Abs(scale - 1.15f) < 0.001f, $"Unexpected model scale: {scale}");
            Require(Math.Abs(fieldOfView - 55f) < 0.001f, $"Unexpected field of view: {fieldOfView}");

            BuildableItemDefinition donor = S1Registry.GetItem("launderingstation") as BuildableItemDefinition
                ?? throw new InvalidOperationException("The laundering station donor is unavailable.");
            donorWidth = CalculateWidth(donor.BuiltItem.gameObject.GetComponentsInChildren<Renderer>(true));
            _computerModel = CreateComputerModel(assembly, donor.BuiltItem.gameObject);
            rendererCount = _computerModel.GetComponentsInChildren<Renderer>(true).Length;
            colliderCount = _computerModel.GetComponentsInChildren<Collider>(true).Length;
            modelWidth = CalculateWidth(_computerModel.GetComponentsInChildren<Renderer>(true));
            Require(rendererCount > 0, "The runtime model has no renderers.");
            Require(colliderCount == 0, "The runtime model inherited a collider.");
            Require(modelWidth > donorWidth, $"The model did not grow: donor={donorWidth}, model={modelWidth}");

            Require(S1CameraSingleton.InstanceExists, "The player camera singleton is unavailable.");
            _playerCamera = S1CameraSingleton.Instance;
            Camera camera = _playerCamera.Camera;
            Transform cameraAnchor = FindDescendant(_computerModel.transform, "UsableComputer_CameraAnchor")
                ?? throw new InvalidOperationException("The fixture computer is missing its camera anchor.");
            Transform screenAnchor = FindDescendant(_computerModel.transform, "UsableComputer_ScreenAnchor")
                ?? throw new InvalidOperationException("The fixture computer is missing its screen anchor.");
            _computerModel.transform.rotation = camera.transform.rotation * Quaternion.Inverse(cameraAnchor.localRotation);
            Vector3 scaledAnchor = Vector3.Scale(cameraAnchor.localPosition, _computerModel.transform.localScale);
            _computerModel.transform.position = camera.transform.position - (_computerModel.transform.rotation * scaledAnchor);
            _computerModel.SetActive(true);
            _playerCamera.OverrideTransform(cameraAnchor.position, cameraAnchor.rotation, 0f);
            _cameraOverridden = true;
            _playerCamera.OverrideFOV(fieldOfView, 0f);
            _fovOverridden = true;

            Type shellType = assembly.GetType("UsableComputer.UI.DesktopShell", throwOnError: true)!;
            object shell = Activator.CreateInstance(
                shellType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { screenAnchor, camera, new Action(() => { }) },
                culture: null) ?? throw new InvalidOperationException("DesktopShell could not be created.");
            shellType.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(shell, null);
            shellType.GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(shell, new object[] { "app-studio" });
            shellType.GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(shell, new object[] { "settings" });
            GameObject? settingsWindow = GameObject.Find("Window_settings");
            Require(
                settingsWindow != null && settingsWindow.activeInHierarchy,
                "The Settings window did not open for visual validation.");
            _desktop = (IDisposable)shell;

            string expectedClock = S1NativeTimeManager.Get12HourTime(S1NativeTimeManager.Instance.CurrentTime, true);
            clockText = ReadText(GameObject.Find("Clock"));
            Require(
                string.Equals(clockText, expectedClock, StringComparison.Ordinal),
                $"Desktop clock '{clockText}' did not match game time '{expectedClock}'.");
        }
        catch (Exception exception)
        {
            Fail("Display scenario setup failed", Unwrap(exception));
            yield break;
        }

        Screen.SetResolution(1280, 720, false);
        yield return new WaitForSecondsRealtime(2f);

        string screenshotPath = Path.Combine(_outputDirectory, "display.png");
        try
        {
            RequireDesktopFitsViewport(Camera.main!);
            ScreenCapture.CaptureScreenshot(screenshotPath);
        }
        catch (Exception exception)
        {
            Fail("Display validation failed", Unwrap(exception));
            yield break;
        }

        deadline = Time.realtimeSinceStartup + 10f;
        while ((!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        try
        {
            Require(File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0, "Screenshot was not written.");
            Pass(screenshotPath, scale, fieldOfView, donorWidth, modelWidth, rendererCount, colliderCount, clockText);
        }
        catch (Exception exception)
        {
            Fail("Display validation failed", Unwrap(exception));
        }
    }

    private static void RequireDesktopFitsViewport(Camera camera)
    {
        GameObject canvasObject = GameObject.Find("UsableComputer_DesktopCanvas")
            ?? throw new InvalidOperationException("The desktop canvas was not found.");
        RectTransform rect = canvasObject.GetComponent<RectTransform>();
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            Require(viewport.z > 0f, "The desktop is behind the interaction camera.");
            Require(viewport.x >= 0f && viewport.x <= 1f, $"Desktop x={viewport.x} is clipped.");
            Require(viewport.y >= 0f && viewport.y <= 1f, $"Desktop y={viewport.y} is clipped.");
        }
    }

    private static GameObject CreateComputerModel(Assembly assembly, GameObject donorObject)
    {
        Type factory = assembly.GetType("UsableComputer.Content.NativeComputerModelFactory", throwOnError: true)!;
        MethodInfo create = factory.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (GameObject)(create.Invoke(null, new object[] { donorObject })
            ?? throw new InvalidOperationException("The physical computer model could not be created."));
    }

    private static float CalculateWidth(Renderer[] renderers)
    {
        Require(renderers.Length > 0, "No renderers were available for bounds measurement.");
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds.size.x;
    }

    private static string ReadText(GameObject? gameObject)
    {
        if (gameObject == null)
            throw new InvalidOperationException("The taskbar clock object was not found.");

        foreach (Component component in gameObject.GetComponents<Component>())
        {
            PropertyInfo? text = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (text?.PropertyType == typeof(string))
                return (string?)text.GetValue(component) ?? string.Empty;
        }

        throw new InvalidOperationException("The taskbar clock has no text component.");
    }

    private static Transform? FindDescendant(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform? match = FindDescendant(root.GetChild(index), name);
            if (match != null)
                return match;
        }
        return null;
    }

    private static Assembly GetUsableComputerAssembly()
    {
        return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                   assembly.GetName().Name?.StartsWith("UsableComputer_", StringComparison.Ordinal) == true)
               ?? throw new InvalidOperationException("The Usable Computer assembly is not loaded.");
    }

    private static T ReadConstant<T>(Type type, string name)
    {
        return (T)(type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()
            ?? throw new InvalidOperationException($"{type.FullName}.{name} has no value."));
    }

    private void Pass(
        string screenshotPath,
        float scale,
        float fieldOfView,
        float donorWidth,
        float modelWidth,
        int rendererCount,
        int colliderCount,
        string clockText)
    {
        _completed = true;
        string result =
            $"PASS|Runtime=Mono|Scene={SceneManager.GetActiveScene().name}|Scale={scale:F2}|FOV={fieldOfView:F0}|" +
            $"DonorWidth={donorWidth:F3}|ModelWidth={modelWidth:F3}|Renderers={rendererCount}|" +
            $"Colliders={colliderCount}|DesktopFits=True|GameClock={clockText}|Screenshot={screenshotPath}";
        File.WriteAllText(Path.Combine(_outputDirectory, "result.txt"), result);
        LoggerInstance.Msg($"[UsableComputerDisplaySmoke] {result}");
        Application.Quit();
    }

    private void Fail(string message, Exception exception)
    {
        if (_completed)
            return;
        _completed = true;
        string result = $"FAIL|Runtime=Mono|Reason={message}|Exception={exception.GetType().Name}:{exception.Message}";
        File.WriteAllText(Path.Combine(_outputDirectory, "result.txt"), result);
        LoggerInstance.Error($"[UsableComputerDisplaySmoke] {result}");
        LoggerInstance.Error(exception.ToString());
        Application.Quit();
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException
            : exception;
    }

    private static string GetArgument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

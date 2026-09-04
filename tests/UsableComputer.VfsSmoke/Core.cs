using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

#if IL2CPP
using Il2CppInterop.Runtime;
using S1BuildableDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1DateTime = Il2CppSystem.DateTime;
using S1DateTimeData = Il2CppScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using S1MetaData = Il2CppScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = Il2CppScheduleOne.Persistence.SaveInfo;
using S1SaveManager = Il2CppScheduleOne.Persistence.SaveManager;
using S1Registry = Il2CppScheduleOne.Registry;
#else
using S1BuildableDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1DateTime = System.DateTime;
using S1DateTimeData = ScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
using S1MetaData = ScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = ScheduleOne.Persistence.SaveInfo;
using S1SaveManager = ScheduleOne.Persistence.SaveManager;
using S1Registry = ScheduleOne.Registry;
#endif

[assembly: MelonInfo(
    typeof(UsableComputer.VfsSmoke.Core),
    "Usable Computer VFS Smoke",
    "1.0.0",
    "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.VfsSmoke;

public sealed class Core : MelonMod
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

    public override void OnInitializeMelon()
    {
        string[] args = Environment.GetCommandLineArgs();
        _enabled = Array.IndexOf(args, "--usable-computer-vfs-smoke") >= 0;
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

    public override void OnDeinitializeMelon()
    {
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

    private string VerifyReloadedVirtualFileSystem()
    {
        object service = GetServiceType();
        object folder = FindChild(service, "desktop", FolderName, targetId: null)
            ?? throw new InvalidOperationException("Persisted folder was not restored after process reload.");
        string folderId = ReadString(folder, "Id");
        Require(FindChild(service, folderId, name: null, NotesAppId) != null, "Persisted Notes shortcut was not restored inside the folder.");
        return folderId;
    }

    private void CleanVirtualFileSystem(string folderId)
    {
        object service = GetServiceType();
        object notes = FindChild(service, folderId, name: null, NotesAppId)
            ?? throw new InvalidOperationException("Notes shortcut disappeared before cleanup.");
        Invoke(service, "Move", ReadString(notes, "Id"), "desktop");
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

        // Put the physical model at the production viewing pose relative to the
        // current gameplay camera. The screenshot therefore includes the CRT,
        // keyboard, room, and HUD—not a camera-mounted replacement canvas.
        _computerModel.transform.rotation = camera.transform.rotation * Quaternion.Inverse(cameraAnchor.localRotation);
        _computerModel.transform.position = camera.transform.position -
            (_computerModel.transform.rotation * cameraAnchor.localPosition) -
            (camera.transform.forward * 0.16f);
        _computerModel.SetActive(true);
        camera.fieldOfView = 55f;
        object shell = Activator.CreateInstance(
            shellType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { _screenAnchor.transform, camera, new Action(() => { }) },
            culture: null) ?? throw new InvalidOperationException("DesktopShell could not be created.");
        shellType.GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(shell, null);
        shellType.GetMethod("OpenFolder", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(shell, new object[] { folderId });
        _desktop = (IDisposable)shell;
    }

    private static GameObject CreateComputerModel(Assembly assembly)
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
            $"Persisted=True|Screenshot={screenshotPath}|SaveFile={persistedPath}";
        File.WriteAllText(Path.Combine(_outputDirectory, "result.txt"), result);
        LoggerInstance.Msg($"[UsableComputerVfsSmoke] {result}");
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(
    typeof(UsableComputer.ChildHost.Core),
    "Usable Computer Nested Game Host",
    "1.0.0",
    "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.ChildHost;

public sealed class Core : MelonMod
{
    private const float CaptureInterval = 0.1f;
    private const int CaptureWidth = UsableComputer.DisplayProfile.NestedWidth;
    private const int CaptureHeight = UsableComputer.DisplayProfile.NestedHeight;
    private MemoryMappedFile? _mapping;
    private MemoryMappedViewAccessor? _view;
    private Process? _parent;
    private float _nextCapture;
    private int _frameId;
    private bool _active;
    private bool _windowMoved;
    private IntPtr _windowHandle;
    private RenderTexture? _captureTarget;
    private readonly Dictionary<Camera, RenderTexture?> _redirectedCameras = new();
    private readonly Dictionary<Canvas, CanvasState> _redirectedCanvases = new();

    private sealed class CanvasState
    {
        internal RenderMode RenderMode;
        internal Camera? WorldCamera;
        internal float PlaneDistance;
    }

    public override void OnInitializeMelon()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, NestedChildProtocol.MarkerArgument) < 0)
            return;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MelonLogger.Error("[NestedGameHost] Nested Schedule I currently requires Windows.");
            return;
        }

        string mappingName = GetArgument(args, NestedChildProtocol.MappingArgument);
        string profilePath = GetArgument(args, NestedChildProtocol.ProfileArgument);
        string parentText = GetArgument(args, NestedChildProtocol.ParentArgument);
        if (string.IsNullOrWhiteSpace(mappingName) ||
            string.IsNullOrWhiteSpace(profilePath) ||
            !int.TryParse(parentText, out int parentId))
        {
            MelonLogger.Error("[NestedGameHost] Required launch arguments are missing.");
            return;
        }

        try
        {
            Directory.CreateDirectory(profilePath);
            NestedChildContext.Activate(profilePath);
            PatchChildDisplaySettings();
            _mapping = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.ReadWrite);
            _view = _mapping.CreateViewAccessor(0, NestedChildProtocol.MappingSize, MemoryMappedFileAccess.ReadWrite);
            _parent = Process.GetProcessById(parentId);
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            WriteHeader(NestedChildProtocol.StateStarting, 0, 0, 0, 0);
            _active = true;
            MelonLogger.Msg($"[NestedGameHost] Started with isolated profile '{profilePath}'.");
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[NestedGameHost] Initialization failed: {exception}");
            WriteError();
        }
    }

    public override void OnUpdate()
    {
        if (!_active)
            return;

        if (_parent == null || _parent.HasExited)
        {
            Application.Quit();
            return;
        }

        if (!_windowMoved)
            TryMoveWindowOffscreen();

        EnsureOffscreenRendering();
    }

    public override void OnLateUpdate()
    {
        if (!_active || _captureTarget == null || Time.realtimeSinceStartup < _nextCapture)
            return;

        _nextCapture = Time.realtimeSinceStartup + CaptureInterval;
        CaptureFrame();
    }

    private void CaptureFrame()
    {
        MemoryMappedViewAccessor? view = _view;
        if (view == null)
            return;

        RenderTexture? target = _captureTarget;
        if (target == null || !target.IsCreated())
            return;

        Texture2D? frameTexture = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            frameTexture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, mipChain: false);
            frameTexture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, recalculateMipMaps: false);
            frameTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            int width = frameTexture.width;
            int height = frameTexture.height;

            var pixels = frameTexture.GetPixels32();
            var bytes = new byte[pixels.Length * 4];
            for (int index = 0; index < pixels.Length; index++)
            {
                int offset = index * 4;
                Color32 pixel = pixels[index];
                bytes[offset] = pixel.r;
                bytes[offset + 1] = pixel.g;
                bytes[offset + 2] = pixel.b;
                bytes[offset + 3] = pixel.a;
            }

            view.WriteArray(NestedChildProtocol.HeaderSize, bytes, 0, bytes.Length);
            _frameId++;
            long handle = _windowHandle.ToInt64();
            WriteHeader(NestedChildProtocol.StateReady, _frameId, width, height, handle);
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"[NestedGameHost] Frame capture failed: {exception.Message}");
            WriteError();
        }
        finally
        {
            RenderTexture.active = previous;
            if (frameTexture != null)
                UnityEngine.Object.Destroy(frameTexture);
        }
    }

    private void EnsureOffscreenRendering()
    {
        if (_captureTarget == null)
        {
            _captureTarget = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "UsableComputer.NestedGameCapture",
                filterMode = FilterMode.Bilinear,
            };
            _captureTarget.Create();
        }

        Camera? uiCamera = null;
        foreach (Camera camera in UnityEngine.Object.FindObjectsOfType<Camera>())
        {
            if (!camera.isActiveAndEnabled || camera.targetDisplay != 0)
                continue;

            if (camera.targetTexture == null)
            {
                _redirectedCameras[camera] = null;
                camera.targetTexture = _captureTarget;
            }
            if (camera.targetTexture == _captureTarget && (uiCamera == null || camera.depth > uiCamera.depth))
                uiCamera = camera;
        }

        if (uiCamera == null)
            return;

        foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;

            _redirectedCanvases[canvas] = new CanvasState
            {
                RenderMode = canvas.renderMode,
                WorldCamera = canvas.worldCamera,
                PlaneDistance = canvas.planeDistance,
            };
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = Math.Max(uiCamera.nearClipPlane + 0.1f, 1f);
        }
        Canvas.ForceUpdateCanvases();
    }

    public override void OnDeinitializeMelon() => DisposeBridge();

    public override void OnApplicationQuit() => DisposeBridge();

    private void TryMoveWindowOffscreen()
    {
        IntPtr handle = FindProcessWindow();
        if (handle == IntPtr.Zero)
            return;

        _windowHandle = handle;
        long extendedStyle = GetWindowLongPtr(handle, -20).ToInt64();
        extendedStyle = (extendedStyle | 0x00000080L) & ~0x00040000L;
        SetWindowLongPtr(handle, -20, new IntPtr(extendedStyle));
        SetWindowPos(handle, IntPtr.Zero, -32000, -32000, 640, 400, 0x0010 | 0x0004 | 0x0020);
        ShowWindow(handle, 8);
        _windowMoved = true;
    }

    private static IntPtr FindProcessWindow()
    {
        IntPtr result = Process.GetCurrentProcess().MainWindowHandle;
        if (result != IntPtr.Zero)
            return result;

        uint currentId = (uint)Process.GetCurrentProcess().Id;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint processId);
            if (processId != currentId)
                return true;
            result = window;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    private void WriteHeader(int state, int frameId, int width, int height, long handle)
    {
        if (_view == null)
            return;

        _view.Write(0, NestedChildProtocol.Magic);
        _view.Write(4, state);
        _view.Write(8, frameId);
        _view.Write(12, width);
        _view.Write(16, height);
        _view.Write(20, width * height * 4);
        _view.Write(24, handle);
        _view.Flush();
    }

    private void WriteError()
    {
        if (_view != null)
            WriteHeader(NestedChildProtocol.StateError, _frameId, 0, 0, 0);
    }

    private void DisposeBridge()
    {
        if (!_active && _view == null && _mapping == null)
            return;

        _active = false;
        foreach (KeyValuePair<Canvas, CanvasState> pair in _redirectedCanvases)
        {
            if (pair.Key == null)
                continue;
            pair.Key.renderMode = pair.Value.RenderMode;
            pair.Key.worldCamera = pair.Value.WorldCamera;
            pair.Key.planeDistance = pair.Value.PlaneDistance;
        }
        _redirectedCanvases.Clear();
        foreach (KeyValuePair<Camera, RenderTexture?> pair in _redirectedCameras)
        {
            if (pair.Key != null && pair.Key.targetTexture == _captureTarget)
                pair.Key.targetTexture = pair.Value;
        }
        _redirectedCameras.Clear();
        if (_captureTarget != null)
        {
            _captureTarget.Release();
            UnityEngine.Object.Destroy(_captureTarget);
            _captureTarget = null;
        }
        _view?.Dispose();
        _view = null;
        _mapping?.Dispose();
        _mapping = null;
        _parent?.Dispose();
        _parent = null;
    }

    private static void PatchChildDisplaySettings()
    {
        Type? settingsType = AccessTools.TypeByName("ScheduleOne.DevUtilities.Settings");
        var applyMethod = settingsType == null
            ? null
            : AccessTools.Method(settingsType, "ApplyDisplaySettings");
        var prefixMethod = AccessTools.Method(typeof(Core), nameof(SkipDisplaySettings));
        if (applyMethod == null || prefixMethod == null)
            throw new MissingMethodException("Could not patch Schedule I display settings for the nested process.");

        new HarmonyLib.Harmony("UsableComputer.NestedGame.DisplaySettings")
            .Patch(applyMethod, prefix: new HarmonyMethod(prefixMethod));
    }

    private static bool SkipDisplaySettings() => false;

    private static string GetArgument(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
                return args[index + 1].Trim('"');
        }

        return string.Empty;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}

internal static class NestedChildContext
{
    internal static bool IsActive { get; private set; }
    internal static string ProfilePath { get; private set; } = string.Empty;

    internal static void Activate(string profilePath)
    {
        ProfilePath = profilePath;
        IsActive = true;
    }
}

[HarmonyPatch(typeof(Application), "get_persistentDataPath")]
internal static class PersistentDataPathPatch
{
    private static bool Prefix(ref string __result)
    {
        if (!NestedChildContext.IsActive)
            return true;

        __result = NestedChildContext.ProfilePath;
        return false;
    }
}

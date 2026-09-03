using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using MelonLoader;
using MelonLoader.Utils;

namespace UsableComputer.NestedGame;

internal sealed class NestedGameProcess : IDisposable
{
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private Process? _process;
    private byte[] _frame = Array.Empty<byte>();
    private int _frameId;
    private IntPtr _childWindow;
    private bool _inputFocused;
    private bool _bridgeReady;
    private bool _disposed;

    private NestedGameProcess(MemoryMappedFile mapping, MemoryMappedViewAccessor view, Process process)
    {
        _mapping = mapping;
        _view = view;
        _process = process;
    }

    internal bool IsRunning => !_disposed && _process is { HasExited: false };
    internal bool HasFrame => Width > 0 && Height > 0 && _frame.Length == Width * Height * 4;
    internal bool HasVisibleFrame { get; private set; }
    internal int Width { get; private set; }
    internal int Height { get; private set; }
    internal string Status { get; private set; } = "Starting Schedule I...";

    internal static NestedGameProcess Start()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Nested Schedule I currently requires Windows.");

        string userData = MelonEnvironment.UserDataDirectory;
        string gameRoot = Directory.GetParent(userData)?.FullName
            ?? throw new InvalidOperationException("Could not resolve the Schedule I installation directory.");
        string runtimeRoot = Path.Combine(userData, "UsableComputer", "NestedScheduleOne");
        string loaderRoot = Path.Combine(runtimeRoot, "Loader");
        string modsDirectory = Path.Combine(loaderRoot, "Mods");
        string profileDirectory = Path.Combine(runtimeRoot, "Profile");
        PrepareLoaderRuntime(gameRoot, loaderRoot);
        Directory.CreateDirectory(modsDirectory);
        Directory.CreateDirectory(profileDirectory);

        string[] existingMods = Directory.GetFiles(modsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (string existingMod in existingMods)
        {
            if (!string.Equals(Path.GetFileName(existingMod), "UsableComputer.ChildHost.dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The isolated nested-game Mods folder contains an unexpected DLL. Remove it before launching: " + existingMod);
            }
        }

        string hostSource = Path.Combine(gameRoot, "UserLibs", "UsableComputer.ChildHost.dll");
        if (!File.Exists(hostSource))
            throw new FileNotFoundException("UsableComputer.ChildHost.dll is missing from the game's UserLibs folder.", hostSource);
        File.Copy(hostSource, Path.Combine(modsDirectory, "UsableComputer.ChildHost.dll"), overwrite: true);

        string executable = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Could not resolve the running Schedule I executable.");
        string mapName = "UsableComputerNested_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N");
        MemoryMappedFile? mapping = null;
        MemoryMappedViewAccessor? view = null;
        try
        {
            mapping = MemoryMappedFile.CreateOrOpen(mapName, NestedGameProtocol.MappingSize, MemoryMappedFileAccess.ReadWrite);
            view = mapping.CreateViewAccessor(0, NestedGameProtocol.MappingSize, MemoryMappedFileAccess.ReadWrite);
            view.Write(0, NestedGameProtocol.Magic);
            view.Write(4, NestedGameProtocol.StateStarting);

            int parentId = Process.GetCurrentProcess().Id;
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = gameRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Arguments =
                    "--melonloader.basedir " + Quote(loaderRoot) + " " +
                    "--melonloader.hideconsole --melonloader.disablestartscreen " +
                    NestedGameProtocol.MarkerArgument + " " +
                    NestedGameProtocol.MappingArgument + " " + Quote(mapName) + " " +
                    NestedGameProtocol.ProfileArgument + " " + Quote(profileDirectory) + " " +
                    NestedGameProtocol.ParentArgument + " " + parentId + " " +
                    "-screen-width 640 -screen-height 360 -screen-fullscreen 0 -popupwindow",
            };
            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows did not start the nested Schedule I process.");
            return new NestedGameProcess(mapping, view, process);
        }
        catch
        {
            view?.Dispose();
            mapping?.Dispose();
            throw;
        }
    }

    internal bool TryReadFrame(out byte[] frame)
    {
        frame = Array.Empty<byte>();
        if (_disposed)
            return false;
        if (_process is { HasExited: true })
        {
            Status = "Nested Schedule I exited.";
            return false;
        }

        if (_childWindow == IntPtr.Zero)
        {
            _process?.Refresh();
            _childWindow = _process?.MainWindowHandle ?? IntPtr.Zero;
        }
        if (!_inputFocused && !_bridgeReady && _childWindow != IntPtr.Zero)
            ShowWindow(_childWindow, 0);

        int magic = _view.ReadInt32(0);
        int state = _view.ReadInt32(4);
        int frameId = _view.ReadInt32(8);
        int width = _view.ReadInt32(12);
        int height = _view.ReadInt32(16);
        int byteCount = _view.ReadInt32(20);
        long window = _view.ReadInt64(24);
        if (magic != NestedGameProtocol.Magic)
        {
            Status = "Waiting for the nested-game bridge...";
            return false;
        }
        if (state == NestedGameProtocol.StateError)
        {
            Status = "The nested game could not capture its display. Check the nested MelonLoader log.";
            return false;
        }
        if (state != NestedGameProtocol.StateReady || frameId == _frameId)
        {
            Status = "Starting Schedule I...";
            return false;
        }
        if (width <= 0 || height <= 0 ||
            width > NestedGameProtocol.MaximumWidth || height > NestedGameProtocol.MaximumHeight ||
            byteCount != width * height * 4)
        {
            Status = "The nested game returned an invalid display frame.";
            return false;
        }

        if (_frame.Length != byteCount)
            _frame = new byte[byteCount];
        _view.ReadArray(NestedGameProtocol.HeaderSize, _frame, 0, byteCount);
        _frameId = frameId;
        Width = width;
        Height = height;
        _childWindow = new IntPtr(window);
        _bridgeReady = _childWindow != IntPtr.Zero;
        if (!_inputFocused && _bridgeReady)
            ShowWindow(_childWindow, 8);
        HasVisibleFrame = ContainsVisiblePixels(_frame);
        Status = "Running · click the game to control it · F10 returns to the desktop";
        frame = _frame;
        return true;
    }

    internal bool FocusChild()
    {
        if (!IsRunning)
            return false;
        if (_childWindow == IntPtr.Zero)
        {
            _process?.Refresh();
            _childWindow = _process?.MainWindowHandle ?? IntPtr.Zero;
        }
        if (_childWindow == IntPtr.Zero)
            return false;
        _inputFocused = true;
        ShowWindow(_childWindow, 5);
        if (SetForegroundWindow(_childWindow))
            return true;
        _inputFocused = false;
        ShowWindow(_childWindow, 0);
        return false;
    }

    internal void ReleaseInput()
    {
        _inputFocused = false;
        if (_childWindow != IntPtr.Zero)
            ShowWindow(_childWindow, _bridgeReady ? 8 : 0);
    }

    internal static void FocusParent()
    {
        IntPtr parent = Process.GetCurrentProcess().MainWindowHandle;
        if (parent != IntPtr.Zero)
            SetForegroundWindow(parent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill();
                _process.WaitForExit(5000);
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"[{Constants.ModName}] Could not stop nested Schedule I: {exception.Message}");
        }
        _process?.Dispose();
        _process = null;
        _view.Dispose();
        _mapping.Dispose();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static bool ContainsVisiblePixels(byte[] frame)
    {
        int visibleSamples = 0;
        for (int offset = 0; offset < frame.Length; offset += 64)
        {
            if (frame[offset] + frame[offset + 1] + frame[offset + 2] < 48)
                continue;
            if (++visibleSamples >= 16)
                return true;
        }
        return false;
    }

    private static void PrepareLoaderRuntime(string gameRoot, string loaderRoot)
    {
        string sourceRoot = Path.Combine(gameRoot, "MelonLoader");
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException("The game's MelonLoader runtime is missing: " + sourceRoot);

        string destinationRoot = Path.Combine(loaderRoot, "MelonLoader");
        foreach (string directoryName in new[] { "Dependencies", "net35", "net472", "net6" })
        {
            string source = Path.Combine(sourceRoot, directoryName);
            if (Directory.Exists(source))
                CopyDirectoryIncremental(source, Path.Combine(destinationRoot, directoryName));
        }
    }

    private static void CopyDirectoryIncremental(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string sourceFile in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            string destinationFile = Path.Combine(destination, Path.GetFileName(sourceFile));
            var sourceInfo = new FileInfo(sourceFile);
            var destinationInfo = new FileInfo(destinationFile);
            if (!destinationInfo.Exists || destinationInfo.Length != sourceInfo.Length)
                File.Copy(sourceFile, destinationFile, overwrite: true);
        }
        foreach (string sourceDirectory in Directory.GetDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectoryIncremental(
                sourceDirectory,
                Path.Combine(destination, Path.GetFileName(sourceDirectory)));
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}

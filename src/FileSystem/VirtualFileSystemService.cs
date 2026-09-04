using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Persistence;

namespace UsableComputer.FileSystem;

internal static class VirtualFileSystemService
{
    private static readonly Action RegistryChangedHandler = ReconcileApps;
    private static VirtualFileSystem _fileSystem = new();
    private static bool _initialized;

    internal static event Action? Changed;

    internal static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        DesktopAppRegistry.Changed += RegistryChangedHandler;
        ReconcileApps();
    }

    internal static void PrepareForLoad()
    {
        _fileSystem = new VirtualFileSystem();
        ReconcileApps(notify: false);
        UsableComputerFileSystemSave.ResetSnapshot(_fileSystem.CreateSnapshot());
        NotifyChanged();
    }

    internal static void Load(VirtualFileSystemSnapshot snapshot)
    {
        try
        {
            _fileSystem = new VirtualFileSystem(snapshot);
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Virtual filesystem data was invalid and has been reset: {exception.Message}");
            _fileSystem = new VirtualFileSystem();
        }

        ReconcileApps(notify: false);
        UsableComputerFileSystemSave.Capture(_fileSystem.CreateSnapshot());
        NotifyChanged();
    }

    internal static IReadOnlyList<VirtualFileSystemNode> GetChildren(string parentId)
    {
        return _fileSystem.GetChildren(parentId);
    }

    internal static bool TryGetNode(string nodeId, out VirtualFileSystemNode node)
    {
        return _fileSystem.TryGetNode(nodeId, out node!);
    }

    internal static string GetPath(string nodeId)
    {
        return _fileSystem.GetPath(nodeId);
    }

    internal static VirtualFileSystemNode CreateDirectory(string parentId, string name)
    {
        VirtualFileSystemNode node = _fileSystem.CreateDirectory(parentId, name);
        Commit();
        return node;
    }

    internal static string CreateUniqueFolderName(string parentId)
    {
        return _fileSystem.CreateUniqueFolderName(parentId);
    }

    internal static void Rename(string nodeId, string name)
    {
        _fileSystem.Rename(nodeId, name);
        Commit();
    }

    internal static void Move(string nodeId, string destinationId)
    {
        _fileSystem.Move(nodeId, destinationId);
        Commit();
    }

    internal static void Delete(string nodeId)
    {
        _fileSystem.Delete(nodeId);
        Commit();
    }

    internal static void ReconcileApps()
    {
        ReconcileApps(notify: true);
    }

    internal static void Shutdown()
    {
        if (!_initialized)
            return;

        DesktopAppRegistry.Changed -= RegistryChangedHandler;
        Changed = null;
        _fileSystem = new VirtualFileSystem();
        _initialized = false;
    }

    private static void ReconcileApps(bool notify)
    {
        bool changed = false;
        foreach (DesktopAppDescriptor descriptor in DesktopAppRegistry.GetAll())
        {
            if (string.Equals(descriptor.Id, Constants.SettingsAppId, StringComparison.Ordinal))
                continue;
            changed |= _fileSystem.EnsureAppShortcut(descriptor.Id, descriptor.Title);
        }

        if (!changed)
            return;

        UsableComputerFileSystemSave.Capture(_fileSystem.CreateSnapshot());
        if (notify)
            NotifyChanged();
    }

    private static void Commit()
    {
        UsableComputerFileSystemSave.Capture(_fileSystem.CreateSnapshot());
        NotifyChanged();
    }

    private static void NotifyChanged()
    {
        Delegate[] handlers = Changed?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Virtual filesystem listener failed: {exception.Message}");
            }
        }
    }
}

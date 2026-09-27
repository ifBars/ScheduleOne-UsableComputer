using S1API.Internal.Abstraction;
using S1API.Saveables;
using UsableComputer.FileSystem;

namespace UsableComputer.Persistence;

public sealed class UsableComputerFileSystemSave : Saveable
{
    private static UsableComputerFileSystemSave? _instance;

    [SaveableField("virtual-filesystem")]
    private VirtualFileSystemSnapshot _snapshot;

    public UsableComputerFileSystemSave()
    {
        _snapshot = VirtualFileSystem.CreateFreshSnapshot();
        _instance = this;
    }

    internal static void Capture(VirtualFileSystemSnapshot snapshot)
    {
        if (_instance != null)
            _instance._snapshot = snapshot.Clone();
    }

    internal static void ResetSnapshot(VirtualFileSystemSnapshot snapshot)
    {
        if (_instance != null)
            _instance._snapshot = snapshot.Clone();
    }

    protected override void OnCreated()
    {
        VirtualFileSystemService.Load(_snapshot);
    }

    protected override void OnLoaded()
    {
        VirtualFileSystemService.Load(_snapshot);
    }
}

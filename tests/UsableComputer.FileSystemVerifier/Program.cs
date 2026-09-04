using Newtonsoft.Json;
using UsableComputer.FileSystem;

var tests = new (string Name, Action Test)[]
{
    ("fresh filesystem has stable roots", TestFreshRoots),
    ("folders are created and ordered before shortcuts", TestOrdering),
    ("names reject path traversal and separators", TestInvalidNames),
    ("sibling names are unique case-insensitively", TestDuplicateNames),
    ("app shortcut reconciliation is idempotent", TestAppShortcutReconciliation),
    ("moves preserve identity", TestMove),
    ("folders can be renamed and deleted when empty", TestRenameAndDelete),
    ("folder cycles are rejected", TestMoveCycle),
    ("non-empty folders cannot be deleted", TestNonEmptyDelete),
    ("snapshot round trip preserves unavailable shortcuts", TestSnapshotRoundTrip),
    ("invalid snapshots are rejected", TestInvalidSnapshot),
    ("persisted names and shortcut targets remain canonical", TestCanonicalSnapshotValues),
    ("shortcut reconciliation tolerates a full filesystem", TestShortcutCapacity),
};

foreach ((string name, Action test) in tests)
{
    test();
    Console.WriteLine($"PASS {name}");
}

static void TestFreshRoots()
{
    var fileSystem = new VirtualFileSystem();
    Assert(fileSystem.TryGetNode(VirtualFileSystem.RootId, out VirtualFileSystemNode root), "root should exist");
    Assert(root.ParentId == null, "root should not have a parent");
    Assert(fileSystem.TryGetNode(VirtualFileSystem.DesktopId, out VirtualFileSystemNode desktop), "desktop should exist");
    Assert(desktop.ParentId == VirtualFileSystem.RootId, "desktop should belong to root");
    Assert(fileSystem.GetPath(VirtualFileSystem.DesktopId) == "/Desktop", "desktop path should be stable");
}

static void TestOrdering()
{
    var fileSystem = new VirtualFileSystem();
    fileSystem.EnsureAppShortcut("notes", "Notes");
    fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Work");
    IReadOnlyList<VirtualFileSystemNode> children = fileSystem.GetChildren(VirtualFileSystem.DesktopId);
    Assert(children.Count == 2, "desktop should contain two entries");
    Assert(children[0].Kind == VirtualFileSystemNodeKind.Directory, "folders should sort before shortcuts");
    Assert(children[1].TargetId == "notes", "app shortcut should remain addressable by target id");
}

static void TestInvalidNames()
{
    var fileSystem = new VirtualFileSystem();
    string[] invalidNames = { "", "   ", ".", "..", "work/files", "work\\files", "bad\0name" };
    foreach (string invalidName in invalidNames)
    {
        AssertThrows<ArgumentException>(
            () => fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, invalidName),
            $"'{invalidName}' should be rejected");
    }
}

static void TestDuplicateNames()
{
    var fileSystem = new VirtualFileSystem();
    fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Work");
    InvalidOperationException exception = AssertThrows<InvalidOperationException>(
        () => fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "work"),
        "case-insensitive duplicate should fail");
    Assert(exception.Message.Contains("already exists", StringComparison.Ordinal), "duplicate error should be actionable");
}

static void TestAppShortcutReconciliation()
{
    var fileSystem = new VirtualFileSystem();
    Assert(fileSystem.EnsureAppShortcut("mod.dashboard", "Dashboard"), "first reconciliation should add shortcut");
    Assert(!fileSystem.EnsureAppShortcut("mod.dashboard", "Renamed Dashboard"), "same app id should not duplicate");
    Assert(fileSystem.GetChildren(VirtualFileSystem.DesktopId).Count == 1, "desktop should contain one shortcut");
}

static void TestMove()
{
    var fileSystem = new VirtualFileSystem();
    VirtualFileSystemNode folder = fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Work");
    fileSystem.EnsureAppShortcut("notes", "Notes");
    VirtualFileSystemNode shortcut = fileSystem.GetChildren(VirtualFileSystem.DesktopId)
        .Single(node => node.Kind == VirtualFileSystemNodeKind.AppShortcut);

    fileSystem.Move(shortcut.Id, folder.Id);
    VirtualFileSystemNode moved = fileSystem.GetChildren(folder.Id).Single();
    Assert(moved.Id == shortcut.Id, "move should preserve stable node identity");
    Assert(moved.ParentId == folder.Id, "move should update the parent");
}

static void TestRenameAndDelete()
{
    var fileSystem = new VirtualFileSystem();
    VirtualFileSystemNode folder = fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Draft");
    fileSystem.Rename(folder.Id, "  Work  ");
    Assert(fileSystem.TryGetNode(folder.Id, out VirtualFileSystemNode renamed), "renamed folder should remain");
    Assert(renamed.Name == "Work", "rename should trim surrounding whitespace");
    fileSystem.Delete(folder.Id);
    Assert(!fileSystem.TryGetNode(folder.Id, out _), "empty folder should be deleted");
}

static void TestMoveCycle()
{
    var fileSystem = new VirtualFileSystem();
    VirtualFileSystemNode parent = fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Parent");
    VirtualFileSystemNode child = fileSystem.CreateDirectory(parent.Id, "Child");
    AssertThrows<InvalidOperationException>(
        () => fileSystem.Move(parent.Id, child.Id),
        "moving a parent into its child should fail");
}

static void TestNonEmptyDelete()
{
    var fileSystem = new VirtualFileSystem();
    VirtualFileSystemNode folder = fileSystem.CreateDirectory(VirtualFileSystem.DesktopId, "Work");
    fileSystem.CreateDirectory(folder.Id, "Nested");
    AssertThrows<InvalidOperationException>(() => fileSystem.Delete(folder.Id), "non-empty delete should fail");
    Assert(fileSystem.TryGetNode(folder.Id, out _), "failed delete should preserve folder");
}

static void TestSnapshotRoundTrip()
{
    var original = new VirtualFileSystem();
    VirtualFileSystemNode folder = original.CreateDirectory(VirtualFileSystem.DesktopId, "Old mod");
    original.EnsureAppShortcut("missing.mod", "Missing app");
    VirtualFileSystemNode shortcut = original.GetChildren(VirtualFileSystem.DesktopId)
        .Single(node => node.Kind == VirtualFileSystemNodeKind.AppShortcut);
    original.Move(shortcut.Id, folder.Id);

    string json = JsonConvert.SerializeObject(original.CreateSnapshot());
    VirtualFileSystemSnapshot serialized = JsonConvert.DeserializeObject<VirtualFileSystemSnapshot>(json)
        ?? throw new InvalidOperationException("serialized snapshot should deserialize");
    var restored = new VirtualFileSystem(serialized);
    VirtualFileSystemNode restoredShortcut = restored.GetChildren(folder.Id).Single();
    Assert(restoredShortcut.TargetId == "missing.mod", "unavailable app target should survive round trip");
    Assert(restored.GetPath(restoredShortcut.Id) == "/Desktop/Old mod/Missing app", "restored path should be correct");
}

static void TestInvalidSnapshot()
{
    VirtualFileSystemSnapshot snapshot = VirtualFileSystem.CreateFreshSnapshot();
    snapshot.Nodes.Add(new VirtualFileSystemNode
    {
        Id = "orphan",
        ParentId = "missing",
        Name = "Orphan",
        Kind = VirtualFileSystemNodeKind.Directory,
    });
    AssertThrows<InvalidOperationException>(() => new VirtualFileSystem(snapshot), "orphaned nodes should be rejected");
}

static void TestCanonicalSnapshotValues()
{
    VirtualFileSystemSnapshot paddedName = VirtualFileSystem.CreateFreshSnapshot();
    paddedName.Nodes.Add(new VirtualFileSystemNode
    {
        Id = "padded",
        ParentId = VirtualFileSystem.DesktopId,
        Name = " Work ",
        Kind = VirtualFileSystemNodeKind.Directory,
    });
    AssertThrows<InvalidOperationException>(
        () => new VirtualFileSystem(paddedName),
        "persisted names with surrounding whitespace should be rejected");

    VirtualFileSystemSnapshot oversizedTarget = VirtualFileSystem.CreateFreshSnapshot();
    oversizedTarget.Nodes.Add(new VirtualFileSystemNode
    {
        Id = "shortcut",
        ParentId = VirtualFileSystem.DesktopId,
        Name = "App",
        Kind = VirtualFileSystemNodeKind.AppShortcut,
        TargetId = new string('a', 129),
    });
    AssertThrows<InvalidOperationException>(
        () => new VirtualFileSystem(oversizedTarget),
        "persisted shortcut targets should enforce the runtime length limit");
}

static void TestShortcutCapacity()
{
    VirtualFileSystemSnapshot snapshot = VirtualFileSystem.CreateFreshSnapshot();
    for (int index = snapshot.Nodes.Count; index < VirtualFileSystem.MaximumNodeCount; index++)
    {
        snapshot.Nodes.Add(new VirtualFileSystemNode
        {
            Id = $"node-{index}",
            ParentId = VirtualFileSystem.DesktopId,
            Name = $"Node {index}",
            Kind = VirtualFileSystemNodeKind.Directory,
        });
    }

    var fileSystem = new VirtualFileSystem(snapshot);
    Assert(!fileSystem.EnsureAppShortcut("notes", "Notes"), "a full filesystem should skip a new app shortcut");
    Assert(fileSystem.GetChildren(VirtualFileSystem.DesktopId).Count == VirtualFileSystem.MaximumNodeCount - 2,
        "capacity handling should preserve the loaded snapshot");
}

static TException AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

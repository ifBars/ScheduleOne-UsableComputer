using Newtonsoft.Json;
using UsableComputer.FileSystem;

internal static class TextFileTests
{
    internal static void Run()
    {
        Check("version 1 saves upgrade without changing identities", Migration);
        Check("Unicode text survives serialization, moves and renames", RoundTrip);
        Check("file limits count UTF-8 bytes and failed writes preserve data", FileLimits);
        Check("total quota supports replacement and reclaims deleted storage", StorageLimits);
        Check("invalid persisted content and future schemas are rejected", InvalidSnapshots);
        Check("paths stay on the virtual disk", Paths);
        Check("Notes preserves dirty drafts and saves by stable ID", NotesDrafts);
        Check("Notes rejects overwrites and external edit conflicts", NotesConflicts);
        Check("Notes imports the legacy note without modifying its source", NotesImport);
    }

    private static void Migration()
    {
        var files = new VirtualFileSystem();
        VirtualFileSystemNode folder = files.CreateDirectory("desktop", "Work");
        files.EnsureAppShortcut("notes", "Notes");
        VirtualFileSystemSnapshot old = files.CreateSnapshot();
        old.SchemaVersion = 1;
        var migrated = new VirtualFileSystem(JsonConvert.DeserializeObject<VirtualFileSystemSnapshot>(JsonConvert.SerializeObject(old))!);
        Require(migrated.CreateSnapshot().SchemaVersion == 2, "schema did not advance");
        Require(migrated.ResolvePath("/Desktop/Work").Id == folder.Id, "folder identity changed");
        Require(migrated.GetChildren("desktop").Count == 2, "old items were lost");
        Require(old.SchemaVersion == 1, "input snapshot was mutated");
    }

    private static void RoundTrip()
    {
        var files = new VirtualFileSystem();
        string text = "Shopping list\nMilk\n日本語 📝 <b>literal</b>\r\n";
        VirtualFileSystemNode file = files.CreateTextFile("desktop", "List.txt", text);
        string folder = files.CreateDirectory("desktop", "Work").Id;
        files.Move(file.Id, folder);
        files.Rename(file.Id, "Moved.txt");
        file.TextContent = "not a live reference";
        var restored = new VirtualFileSystem(JsonConvert.DeserializeObject<VirtualFileSystemSnapshot>(JsonConvert.SerializeObject(files.CreateSnapshot()))!);
        Require(restored.ReadText(file.Id) == text, "content was not preserved");
        Require(restored.ResolvePath("/desktop/work/moved.txt").Id == file.Id, "identity changed");
        Reject(() => restored.CreateDirectory(file.Id, "Child"));
        Reject(() => restored.ReadText(folder));
        Reject(() => restored.CreateTextFile(folder, "MOVED.txt", "duplicate"));
    }

    private static void FileLimits()
    {
        var files = new VirtualFileSystem();
        string id = files.CreateTextFile("desktop", "full.txt", new string('a', VirtualFileSystem.MaximumFileBytes)).Id;
        Reject(() => files.WriteText(id, new string('é', VirtualFileSystem.MaximumFileBytes / 2 + 1)));
        Require(files.ReadText(id).Length == VirtualFileSystem.MaximumFileBytes, "failed write changed content");
        Reject(() => files.CreateTextFile("desktop", "large.txt", new string('a', VirtualFileSystem.MaximumFileBytes + 1)));
        Reject(() => files.WriteText(id, null!));
        files.WriteText(id, string.Empty);
        Require(files.ReadText(id) == string.Empty, "empty files are not supported");
    }

    private static void StorageLimits()
    {
        var files = new VirtualFileSystem();
        string full = new('a', VirtualFileSystem.MaximumFileBytes);
        var ids = new List<string>();
        for (int index = 0; index < VirtualFileSystem.MaximumStorageBytes / VirtualFileSystem.MaximumFileBytes; index++)
            ids.Add(files.CreateTextFile("desktop", $"{index}.txt", full).Id);
        Reject(() => files.CreateTextFile("desktop", "overflow.txt", "x"));
        files.WriteText(ids[0], full);
        files.Delete(ids[0]);
        files.CreateTextFile("desktop", "replacement.txt", full);
        string empty = files.CreateTextFile("desktop", "empty.txt", "").Id;
        Reject(() => files.WriteText(empty, "x"));
        Require(files.ReadText(empty) == "", "failed quota check changed a file");
    }

    private static void InvalidSnapshots()
    {
        var files = new VirtualFileSystem();
        files.CreateTextFile("desktop", "note.txt", "hello");
        VirtualFileSystemSnapshot snapshot = files.CreateSnapshot();
        snapshot.SchemaVersion = 99;
        Reject(() => new VirtualFileSystem(snapshot));
        snapshot.SchemaVersion = 1;
        Reject(() => new VirtualFileSystem(snapshot));
        snapshot.SchemaVersion = 2;
        snapshot.Nodes.Single(node => node.Kind == VirtualFileSystemNodeKind.File).TextContent = null;
        Reject(() => new VirtualFileSystem(snapshot));
        snapshot = VirtualFileSystem.CreateFreshSnapshot();
        snapshot.Nodes[0].TextContent = "invalid folder content";
        Reject(() => new VirtualFileSystem(snapshot));
    }

    private static void Paths()
    {
        var files = new VirtualFileSystem();
        foreach (string path in new[] { "C:\\secret.txt", "../secret", "/Desktop/../secret", "/Desktop//x", "/Desktop/", "/missing" })
            Reject(() => files.ResolvePath(path));
        Require(files.ResolvePath("/").Id == "root", "root path did not resolve");
    }

    private static void NotesDrafts()
    {
        var files = new VirtualFileSystem();
        int commits = 0;
        var notes = new NotesDocument(files, () => commits++);
        notes.Text = "draft";
        Reject(() => notes.New());
        string another = files.CreateTextFile("desktop", "Other.txt", "other").Id;
        Reject(() => notes.Open(another));
        Require(notes.Text == "draft", "draft was lost");
        notes.Path = "/Desktop/Note.txt";
        notes.Save();
        string id = notes.FileId!;
        string folder = files.CreateDirectory("desktop", "Work").Id;
        files.Move(id, folder);
        files.Rename(id, "Renamed.txt");
        notes.Text = "updated";
        notes.Save();
        Require(notes.FileId == id && files.ReadText(id) == "updated", "save did not follow file identity");
        Require(notes.Path == "/Desktop/Work/Renamed.txt" && commits == 2, "save path or commit count is wrong");
        notes.New();
        notes.Open(id);
        Require(notes.Text == "updated" && !notes.IsDirty, "open failed");
    }

    private static void NotesConflicts()
    {
        var files = new VirtualFileSystem();
        string id = files.CreateTextFile("desktop", "Existing.txt", "original").Id;
        var notes = new NotesDocument(files, () => { });
        notes.Path = "/Desktop/Existing.txt";
        notes.Text = "draft";
        Reject(() => notes.Save(saveAs: true));
        Require(files.ReadText(id) == "original" && notes.Text == "draft", "Save as overwrote an existing file");
        notes.New(discard: true);
        notes.Open(id);
        notes.Text = "my edit";
        files.WriteText(id, "external edit");
        Reject(() => notes.Save());
        notes.Path = "/Desktop/Copy.txt";
        notes.Save(saveAs: true);
        Require(files.ReadText(id) == "external edit", "conflict overwrote external data");
        Require(files.ReadText(notes.FileId!) == "my edit", "Save as lost draft");
        files.Delete(notes.FileId!);
        notes.Text = "after delete";
        Reject(() => notes.Save());
        Require(notes.Text == "after delete", "deleted file lost draft");
    }

    private static void NotesImport()
    {
        var files = new VirtualFileSystem();
        var notes = new NotesDocument(files, () => { });
        string legacy = "old global note";
        notes.Import(legacy);
        notes.Save();
        Require(files.ReadText(notes.FileId!) == legacy, "import did not preserve content");
        Require(legacy == "old global note", "legacy data changed");
    }

    private static void Check(string name, Action test)
    {
        test();
        Console.WriteLine($"PASS {name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException) { return; }
        throw new InvalidOperationException("Invalid operation unexpectedly succeeded.");
    }
}

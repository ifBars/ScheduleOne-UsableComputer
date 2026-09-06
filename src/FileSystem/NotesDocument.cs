using System;

namespace UsableComputer.FileSystem;

/// <summary>Keeps the current draft across window closes, until another game save is loaded.</summary>
internal sealed class NotesDocument
{
    private readonly VirtualFileSystem _files;
    private readonly Action _commit;
    private string _savedText = string.Empty;

    internal NotesDocument(VirtualFileSystem files, Action commit)
    {
        _files = files;
        _commit = commit;
    }

    internal string? FileId { get; private set; }
    internal string Text { get; set; } = string.Empty;
    internal string Path { get; set; } = "/Desktop/Untitled.txt";
    internal bool IsDirty => Text != _savedText;

    internal void New(bool discard = false)
    {
        if (!discard) RequireSaved();
        FileId = null;
        Text = _savedText = string.Empty;
        Path = "/Desktop/Untitled.txt";
    }

    internal void Open(string fileId)
    {
        RequireSaved();
        string text = _files.ReadText(fileId);
        Path = _files.GetPath(fileId);
        FileId = fileId;
        Text = _savedText = text;
    }

    internal void OpenPath() => Open(_files.ResolvePath(Path).Id);

    internal void Import(string text)
    {
        RequireSaved();
        FileId = null;
        Path = "/Desktop/Imported note.txt";
        _savedText = string.Empty;
        Text = text;
    }

    internal void Save(bool saveAs = false)
    {
        if (FileId != null && !saveAs)
        {
            if (_files.ReadText(FileId) != _savedText)
                throw new InvalidOperationException("This file changed in another app. Use Save as with a new name to keep your draft.");
            _files.WriteText(FileId, Text);
        }
        else
        {
            int separator = Path.LastIndexOf('/');
            if (separator < 0)
                throw new InvalidOperationException("Enter a full path such as /Desktop/Note.txt.");
            string parentPath = separator == 0 ? "/" : Path.Substring(0, separator);
            VirtualFileSystemNode parent = _files.ResolvePath(parentPath);
            FileId = _files.CreateTextFile(parent.Id, Path.Substring(separator + 1), Text).Id;
        }
        Path = _files.GetPath(FileId);
        _savedText = Text;
        _commit();
    }

    private void RequireSaved()
    {
        if (IsDirty)
            throw new InvalidOperationException("Save your draft or choose Discard before opening another note.");
    }
}

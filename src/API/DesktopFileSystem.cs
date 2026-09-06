using System.Collections.Generic;
using System.Linq;
using UsableComputer.FileSystem;

namespace UsableComputer.API;

/// <summary>
/// The current game save's virtual disk. Call on the game thread after the save loads.
/// Writes are captured for the next game save; no host filesystem paths are exposed.
/// </summary>
public static class DesktopFileSystem
{
    public const string DesktopId = VirtualFileSystem.DesktopId;
    public const int MaximumFileBytes = VirtualFileSystem.MaximumFileBytes;
    public const int MaximumStorageBytes = VirtualFileSystem.MaximumStorageBytes;

    public static IReadOnlyList<DesktopFileEntry> GetChildren(string directoryId) =>
        VirtualFileSystemService.GetChildren(directoryId).Select(node => new DesktopFileEntry(node)).ToArray();

    public static DesktopFileEntry ResolvePath(string path) => new(VirtualFileSystemService.ResolvePath(path));
    public static string GetPath(string id) => VirtualFileSystemService.GetPath(id);
    public static string ReadText(string id) => VirtualFileSystemService.ReadText(id);
    public static void WriteText(string id, string text) => VirtualFileSystemService.WriteText(id, text);
    public static DesktopFileEntry CreateTextFile(string directoryId, string name, string text) =>
        new(VirtualFileSystemService.CreateTextFile(directoryId, name, text));
    public static DesktopFileEntry CreateDirectory(string directoryId, string name) =>
        new(VirtualFileSystemService.CreateDirectory(directoryId, name));
    public static void Rename(string id, string name) => VirtualFileSystemService.Rename(id, name);
    public static void Move(string id, string directoryId) => VirtualFileSystemService.Move(id, directoryId);
    public static void Delete(string id) => VirtualFileSystemService.Delete(id);
}

/// <summary>A detached view of an item on the virtual disk.</summary>
public sealed class DesktopFileEntry
{
    internal DesktopFileEntry(VirtualFileSystemNode node)
    {
        Id = node.Id;
        ParentId = node.ParentId;
        Name = node.Name;
        IsDirectory = node.Kind == VirtualFileSystemNodeKind.Directory;
        IsTextFile = node.Kind == VirtualFileSystemNodeKind.File;
    }

    public string Id { get; }
    public string? ParentId { get; }
    public string Name { get; }
    public bool IsDirectory { get; }
    public bool IsTextFile { get; }
}

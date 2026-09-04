using System.Collections.Generic;

namespace UsableComputer.FileSystem;

internal sealed class VirtualFileSystemSnapshot
{
    public int SchemaVersion { get; set; } = VirtualFileSystem.CurrentSchemaVersion;

    public List<VirtualFileSystemNode> Nodes { get; set; } = new();

    internal VirtualFileSystemSnapshot Clone()
    {
        var clone = new VirtualFileSystemSnapshot
        {
            SchemaVersion = SchemaVersion,
        };
        foreach (VirtualFileSystemNode node in Nodes)
            clone.Nodes.Add(node.Clone());
        return clone;
    }
}

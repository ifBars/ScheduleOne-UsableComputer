namespace UsableComputer.FileSystem;

internal sealed class VirtualFileSystemNode
{
    public string Id { get; set; } = string.Empty;

    public string? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public VirtualFileSystemNodeKind Kind { get; set; }

    public string? TargetId { get; set; }

    public string? TextContent { get; set; }

    internal VirtualFileSystemNode Clone()
    {
        return new VirtualFileSystemNode
        {
            Id = Id,
            ParentId = ParentId,
            Name = Name,
            Kind = Kind,
            TargetId = TargetId,
            TextContent = TextContent,
        };
    }
}

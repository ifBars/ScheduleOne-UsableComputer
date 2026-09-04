using System;
using System.Collections.Generic;
using System.Linq;

namespace UsableComputer.FileSystem;

internal sealed class VirtualFileSystem
{
    internal const int CurrentSchemaVersion = 1;
    internal const int MaximumNodeCount = 2048;
    internal const int MaximumNameLength = 64;
    internal const string RootId = "root";
    internal const string DesktopId = "desktop";

    private readonly Dictionary<string, VirtualFileSystemNode> _nodes;

    internal VirtualFileSystem()
        : this(CreateFreshSnapshot())
    {
    }

    internal VirtualFileSystem(VirtualFileSystemSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        _nodes = snapshot.Nodes.ToDictionary(node => node.Id, node => node.Clone(), StringComparer.Ordinal);
    }

    internal IReadOnlyList<VirtualFileSystemNode> GetChildren(string parentId)
    {
        RequireDirectory(parentId);
        return _nodes.Values
            .Where(node => string.Equals(node.ParentId, parentId, StringComparison.Ordinal))
            .OrderBy(node => node.Kind == VirtualFileSystemNodeKind.Directory ? 0 : 1)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .Select(node => node.Clone())
            .ToArray();
    }

    internal bool TryGetNode(string id, out VirtualFileSystemNode node)
    {
        if (_nodes.TryGetValue(id, out VirtualFileSystemNode? existing))
        {
            node = existing.Clone();
            return true;
        }

        node = null!;
        return false;
    }

    internal VirtualFileSystemNode CreateDirectory(string parentId, string name)
    {
        RequireDirectory(parentId);
        string normalizedName = NormalizeName(name);
        EnsureUniqueName(parentId, normalizedName, exceptId: null);
        EnsureCapacity();

        var node = new VirtualFileSystemNode
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = parentId,
            Name = normalizedName,
            Kind = VirtualFileSystemNodeKind.Directory,
        };
        _nodes.Add(node.Id, node);
        return node.Clone();
    }

    internal bool EnsureAppShortcut(string targetId, string title)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("An app shortcut target id is required.", nameof(targetId));
        if (targetId.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(targetId), "An app shortcut target id may be at most 128 characters.");

        if (_nodes.Values.Any(node =>
                node.Kind == VirtualFileSystemNodeKind.AppShortcut &&
                string.Equals(node.TargetId, targetId, StringComparison.Ordinal)))
        {
            return false;
        }

        if (_nodes.Count >= MaximumNodeCount)
            return false;
        string safeTitle = MakeSafeName(title, "Untitled app");
        safeTitle = MakeUniqueName(DesktopId, safeTitle);
        var node = new VirtualFileSystemNode
        {
            Id = Guid.NewGuid().ToString("N"),
            ParentId = DesktopId,
            Name = safeTitle,
            Kind = VirtualFileSystemNodeKind.AppShortcut,
            TargetId = targetId,
        };
        _nodes.Add(node.Id, node);
        return true;
    }

    internal void Rename(string nodeId, string name)
    {
        VirtualFileSystemNode node = RequireMutableNode(nodeId);
        string normalizedName = NormalizeName(name);
        EnsureUniqueName(node.ParentId!, normalizedName, node.Id);
        node.Name = normalizedName;
    }

    internal void Move(string nodeId, string destinationId)
    {
        VirtualFileSystemNode node = RequireMutableNode(nodeId);
        RequireDirectory(destinationId);
        if (string.Equals(node.ParentId, destinationId, StringComparison.Ordinal))
            return;
        if (node.Kind == VirtualFileSystemNodeKind.Directory && IsDescendant(destinationId, node.Id))
            throw new InvalidOperationException("A folder cannot be moved into itself or one of its descendants.");

        EnsureUniqueName(destinationId, node.Name, node.Id);
        node.ParentId = destinationId;
    }

    internal void Delete(string nodeId)
    {
        VirtualFileSystemNode node = RequireMutableNode(nodeId);
        if (node.Kind == VirtualFileSystemNodeKind.Directory &&
            _nodes.Values.Any(candidate => string.Equals(candidate.ParentId, node.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The folder is not empty. Move its contents before deleting it.");
        }

        _nodes.Remove(node.Id);
    }

    internal string CreateUniqueFolderName(string parentId)
    {
        RequireDirectory(parentId);
        return MakeUniqueName(parentId, "New folder");
    }

    internal string GetPath(string nodeId)
    {
        VirtualFileSystemNode node = RequireNode(nodeId);
        var names = new Stack<string>();
        while (!string.Equals(node.Id, RootId, StringComparison.Ordinal))
        {
            names.Push(node.Name);
            node = RequireNode(node.ParentId!);
        }

        return "/" + string.Join("/", names);
    }

    internal VirtualFileSystemSnapshot CreateSnapshot()
    {
        var snapshot = new VirtualFileSystemSnapshot();
        foreach (VirtualFileSystemNode node in _nodes.Values.OrderBy(node => node.Id, StringComparer.Ordinal))
            snapshot.Nodes.Add(node.Clone());
        return snapshot;
    }

    internal static VirtualFileSystemSnapshot CreateFreshSnapshot()
    {
        return new VirtualFileSystemSnapshot
        {
            Nodes = new List<VirtualFileSystemNode>
            {
                new()
                {
                    Id = RootId,
                    Name = "Root",
                    Kind = VirtualFileSystemNodeKind.Directory,
                },
                new()
                {
                    Id = DesktopId,
                    ParentId = RootId,
                    Name = "Desktop",
                    Kind = VirtualFileSystemNodeKind.Directory,
                },
            },
        };
    }

    private static void ValidateSnapshot(VirtualFileSystemSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"Unsupported virtual filesystem schema version {snapshot.SchemaVersion}.");
        if (snapshot.Nodes == null)
            throw new InvalidOperationException("The virtual filesystem node collection is missing.");
        if (snapshot.Nodes.Count > MaximumNodeCount)
            throw new InvalidOperationException($"The virtual filesystem exceeds the {MaximumNodeCount}-node limit.");

        var nodes = new Dictionary<string, VirtualFileSystemNode>(StringComparer.Ordinal);
        foreach (VirtualFileSystemNode? node in snapshot.Nodes)
        {
            if (node == null)
                throw new InvalidOperationException("The virtual filesystem contains a null node.");
            if (string.IsNullOrWhiteSpace(node.Id) || node.Id.Length > 64)
                throw new InvalidOperationException("Every virtual filesystem node requires a valid id.");
            if (!nodes.TryAdd(node.Id, node))
                throw new InvalidOperationException($"The virtual filesystem contains duplicate node id '{node.Id}'.");
            if (!Enum.IsDefined(typeof(VirtualFileSystemNodeKind), node.Kind))
                throw new InvalidOperationException($"Virtual filesystem node '{node.Id}' has an unsupported kind.");
            string normalizedName = NormalizeName(node.Name);
            if (!string.Equals(node.Name, normalizedName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Virtual filesystem node '{node.Id}' has a non-canonical name.");
            if (node.Kind == VirtualFileSystemNodeKind.AppShortcut &&
                (string.IsNullOrWhiteSpace(node.TargetId) || node.TargetId.Length > 128))
            {
                throw new InvalidOperationException($"App shortcut '{node.Id}' has an invalid target id.");
            }
            if (node.Kind == VirtualFileSystemNodeKind.Directory && node.TargetId != null)
                throw new InvalidOperationException($"Directory '{node.Id}' cannot have an app target.");
        }

        if (!nodes.TryGetValue(RootId, out VirtualFileSystemNode? root) ||
            root.Kind != VirtualFileSystemNodeKind.Directory ||
            !string.IsNullOrEmpty(root.ParentId))
        {
            throw new InvalidOperationException("The virtual filesystem root is missing or invalid.");
        }
        if (!nodes.TryGetValue(DesktopId, out VirtualFileSystemNode? desktop) ||
            desktop.Kind != VirtualFileSystemNodeKind.Directory ||
            !string.Equals(desktop.ParentId, RootId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The virtual filesystem desktop is missing or invalid.");
        }

        foreach (VirtualFileSystemNode node in nodes.Values)
        {
            if (string.Equals(node.Id, RootId, StringComparison.Ordinal))
                continue;
            if (string.IsNullOrEmpty(node.ParentId) || !nodes.TryGetValue(node.ParentId, out VirtualFileSystemNode? parent))
                throw new InvalidOperationException($"Virtual filesystem node '{node.Id}' has no valid parent.");
            if (parent.Kind != VirtualFileSystemNodeKind.Directory)
                throw new InvalidOperationException($"Virtual filesystem node '{node.Id}' has a non-directory parent.");

            VirtualFileSystemNode cursor = node;
            for (int depth = 0; depth <= nodes.Count; depth++)
            {
                if (string.Equals(cursor.Id, RootId, StringComparison.Ordinal))
                    break;
                if (depth == nodes.Count)
                    throw new InvalidOperationException($"Virtual filesystem node '{node.Id}' is part of a cycle.");
                cursor = nodes[cursor.ParentId!];
            }
        }

        foreach (IGrouping<string?, VirtualFileSystemNode> siblings in nodes.Values.GroupBy(node => node.ParentId, StringComparer.Ordinal))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VirtualFileSystemNode node in siblings)
            {
                if (!names.Add(node.Name))
                    throw new InvalidOperationException($"Folder '{node.ParentId}' contains duplicate name '{node.Name}'.");
            }
        }
    }

    private VirtualFileSystemNode RequireNode(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !_nodes.TryGetValue(id, out VirtualFileSystemNode? node))
            throw new InvalidOperationException($"Virtual filesystem node '{id}' does not exist.");
        return node;
    }

    private VirtualFileSystemNode RequireDirectory(string id)
    {
        VirtualFileSystemNode node = RequireNode(id);
        if (node.Kind != VirtualFileSystemNodeKind.Directory)
            throw new InvalidOperationException($"Virtual filesystem node '{id}' is not a directory.");
        return node;
    }

    private VirtualFileSystemNode RequireMutableNode(string id)
    {
        VirtualFileSystemNode node = RequireNode(id);
        if (string.Equals(node.Id, RootId, StringComparison.Ordinal) ||
            string.Equals(node.Id, DesktopId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The virtual filesystem root and desktop cannot be modified.");
        }
        return node;
    }

    private bool IsDescendant(string candidateId, string ancestorId)
    {
        VirtualFileSystemNode cursor = RequireNode(candidateId);
        while (!string.Equals(cursor.Id, RootId, StringComparison.Ordinal))
        {
            if (string.Equals(cursor.Id, ancestorId, StringComparison.Ordinal))
                return true;
            cursor = RequireNode(cursor.ParentId!);
        }
        return false;
    }

    private void EnsureUniqueName(string parentId, string name, string? exceptId)
    {
        if (_nodes.Values.Any(node =>
                string.Equals(node.ParentId, parentId, StringComparison.Ordinal) &&
                !string.Equals(node.Id, exceptId, StringComparison.Ordinal) &&
                string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"'{name}' already exists in this folder.");
        }
    }

    private string MakeUniqueName(string parentId, string baseName)
    {
        if (!_nodes.Values.Any(node =>
                string.Equals(node.ParentId, parentId, StringComparison.Ordinal) &&
                string.Equals(node.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        for (int suffix = 2; suffix < MaximumNodeCount; suffix++)
        {
            string candidateSuffix = $" ({suffix})";
            int prefixLength = Math.Min(baseName.Length, MaximumNameLength - candidateSuffix.Length);
            string candidate = baseName[..prefixLength] + candidateSuffix;
            if (!_nodes.Values.Any(node =>
                    string.Equals(node.ParentId, parentId, StringComparison.Ordinal) &&
                    string.Equals(node.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No unique name is available in this folder.");
    }

    private static string MakeSafeName(string? value, string fallback)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        char[] characters = candidate
            .Where(character => character != '/' && character != '\\' && !char.IsControl(character))
            .ToArray();
        candidate = new string(characters).Trim();
        if (candidate.Length == 0 || candidate == "." || candidate == "..")
            candidate = fallback;
        return candidate.Length <= MaximumNameLength ? candidate : candidate[..MaximumNameLength];
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A file or folder name is required.", nameof(value));
        string name = value.Trim();
        if (name.Length > MaximumNameLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"Names may be at most {MaximumNameLength} characters.");
        if (name == "." || name == "..")
            throw new ArgumentException("Relative path segments are not valid names.", nameof(value));
        if (name.Any(character => character == '/' || character == '\\' || char.IsControl(character)))
            throw new ArgumentException("Names cannot contain path separators or control characters.", nameof(value));
        return name;
    }

    private void EnsureCapacity()
    {
        if (_nodes.Count >= MaximumNodeCount)
            throw new InvalidOperationException($"The virtual filesystem is limited to {MaximumNodeCount} nodes.");
    }
}

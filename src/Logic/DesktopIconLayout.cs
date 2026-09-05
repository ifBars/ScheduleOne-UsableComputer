using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.FileSystem;

namespace UsableComputer.Logic;

internal enum DesktopIconSize { Small, Medium, Large }
internal enum DesktopIconOrder { Name, Kind }

internal readonly struct DesktopIconLayout
{
    internal const int DesktopWidth = 800;
    internal const int DesktopHeight = 464;
    internal const int Margin = 12;

    internal DesktopIconLayout(DesktopIconSize size)
    {
        IconSize = size switch { DesktopIconSize.Small => 32, DesktopIconSize.Large => 56, _ => 42 };
        CellWidth = size switch { DesktopIconSize.Small => 80, DesktopIconSize.Large => 108, _ => 92 };
        CellHeight = IconSize + 42;
    }

    internal int IconSize { get; }
    internal int CellWidth { get; }
    internal int CellHeight { get; }
    internal int Rows => (DesktopHeight - 2 * Margin) / CellHeight;
    internal int Columns(int count) => (Math.Max(0, count) + Rows - 1) / Rows;
    internal int ContentWidth(int count) => Math.Max(DesktopWidth, Columns(count) * CellWidth + 2 * Margin);
    internal float X(int index) => Margin + CellWidth * (index / Rows + 0.5f);
    internal float Y(int index) => -Margin - CellHeight * (index % Rows);

    internal static IReadOnlyList<VirtualFileSystemNode> Arrange(
        IEnumerable<VirtualFileSystemNode> nodes, DesktopIconOrder order)
    {
        return nodes.OrderBy(node => order == DesktopIconOrder.Kind && node.Kind != VirtualFileSystemNodeKind.Directory ? 1 : 0)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
    }

    internal static T Parse<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
}

using UsableComputer.FileSystem;
using UsableComputer.Logic;
using UsableComputer;

int passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
void Test(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

foreach (DesktopIconSize size in Enum.GetValues<DesktopIconSize>())
{
    Test($"{size}: bounded grid and reachable overflow", () =>
    {
        var layout = new DesktopIconLayout(size);
        for (int count = 0; count <= 300; count++)
        {
            for (int index = 0; index < count; index++)
            {
                float left = layout.X(index) - layout.CellWidth / 2f;
                float right = layout.X(index) + layout.CellWidth / 2f;
                float top = -layout.Y(index);
                Check(left >= 0 && right <= layout.ContentWidth(count), "Icon lies beyond scroll content.");
                Check(top >= 0 && top + layout.CellHeight <= DesktopIconLayout.DesktopHeight, "Icon overlaps bottom edge.");
                if (index > 0 && index % layout.Rows != 0)
                    Check(layout.Y(index - 1) - layout.Y(index) >= layout.CellHeight, "Rows overlap.");
            }
        }
        Check(layout.ContentWidth(300) > DesktopIconLayout.DesktopWidth, "Overflow is unreachable.");
    });
}

var nodes = new[]
{
    new VirtualFileSystemNode { Id = "z", Name = "alpha", Kind = VirtualFileSystemNodeKind.AppShortcut },
    new VirtualFileSystemNode { Id = "b", Name = "Zebra folder", Kind = VirtualFileSystemNodeKind.Directory },
    new VirtualFileSystemNode { Id = "a", Name = "ALPHA", Kind = VirtualFileSystemNodeKind.AppShortcut },
};
Test("Name order is stable across enumeration and reload", () =>
{
    var arranged = DesktopIconLayout.Arrange(nodes, DesktopIconOrder.Name).Select(n => n.Id).ToArray();
    Check(arranged.SequenceEqual(new[] { "a", "z", "b" }), "Name/id ordering is incorrect.");
    Check(arranged.SequenceEqual(DesktopIconLayout.Arrange(nodes.Reverse(), DesktopIconOrder.Name).Select(n => n.Id)), "Input ordering changes the grid.");
});
Test("Folders first and node changes preserve identities", () =>
{
    Check(DesktopIconLayout.Arrange(nodes, DesktopIconOrder.Kind).Select(n => n.Id).SequenceEqual(new[] { "b", "a", "z" }), "Folders were not placed first.");
    Check(nodes[0].Id == "z", "Arrangement mutated filesystem order.");
    Check(DesktopIconLayout.Arrange(nodes.Take(2), DesktopIconOrder.Name).Count == 2, "Removed nodes remain in layout.");
});
Test("Invalid persisted preferences use compatible defaults", () =>
{
    foreach (string? invalid in new[] { null, "", "Huge", "999", "-1" })
        Check(DesktopIconLayout.Parse(invalid, DesktopIconSize.Medium) == DesktopIconSize.Medium, "Invalid size accepted.");
    Check(DesktopIconLayout.Parse("large", DesktopIconSize.Medium) == DesktopIconSize.Large, "Saved size did not parse.");
    Check(DesktopIconLayout.Parse("Name", DesktopIconOrder.Kind) == DesktopIconOrder.Name, "Saved order did not parse.");
});
Test("Camera keeps horizontal coverage on narrow windows", () =>
{
    double referenceWidth = Math.Tan(55d * Math.PI / 360d) * (16d / 9d);
    foreach (float aspect in new[] { 16f / 9f, 4f / 3f, 8f / 9f, 21f / 9f })
    {
        float fov = DisplayProfile.GetInteractionFieldOfView(aspect);
        Check(fov >= 55f && fov < 180f, "Invalid camera field of view.");
        Check(Math.Tan(fov * Math.PI / 360d) * aspect >= referenceWidth - 0.00001d, "Narrow viewport loses horizontal coverage.");
    }
    foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        Check(Math.Abs(DisplayProfile.GetInteractionFieldOfView(invalid) - 55f) < 0.001f, "Invalid aspect did not use baseline view.");
});
Console.WriteLine($"{passed}/{passed} passed");

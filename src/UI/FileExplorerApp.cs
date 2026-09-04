using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.FileSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#else
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class FileExplorerApp : IDesktopAppSession, IDesktopDirectorySession
{
    private static readonly Color ExplorerBlue = new(0.18f, 0.36f, 0.72f, 1f);
    private static readonly Color TaskPane = new(0.76f, 0.84f, 0.97f, 1f);
    private static readonly Color TaskBody = new(0.84f, 0.89f, 0.98f, 1f);
    private static readonly Color ExplorerWhite = new(0.99f, 0.99f, 0.98f, 1f);

    private readonly DesktopAppContext _context;
    private readonly RectTransform _items;
    private readonly S1Text _path;
    private readonly S1Text _status;
    private readonly S1Text _details;
    private readonly S1Input _nameInput;
    private UiListenerRegistry _itemListeners = new();
    private string _currentDirectoryId = VirtualFileSystem.DesktopId;
    private string? _selectedNodeId;
    private string? _cutNodeId;
    private bool _disposed;

    internal FileExplorerApp(DesktopAppContext context)
    {
        _context = context;

        GameObject menu = Panel(context.Container, "MenuBar", UiFactory.Surface, 22f, 0f);
        string[] labels = { "File", "Edit", "View", "Favorites", "Tools", "Help" };
        float x = 8f;
        foreach (string value in labels)
        {
            S1Text label = UiFactory.CreateText(menu.transform, value, value, 13f, UiFactory.TextPrimary, Left());
            Fixed(label.rectTransform, x, 0f, value.Length * 8f + 14f, 22f);
            x += value.Length * 8f + 14f;
        }

        GameObject toolbar = Panel(context.Container, "NavigationToolbar", UiFactory.SurfaceRaised, 42f, -22f);
        Button back = ToolbarButton(toolbar.transform, "Back", "Back", 8f, 84f);
        Button up = ToolbarButton(toolbar.transform, "Up", "Up", 98f, 38f);
        GameObject separator = UiFactory.CreatePanel(toolbar.transform, "Separator", new Color(0.55f, 0.55f, 0.51f, 1f));
        Fixed(separator.GetComponent<RectTransform>(), 143f, 7f, 1f, 28f);
        Button search = ToolbarButton(toolbar.transform, "Search", "Search", 153f, 88f);
        Button folders = ToolbarButton(toolbar.transform, "Folders", "Folders", 247f, 94f);
        context.Bind(back, NavigateUp);
        context.Bind(up, NavigateUp);
        context.Bind(search, ShowSearchUnavailable);
        context.Bind(folders, ShowFolderTasks);

        GameObject address = Panel(context.Container, "AddressBar", UiFactory.Surface, 30f, -64f);
        S1Text addressLabel = UiFactory.CreateText(address.transform, "AddressLabel", "Address", 13f, UiFactory.TextMuted, Left());
        Fixed(addressLabel.rectTransform, 8f, 2f, 52f, 26f);
        GameObject addressField = UiFactory.CreatePanel(address.transform, "AddressField", ExplorerWhite);
        RectTransform addressRect = addressField.GetComponent<RectTransform>();
        addressRect.anchorMin = Vector2.zero;
        addressRect.anchorMax = Vector2.one;
        addressRect.offsetMin = new Vector2(62f, 3f);
        addressRect.offsetMax = new Vector2(-31f, -3f);
        Icon(addressField.transform, "AddressIcon", RuntimeAppIcons.Get(BuiltInIcon.Folder), 4f, 3f, 20f);
        _path = UiFactory.CreateText(addressField.transform, "Path", "My Computer  >  Desktop", 14f, UiFactory.TextPrimary, Left());
        UiFactory.Stretch(_path.rectTransform, new Vector2(28f, 2f));
        Button go = UiFactory.CreateButton(address.transform, "Go", "Go", UiFactory.SurfaceRaised, out _);
        Fixed(go.GetComponent<RectTransform>(), -3f, 3f, 25f, 24f, true);
        context.Bind(go, Refresh);

        GameObject taskPane = UiFactory.CreatePanel(context.Container, "TaskPane", TaskPane);
        RectTransform taskRect = taskPane.GetComponent<RectTransform>();
        taskRect.anchorMin = Vector2.zero;
        taskRect.anchorMax = new Vector2(0f, 1f);
        taskRect.offsetMin = new Vector2(0f, 24f);
        taskRect.offsetMax = new Vector2(158f, -94f);

        GameObject tasks = TaskGroup(taskPane.transform, "FileTasks", "File and Folder Tasks", -8f, 200f);
        Button create = TaskLink(tasks.transform, "NewFolder", "Make a new folder", 34f);
        Button rename = TaskLink(tasks.transform, "Rename", "Rename this folder", 58f);
        Button cut = TaskLink(tasks.transform, "Cut", "Move this item", 82f);
        Button paste = TaskLink(tasks.transform, "Paste", "Paste here", 106f);
        Button delete = TaskLink(tasks.transform, "Delete", "Delete this folder", 130f, UiFactory.Danger);
        context.Bind(create, CreateFolder);
        context.Bind(rename, RenameSelected);
        context.Bind(cut, CutSelected);
        context.Bind(paste, Paste);
        context.Bind(delete, DeleteSelected);

        S1Text nameLabel = UiFactory.CreateText(tasks.transform, "NameLabel", "Folder name:", 11f, UiFactory.TextMuted, Left());
        Fixed(nameLabel.rectTransform, 10f, 154f, 130f, 17f);
        _nameInput = UiFactory.CreateInputField(tasks.transform, "NameInput", string.Empty, "New folder", false, out _);
        Fixed(_nameInput.GetComponent<RectTransform>(), 10f, 173f, 130f, 23f);
        var trigger = _nameInput.gameObject.AddComponent<EventTrigger>();
        context.Listeners.AddTrigger(trigger, EventTriggerType.Select, _ => context.SetTyping(true));
        context.Listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => context.SetTyping(false));
        context.Listeners.Add<string>(_ => context.SetTyping(false), _nameInput.onEndEdit);

        GameObject details = TaskGroup(taskPane.transform, "DetailsGroup", "Details", -216f, 84f);
        _details = UiFactory.CreateText(details.transform, "Details", "Desktop\nVirtual folder", 12f, UiFactory.TextMuted, TopLeft());
        _details.rectTransform.anchorMin = Vector2.zero;
        _details.rectTransform.anchorMax = Vector2.one;
        _details.rectTransform.offsetMin = new Vector2(10f, 7f);
        _details.rectTransform.offsetMax = new Vector2(-8f, -32f);

        GameObject list = UiFactory.CreatePanel(context.Container, "FileList", ExplorerWhite);
        RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = Vector2.zero;
        listRect.anchorMax = Vector2.one;
        listRect.offsetMin = new Vector2(158f, 24f);
        listRect.offsetMax = new Vector2(0f, -94f);
        ScrollRect scroll = list.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 34f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        GameObject viewport = new("FileViewport");
        viewport.transform.SetParent(list.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        UiFactory.Stretch(viewportRect, new Vector2(8f, 8f));
        viewport.AddComponent<RectMask2D>();
        GameObject itemRoot = new("FileItems");
        itemRoot.transform.SetParent(viewport.transform, false);
        _items = itemRoot.AddComponent<RectTransform>();
        _items.anchorMin = new Vector2(0f, 1f);
        _items.anchorMax = new Vector2(1f, 1f);
        _items.pivot = new Vector2(0.5f, 1f);
        _items.anchoredPosition = Vector2.zero;
        scroll.viewport = viewportRect;
        scroll.content = _items;

        GameObject status = UiFactory.CreatePanel(context.Container, "StatusBar", UiFactory.Surface);
        RectTransform statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.sizeDelta = new Vector2(0f, 24f);
        _status = UiFactory.CreateText(status.transform, "Status", "Ready", 12f, UiFactory.TextMuted, Left());
        UiFactory.Stretch(_status.rectTransform, new Vector2(8f, 2f));

        VirtualFileSystemService.Changed += Refresh;
        Refresh();
    }

    public void OnOpened() => Refresh();
    public void OnClosed() => _context.SetTyping(false);
    public void OnTick() { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        VirtualFileSystemService.Changed -= Refresh;
        _itemListeners.Dispose();
    }

    public void OpenDirectory(string directoryId)
    {
        if (_disposed || !VirtualFileSystemService.TryGetNode(directoryId, out VirtualFileSystemNode node) || node.Kind != VirtualFileSystemNodeKind.Directory) return;
        _currentDirectoryId = directoryId;
        _selectedNodeId = null;
        Refresh();
    }

    private void Refresh()
    {
        if (_disposed) return;
        if (!VirtualFileSystemService.TryGetNode(_currentDirectoryId, out VirtualFileSystemNode current) || current.Kind != VirtualFileSystemNodeKind.Directory)
        {
            _currentDirectoryId = VirtualFileSystem.DesktopId;
            _selectedNodeId = null;
            VirtualFileSystemService.TryGetNode(_currentDirectoryId, out current);
        }
        _path.text = "My Computer  >  " + VirtualFileSystemService.GetPath(_currentDirectoryId).TrimStart('/').Replace("/", "  >  ");
        IReadOnlyList<VirtualFileSystemNode> children = VirtualFileSystemService.GetChildren(_currentDirectoryId);
        _itemListeners.Dispose();
        _itemListeners = new UiListenerRegistry();
        ClearItems();
        for (int index = 0; index < children.Count; index++) CreateItem(children[index], index);
        _items.sizeDelta = new Vector2(0f, Math.Max(1, (children.Count + 3) / 4) * 92f);
        _details.text = current.Name + "\nVirtual folder";
        _status.text = children.Count == 1 ? "1 object" : $"{children.Count} objects";
        UiFactory.SetLayerRecursively(_items.gameObject, Constants.UiLayer);
    }

    private void CreateItem(VirtualFileSystemNode node, int index)
    {
        bool selected = string.Equals(_selectedNodeId, node.Id, StringComparison.Ordinal);
        var buttonObject = new GameObject($"Item_{node.Id}");
        buttonObject.transform.SetParent(_items, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        Image background = buttonObject.AddComponent<Image>();
        background.color = selected ? new Color(0.22f, 0.42f, 0.78f, 0.65f) : new Color(1f, 1f, 1f, 0f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = background.color;
        colors.highlightedColor = selected ? background.color : new Color(0.34f, 0.52f, 0.84f, 0.2f);
        colors.pressedColor = new Color(0.22f, 0.42f, 0.78f, 0.4f);
        button.colors = colors;
        Fixed(rect, (index % 4) * 112f, (index / 4) * 92f, 102f, 84f);
        Sprite sprite = node.Kind == VirtualFileSystemNodeKind.Directory
            ? RuntimeAppIcons.Get(BuiltInIcon.Folder)
            : ResolveShortcutIcon(node);
        Icon(buttonObject.transform, "Icon", sprite, 30f, 4f, 43f);
        bool unavailable = node.Kind == VirtualFileSystemNodeKind.AppShortcut &&
            (string.IsNullOrEmpty(node.TargetId) || !DesktopAppRegistry.TryGet(node.TargetId, out _));
        S1Text label = UiFactory.CreateText(buttonObject.transform, "Name", node.Name + (unavailable ? "\n(unavailable)" : string.Empty), 12f,
            selected ? UiFactory.TextOnAccent : UiFactory.TextPrimary, TopCenter());
        Fixed(label.rectTransform, 2f, 50f, 98f, 32f);
        _itemListeners.Add(() => Select(node.Id), button.onClick);
        var clicks = buttonObject.AddComponent<EventTrigger>();
        _itemListeners.AddTrigger(clicks, EventTriggerType.PointerClick, data =>
        {
            if (data is PointerEventData pointer && pointer.clickCount >= 2) Open(node.Id);
        });
    }

    private static Sprite ResolveShortcutIcon(VirtualFileSystemNode node) =>
        !string.IsNullOrEmpty(node.TargetId) && DesktopAppRegistry.TryGet(node.TargetId, out DesktopAppDescriptor descriptor)
            ? RuntimeAppIcons.Resolve(descriptor)
            : RuntimeAppIcons.Get(BuiltInIcon.Generic);

    private void Select(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node)) return;
        _selectedNodeId = node.Id;
        _nameInput.text = node.Name;
        Refresh();
        _details.text = node.Name + "\n" + (node.Kind == VirtualFileSystemNodeKind.Directory ? "File Folder" : "Application shortcut");
        _status.text = $"Selected {node.Name}";
    }

    private void Open(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node)) return;
        if (node.Kind == VirtualFileSystemNodeKind.Directory) { OpenDirectory(node.Id); return; }
        if (!string.IsNullOrEmpty(node.TargetId) && DesktopAppRegistry.TryGet(node.TargetId, out _)) { _context.OpenApp(node.TargetId); return; }
        _status.text = $"{node.Name} is unavailable. Reinstall or re-enable its mod to restore it.";
    }

    private void NavigateUp()
    {
        if (string.Equals(_currentDirectoryId, VirtualFileSystem.DesktopId, StringComparison.Ordinal)) { _status.text = "Desktop is the top of this virtual disk."; return; }
        if (VirtualFileSystemService.TryGetNode(_currentDirectoryId, out VirtualFileSystemNode current) && !string.IsNullOrEmpty(current.ParentId)) OpenDirectory(current.ParentId);
    }

    private void ShowSearchUnavailable() => _status.text = "Search is not available on this virtual computer.";

    private void ShowFolderTasks() => _status.text = "Folder tasks are shown on the left.";

    private void CreateFolder() => Mutate(() =>
    {
        string name = string.IsNullOrWhiteSpace(_nameInput.text) ? VirtualFileSystemService.CreateUniqueFolderName(_currentDirectoryId) : _nameInput.text;
        VirtualFileSystemNode folder = VirtualFileSystemService.CreateDirectory(_currentDirectoryId, name);
        _selectedNodeId = folder.Id;
        _nameInput.text = folder.Name;
        return $"Created {folder.Name}.";
    });

    private void RenameSelected() => Mutate(() =>
    {
        VirtualFileSystemNode node = RequireSelected();
        if (node.Kind != VirtualFileSystemNodeKind.Directory) throw new InvalidOperationException("Only folders can be renamed.");
        VirtualFileSystemService.Rename(node.Id, _nameInput.text);
        return $"Renamed folder to {_nameInput.text.Trim()}.";
    });

    private void CutSelected()
    {
        try { VirtualFileSystemNode node = RequireSelected(); _cutNodeId = node.Id; _status.text = $"Cut {node.Name}. Open the destination and choose Paste."; }
        catch (Exception exception) { _status.text = exception.Message; }
    }

    private void Paste() => Mutate(() =>
    {
        if (string.IsNullOrEmpty(_cutNodeId) || !VirtualFileSystemService.TryGetNode(_cutNodeId, out VirtualFileSystemNode node)) throw new InvalidOperationException("Cut a folder or app shortcut before pasting.");
        VirtualFileSystemService.Move(node.Id, _currentDirectoryId);
        _cutNodeId = null;
        _selectedNodeId = node.Id;
        return $"Moved {node.Name} here.";
    });

    private void DeleteSelected() => Mutate(() =>
    {
        VirtualFileSystemNode node = RequireSelected();
        if (node.Kind != VirtualFileSystemNodeKind.Directory) throw new InvalidOperationException("Only folders can be deleted. App shortcuts can be moved into folders.");
        VirtualFileSystemService.Delete(node.Id);
        _selectedNodeId = null;
        _nameInput.text = string.Empty;
        return $"Deleted {node.Name}.";
    });

    private VirtualFileSystemNode RequireSelected()
    {
        if (string.IsNullOrEmpty(_selectedNodeId) || !VirtualFileSystemService.TryGetNode(_selectedNodeId, out VirtualFileSystemNode node)) throw new InvalidOperationException("Select an item first.");
        return node;
    }

    private void Mutate(Func<string> mutation)
    {
        try { _status.text = mutation(); }
        catch (Exception exception) { _status.text = exception.Message; }
    }

    private void ClearItems()
    {
        for (int index = _items.childCount - 1; index >= 0; index--)
        {
            Transform child = _items.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static GameObject TaskGroup(Transform parent, string name, string title, float y, float height)
    {
        GameObject group = UiFactory.CreatePanel(parent, name, TaskBody);
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-12f, height); rect.anchoredPosition = new Vector2(0f, y);
        GameObject header = Panel(group.transform, "Header", ExplorerWhite, 30f, 0f);
        S1Text label = UiFactory.CreateText(header.transform, "Title", title, 13f, ExplorerBlue, Left(), true);
        UiFactory.Stretch(label.rectTransform, new Vector2(9f, 2f));
        return group;
    }

    private static Button TaskLink(Transform parent, string name, string label, float y, Color? color = null)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.32f);
        colors.pressedColor = new Color(0.35f, 0.48f, 0.75f, 0.24f);
        button.colors = colors;
        S1Text text = UiFactory.CreateText(buttonObject.transform, "Label", label, 11.5f, color ?? ExplorerBlue, Left());
        UiFactory.Stretch(text.rectTransform, new Vector2(10f, 1f));
        Fixed(rect, 0f, y, 146f, 22f);
        return button;
    }

    private static Button ToolbarButton(Transform parent, string name, string label, float x, float width)
    {
        Button button = UiFactory.CreateButton(parent, name, label, UiFactory.SurfaceRaised, out _);
        Fixed(button.GetComponent<RectTransform>(), x, 5f, width, 32f);
        return button;
    }

    private static GameObject Panel(Transform parent, string name, Color color, float height, float y)
    {
        GameObject panel = UiFactory.CreatePanel(parent, name, color);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height); rect.anchoredPosition = new Vector2(0f, y);
        return panel;
    }

    private static void Icon(Transform parent, string name, Sprite sprite, float x, float y, float size)
    {
        GameObject icon = new(name); icon.transform.SetParent(parent, false);
        RectTransform rect = icon.AddComponent<RectTransform>(); Fixed(rect, x, y, size, size);
        Image image = icon.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
    }

    private static void Fixed(RectTransform rect, float x, float y, float width, float height, bool right = false)
    {
        rect.anchorMin = new Vector2(right ? 1f : 0f, 1f); rect.anchorMax = rect.anchorMin; rect.pivot = new Vector2(right ? 1f : 0f, 1f);
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions Left() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions TopLeft() => Il2CppTMPro.TextAlignmentOptions.TopLeft;
    private static Il2CppTMPro.TextAlignmentOptions TopCenter() => Il2CppTMPro.TextAlignmentOptions.Top;
#else
    private static TMPro.TextAlignmentOptions Left() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions TopLeft() => TMPro.TextAlignmentOptions.TopLeft;
    private static TMPro.TextAlignmentOptions TopCenter() => TMPro.TextAlignmentOptions.Top;
#endif
}

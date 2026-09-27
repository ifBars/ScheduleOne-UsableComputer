using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.FileSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    private readonly S1Input _path;
    private readonly S1Input _search;
    private readonly Button _back;
    private readonly Button _forward;
    private readonly GameObject _taskPane;
    private readonly RectTransform _fileList;
    private readonly ScrollRect _scroll;
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private readonly Dictionary<string, (Button Button, S1Text Label)> _itemViews = new();
    private readonly S1Text _status;
    private readonly S1Text _details;
    private UiListenerRegistry _itemListeners = new();
    private string _currentDirectoryId = VirtualFileSystem.DesktopId;
    private string? _selectedNodeId;
    private bool _disposed;
    private int _lastColumns;

    internal FileExplorerApp(DesktopAppContext context)
    {
        _context = context;

        GameObject menu = Panel(context.Container, "MenuBar", UiFactory.Surface, 22f, 0f);
        string[] labels = { "File", "Edit", "View", "Help" };
        float x = 8f;
        foreach (string value in labels)
        {
            Button menuButton = UiFactory.CreateButton(menu.transform, value, value, UiFactory.Surface, out S1Text menuLabel);
            FitLabel(menuLabel);
            Fixed(menuButton.GetComponent<RectTransform>(), x, 0f, value.Length * 8f + 14f, 22f);
            context.Bind(menuButton, () => ShowMenu(value, menuButton.transform));
            x += value.Length * 8f + 14f;
        }

        GameObject toolbar = Panel(context.Container, "NavigationToolbar", UiFactory.SurfaceRaised, 42f, -22f);
        _back = ToolbarButton(toolbar.transform, "Back", "Back", 8f, 54f);
        _forward = ToolbarButton(toolbar.transform, "Forward", "Forward", 65f, 65f);
        Button up = ToolbarButton(toolbar.transform, "Up", "Up", 133f, 38f);
        GameObject separator = UiFactory.CreatePanel(toolbar.transform, "Separator", new Color(0.55f, 0.55f, 0.51f, 1f));
        Fixed(separator.GetComponent<RectTransform>(), 178f, 7f, 1f, 28f);
        _search = UiFactory.CreateInputField(toolbar.transform, "Search", string.Empty, "Search this folder", false, out _);
        FitInput(_search);
        BindTyping(_search);
        Fixed(_search.GetComponent<RectTransform>(), 186f, 7f, 168f, 28f);
        Button folders = ToolbarButton(toolbar.transform, "Folders", "Folders", 362f, 78f);
        context.Bind(_back, () => NavigateHistory(_backHistory, _forwardHistory));
        context.Bind(_forward, () => NavigateHistory(_forwardHistory, _backHistory));
        context.Bind(up, NavigateUp);
        context.Listeners.Add<string>(_ => Refresh(), _search.onValueChanged);
        context.Bind(folders, ToggleTasks);

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
        _path = UiFactory.CreateInputField(addressField.transform, "Path", "/Desktop", "/Desktop", false, out _);
        FitInput(_path);
        BindTyping(_path);
        UiFactory.Stretch(_path.GetComponent<RectTransform>(), new Vector2(28f, 2f));
        context.Listeners.Add<string>(_ => NavigateAddress(), _path.onSubmit);
        Button go = UiFactory.CreateButton(address.transform, "Go", "Go", UiFactory.SurfaceRaised, out S1Text goLabel);
        FitLabel(goLabel);
        Fixed(go.GetComponent<RectTransform>(), -3f, 3f, 25f, 24f, true);
        context.Bind(go, NavigateAddress);

        GameObject taskPane = UiFactory.CreatePanel(context.Container, "TaskPane", TaskPane);
        _taskPane = taskPane;
        RectTransform taskRect = taskPane.GetComponent<RectTransform>();
        taskRect.anchorMin = Vector2.zero;
        taskRect.anchorMax = new Vector2(0f, 1f);
        taskRect.offsetMin = new Vector2(0f, 24f);
        taskRect.offsetMax = new Vector2(158f, -94f);

        GameObject tasks = TaskGroup(taskPane.transform, "FileTasks", "File and Folder Tasks", -8f, 200f);
        Button create = TaskLink(tasks.transform, "NewFolder", "Make a new folder", 34f);
        Button rename = TaskLink(tasks.transform, "Rename", "Rename this item", 58f);
        Button cut = TaskLink(tasks.transform, "Cut", "Move this item", 82f);
        Button paste = TaskLink(tasks.transform, "Paste", "Paste here", 106f);
        Button delete = TaskLink(tasks.transform, "Delete", "Delete this item", 130f, UiFactory.Danger);
        context.Bind(create, CreateFolder);
        context.Bind(rename, RenameSelected);
        context.Bind(cut, CutSelected);
        context.Bind(paste, Paste);
        context.Bind(delete, DeleteSelected);

        Button copy = TaskLink(tasks.transform, "Copy", "Copy this item", 154f);
        Button textFile = TaskLink(tasks.transform, "NewText", "New text document", 178f);
        context.Bind(copy, () => { if (_selectedNodeId != null) context.FileInteraction.Copy(_selectedNodeId); });
        context.Bind(textFile, () => context.FileInteraction.NewText(_currentDirectoryId));

        GameObject details = TaskGroup(taskPane.transform, "DetailsGroup", "Details", -216f, 84f);
        _details = UiFactory.CreateText(details.transform, "Details", "Desktop\nVirtual folder", 12f, UiFactory.TextMuted, TopLeft());
        _details.rectTransform.anchorMin = Vector2.zero;
        _details.rectTransform.anchorMax = Vector2.one;
        _details.rectTransform.offsetMin = new Vector2(10f, 7f);
        _details.rectTransform.offsetMax = new Vector2(-8f, -32f);

        GameObject list = UiFactory.CreatePanel(context.Container, "FileList", ExplorerWhite);
        RectTransform listRect = list.GetComponent<RectTransform>();
        _fileList = listRect;
        listRect.anchorMin = Vector2.zero;
        listRect.anchorMax = Vector2.one;
        listRect.offsetMin = new Vector2(158f, 24f);
        listRect.offsetMax = new Vector2(0f, -94f);
        ScrollRect scroll = list.AddComponent<ScrollRect>();
        _scroll = scroll;
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
        context.FileInteraction.BindBackground(list, () => _currentDirectoryId, context.Listeners, ClearSelection);

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
    public void OnTick()
    {
        if (_lastColumns != Columns) Refresh();
        if (_context.IsFocused())
        {
            _context.FileInteraction.HandleKeyboard(_selectedNodeId, _currentDirectoryId,
                () => { if (_selectedNodeId != null) Open(_selectedNodeId); }, Refresh);
            Keyboard? keyboard = Keyboard.current;
            if (keyboard != null && !_path.isFocused && !_search.isFocused && !_context.FileInteraction.HasPopup)
            {
                bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
                if (alt && keyboard.leftArrowKey.wasPressedThisFrame) NavigateHistory(_backHistory, _forwardHistory);
                else if (alt && keyboard.rightArrowKey.wasPressedThisFrame) NavigateHistory(_forwardHistory, _backHistory);
                else if (keyboard.backspaceKey.wasPressedThisFrame) NavigateUp();
            }
        }
    }

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
        if (_currentDirectoryId != directoryId)
        {
            _backHistory.Push(_currentDirectoryId);
            _forwardHistory.Clear();
        }
        _currentDirectoryId = directoryId;
        _selectedNodeId = null;
        _search.SetTextWithoutNotify(string.Empty);
        _scroll.verticalNormalizedPosition = 1f;
        Refresh();
    }

    private int Columns => Math.Max(1, (int)((_fileList.rect.width - 16f) / 112f));
    private static Color SelectionColor => new(0.22f, 0.42f, 0.78f, 0.75f);

    private void UpdateSelection()
    {
        foreach (var entry in _itemViews)
        {
            bool selected = entry.Key == _selectedNodeId;
            ColorBlock colors = entry.Value.Button.colors;
            colors.normalColor = colors.selectedColor = selected ? SelectionColor : Color.clear;
            entry.Value.Button.colors = colors;
            entry.Value.Label.color = selected ? Color.white : UiFactory.TextPrimary;
        }
    }

    private void ClearSelection()
    {
        _selectedNodeId = null;
        UpdateSelection();
    }

    private void NavigateHistory(Stack<string> source, Stack<string> destination)
    {
        while (source.Count > 0)
        {
            string id = source.Pop();
            if (!VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode node) || node.Kind != VirtualFileSystemNodeKind.Directory) continue;
            destination.Push(_currentDirectoryId);
            _currentDirectoryId = id;
            _selectedNodeId = null;
            _search.SetTextWithoutNotify(string.Empty);
            _scroll.verticalNormalizedPosition = 1f;
            Refresh();
            return;
        }
        Refresh();
    }

    private void NavigateAddress() => Mutate(() =>
    {
        VirtualFileSystemNode node = VirtualFileSystemService.ResolvePath(_path.text.Trim().Replace('\\', '/'));
        Open(node.Id);
        return VirtualFileSystemService.GetPath(node.Id);
    });

    private void ToggleTasks()
    {
        _taskPane.SetActive(!_taskPane.activeSelf);
        _fileList.offsetMin = new Vector2(_taskPane.activeSelf ? 158f : 0f, 24f);
        Refresh();
    }

    private void ShowMenu(string name, Transform anchor)
    {
        var actions = new List<DesktopFileInteraction.MenuAction>();
        if (name == "File")
        {
            actions.Add(new("Open", _selectedNodeId == null ? null : () => Open(_selectedNodeId)));
            actions.Add(new("New Folder...", CreateFolder));
            actions.Add(new("New Text Document...", () => _context.FileInteraction.NewText(_currentDirectoryId)));
            actions.Add(new("Delete...", _selectedNodeId == null ? null : DeleteSelected));
        }
        else if (name == "Edit")
        {
            actions.Add(new("Cut", _selectedNodeId == null ? null : CutSelected));
            actions.Add(new("Copy", _selectedNodeId == null ? null : () => _context.FileInteraction.Copy(_selectedNodeId)));
            actions.Add(new("Paste", _context.FileInteraction.CanPaste ? Paste : null));
            actions.Add(new("Rename...", _selectedNodeId == null ? null : RenameSelected));
        }
        else if (name == "View")
        {
            actions.Add(new("Refresh", Refresh));
            actions.Add(new(_taskPane.activeSelf ? "Hide folder tasks" : "Show folder tasks", ToggleTasks));
        }
        else actions.Add(new("Using Files", () => _context.FileInteraction.ShowMessage(
            "Click to select; double-click to open.\nRight-click for file actions. Drag an item onto a folder to move it.\nUse Back, Forward, Up or enter a virtual path in Address.")));
        _context.FileInteraction.ShowMenu(RectTransformUtility.WorldToScreenPoint(_context.EventCamera, anchor.position), actions);
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
        _path.SetTextWithoutNotify(VirtualFileSystemService.GetPath(_currentDirectoryId));
        _lastColumns = Columns;
        var children = new List<VirtualFileSystemNode>();
        foreach (VirtualFileSystemNode child in VirtualFileSystemService.GetChildren(_currentDirectoryId))
            if (string.IsNullOrWhiteSpace(_search.text) || child.Name.IndexOf(_search.text, StringComparison.OrdinalIgnoreCase) >= 0)
                children.Add(child);
        _itemListeners.Dispose();
        _itemListeners = new UiListenerRegistry();
        ClearItems();
        _itemViews.Clear();
        for (int index = 0; index < children.Count; index++) CreateItem(children[index], index);
        _items.sizeDelta = new Vector2(0f, Math.Max(1, (children.Count + Columns - 1) / Columns) * 92f);
        _details.text = current.Name + "\nVirtual folder";
        _status.text = children.Count == 1 ? "1 object" : $"{children.Count} objects";
        _back.interactable = _backHistory.Count > 0;
        _forward.interactable = _forwardHistory.Count > 0;
        UiFactory.SetLayerRecursively(_items.gameObject, Constants.UiLayer);
    }

    private void CreateItem(VirtualFileSystemNode node, int index)
    {
        bool selected = string.Equals(_selectedNodeId, node.Id, StringComparison.Ordinal);
        var buttonObject = new GameObject($"Item_{node.Id}");
        buttonObject.transform.SetParent(_items, false);
        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        Image background = buttonObject.AddComponent<Image>();
        background.color = Color.white;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? SelectionColor : Color.clear;
        colors.highlightedColor = new Color(0.34f, 0.52f, 0.84f, 0.2f);
        colors.selectedColor = colors.normalColor;
        colors.fadeDuration = 0.12f;
        colors.pressedColor = new Color(0.22f, 0.42f, 0.78f, 0.4f);
        button.colors = colors;
        Fixed(rect, (index % Columns) * 112f, (index / Columns) * 92f, 102f, 84f);
        Sprite sprite = node.Kind == VirtualFileSystemNodeKind.Directory
            ? RuntimeAppIcons.Get(BuiltInIcon.Folder)
            : node.Kind == VirtualFileSystemNodeKind.File
                ? RuntimeAppIcons.Get(BuiltInIcon.Notes)
            : ResolveShortcutIcon(node);
        Icon(buttonObject.transform, "Icon", sprite, 30f, 4f, 43f);
        bool unavailable = node.Kind == VirtualFileSystemNodeKind.AppShortcut &&
            (string.IsNullOrEmpty(node.TargetId) || !DesktopAppRegistry.TryGet(node.TargetId, out _));
        S1Text label = UiFactory.CreateText(buttonObject.transform, "Name", node.Name + (unavailable ? "\n(unavailable)" : string.Empty), 12f,
            selected ? UiFactory.TextOnAccent : UiFactory.TextPrimary, TopCenter());
        Fixed(label.rectTransform, 2f, 50f, 98f, 32f);
        _itemViews[node.Id] = (button, label);
        _context.FileInteraction.BindItem(buttonObject, node.Id, _itemListeners, () => Select(node.Id), () => Open(node.Id));
    }

    private static Sprite ResolveShortcutIcon(VirtualFileSystemNode node) =>
        !string.IsNullOrEmpty(node.TargetId) && DesktopAppRegistry.TryGet(node.TargetId, out DesktopAppDescriptor descriptor)
            ? RuntimeAppIcons.Resolve(descriptor)
            : RuntimeAppIcons.Get(BuiltInIcon.Generic);

    private void Select(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node)) return;
        _selectedNodeId = node.Id;
        UpdateSelection();
        _details.text = node.Name + "\n" + (node.Kind == VirtualFileSystemNodeKind.Directory ? "File Folder" :
            node.Kind == VirtualFileSystemNodeKind.File ? "Text document" : "Application shortcut");
        _status.text = $"Selected {node.Name}";
    }

    private void Open(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node)) return;
        if (node.Kind == VirtualFileSystemNodeKind.Directory) { OpenDirectory(node.Id); return; }
        if (node.Kind == VirtualFileSystemNodeKind.File) { _context.OpenFile(node.Id); return; }
        if (!string.IsNullOrEmpty(node.TargetId) && DesktopAppRegistry.TryGet(node.TargetId, out _)) { _context.OpenApp(node.TargetId); return; }
        _status.text = $"{node.Name} is unavailable. Reinstall or re-enable its mod to restore it.";
    }

    private void NavigateUp()
    {
        if (string.Equals(_currentDirectoryId, VirtualFileSystem.RootId, StringComparison.Ordinal)) return;
        if (VirtualFileSystemService.TryGetNode(_currentDirectoryId, out VirtualFileSystemNode current) && !string.IsNullOrEmpty(current.ParentId)) OpenDirectory(current.ParentId);
    }

    private void CreateFolder() => _context.FileInteraction.NewFolder(_currentDirectoryId);

    private void RenameSelected() => Mutate(() =>
    {
        VirtualFileSystemNode node = RequireSelected();
        _context.FileInteraction.Rename(node.Id);
        return $"Renaming {node.Name}";
    });

    private void CutSelected()
    {
        try { VirtualFileSystemNode node = RequireSelected(); _context.FileInteraction.Cut(node.Id); _status.text = $"Cut {node.Name}. Open the destination and choose Paste."; }
        catch (Exception exception) { _status.text = exception.Message; }
    }

    private void Paste() => Mutate(() =>
    {
        _context.FileInteraction.Paste(_currentDirectoryId);
        return "Pasted.";
    });

    private void DeleteSelected() => Mutate(() =>
    {
        VirtualFileSystemNode node = RequireSelected();
        _context.FileInteraction.Delete(node.Id);
        return $"Delete {node.Name}?";
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
        Button button = UiFactory.CreateButton(parent, name, label, UiFactory.SurfaceRaised, out S1Text text);
        FitLabel(text);
        Fixed(button.GetComponent<RectTransform>(), x, 5f, width, 32f);
        return button;
    }

    private static void FitLabel(S1Text text)
    {
        text.fontSize = 12f;
#if IL2CPPMELON
        text.textWrappingMode = Il2CppTMPro.TextWrappingModes.NoWrap;
#else
        text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
#endif
        UiFactory.Stretch(text.rectTransform, new Vector2(3f, 1f));
    }

    private static void FitInput(S1Input input)
    {
        UiFactory.Stretch(input.textViewport, new Vector2(4f, 2f));
        foreach (S1Text text in input.GetComponentsInChildren<S1Text>(true))
        {
            text.fontSize = 12f;
            text.alignment = Left();
        }
    }

    private void BindTyping(S1Input input)
    {
        EventTrigger trigger = input.gameObject.AddComponent<EventTrigger>();
        _context.Listeners.AddTrigger(trigger, EventTriggerType.Select, _ => _context.SetTyping(true));
        _context.Listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => _context.SetTyping(false));
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

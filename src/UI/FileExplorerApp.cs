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
#elif MONOMELON
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class FileExplorerApp : IDesktopAppSession, IDesktopDirectorySession
{
    private readonly DesktopAppContext _context;
    private readonly RectTransform _rows;
    private readonly ScrollRect _scroll;
    private readonly S1Text _path;
    private readonly S1Text _status;
    private readonly S1Input _nameInput;
    private UiListenerRegistry _rowListeners = new();
    private string _currentDirectoryId = VirtualFileSystem.DesktopId;
    private string? _selectedNodeId;
    private string? _cutNodeId;
    private bool _disposed;

    internal FileExplorerApp(DesktopAppContext context)
    {
        _context = context;

        _path = UiFactory.CreateText(
            context.Container,
            "Path",
            "/Desktop",
            17f,
            UiFactory.TextPrimary,
            GetLeftAlignment(),
            bold: true);
        SetTopRect(_path.rectTransform, 28f, -8f, 8f, 8f);

        Button up = CreateToolbarButton(context.Container, "Up", "Up", 8f, 58f);
        Button create = CreateToolbarButton(context.Container, "NewFolder", "New folder", 72f, 96f);
        Button rename = CreateToolbarButton(context.Container, "Rename", "Rename", 174f, 82f);
        Button cut = CreateToolbarButton(context.Container, "Cut", "Cut", 262f, 56f);
        Button paste = CreateToolbarButton(context.Container, "Paste", "Paste", 324f, 62f);
        Button delete = CreateToolbarButton(context.Container, "Delete", "Delete", 392f, 68f, UiFactory.Danger);

        context.Bind(up, NavigateUp);
        context.Bind(create, CreateFolder);
        context.Bind(rename, RenameSelected);
        context.Bind(cut, CutSelected);
        context.Bind(paste, Paste);
        context.Bind(delete, DeleteSelected);

        _nameInput = UiFactory.CreateInputField(
            context.Container,
            "NameInput",
            string.Empty,
            "Folder name",
            multiline: false,
            out _);
        RectTransform inputRect = _nameInput.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.pivot = new Vector2(0.5f, 1f);
        inputRect.sizeDelta = new Vector2(-16f, 32f);
        inputRect.anchoredPosition = new Vector2(0f, -78f);

        var trigger = _nameInput.gameObject.AddComponent<EventTrigger>();
        context.Listeners.AddTrigger(trigger, EventTriggerType.Select, _ => context.SetTyping(true));
        context.Listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => context.SetTyping(false));
        context.Listeners.Add<string>(_ => context.SetTyping(false), _nameInput.onEndEdit);
        context.Listeners.Add<string>(_ =>
        {
            if (_nameInput.isFocused)
                context.SetTyping(true);
        }, _nameInput.onValueChanged);

        GameObject listPanel = UiFactory.CreatePanel(context.Container, "FileList", UiFactory.SurfaceRaised);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = Vector2.zero;
        listRect.anchorMax = Vector2.one;
        listRect.offsetMin = new Vector2(8f, 36f);
        listRect.offsetMax = new Vector2(-8f, -118f);

        _scroll = listPanel.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.inertia = true;
        _scroll.scrollSensitivity = 28f;
        _scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = new("FileViewport");
        viewportObject.transform.SetParent(listPanel.transform, false);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        UiFactory.Stretch(viewportRect, new Vector2(6f, 6f));
        viewportObject.AddComponent<RectMask2D>();

        GameObject rowsObject = new("FileRows");
        rowsObject.transform.SetParent(viewportObject.transform, false);
        _rows = rowsObject.AddComponent<RectTransform>();
        _rows.anchorMin = new Vector2(0f, 1f);
        _rows.anchorMax = new Vector2(1f, 1f);
        _rows.pivot = new Vector2(0.5f, 1f);
        _rows.sizeDelta = new Vector2(0f, 1f);
        _rows.anchoredPosition = Vector2.zero;
        _scroll.viewport = viewportRect;
        _scroll.content = _rows;

        _status = UiFactory.CreateText(
            context.Container,
            "Status",
            "Select an item to organize it. Changes save with this game.",
            13f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        RectTransform statusRect = _status.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.sizeDelta = new Vector2(-16f, 26f);
        statusRect.anchoredPosition = new Vector2(0f, 6f);

        VirtualFileSystemService.Changed += Refresh;
        Refresh();
    }

    public void OnOpened()
    {
        Refresh();
    }

    public void OnClosed()
    {
        _context.SetTyping(false);
    }

    public void OnTick()
    {
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        VirtualFileSystemService.Changed -= Refresh;
        _rowListeners.Dispose();
    }

    public void OpenDirectory(string directoryId)
    {
        if (_disposed || !VirtualFileSystemService.TryGetNode(directoryId, out VirtualFileSystemNode node) ||
            node.Kind != VirtualFileSystemNodeKind.Directory)
        {
            return;
        }

        _currentDirectoryId = directoryId;
        _selectedNodeId = null;
        Refresh();
    }

    private void Refresh()
    {
        if (_disposed)
            return;
        if (!VirtualFileSystemService.TryGetNode(_currentDirectoryId, out VirtualFileSystemNode current) ||
            current.Kind != VirtualFileSystemNodeKind.Directory)
        {
            _currentDirectoryId = VirtualFileSystem.DesktopId;
            _selectedNodeId = null;
        }

        _path.text = VirtualFileSystemService.GetPath(_currentDirectoryId);
        IReadOnlyList<VirtualFileSystemNode> children = VirtualFileSystemService.GetChildren(_currentDirectoryId);
        _rowListeners.Dispose();
        _rowListeners = new UiListenerRegistry();
        ClearRows();

        for (int index = 0; index < children.Count; index++)
            CreateRow(children[index], index);

        _rows.sizeDelta = new Vector2(0f, Math.Max(1f, children.Count * 38f));
        if (children.Count == 0)
            _status.text = "This folder is empty.";
        UiFactory.SetLayerRecursively(_rows.gameObject, Constants.UiLayer);
    }

    private void CreateRow(VirtualFileSystemNode node, int index)
    {
        string prefix = node.Kind == VirtualFileSystemNodeKind.Directory ? "Folder" : "App";
        string label = $"{prefix}  ·  {node.Name}";
        if (node.Kind == VirtualFileSystemNodeKind.AppShortcut &&
            (string.IsNullOrEmpty(node.TargetId) || !DesktopAppRegistry.TryGet(node.TargetId, out _)))
        {
            label += "  (unavailable)";
        }

        Button select = UiFactory.CreateButton(
            _rows,
            $"Select_{node.Id}",
            label,
            UiFactory.SurfaceInset,
            out S1Text selectLabel);
        RectTransform selectRect = select.GetComponent<RectTransform>();
        selectRect.anchorMin = new Vector2(0f, 1f);
        selectRect.anchorMax = new Vector2(1f, 1f);
        selectRect.pivot = new Vector2(0.5f, 1f);
        selectRect.sizeDelta = new Vector2(-84f, 32f);
        selectRect.anchoredPosition = new Vector2(-38f, -(index * 38f));
        selectLabel.alignment = GetLeftAlignment();
        selectLabel.rectTransform.offsetMin = new Vector2(10f, 3f);
        _rowListeners.Add(() => Select(node.Id), select.onClick);

        Button open = UiFactory.CreateButton(
            _rows,
            $"Open_{node.Id}",
            "Open",
            UiFactory.Accent,
            out _);
        RectTransform openRect = open.GetComponent<RectTransform>();
        openRect.anchorMin = new Vector2(1f, 1f);
        openRect.anchorMax = new Vector2(1f, 1f);
        openRect.pivot = new Vector2(1f, 1f);
        openRect.sizeDelta = new Vector2(72f, 32f);
        openRect.anchoredPosition = new Vector2(0f, -(index * 38f));
        _rowListeners.Add(() => Open(node.Id), open.onClick);
    }

    private void Select(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node))
            return;

        _selectedNodeId = node.Id;
        _nameInput.text = node.Name;
        _status.text = $"Selected {node.Name}.";
    }

    private void Open(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node))
            return;

        if (node.Kind == VirtualFileSystemNodeKind.Directory)
        {
            OpenDirectory(node.Id);
            return;
        }

        if (!string.IsNullOrEmpty(node.TargetId) && DesktopAppRegistry.TryGet(node.TargetId, out _))
        {
            _context.OpenApp(node.TargetId);
            return;
        }

        _status.text = $"{node.Name} is unavailable. Reinstall or re-enable its mod to restore it.";
    }

    private void NavigateUp()
    {
        if (string.Equals(_currentDirectoryId, VirtualFileSystem.DesktopId, StringComparison.Ordinal))
        {
            _status.text = "Desktop is the top of this virtual disk.";
            return;
        }
        if (VirtualFileSystemService.TryGetNode(_currentDirectoryId, out VirtualFileSystemNode current) &&
            !string.IsNullOrEmpty(current.ParentId))
        {
            OpenDirectory(current.ParentId);
        }
    }

    private void CreateFolder()
    {
        RunMutation(() =>
        {
            string name = string.IsNullOrWhiteSpace(_nameInput.text)
                ? VirtualFileSystemService.CreateUniqueFolderName(_currentDirectoryId)
                : _nameInput.text;
            VirtualFileSystemNode folder = VirtualFileSystemService.CreateDirectory(_currentDirectoryId, name);
            _selectedNodeId = folder.Id;
            _nameInput.text = folder.Name;
            return $"Created {folder.Name}.";
        });
    }

    private void RenameSelected()
    {
        RunMutation(() =>
        {
            VirtualFileSystemNode node = RequireSelected();
            if (node.Kind != VirtualFileSystemNodeKind.Directory)
                throw new InvalidOperationException("Only folders can be renamed.");
            VirtualFileSystemService.Rename(node.Id, _nameInput.text);
            return $"Renamed folder to {_nameInput.text.Trim()}.";
        });
    }

    private void CutSelected()
    {
        try
        {
            VirtualFileSystemNode node = RequireSelected();
            _cutNodeId = node.Id;
            _status.text = $"Cut {node.Name}. Open the destination and choose Paste.";
        }
        catch (Exception exception)
        {
            _status.text = exception.Message;
        }
    }

    private void Paste()
    {
        RunMutation(() =>
        {
            if (string.IsNullOrEmpty(_cutNodeId) || !VirtualFileSystemService.TryGetNode(_cutNodeId, out VirtualFileSystemNode node))
                throw new InvalidOperationException("Cut a folder or app shortcut before pasting.");
            VirtualFileSystemService.Move(node.Id, _currentDirectoryId);
            _cutNodeId = null;
            _selectedNodeId = node.Id;
            return $"Moved {node.Name} here.";
        });
    }

    private void DeleteSelected()
    {
        RunMutation(() =>
        {
            VirtualFileSystemNode node = RequireSelected();
            if (node.Kind != VirtualFileSystemNodeKind.Directory)
                throw new InvalidOperationException("Only folders can be deleted. App shortcuts can be moved into folders.");
            VirtualFileSystemService.Delete(node.Id);
            _selectedNodeId = null;
            _nameInput.text = string.Empty;
            return $"Deleted {node.Name}.";
        });
    }

    private VirtualFileSystemNode RequireSelected()
    {
        if (string.IsNullOrEmpty(_selectedNodeId) ||
            !VirtualFileSystemService.TryGetNode(_selectedNodeId, out VirtualFileSystemNode node))
        {
            throw new InvalidOperationException("Select an item first.");
        }
        return node;
    }

    private void RunMutation(Func<string> mutation)
    {
        try
        {
            string message = mutation();
            _status.text = message;
        }
        catch (Exception exception)
        {
            _status.text = exception.Message;
        }
    }

    private void ClearRows()
    {
        for (int index = _rows.childCount - 1; index >= 0; index--)
        {
            Transform child = _rows.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static Button CreateToolbarButton(
        Transform parent,
        string name,
        string label,
        float x,
        float width,
        Color? color = null)
    {
        Button button = UiFactory.CreateButton(parent, name, label, color ?? UiFactory.SurfaceRaised, out _);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, 30f);
        rect.anchoredPosition = new Vector2(x, -42f);
        return button;
    }

    private static void SetTopRect(RectTransform rect, float height, float y, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-(left + right), height);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
#else
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
#endif
}

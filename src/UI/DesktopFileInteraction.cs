using UsableComputer.FileSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
#if IL2CPPMELON
using S1Input = Il2CppTMPro.TMP_InputField;
using S1GameInput = Il2CppScheduleOne.GameInput;
#else
using S1Input = TMPro.TMP_InputField;
using S1GameInput = ScheduleOne.GameInput;
#endif

namespace UsableComputer.UI;

/// <summary>Desktop-owned clipboard, menus and drag state shared by file surfaces.</summary>
internal sealed class DesktopFileInteraction : IDisposable
{
    private readonly RectTransform _desktop;
    private readonly Func<Camera?> _camera;
    private readonly Action<string> _open;
    private UiListenerRegistry _popupListeners = new();
    private GameObject? _popup;
    private string? _clipboard;
    private bool _cut;
    private string? _dragNode;
    private RectTransform? _dragVisual;
    private Vector2 _dragStart;
    private int _dragPointer;
    private int _dismissedFrame = -1;

    internal DesktopFileInteraction(RectTransform desktop, Func<Camera?> camera, Action<string> open)
    {
        _desktop = desktop;
        _camera = camera;
        _open = open;
    }

    internal bool CanPaste => _clipboard != null && VirtualFileSystemService.TryGetNode(_clipboard, out _);
    internal bool HasPopup => _popup != null;

    internal void Tick()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) TryDismissTransient();
    }

    internal bool TryDismissTransient()
    {
        if (_dismissedFrame == Time.frameCount) return true;
        if (_popup == null && _dragNode == null) return false;
        _dismissedFrame = Time.frameCount;
        ClosePopup(); CancelDrag();
        return true;
    }

    internal void BindItem(GameObject item, string id, UiListenerRegistry listeners, Action select,
        Action open, Action<Vector2>? reposition = null)
    {
        EventTrigger trigger = item.GetComponent<EventTrigger>() ?? item.AddComponent<EventTrigger>();
        listeners.AddTrigger(trigger, EventTriggerType.PointerClick, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer == null || pointer.dragging) return;
            if (pointer.button == PointerEventData.InputButton.Left)
            {
                select();
                if (pointer.clickCount == 2) open();
            }
            else if (pointer.button == PointerEventData.InputButton.Right)
            {
                select();
                ShowItemMenu(id, pointer.position, open);
            }
        });
        listeners.AddTrigger(trigger, EventTriggerType.BeginDrag, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
            ClosePopup();
            select();
            CancelDrag();
            _dragNode = id;
            _dragPointer = pointer.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(item.transform.parent.GetComponent<RectTransform>(),
                pointer.position, _camera(), out _dragStart);
            GameObject visual = UiFactory.CreatePanel(_desktop, "FileDragPreview", new Color(0.2f, 0.4f, 0.8f, 0.6f));
            _dragVisual = visual.GetComponent<RectTransform>();
            _dragVisual.sizeDelta = new Vector2(140f, 28f);
            visual.GetComponent<Image>().raycastTarget = false;
            string name = VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode node) ? node.Name : "Move item";
            var label = UiFactory.CreateText(visual.transform, "Name", name, 12f, Color.white);
            UiFactory.Stretch(label.rectTransform, new Vector2(5f, 2f));
            UiFactory.SetLayerRecursively(visual, Constants.UiLayer);
            MoveDrag(pointer);
        });
        listeners.AddTrigger(trigger, EventTriggerType.Drag, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer != null && pointer.pointerId == _dragPointer) MoveDrag(pointer);
        });
        listeners.AddTrigger(trigger, EventTriggerType.EndDrag, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer == null || pointer.pointerId != _dragPointer) return;
            if (_dragNode == id && reposition != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                item.transform.parent.GetComponent<RectTransform>(), pointer.position, _camera(), out Vector2 end))
                reposition(end - _dragStart);
            CancelDrag();
        });
        if (VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode entry) && entry.Kind == VirtualFileSystemNodeKind.Directory)
            BindDrop(item, () => id, listeners);
        listeners.AddTrigger(trigger, EventTriggerType.Scroll, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            ScrollRect scroll = item.GetComponentInParent<ScrollRect>();
            if (pointer != null && scroll != null) scroll.OnScroll(pointer);
        });
    }

    internal void HandleKeyboard(string? selected, string directory, Action open, Action? refresh = null)
    {
        Keyboard? keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame) { TryDismissTransient(); return; }
        if (_popup != null) return;
        GameObject current = EventSystem.current?.currentSelectedGameObject!;
        if (current != null && current.GetComponent<S1Input>() != null) return;
        bool control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        if (control)
        {
            if (keyboard.cKey.wasPressedThisFrame && selected != null) Copy(selected);
            else if (keyboard.xKey.wasPressedThisFrame && selected != null) Cut(selected);
            else if (keyboard.vKey.wasPressedThisFrame) Execute(() => Paste(directory));
            else if (keyboard.nKey.wasPressedThisFrame && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) NewFolder(directory);
        }
        else if (keyboard.enterKey.wasPressedThisFrame && selected != null) open();
        else if (keyboard.f2Key.wasPressedThisFrame && selected != null) Rename(selected);
        else if (keyboard.deleteKey.wasPressedThisFrame && selected != null) Delete(selected);
        else if (keyboard.f5Key.wasPressedThisFrame) refresh?.Invoke();
    }

    internal void BindBackground(GameObject surface, Func<string> directory, UiListenerRegistry listeners,
        Action clearSelection, Action? properties = null, Action? arrange = null)
    {
        EventTrigger trigger = surface.GetComponent<EventTrigger>() ?? surface.AddComponent<EventTrigger>();
        listeners.AddTrigger(trigger, EventTriggerType.PointerClick, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer == null) return;
            clearSelection();
            if (pointer.button == PointerEventData.InputButton.Right)
                ShowFolderMenu(directory(), pointer.position, properties, arrange);
        });
        BindDrop(surface, directory, listeners);
    }

    private void BindDrop(GameObject surface, Func<string> directory, UiListenerRegistry listeners)
    {
        EventTrigger trigger = surface.GetComponent<EventTrigger>() ?? surface.AddComponent<EventTrigger>();
        listeners.AddTrigger(trigger, EventTriggerType.Drop, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (_dragNode == null || pointer == null || pointer.pointerId != _dragPointer) return;
            string destination = directory();
            if (!VirtualFileSystemService.TryGetNode(_dragNode, out VirtualFileSystemNode node)) { CancelDrag(); return; }
            // A same-folder drop is left to the desktop's position handler.
            if (node.ParentId == destination) return;
            string id = _dragNode;
            CancelDrag();
            Execute(() => VirtualFileSystemService.Move(id, destination));
        });
    }

    private void MoveDrag(PointerEventData pointer)
    {
        if (_dragVisual != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _desktop, pointer.position, _camera(), out Vector2 point))
            _dragVisual.localPosition = point + new Vector2(55f, -20f);
    }

    internal void Cut(string id) { _clipboard = id; _cut = true; }
    internal void Copy(string id) { _clipboard = id; _cut = false; }
    internal void Paste(string directory)
    {
        if (!CanPaste) return;
        string id = _clipboard!;
        if (_cut)
        {
            VirtualFileSystemService.Move(id, directory);
            _clipboard = null;
        }
        else VirtualFileSystemService.Copy(id, directory);
    }

    internal void Rename(string id)
    {
        if (!VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode node)) return;
        Prompt("Rename", node.Name, name => VirtualFileSystemService.Rename(id, name));
    }

    internal void Delete(string id)
    {
        if (!VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode node)) return;
        if (node.Kind == VirtualFileSystemNodeKind.AppShortcut)
        {
            ShowMessage("Application shortcuts are managed by installed apps. You can move them into a folder.");
            return;
        }
        ShowMenu(new Vector2(Screen.width / 2f, Screen.height / 2f), new[]
        {
            new MenuAction($"Delete {node.Name}?", null),
            new MenuAction("Delete permanently", () => VirtualFileSystemService.Delete(id)),
            new MenuAction("Cancel", () => { })
        });
    }

    internal void NewFolder(string directory) => Prompt("New Folder", VirtualFileSystemService.CreateUniqueFolderName(directory),
        name => VirtualFileSystemService.CreateDirectory(directory, name));
    internal void NewText(string directory) => Prompt("New Text Document", "New Text Document.txt",
        name => VirtualFileSystemService.CreateTextFile(directory, name, string.Empty));

    internal void ShowItemMenu(string id, Vector2 position, Action? open = null)
    {
        if (!VirtualFileSystemService.TryGetNode(id, out VirtualFileSystemNode node)) return;
        ShowMenu(position, new[]
        {
            new MenuAction("Open", open ?? (() => _open(id))),
            new MenuAction("Cut", () => Cut(id)), new MenuAction("Copy", () => Copy(id)),
            new MenuAction("Rename...", () => Rename(id)),
            new MenuAction("Delete...", node.Kind == VirtualFileSystemNodeKind.AppShortcut ? null : () => Delete(id)),
            new MenuAction("Properties", () => ShowMessage(node.Name + "\n" + node.Kind + "\n" + VirtualFileSystemService.GetPath(id)))
        });
    }

    internal void ShowFolderMenu(string directory, Vector2 position, Action? properties = null, Action? arrange = null)
    {
        var entries = new List<MenuAction>
        {
            new("New Folder...", () => NewFolder(directory)), new("New Text Document...", () => NewText(directory)),
            new("Paste", CanPaste ? () => Paste(directory) : null),
        };
        if (arrange != null) entries.Add(new("Arrange Icons", arrange));
        if (properties != null) entries.Add(new("Properties", properties));
        ShowMenu(position, entries);
    }

    internal readonly struct MenuAction
    {
        internal MenuAction(string label, Action? action) { Label = label; Action = action; }
        internal string Label { get; }
        internal Action? Action { get; }
    }

    internal void ShowMenu(Vector2 screenPosition, IEnumerable<MenuAction> actions)
    {
        CreateOverlay();
        var entries = new List<MenuAction>(actions);
        GameObject menu = UiFactory.CreatePanel(_popup!.transform, "ContextMenu", UiFactory.Surface);
        RectTransform rect = menu.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(218f, entries.Count * 27f + 6f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_desktop, screenPosition, _camera(), out Vector2 point);
        point.x = Mathf.Clamp(point.x, _desktop.rect.xMin, _desktop.rect.xMax - rect.sizeDelta.x);
        point.y = Mathf.Clamp(point.y, _desktop.rect.yMin + rect.sizeDelta.y, _desktop.rect.yMax);
        rect.localPosition = point;
        for (int i = 0; i < entries.Count; i++)
        {
            MenuAction entry = entries[i];
            Button button = UiFactory.CreateButton(menu.transform, "Menu_" + i, entry.Label, UiFactory.Surface, out _);
            Place(button.GetComponent<RectTransform>(), 3f, -3f - i * 27f, 212f, 27f);
            button.interactable = entry.Action != null;
            _popupListeners.Add(() => { ClosePopup(); if (entry.Action != null) Execute(entry.Action); }, button.onClick);
        }
        UiFactory.SetLayerRecursively(_popup, Constants.UiLayer);
    }

    private void Prompt(string title, string initial, Action<string> accept)
    {
        CreateOverlay();
        GameObject dialog = UiFactory.CreatePanel(_popup!.transform, "FileDialog", UiFactory.Surface);
        dialog.GetComponent<RectTransform>().sizeDelta = new Vector2(350f, 150f);
        var label = UiFactory.CreateText(dialog.transform, "Title", title, 15f, UiFactory.TextPrimary);
        Place(label.rectTransform, 12f, -10f, 326f, 25f);
        S1Input input = UiFactory.CreateInputField(dialog.transform, "Name", initial, "Name", false, out _);
        Place(input.GetComponent<RectTransform>(), 12f, -42f, 326f, 30f);
        var error = UiFactory.CreateText(dialog.transform, "Error", string.Empty, 11f, UiFactory.Danger);
        Place(error.rectTransform, 12f, -77f, 326f, 30f);
        Button ok = UiFactory.CreateButton(dialog.transform, "OK", "OK", UiFactory.SurfaceRaised, out _);
        Place(ok.GetComponent<RectTransform>(), 172f, -112f, 78f, 27f);
        Button cancel = UiFactory.CreateButton(dialog.transform, "Cancel", "Cancel", UiFactory.SurfaceRaised, out _);
        Place(cancel.GetComponent<RectTransform>(), 260f, -112f, 78f, 27f);
        Action submit = () => { try { accept(input.text); ClosePopup(); } catch (Exception exception) { error.text = exception.Message; } };
        _popupListeners.Add(submit, ok.onClick);
        _popupListeners.Add(ClosePopup, cancel.onClick);
        _popupListeners.Add<string>(_ => submit(), input.onSubmit);
        EventTrigger typing = input.gameObject.AddComponent<EventTrigger>();
        _popupListeners.AddTrigger(typing, EventTriggerType.Select, _ => S1GameInput.IsTyping = true);
        _popupListeners.AddTrigger(typing, EventTriggerType.Deselect, _ => S1GameInput.IsTyping = false);
        UiFactory.SetLayerRecursively(_popup, Constants.UiLayer);
        input.ActivateInputField();
    }

    internal void Execute(Action action)
    {
        try { action(); } catch (Exception exception) { ShowMessage(exception.Message); }
    }

    internal void ShowMessage(string message)
    {
        CreateOverlay();
        GameObject dialog = UiFactory.CreatePanel(_popup!.transform, "Message", UiFactory.Surface);
        dialog.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 170f);
        var label = UiFactory.CreateText(dialog.transform, "Text", message, 13f, UiFactory.TextPrimary);
        Place(label.rectTransform, 12f, -10f, 356f, 112f);
        Button ok = UiFactory.CreateButton(dialog.transform, "OK", "OK", UiFactory.SurfaceRaised, out _);
        Place(ok.GetComponent<RectTransform>(), 280f, -130f, 88f, 28f);
        _popupListeners.Add(ClosePopup, ok.onClick);
        UiFactory.SetLayerRecursively(_popup, Constants.UiLayer);
    }

    private void CreateOverlay()
    {
        ClosePopup();
        _popup = UiFactory.CreatePanel(_desktop, "DesktopPopup", Color.clear);
        UiFactory.Stretch(_popup.GetComponent<RectTransform>(), Vector2.zero);
        EventTrigger trigger = _popup.AddComponent<EventTrigger>();
        _popupListeners.AddTrigger(trigger, EventTriggerType.PointerClick, _ => ClosePopup());
    }

    internal void BringToFront() { if (_popup != null) _popup.transform.SetAsLastSibling(); }
    internal void ClosePopup()
    {
        _popupListeners.Dispose();
        _popupListeners = new UiListenerRegistry();
        if (_popup != null) { S1GameInput.IsTyping = false; _popup.SetActive(false); UnityEngine.Object.Destroy(_popup); }
        _popup = null;
    }
    internal void CancelDrag()
    {
        _dragNode = null;
        if (_dragVisual != null) UnityEngine.Object.Destroy(_dragVisual.gameObject);
        _dragVisual = null;
    }
    public void Dispose() { ClosePopup(); CancelDrag(); }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }
}

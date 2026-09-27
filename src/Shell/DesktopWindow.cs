using System;
using UsableComputer.API;
using UsableComputer.Apps.Notes;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using Il2CppInterop.Runtime;
using S1GameInput = Il2CppScheduleOne.GameInput;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1GameInput = ScheduleOne.GameInput;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.Shell;

internal sealed class DesktopWindow : IDisposable
{
    private readonly WindowManager _manager;
    private readonly RectTransform _parent;
    private readonly DesktopAppDescriptor _descriptor;
    private Camera? _eventCamera;
    private readonly UiListenerRegistry _listeners = new();
    private DesktopAppContext? _context;
    private IDesktopAppSession? _session;
    private bool _sessionOpened;
    private bool _disposed;
    private bool _dragging;
    private int _dragPointerId;
    private Vector2 _dragOffset;
    private bool _tickFaultLogged;
    private bool _maximized;
    private Vector2 _restoreSize;
    private Vector2 _restorePosition;
    private S1Text? _maximizeLabel;
    private Image _titleBar = null!;
    internal bool IsFocused { get; private set; }

    internal DesktopWindow(
        WindowManager manager,
        RectTransform parent,
        RectTransform taskbarParent,
        Camera? eventCamera,
        DesktopAppDescriptor descriptor)
    {
        _manager = manager;
        _parent = parent;
        _eventCamera = eventCamera;
        _descriptor = descriptor;
        BuildChrome(taskbarParent);
    }

    internal string AppId => _descriptor.Id;

    internal string Title => _descriptor.Title;

    internal bool IsDisposed => _disposed;

    internal RectTransform Root { get; private set; } = null!;

    internal RectTransform TaskbarButton { get; private set; } = null!;

    internal void Initialize()
    {
        if (_disposed)
            return;

        _context = new DesktopAppContext(
            Content.transform,
            Content,
            _eventCamera,
            _listeners,
            Close,
            _manager.RequestOpenApp,
            value => S1GameInput.IsTyping = value,
            _manager.OpenFile);
        _context.FileInteraction = _manager.FileInteraction;
        _context.IsFocused = () => IsFocused;
        IDesktopAppSession? createdSession = _descriptor.CreateSession(_context);
        if (createdSession == null)
        {
            if (_disposed)
                return;

            throw new InvalidOperationException($"Desktop app '{AppId}' returned a null session.");
        }

        if (_disposed)
        {
            DisposeReturnedSession(createdSession);
            return;
        }

        _session = createdSession;
        _sessionOpened = true;
        _session.OnOpened();

        if (_disposed)
            return;
    }

    internal void Show()
    {
        if (_disposed)
            return;

        SetVisible(true);
    }

    internal void Tick()
    {
        if (_disposed || !Root.gameObject.activeSelf || _session == null)
            return;

        try
        {
            _session.OnTick();
        }
        catch (Exception exception)
        {
            if (!_tickFaultLogged)
            {
                _tickFaultLogged = true;
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Desktop app '{AppId}' tick failed: {exception.Message}");
            }
        }
    }

    internal void SetEventCamera(Camera? eventCamera)
    {
        _eventCamera = eventCamera;
        _context?.SetEventCamera(eventCamera);
    }

    internal void OpenDirectory(string directoryId)
    {
        if (!_disposed && _session is IDesktopDirectorySession directorySession)
            directorySession.OpenDirectory(directoryId);
    }

    internal void OpenFile(string fileId)
    {
        if (!_disposed && _session is NotesApp notes)
            notes.OpenFile(fileId);
    }

    internal void SetVisible(bool visible)
    {
        if (_disposed || Root == null)
            return;

        if (Root.gameObject.activeSelf == visible)
            return;

        Root.gameObject.SetActive(visible);
        if (!visible)
            _dragging = false;
        _manager.FocusTopWindow();
        if (_session is IDesktopAppVisibilitySession visibilitySession)
            visibilitySession.OnVisibilityChanged(visible);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dragging = false;
        S1GameInput.IsTyping = false;
        _manager.Remove(this);

        if (_sessionOpened && _session != null)
        {
            try
            {
                _session.OnClosed();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Desktop app '{AppId}' close hook failed: {exception.Message}");
            }
        }

        if (_session != null)
        {
            try
            {
                _session.Dispose();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Desktop app '{AppId}' dispose hook failed: {exception.Message}");
            }
        }

        _sessionOpened = false;
        _session = null;
        _context?.Dispose();
        _context = null;
        _listeners.Dispose();

        if (Root != null)
            UnityEngine.Object.Destroy(Root.gameObject);
        if (TaskbarButton != null)
            UnityEngine.Object.Destroy(TaskbarButton.gameObject);
    }

    private void DisposeReturnedSession(IDesktopAppSession session)
    {
        try
        {
            session.Dispose();
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Desktop app '{AppId}' factory session cleanup failed: {exception.Message}");
        }
    }

    private RectTransform Content { get; set; } = null!;

    private void BuildChrome(RectTransform taskbarParent)
    {
        GameObject rootObject = UiFactory.CreatePanel(
            _parent,
            $"Window_{_descriptor.Id}",
            UiFactory.Surface);
        Root = rootObject.GetComponent<RectTransform>();
        Root.pivot = new Vector2(0.5f, 0.5f);
        Root.anchorMin = new Vector2(0.5f, 0.5f);
        Root.anchorMax = new Vector2(0.5f, 0.5f);
        Root.sizeDelta = _descriptor.PreferredWindowSize;
        Root.anchoredPosition = _descriptor.PreferredWindowPosition;

        GameObject titleBar = UiFactory.CreatePanel(rootObject.transform, "TitleBar", UiFactory.TitleBar);
        _titleBar = titleBar.GetComponent<Image>();
        RectTransform titleBarRect = titleBar.GetComponent<RectTransform>();
        titleBarRect.anchorMin = new Vector2(0f, 1f);
        titleBarRect.anchorMax = new Vector2(1f, 1f);
        titleBarRect.pivot = new Vector2(0.5f, 1f);
        titleBarRect.sizeDelta = new Vector2(0f, 28f);
        titleBarRect.anchoredPosition = Vector2.zero;

        GameObject titleHighlight = UiFactory.CreatePanel(titleBar.transform, "Highlight", UiFactory.AccentHighlight);
        RectTransform highlightRect = titleHighlight.GetComponent<RectTransform>();
        highlightRect.anchorMin = new Vector2(0f, 1f);
        highlightRect.anchorMax = new Vector2(1f, 1f);
        highlightRect.pivot = new Vector2(0.5f, 1f);
        highlightRect.sizeDelta = new Vector2(-4f, 2f);
        highlightRect.anchoredPosition = new Vector2(0f, -1f);
        titleHighlight.GetComponent<Image>().raycastTarget = false;

        GameObject titleIconObject = new("Icon");
        titleIconObject.transform.SetParent(titleBar.transform, false);
        RectTransform titleIconRect = titleIconObject.AddComponent<RectTransform>();
        UiFactory.SetRect(
            titleIconRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(18f, 18f),
            new Vector2(5f, 0f));
        Image titleIcon = titleIconObject.AddComponent<Image>();
        titleIcon.sprite = RuntimeAppIcons.Resolve(_descriptor);
        titleIcon.preserveAspect = true;
        titleIcon.raycastTarget = false;

        S1Text titleLabel = UiFactory.CreateText(
            titleBar.transform,
            "Title",
            _descriptor.Title,
            14f,
            UiFactory.TextOnAccent,
            GetAlignment(),
            bold: true);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleLabel.rectTransform.offsetMin = new Vector2(27f, 0f);
        titleLabel.rectTransform.offsetMax = new Vector2(-80f, 0f);

        Button minimizeButton = UiFactory.CreateButton(
            titleBar.transform,
            "Minimize",
            "—",
            UiFactory.TitleBarDark,
            out S1Text minimizeLabel);
        minimizeLabel.color = UiFactory.TextOnAccent;
        SetTitleButtonRect(minimizeButton.GetComponent<RectTransform>(), -59f);
        _listeners.Add(() => SetVisible(false), minimizeButton.onClick);

        Button maximizeButton = UiFactory.CreateButton(
            titleBar.transform,
            "Maximize",
            "□",
            UiFactory.TitleBarDark,
            out _maximizeLabel);
        _maximizeLabel.color = UiFactory.TextOnAccent;
        SetTitleButtonRect(maximizeButton.GetComponent<RectTransform>(), -36f);
        _listeners.Add(ToggleMaximized, maximizeButton.onClick);

        Button closeButton = UiFactory.CreateButton(
            titleBar.transform,
            "Close",
            "×",
            UiFactory.Danger,
            out S1Text closeLabel);
        closeLabel.color = UiFactory.TextOnAccent;
        SetTitleButtonRect(closeButton.GetComponent<RectTransform>(), -13f);
        _listeners.Add(Close, closeButton.onClick);

        GameObject content = UiFactory.CreatePanel(
            rootObject.transform,
            "Content",
            UiFactory.SurfaceInset);
        Content = content.GetComponent<RectTransform>();
        Content.anchorMin = Vector2.zero;
        Content.anchorMax = Vector2.one;
        Content.offsetMin = new Vector2(7f, 7f);
        Content.offsetMax = new Vector2(-7f, -35f);

        TaskbarButton = CreateTaskbarButton(taskbarParent);
        _listeners.Add(ToggleFromTaskbar, TaskbarButton.GetComponent<Button>().onClick);

        var titleTrigger = titleBar.AddComponent<EventTrigger>();
        _listeners.AddTrigger(titleTrigger, EventTriggerType.PointerDown, OnPointerDown);
        _listeners.AddTrigger(titleTrigger, EventTriggerType.Drag, OnDrag);
        _listeners.AddTrigger(titleTrigger, EventTriggerType.PointerUp, OnPointerUp);
        _listeners.AddTrigger(titleTrigger, EventTriggerType.EndDrag, OnPointerUp);
        _listeners.AddTrigger(titleTrigger, EventTriggerType.PointerClick, data =>
        {
            PointerEventData? pointer = PointerEvents.Get(data);
            if (pointer != null && pointer.button == PointerEventData.InputButton.Left && pointer.clickCount == 2)
                ToggleMaximized();
        });

        UiFactory.SetLayerRecursively(rootObject, Constants.UiLayer);
        UiFactory.SetLayerRecursively(TaskbarButton.gameObject, Constants.UiLayer);
    }

    private RectTransform CreateTaskbarButton(RectTransform parent)
    {
        Button button = UiFactory.CreateButton(
            parent,
            $"Task_{_descriptor.Id}",
            string.Empty,
            UiFactory.TaskbarButton,
            out S1Text label);
        label.gameObject.SetActive(false);

        GameObject iconObject = new("Icon");
        iconObject.transform.SetParent(button.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        UiFactory.Stretch(iconRect, new Vector2(5f, 3f));
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = RuntimeAppIcons.Resolve(_descriptor);
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return button.GetComponent<RectTransform>();
    }

    private void ToggleFromTaskbar()
    {
        if (_disposed)
            return;

        if (Root.gameObject.activeSelf && IsFocused)
            SetVisible(false);
        else if (Root.gameObject.activeSelf)
            _manager.BringToFront(this);
        else
        {
            Show();
            _manager.BringToFront(this);
        }
    }

    private void OnPointerDown(BaseEventData data)
    {
        PointerEventData? pointer = GetPointerData(data);
        if (pointer == null || pointer.button != PointerEventData.InputButton.Left || _disposed || _maximized)
            return;

        _manager.BringToFront(this);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent,
                pointer.position,
                _eventCamera,
                out Vector2 localPoint))
        {
            _dragOffset = localPoint - (Vector2)Root.localPosition;
            _dragPointerId = pointer.pointerId;
            _dragging = true;
        }
    }

    private void OnDrag(BaseEventData data)
    {
        PointerEventData? pointer = GetPointerData(data);
        if (!_dragging || pointer == null || pointer.pointerId != _dragPointerId || _disposed || _maximized)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent,
                pointer.position,
                _eventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        Vector2 desired = localPoint - _dragOffset;
        Vector2 halfWindow = Root.rect.size * 0.5f;
        Rect desktop = _parent.rect;
        float maximumX = desktop.xMax - halfWindow.x;
        float maximumY = desktop.yMax - halfWindow.y;
        // Keep the title bar reachable even for an app larger than the desktop.
        desired.x = Mathf.Clamp(desired.x, Mathf.Min(desktop.xMin + halfWindow.x, maximumX), maximumX);
        desired.y = Mathf.Clamp(desired.y, Mathf.Min(desktop.yMin + 42f + halfWindow.y, maximumY), maximumY);
        Root.localPosition = new Vector3(desired.x, desired.y, Root.localPosition.z);
    }

    private void OnPointerUp(BaseEventData data)
    {
        PointerEventData? pointer = GetPointerData(data);
        if (pointer != null && pointer.pointerId == _dragPointerId)
            _dragging = false;
    }

    internal void SetFocused(bool focused)
    {
        IsFocused = focused;
        if (_titleBar != null) _titleBar.color = focused ? UiFactory.TitleBar : UiFactory.TitleBarDark;
    }

    private static PointerEventData? GetPointerData(BaseEventData data)
    {
#if IL2CPPMELON
        // Native callbacks can wrap a PointerEventData as its declared base type.
        return data.TryCast<PointerEventData>();
#else
        return data as PointerEventData;
#endif
    }

    private void Close()
    {
        Dispose();
    }

    private void ToggleMaximized()
    {
        if (_disposed)
            return;

        _dragging = false;
        if (!_maximized)
        {
            _restoreSize = Root.sizeDelta;
            _restorePosition = Root.anchoredPosition;
            Vector2 parentSize = _parent.rect.size;
            Root.sizeDelta = new Vector2(Mathf.Max(240f, parentSize.x - 4f), Mathf.Max(180f, parentSize.y - 46f));
            Root.anchoredPosition = new Vector2(0f, 21f);
            _maximized = true;
            if (_maximizeLabel != null)
                _maximizeLabel.text = "❐";
        }
        else
        {
            Root.sizeDelta = _restoreSize;
            Root.anchoredPosition = _restorePosition;
            _maximized = false;
            if (_maximizeLabel != null)
                _maximizeLabel.text = "□";
        }
        _manager.BringToFront(this);
    }

    private static void SetTitleButtonRect(RectTransform rectTransform, float x)
    {
        rectTransform.anchorMin = new Vector2(1f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(20f, 20f);
        rectTransform.anchoredPosition = new Vector2(x, 0f);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
#else
    private static TMPro.TextAlignmentOptions GetAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
#endif
}

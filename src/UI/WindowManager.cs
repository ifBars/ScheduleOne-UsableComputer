using System;
using System.Collections.Generic;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using S1GameInput = Il2CppScheduleOne.GameInput;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1GameInput = ScheduleOne.GameInput;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class WindowManager : IDisposable
{
    private readonly RectTransform _windowLayer;
    private readonly RectTransform _taskbarApps;
    private Camera? _eventCamera;
    private readonly List<DesktopWindow> _windows = new();
    private bool _disposed;

    internal WindowManager(RectTransform windowLayer, RectTransform taskbarApps, Camera? eventCamera)
    {
        _windowLayer = windowLayer;
        _taskbarApps = taskbarApps;
        _eventCamera = eventCamera;
    }

    internal DesktopWindow? Open(DesktopAppDescriptor descriptor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowManager));
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        var window = new DesktopWindow(
            this,
            _windowLayer,
            _taskbarApps,
            _eventCamera,
            descriptor);
        _windows.Add(window);
        RelayoutTaskbar();
        try
        {
            window.Initialize();
        }
        catch
        {
            if (!window.IsDisposed)
                window.Dispose();
            throw;
        }

        if (window.IsDisposed)
            return null;

        BringToFront(window);
        return window;
    }

    internal bool HasWindow(string appId)
    {
        foreach (DesktopWindow window in _windows)
        {
            if (string.Equals(window.AppId, appId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal void ShowWindow(string appId)
    {
        foreach (DesktopWindow window in _windows)
        {
            if (string.Equals(window.AppId, appId, StringComparison.Ordinal))
            {
                window.Show();
                BringToFront(window);
                return;
            }
        }
    }

    internal void CloseWindow(string appId)
    {
        for (int index = _windows.Count - 1; index >= 0; index--)
        {
            if (string.Equals(_windows[index].AppId, appId, StringComparison.Ordinal))
            {
                _windows[index].Dispose();
                return;
            }
        }
    }

    internal IReadOnlyList<string> GetAppIds()
    {
        var result = new List<string>(_windows.Count);
        foreach (DesktopWindow window in _windows)
            result.Add(window.AppId);
        return result;
    }

    internal void Tick()
    {
        if (_disposed)
            return;

        var snapshot = _windows.ToArray();
        foreach (DesktopWindow window in snapshot)
            window.Tick();
    }

    internal void BringToFront(DesktopWindow window)
    {
        if (!_windows.Contains(window) || window.Root == null)
            return;

        window.Root.SetAsLastSibling();
    }

    internal void SetEventCamera(Camera? eventCamera)
    {
        _eventCamera = eventCamera;
        foreach (DesktopWindow window in _windows)
            window.SetEventCamera(eventCamera);
    }

    internal void Remove(DesktopWindow window)
    {
        if (!_windows.Remove(window))
            return;

        RelayoutTaskbar();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (int index = _windows.Count - 1; index >= 0; index--)
            _windows[index].Dispose();
        _windows.Clear();
    }

    private void RelayoutTaskbar()
    {
        for (int index = 0; index < _windows.Count; index++)
        {
            RectTransform? button = _windows[index].TaskbarButton;
            if (button == null)
                continue;

            button.anchorMin = new Vector2(0f, 0f);
            button.anchorMax = new Vector2(0f, 1f);
            button.pivot = new Vector2(0f, 0.5f);
            button.sizeDelta = new Vector2(42f, -8f);
            button.anchoredPosition = new Vector2(index * 48f, 0f);
        }
    }
}

internal interface IDesktopAppVisibilitySession
{
    void OnVisibilityChanged(bool visible);
}

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
    private Vector2 _dragOffset;
    private bool _tickFaultLogged;
    private bool _maximized;
    private Vector2 _restoreSize;
    private Vector2 _restorePosition;
    private S1Text? _maximizeLabel;

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
            value => S1GameInput.IsTyping = value);
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

    internal void SetVisible(bool visible)
    {
        if (_disposed || Root == null)
            return;

        if (Root.gameObject.activeSelf == visible)
            return;

        Root.gameObject.SetActive(visible);
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

        if (Root.gameObject.activeSelf)
            _manager.BringToFront(this);
        else
        {
            Show();
            _manager.BringToFront(this);
        }
    }

    private void OnPointerDown(BaseEventData data)
    {
        if (data is not PointerEventData pointer || _disposed || _maximized)
            return;

        _manager.BringToFront(this);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parent,
                pointer.position,
                _eventCamera,
                out Vector2 localPoint))
        {
            _dragOffset = localPoint - (Vector2)Root.localPosition;
            _dragging = true;
        }
    }

    private void OnDrag(BaseEventData data)
    {
        if (!_dragging || data is not PointerEventData pointer || _disposed || _maximized)
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
        Vector2 halfParent = _parent.rect.size * 0.5f;
        Vector2 halfWindow = Root.rect.size * 0.5f;
        desired.x = Mathf.Clamp(desired.x, -halfParent.x + halfWindow.x, halfParent.x - halfWindow.x);
        desired.y = Mathf.Clamp(desired.y, -halfParent.y + halfWindow.y, halfParent.y - halfWindow.y);
        Root.localPosition = new Vector3(desired.x, desired.y, Root.localPosition.z);
    }

    private void OnPointerUp(BaseEventData data)
    {
        _dragging = false;
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

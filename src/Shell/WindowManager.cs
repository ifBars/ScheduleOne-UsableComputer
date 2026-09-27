using System;
using System.Collections.Generic;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

#if IL2CPPMELON
using Il2CppInterop.Runtime;
#endif

namespace UsableComputer.Shell;

internal sealed class WindowManager : IDisposable
{
    private readonly RectTransform _windowLayer;
    private readonly RectTransform _taskbarApps;
    private readonly Action<string> _requestOpenApp;
    internal DesktopFileInteraction FileInteraction { get; }
    private Camera? _eventCamera;
    private readonly List<DesktopWindow> _windows = new();
    private bool _disposed;

    internal WindowManager(
        RectTransform windowLayer,
        RectTransform taskbarApps,
        Camera? eventCamera,
        Action<string> requestOpenApp,
        DesktopFileInteraction fileInteraction)
    {
        _windowLayer = windowLayer;
        _taskbarApps = taskbarApps;
        _eventCamera = eventCamera;
        _requestOpenApp = requestOpenApp ?? throw new ArgumentNullException(nameof(requestOpenApp));
        FileInteraction = fileInteraction;
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

    internal void OpenDirectory(string appId, string directoryId)
    {
        foreach (DesktopWindow window in _windows)
        {
            if (string.Equals(window.AppId, appId, StringComparison.Ordinal))
            {
                window.OpenDirectory(directoryId);
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

        Mouse? mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) && EventSystem.current != null)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() };
#if IL2CPPMELON
            var hits = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
#else
            var hits = new List<RaycastResult>();
#endif
            _windowLayer.GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            if (hits.Count > 0)
                foreach (DesktopWindow window in _windows)
                    if (hits[0].gameObject.transform.IsChildOf(window.Root)) { BringToFront(window); break; }
        }

        var snapshot = _windows.ToArray();
        foreach (DesktopWindow window in snapshot)
            window.Tick();
    }

    internal void BringToFront(DesktopWindow window)
    {
        if (!_windows.Contains(window) || window.Root == null)
            return;

        window.Root.SetAsLastSibling();
        foreach (DesktopWindow candidate in _windows) candidate.SetFocused(candidate == window);
    }

    internal bool HasFocusedWindow => _windows.Exists(window => window.IsFocused && window.Root != null && window.Root.gameObject.activeSelf);
    internal void ClearFocus() { foreach (DesktopWindow window in _windows) window.SetFocused(false); }

    internal void FocusTopWindow()
    {
        DesktopWindow? top = null;
        foreach (DesktopWindow window in _windows)
            if (window.Root.gameObject.activeSelf && (top == null || window.Root.GetSiblingIndex() > top.Root.GetSiblingIndex())) top = window;
        foreach (DesktopWindow window in _windows) window.SetFocused(window == top);
    }

    internal void SetEventCamera(Camera? eventCamera)
    {
        _eventCamera = eventCamera;
        foreach (DesktopWindow window in _windows)
            window.SetEventCamera(eventCamera);
    }

    internal void RequestOpenApp(string appId)
    {
        _requestOpenApp(appId);
    }

    internal void OpenFile(string fileId)
    {
        _requestOpenApp(Constants.NotesAppId);
        foreach (DesktopWindow window in _windows)
            if (window.AppId == Constants.NotesAppId)
            {
                window.OpenFile(fileId);
                return;
            }
    }

    internal void Remove(DesktopWindow window)
    {
        if (!_windows.Remove(window))
            return;

        RelayoutTaskbar();
        FocusTopWindow();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseAll();
    }

    internal void CloseAll()
    {
        // Disposal callbacks may change the live window list.
        foreach (DesktopWindow window in _windows.ToArray())
            window.Dispose();
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

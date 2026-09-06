using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UsableComputer.UI;

namespace UsableComputer.API;

/// <summary>
/// Window-owned services exposed to a desktop app session.
/// </summary>
public sealed class DesktopAppContext
{
    private readonly UiListenerRegistry _listeners;
    private readonly List<Action> _cleanupActions = new();
    private readonly Action _requestClose;
    private readonly Action<string> _requestOpenApp;
    private readonly Action<string> _requestOpenFile;
    private readonly Action<bool> _setTyping;
    private bool _disposed;

    internal DesktopAppContext(
        Transform container,
        RectTransform content,
        Camera? eventCamera,
        UiListenerRegistry listeners,
        Action requestClose,
        Action<string> requestOpenApp,
        Action<bool> setTyping,
        Action<string> requestOpenFile)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        EventCamera = eventCamera;
        _listeners = listeners ?? throw new ArgumentNullException(nameof(listeners));
        _requestClose = requestClose ?? throw new ArgumentNullException(nameof(requestClose));
        _requestOpenApp = requestOpenApp ?? throw new ArgumentNullException(nameof(requestOpenApp));
        _setTyping = setTyping ?? throw new ArgumentNullException(nameof(setTyping));
        _requestOpenFile = requestOpenFile ?? throw new ArgumentNullException(nameof(requestOpenFile));
    }

    public Transform Container { get; }

    public RectTransform Content { get; }

    public Camera? EventCamera { get; private set; }

    /// <summary>
    /// Binds a common Unity UI button while keeping its listener owned by this window.
    /// </summary>
    public void Bind(Button button, Action action)
    {
        ThrowIfDisposed();
        if (button == null)
            throw new ArgumentNullException(nameof(button));
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _listeners.Add(action, button.onClick);
    }

    /// <summary>
    /// Binds a common Unity UI input field without exposing a TextMeshPro runtime alias.
    /// </summary>
    public void Bind(InputField inputField, Action<string> action)
    {
        ThrowIfDisposed();
        if (inputField == null)
            throw new ArgumentNullException(nameof(inputField));
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        _listeners.Add(action, inputField.onValueChanged);
    }

    /// <summary>
    /// Adds cleanup for UI objects or other window-owned resources created by the session.
    /// </summary>
    public void RegisterCleanup(Action cleanup)
    {
        ThrowIfDisposed();
        _cleanupActions.Add(cleanup ?? throw new ArgumentNullException(nameof(cleanup)));
    }

    public void RequestClose()
    {
        if (_disposed)
            return;

        _requestClose();
    }

    /// <summary>
    /// Opens or focuses another registered desktop app in the same Usable Computer.
    /// </summary>
    public void OpenApp(string appId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("An app id is required.", nameof(appId));

        _requestOpenApp(appId);
    }

    public void SetTyping(bool typing)
    {
        if (_disposed)
            return;

        _setTyping(typing);
    }

    /// <summary>Opens a virtual text file in Notes using its stable node ID.</summary>
    public void OpenFile(string fileId)
    {
        ThrowIfDisposed();
        _requestOpenFile(fileId);
    }

    internal UiListenerRegistry Listeners => _listeners;

    internal void SetEventCamera(Camera? eventCamera)
    {
        if (!_disposed)
            EventCamera = eventCamera;
    }

    internal void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _setTyping(false);
        for (int index = _cleanupActions.Count - 1; index >= 0; index--)
        {
            try
            {
                _cleanupActions[index]();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Desktop app cleanup failed: {exception.Message}");
            }
        }

        _cleanupActions.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DesktopAppContext));
    }
}

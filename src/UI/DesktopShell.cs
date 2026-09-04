using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Native;
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

internal sealed class DesktopShell : IDisposable
{
    private readonly Action _onPowerRequested;
    private readonly Action _onRegistryChanged;
    private readonly Action<DesktopAppearance, DesktopAppearance> _onAppearanceChanged;
    private readonly UiListenerRegistry _listeners = new();
    private UiListenerRegistry _surfaceListeners = new();
    private readonly GameObject _root;
    private readonly Canvas _canvas;
    private readonly RectTransform _windowLayer;
    private readonly WindowManager _windows;
    private readonly RuntimeWallpaper _wallpaper;
    private readonly NativeGameClock _gameClock = new();
    private readonly S1Text _clock;
    private GameObject? _desktopIconsRoot;
    private GameObject? _startMenu;
    private bool _registrySubscribed;
    private bool _refreshingSurfaces;
    private bool _refreshPending;
    private bool _disposed;
    private string _lastClock = string.Empty;

    internal DesktopShell(Transform screenAnchor, Camera? eventCamera, Action onPowerRequested)
    {
        _onPowerRequested = onPowerRequested;
        _onRegistryChanged = RefreshAppSurfaces;
        _onAppearanceChanged = ApplyAppearance;

        _root = new GameObject(Constants.CanvasName);
        _root.transform.SetParent(screenAnchor, false);
        _root.AddComponent<RectTransform>();
        _root.AddComponent<Canvas>();
        _root.AddComponent<GraphicRaycaster>();
        _root.transform.localPosition = new Vector3(0f, 0f, 0.00015f);
        _root.transform.localRotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one * 0.00046875f;

        RectTransform canvasRect = _root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 536f);
        _canvas = _root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = eventCamera;
        _canvas.sortingOrder = Constants.CanvasSortingOrder;

        _wallpaper = RuntimeWallpaper.Create(_root.transform);

        _desktopIconsRoot = new GameObject("DesktopIcons");
        _desktopIconsRoot.transform.SetParent(_root.transform, false);
        RectTransform desktopIconsRect = _desktopIconsRoot.AddComponent<RectTransform>();
        UiFactory.Stretch(desktopIconsRect, Vector2.zero);

        GameObject windowLayerObject = new GameObject("WindowLayer");
        windowLayerObject.transform.SetParent(_root.transform, false);
        windowLayerObject.AddComponent<RectTransform>();
        RectTransform windowLayer = windowLayerObject.GetComponent<RectTransform>();
        UiFactory.Stretch(windowLayer, Vector2.zero);
        _windowLayer = windowLayer;

        GameObject taskbarObject = UiFactory.CreatePanel(
            _root.transform,
            "Taskbar",
            UiFactory.Taskbar);
        RectTransform taskbarRect = taskbarObject.GetComponent<RectTransform>();
        taskbarRect.anchorMin = new Vector2(0f, 0f);
        taskbarRect.anchorMax = new Vector2(1f, 0f);
        taskbarRect.pivot = new Vector2(0.5f, 0f);
        taskbarRect.sizeDelta = new Vector2(0f, 42f);
        taskbarRect.anchoredPosition = Vector2.zero;

        Button startButton = UiFactory.CreateButton(
            taskbarObject.transform,
            "Start",
            "start",
            UiFactory.StartGreen,
            out S1Text startLabel);
        startLabel.color = UiFactory.TextOnAccent;
        RectTransform startButtonRect = startButton.GetComponent<RectTransform>();
        startButtonRect.anchorMin = new Vector2(0f, 0.5f);
        startButtonRect.anchorMax = new Vector2(0f, 0.5f);
        startButtonRect.pivot = new Vector2(0f, 0.5f);
        startButtonRect.sizeDelta = new Vector2(86f, -8f);
        startButtonRect.anchoredPosition = new Vector2(8f, 0f);

        GameObject taskbarAppsObject = new GameObject("TaskbarApps");
        taskbarAppsObject.transform.SetParent(taskbarObject.transform, false);
        taskbarAppsObject.AddComponent<RectTransform>();
        RectTransform taskbarApps = taskbarAppsObject.GetComponent<RectTransform>();
        taskbarApps.anchorMin = new Vector2(0f, 0f);
        taskbarApps.anchorMax = new Vector2(1f, 1f);
        taskbarApps.offsetMin = new Vector2(102f, 0f);
        taskbarApps.offsetMax = new Vector2(-94f, 0f);

        _clock = UiFactory.CreateText(
            taskbarObject.transform,
            "Clock",
            "",
            14f,
            UiFactory.TextOnAccent,
            GetCenterAlignment());
        _clock.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        _clock.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        _clock.rectTransform.pivot = new Vector2(1f, 0.5f);
        _clock.rectTransform.sizeDelta = new Vector2(84f, -8f);
        _clock.rectTransform.anchoredPosition = new Vector2(-8f, 0f);

        _windows = new WindowManager(windowLayer, taskbarApps, eventCamera);
        _listeners.Add(ToggleStartMenu, startButton.onClick);
        RefreshAppSurfaces();

        DesktopAppRegistry.Changed += _onRegistryChanged;
        PreferencesStore.AppearanceChanged += _onAppearanceChanged;
        _registrySubscribed = true;
        UiFactory.SetLayerRecursively(_root, Constants.UiLayer);
        if (_startMenu != null)
            _startMenu.SetActive(false);
        _root.SetActive(false);
        UpdateClock();
    }

    internal void SetEventCamera(Camera? eventCamera)
    {
        if (_disposed)
            return;

        _canvas.worldCamera = eventCamera;
        _windows.SetEventCamera(eventCamera);
    }

    internal void Show()
    {
        if (_disposed)
            return;

        _root.SetActive(true);
        UpdateClock();
    }

    internal void Hide()
    {
        if (_disposed)
            return;

        HideStartMenu();
        ClearEventSystemSelection();
        _root.SetActive(false);
        S1GameInput.IsTyping = false;
    }

    internal void Tick()
    {
        if (_disposed || !_root.activeSelf)
            return;

        _windows.Tick();
        UpdateClock();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_registrySubscribed)
        {
            DesktopAppRegistry.Changed -= _onRegistryChanged;
            PreferencesStore.AppearanceChanged -= _onAppearanceChanged;
            _registrySubscribed = false;
        }

        ClearEventSystemSelection();
        S1GameInput.IsTyping = false;
        _surfaceListeners.Dispose();
        _listeners.Dispose();
        _windows.Dispose();
        _wallpaper.Dispose();
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
    }

    private void RefreshAppSurfaces()
    {
        if (_disposed)
            return;
        if (_refreshingSurfaces)
        {
            _refreshPending = true;
            return;
        }

        _refreshingSurfaces = true;
        try
        {
            do
            {
                _refreshPending = false;
                IReadOnlyList<DesktopAppDescriptor> descriptors = DesktopAppRegistry.GetAll();
                var registeredIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DesktopAppDescriptor descriptor in descriptors)
                    registeredIds.Add(descriptor.Id);

                IReadOnlyList<string> openAppIds = _windows.GetAppIds();
                foreach (string appId in openAppIds)
                {
                    if (!registeredIds.Contains(appId))
                        _windows.CloseWindow(appId);
                }

                _surfaceListeners.Dispose();
                _surfaceListeners = new UiListenerRegistry();
                EnsureDesktopIconsRoot();
                ClearDesktopIconChildren();
                GameObject desktopIconsRoot = _desktopIconsRoot
                    ?? throw new InvalidOperationException("Desktop icon root was not created.");

                int desktopIndex = 0;
                for (int index = 0; index < descriptors.Count; index++)
                {
                    DesktopAppDescriptor descriptor = descriptors[index];
                    if (string.Equals(descriptor.Id, Constants.SettingsAppId, StringComparison.Ordinal))
                        continue;
                    string appId = descriptor.Id;
                    CreateDesktopIcon(
                        desktopIconsRoot.transform,
                        descriptor,
                        GetIconPosition(desktopIndex++),
                        () => OpenApp(appId));
                }

                if (_startMenu != null)
                    UnityEngine.Object.Destroy(_startMenu);
                _startMenu = BuildStartMenu(descriptors);
                GameObject startMenu = _startMenu;
                startMenu.SetActive(false);
                // Root-level ordering is intentional: icons < windows < taskbar < Start menu.
                desktopIconsRoot.transform.SetSiblingIndex(Mathf.Max(0, _windowLayer.GetSiblingIndex() - 1));
                startMenu.transform.SetAsLastSibling();
                UiFactory.SetLayerRecursively(desktopIconsRoot, Constants.UiLayer);
                UiFactory.SetLayerRecursively(startMenu, Constants.UiLayer);
            }
            while (_refreshPending && !_disposed);
        }
        finally
        {
            _refreshingSurfaces = false;
        }
    }

    private void OpenApp(string appId)
    {
        HideStartMenu();
        if (!DesktopAppRegistry.TryGet(appId, out DesktopAppDescriptor descriptor))
            return;

        if (_windows.HasWindow(appId))
        {
            _windows.ShowWindow(appId);
            return;
        }

        try
        {
            _windows.Open(descriptor);
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Error(
                $"[{Constants.ModName}] Could not open desktop app '{appId}': {exception}");
        }
    }

    private GameObject BuildStartMenu(IReadOnlyList<DesktopAppDescriptor> descriptors)
    {
        int programCount = 0;
        DesktopAppDescriptor? settings = null;
        foreach (DesktopAppDescriptor descriptor in descriptors)
        {
            if (string.Equals(descriptor.Id, Constants.SettingsAppId, StringComparison.Ordinal))
                settings = descriptor;
            else
                programCount++;
        }

        float menuHeight = 70f + (programCount * 36f) + 82f;
        GameObject menu = UiFactory.CreatePanel(
            _root.transform,
            "StartMenu",
            UiFactory.SurfaceRaised);
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0f, 0f);
        menuRect.anchorMax = new Vector2(0f, 0f);
        menuRect.pivot = new Vector2(0f, 0f);
        menuRect.sizeDelta = new Vector2(226f, menuHeight);
        menuRect.anchoredPosition = new Vector2(8f, 50f);

        S1Text title = UiFactory.CreateText(
            menu.transform,
            "Title",
            "Programs",
            17f,
            UiFactory.TextPrimary,
            GetLeftAlignment(),
            bold: true);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-24f, 34f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        int programIndex = 0;
        for (int index = 0; index < descriptors.Count; index++)
        {
            DesktopAppDescriptor descriptor = descriptors[index];
            if (string.Equals(descriptor.Id, Constants.SettingsAppId, StringComparison.Ordinal))
                continue;
            Button entry = CreateStartEntry(
                menu.transform,
                descriptor.Title,
                menuHeight - 58f - (programIndex++ * 36f),
                () => OpenApp(descriptor.Id),
                reserveIconSpace: true);
            AddStartEntryIcon(entry.transform, descriptor);
        }

        if (settings != null)
        {
            Button settingsEntry = CreateStartEntry(
                menu.transform,
                "Settings",
                68f,
                () => OpenApp(Constants.SettingsAppId),
                reserveIconSpace: true);
            AddStartEntryIcon(settingsEntry.transform, settings);
        }

        CreateStartEntry(
            menu.transform,
            "Power off",
            28f,
            _onPowerRequested,
            UiFactory.Danger);
        return menu;
    }

    private Button CreateStartEntry(
        Transform parent,
        string label,
        float y,
        Action action,
        Color? color = null,
        bool reserveIconSpace = false)
    {
        Button button = UiFactory.CreateButton(
            parent,
            $"Start_{label}",
            label,
            color ?? UiFactory.SurfaceRaised,
            out S1Text buttonLabel);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-24f, 34f);
        rect.anchoredPosition = new Vector2(0f, y);
        if (reserveIconSpace)
        {
            buttonLabel.alignment = GetLeftAlignment();
            buttonLabel.rectTransform.offsetMin = new Vector2(44f, 3f);
        }
        _surfaceListeners.Add(action, button.onClick);
        return button;
    }

    private static void AddStartEntryIcon(Transform parent, DesktopAppDescriptor descriptor)
    {
        Sprite icon = RuntimeAppIcons.Resolve(descriptor);

        GameObject iconObject = new("Icon");
        iconObject.transform.SetParent(parent, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        UiFactory.SetRect(
            iconRect,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(28f, 28f),
            new Vector2(8f, 0f));
        Image image = iconObject.AddComponent<Image>();
        image.sprite = icon;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void CreateDesktopIcon(
        Transform parent,
        DesktopAppDescriptor descriptor,
        Vector2 position,
        Action action)
    {
        GameObject iconObject = UiFactory.CreatePanel(
            parent,
            $"DesktopIcon_{descriptor.Id}",
            Color.clear);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(84f, 84f);
        iconRect.anchoredPosition = position;

        Image targetImage = iconObject.GetComponent<Image>();
        var button = iconObject.AddComponent<Button>();
        button.targetGraphic = targetImage;
        ColorBlock colorBlock = button.colors;
        colorBlock.normalColor = Color.clear;
        colorBlock.highlightedColor = new Color(0.31f, 0.55f, 0.95f, 0.5f);
        colorBlock.pressedColor = new Color(0.17f, 0.37f, 0.78f, 0.72f);
        colorBlock.selectedColor = Color.clear;
        colorBlock.disabledColor = Color.clear;
        button.colors = colorBlock;

        GameObject iconFrame = UiFactory.CreatePanel(
            iconObject.transform,
            "IconFrame",
            UiFactory.AccentHighlight);
        RectTransform iconFrameRect = iconFrame.GetComponent<RectTransform>();
        iconFrameRect.anchorMin = new Vector2(0.5f, 1f);
        iconFrameRect.anchorMax = new Vector2(0.5f, 1f);
        iconFrameRect.pivot = new Vector2(0.5f, 1f);
        iconFrameRect.sizeDelta = new Vector2(42f, 42f);
        iconFrameRect.anchoredPosition = new Vector2(0f, -4f);

        Sprite icon = RuntimeAppIcons.Resolve(descriptor);

        Image iconBackground = iconFrame.GetComponent<Image>();
        iconBackground.color = Color.clear;
        iconBackground.sprite = null;
        GameObject imageObject = new("Icon");
        imageObject.transform.SetParent(iconFrame.transform, false);
        RectTransform imageRect = imageObject.AddComponent<RectTransform>();
        UiFactory.Stretch(imageRect, Vector2.zero);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = icon;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        S1Text caption = UiFactory.CreateText(
            iconObject.transform,
            "Caption",
            descriptor.Title,
            12f,
            UiFactory.TextOnAccent,
            GetCenterAlignment(),
            bold: true);
        var captionShadow = caption.gameObject.AddComponent<Shadow>();
        captionShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        captionShadow.effectDistance = new Vector2(1f, -1f);
        caption.rectTransform.anchorMin = new Vector2(0f, 0f);
        caption.rectTransform.anchorMax = new Vector2(1f, 0f);
        caption.rectTransform.pivot = new Vector2(0.5f, 0f);
        caption.rectTransform.sizeDelta = new Vector2(0f, 26f);
        caption.rectTransform.anchoredPosition = new Vector2(0f, 6f);

        _surfaceListeners.Add(action, button.onClick);
    }

    private void ToggleStartMenu()
    {
        if (_startMenu != null)
            _startMenu.SetActive(!_startMenu.activeSelf);
    }

    private void HideStartMenu()
    {
        if (_startMenu != null)
            _startMenu.SetActive(false);
    }

    private void ApplyAppearance(DesktopAppearance previous, DesktopAppearance next)
    {
        if (_disposed)
            return;

        if (previous.Theme != next.Theme)
            UiFactory.ApplyTheme(_root, previous.Theme, next.Theme);
        if (previous.Wallpaper != next.Wallpaper)
            _wallpaper.Apply(next.Wallpaper);
    }

    private void EnsureDesktopIconsRoot()
    {
        if (_desktopIconsRoot != null)
            return;

        _desktopIconsRoot = new GameObject("DesktopIcons");
        _desktopIconsRoot.transform.SetParent(_root.transform, false);
        RectTransform desktopIconsRect = _desktopIconsRoot.AddComponent<RectTransform>();
        UiFactory.Stretch(desktopIconsRect, Vector2.zero);
    }

    private void ClearDesktopIconChildren()
    {
        if (_desktopIconsRoot == null)
            return;

        for (int index = _desktopIconsRoot.transform.childCount - 1; index >= 0; index--)
        {
            Transform child = _desktopIconsRoot.transform.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static Vector2 GetIconPosition(int index)
    {
        int column = index / 5;
        int row = index % 5;
        return new Vector2(56f + (column * 98f), -18f - (row * 92f));
    }

    private static void ClearEventSystemSelection()
    {
        try
        {
            EventSystem? eventSystem = EventSystem.current;
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
        }
        catch
        {
            // Scene teardown can invalidate the event system between lookup and deselection.
        }
    }

    private void UpdateClock()
    {
        string clock = _gameClock.TryGetFormattedTime(out string formattedTime)
            ? formattedTime
            : string.Empty;
        if (string.Equals(clock, _lastClock, StringComparison.Ordinal))
            return;

        _lastClock = clock;
        _clock.text = clock;
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetCenterAlignment() => Il2CppTMPro.TextAlignmentOptions.Center;
#else
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetCenterAlignment() => TMPro.TextAlignmentOptions.Center;
#endif
}

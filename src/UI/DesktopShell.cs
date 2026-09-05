using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Native;
using UsableComputer.FileSystem;
using UsableComputer.Logic;
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
    private readonly Action _onFileSystemChanged;
    private readonly Action<DesktopAppearance, DesktopAppearance> _onAppearanceChanged;
    private readonly UiListenerRegistry _listeners = new();
    private UiListenerRegistry _surfaceListeners = new();
    private readonly GameObject _root;
    private readonly Canvas _canvas;
    private readonly RectTransform _windowLayer;
    private readonly WindowManager _windows;
    private readonly RuntimeWallpaper _wallpaper;
    private readonly NativeGameClock _gameClock = new();
    private readonly Button _startButton;
    private readonly GameObject _startTooltip;
    private readonly S1Text _clock;
    private GameObject? _desktopIconsRoot;
    private readonly RectTransform _iconViewport;
    private readonly ScrollRect _iconScroll;
    private readonly GameObject _iconScrollbar;
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
        _onFileSystemChanged = RefreshAppSurfaces;
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

        GameObject viewport = UiFactory.CreatePanel(_root.transform, "DesktopIconViewport", Color.clear);
        _iconViewport = viewport.GetComponent<RectTransform>();
        UiFactory.Stretch(_iconViewport, Vector2.zero);
        _iconViewport.offsetMin = new Vector2(0f, 72f);
        viewport.AddComponent<RectMask2D>();
        _iconScroll = viewport.AddComponent<ScrollRect>();
        _iconScroll.viewport = _iconViewport;
        _iconScroll.horizontal = true;
        _iconScroll.vertical = false;
        _iconScroll.movementType = ScrollRect.MovementType.Clamped;
        _iconScroll.scrollSensitivity = 30f;
        _iconScroll.inertia = false;
        EnsureDesktopIconsRoot();

        _iconScrollbar = UiFactory.CreatePanel(_root.transform, "DesktopIconScrollbar", UiFactory.SurfaceInset);
        RectTransform scrollbarRect = _iconScrollbar.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = Vector2.zero;
        scrollbarRect.anchorMax = new Vector2(1f, 0f);
        scrollbarRect.pivot = new Vector2(0.5f, 0f);
        scrollbarRect.sizeDelta = new Vector2(-24f, 16f);
        scrollbarRect.anchoredPosition = new Vector2(0f, 48f);
        GameObject handle = UiFactory.CreatePanel(_iconScrollbar.transform, "Handle", UiFactory.SurfaceRaised);
        UiFactory.Stretch(handle.GetComponent<RectTransform>(), Vector2.zero);
        var scrollbar = _iconScrollbar.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        _iconScroll.horizontalScrollbar = scrollbar;
        _iconScroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

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

        _startButton = UiFactory.CreateButton(
            taskbarObject.transform,
            "Start",
            string.Empty,
            UiFactory.StartGreen,
            out S1Text startLabel);
        startLabel.gameObject.SetActive(false);
        RectTransform startButtonRect = _startButton.GetComponent<RectTransform>();
        startButtonRect.anchorMin = new Vector2(0f, 0.5f);
        startButtonRect.anchorMax = new Vector2(0f, 0.5f);
        startButtonRect.pivot = new Vector2(0f, 0.5f);
        startButtonRect.sizeDelta = new Vector2(48f, -8f);
        startButtonRect.anchoredPosition = new Vector2(8f, 0f);
        AddStartButtonIcon(_startButton.transform);

        _startTooltip = UiFactory.CreatePanel(taskbarObject.transform, "StartTooltip", UiFactory.SurfaceRaised);
        RectTransform tooltipRect = _startTooltip.GetComponent<RectTransform>();
        tooltipRect.anchorMin = Vector2.zero;
        tooltipRect.anchorMax = Vector2.zero;
        tooltipRect.pivot = Vector2.zero;
        tooltipRect.sizeDelta = new Vector2(52f, 24f);
        tooltipRect.anchoredPosition = new Vector2(8f, 46f);
        S1Text tooltipLabel = UiFactory.CreateText(
            _startTooltip.transform,
            "Label",
            "Start",
            13f,
            UiFactory.TextPrimary,
            GetCenterAlignment());
        UiFactory.Stretch(tooltipLabel.rectTransform, new Vector2(4f, 2f));
        _startTooltip.SetActive(false);

        var startTrigger = _startButton.gameObject.AddComponent<EventTrigger>();
        _listeners.AddTrigger(startTrigger, EventTriggerType.PointerEnter, _ => ShowStartTooltip());
        _listeners.AddTrigger(startTrigger, EventTriggerType.PointerExit, _ => HideStartTooltip());

        GameObject taskbarAppsObject = new GameObject("TaskbarApps");
        taskbarAppsObject.transform.SetParent(taskbarObject.transform, false);
        taskbarAppsObject.AddComponent<RectTransform>();
        RectTransform taskbarApps = taskbarAppsObject.GetComponent<RectTransform>();
        taskbarApps.anchorMin = new Vector2(0f, 0f);
        taskbarApps.anchorMax = new Vector2(1f, 1f);
        taskbarApps.offsetMin = new Vector2(64f, 0f);
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

        _windows = new WindowManager(windowLayer, taskbarApps, eventCamera, OpenApp);
        _listeners.Add(ToggleStartMenu, _startButton.onClick);
        RefreshAppSurfaces();

        DesktopAppRegistry.Changed += _onRegistryChanged;
        VirtualFileSystemService.Changed += _onFileSystemChanged;
        PreferencesStore.AppearanceChanged += _onAppearanceChanged;
        PreferencesStore.IconLayoutChanged += _onRegistryChanged;
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
            VirtualFileSystemService.Changed -= _onFileSystemChanged;
            PreferencesStore.AppearanceChanged -= _onAppearanceChanged;
            PreferencesStore.IconLayoutChanged -= _onRegistryChanged;
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
                var descriptorsById = new Dictionary<string, DesktopAppDescriptor>(StringComparer.Ordinal);
                var registeredIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (DesktopAppDescriptor descriptor in descriptors)
                {
                    registeredIds.Add(descriptor.Id);
                    descriptorsById.Add(descriptor.Id, descriptor);
                }

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

                var layout = new DesktopIconLayout(PreferencesStore.IconSize);
                IReadOnlyList<VirtualFileSystemNode> desktopNodes =
                    DesktopIconLayout.Arrange(VirtualFileSystemService.GetChildren(VirtualFileSystem.DesktopId), PreferencesStore.IconOrder);
                desktopIconsRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(layout.ContentWidth(desktopNodes.Count), 0f);
                _iconScrollbar.SetActive(layout.ContentWidth(desktopNodes.Count) > DesktopIconLayout.DesktopWidth);
                _iconScroll.StopMovement();
                _iconScroll.horizontalNormalizedPosition = 0f;
                for (int index = 0; index < desktopNodes.Count; index++)
                {
                    VirtualFileSystemNode node = desktopNodes[index];
                    DesktopAppDescriptor? descriptor = null;
                    if (!string.IsNullOrEmpty(node.TargetId))
                        descriptorsById.TryGetValue(node.TargetId, out descriptor);
                    string nodeId = node.Id;
                    CreateDesktopIcon(
                        desktopIconsRoot.transform,
                        node,
                        descriptor,
                        new Vector2(layout.X(index), layout.Y(index)),
                        layout,
                        () => OpenNode(nodeId));
                }

                if (_startMenu != null)
                    UnityEngine.Object.Destroy(_startMenu);
                _startMenu = BuildStartMenu(descriptors);
                GameObject startMenu = _startMenu;
                SetStartMenuOpen(false);
                // Root-level ordering is intentional: icons < windows < taskbar < Start menu.
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

    private void OpenNode(string nodeId)
    {
        if (!VirtualFileSystemService.TryGetNode(nodeId, out VirtualFileSystemNode node))
            return;
        if (node.Kind == VirtualFileSystemNodeKind.Directory)
        {
            OpenFolder(node.Id);
            return;
        }
        if (string.IsNullOrEmpty(node.TargetId) || !DesktopAppRegistry.TryGet(node.TargetId, out _))
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Desktop shortcut '{node.Name}' targets an unavailable app '{node.TargetId}'.");
            return;
        }

        OpenApp(node.TargetId);
    }

    private void OpenFolder(string directoryId)
    {
        OpenApp(Constants.FilesAppId);
        _windows.OpenDirectory(Constants.FilesAppId, directoryId);
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

        float listHeight = Mathf.Min(288f, Mathf.Max(36f, programCount * 36f));
        float menuHeight = 140f + listHeight;
        GameObject menu = UiFactory.CreatePanel(
            _root.transform,
            "StartMenu",
            UiFactory.SurfaceRaised);
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0f, 0f);
        menuRect.anchorMax = new Vector2(0f, 0f);
        menuRect.pivot = new Vector2(0f, 0f);
        menuRect.sizeDelta = new Vector2(246f, menuHeight);
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

        GameObject viewport = UiFactory.CreatePanel(menu.transform, "ProgramsViewport", Color.clear);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 1f);
        viewportRect.anchorMax = Vector2.one;
        viewportRect.pivot = new Vector2(0.5f, 1f);
        viewportRect.sizeDelta = new Vector2(-32f, listHeight);
        viewportRect.anchoredPosition = new Vector2(-4f, -48f);
        viewport.AddComponent<RectMask2D>();
        GameObject content = new("Programs");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, programCount * 36f);
        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        scroll.inertia = false;

        GameObject track = UiFactory.CreatePanel(menu.transform, "ProgramsScrollbar", UiFactory.SurfaceInset);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = Vector2.one;
        trackRect.anchorMax = Vector2.one;
        trackRect.pivot = Vector2.one;
        trackRect.sizeDelta = new Vector2(12f, listHeight);
        trackRect.anchoredPosition = new Vector2(-8f, -48f);
        GameObject thumb = UiFactory.CreatePanel(track.transform, "Handle", UiFactory.SurfaceRaised);
        UiFactory.Stretch(thumb.GetComponent<RectTransform>(), Vector2.zero);
        var scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.handleRect = thumb.GetComponent<RectTransform>();
        scrollbar.targetGraphic = thumb.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        int programIndex = 0;
        for (int index = 0; index < descriptors.Count; index++)
        {
            DesktopAppDescriptor descriptor = descriptors[index];
            if (string.Equals(descriptor.Id, Constants.SettingsAppId, StringComparison.Ordinal))
                continue;
            Button entry = CreateStartEntry(
                content.transform,
                descriptor.Title,
                programCount * 36f - 18f - (programIndex++ * 36f),
                () => OpenApp(descriptor.Id),
                reserveIconSpace: true);
            AddStartEntryIcon(entry.transform, descriptor);
            entry.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
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
#if IL2CPPMELON
        buttonLabel.textWrappingMode = Il2CppTMPro.TextWrappingModes.NoWrap;
        buttonLabel.overflowMode = Il2CppTMPro.TextOverflowModes.Ellipsis;
#else
        buttonLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        buttonLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
#endif
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

    private static void AddStartButtonIcon(Transform parent)
    {
        GameObject iconObject = new("Icon");
        iconObject.transform.SetParent(parent, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        UiFactory.SetRect(
            iconRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(28f, 28f),
            Vector2.zero);
        Image image = iconObject.AddComponent<Image>();
        image.sprite = RuntimeAppIcons.Get(BuiltInIcon.Start);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void CreateDesktopIcon(
        Transform parent,
        VirtualFileSystemNode node,
        DesktopAppDescriptor? descriptor,
        Vector2 position,
        DesktopIconLayout layout,
        Action action)
    {
        GameObject iconObject = UiFactory.CreatePanel(
            parent,
            $"DesktopIcon_{node.Id}",
            Color.clear);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = new Vector2(layout.CellWidth - 8f, layout.CellHeight - 4f);
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
        iconFrameRect.sizeDelta = new Vector2(layout.IconSize, layout.IconSize);
        iconFrameRect.anchoredPosition = new Vector2(0f, -4f);

        Sprite icon = node.Kind == VirtualFileSystemNodeKind.Directory
            ? RuntimeAppIcons.Get(BuiltInIcon.Folder)
            : descriptor != null
                ? RuntimeAppIcons.Resolve(descriptor)
                : RuntimeAppIcons.Get(BuiltInIcon.Generic);

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
            descriptor == null && node.Kind == VirtualFileSystemNodeKind.AppShortcut
                ? node.Name + " (?)"
                : node.Name,
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
        caption.rectTransform.sizeDelta = new Vector2(-4f, 30f);
        caption.rectTransform.anchoredPosition = new Vector2(0f, 0f);
#if IL2CPPMELON
        caption.overflowMode = Il2CppTMPro.TextOverflowModes.Ellipsis;
#else
        caption.overflowMode = TMPro.TextOverflowModes.Ellipsis;
#endif

        _surfaceListeners.Add(action, button.onClick);
    }

    private void ToggleStartMenu()
    {
        if (_startMenu != null)
            SetStartMenuOpen(!_startMenu.activeSelf);
    }

    private void HideStartMenu()
    {
        if (_startMenu != null)
            SetStartMenuOpen(false);
    }

    private void SetStartMenuOpen(bool open)
    {
        _startMenu?.SetActive(open);
        HideStartTooltip();

        Color normal = open
            ? Color.Lerp(UiFactory.StartGreen, Color.black, 0.22f)
            : UiFactory.StartGreen;
        ColorBlock colors = _startButton.colors;
        colors.normalColor = normal;
        colors.selectedColor = normal;
        colors.highlightedColor = Color.Lerp(normal, Color.white, 0.16f);
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.14f);
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.45f);
        _startButton.colors = colors;
        _startButton.targetGraphic.color = normal;
    }

    private void ShowStartTooltip()
    {
        if (_startMenu?.activeSelf != true)
            _startTooltip.SetActive(true);
    }

    private void HideStartTooltip() => _startTooltip.SetActive(false);

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
        _desktopIconsRoot.transform.SetParent(_iconViewport, false);
        RectTransform desktopIconsRect = _desktopIconsRoot.AddComponent<RectTransform>();
        desktopIconsRect.anchorMin = Vector2.zero;
        desktopIconsRect.anchorMax = new Vector2(0f, 1f);
        desktopIconsRect.pivot = new Vector2(0f, 1f);
        desktopIconsRect.sizeDelta = new Vector2(DesktopIconLayout.DesktopWidth, 0f);
        _iconScroll.content = desktopIconsRect;
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

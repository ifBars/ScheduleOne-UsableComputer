using System;
using ManagedDoom;
using ManagedDoom.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UsableComputer.API;
using UsableComputer.Doom;
using UiButton = UnityEngine.UI.Button;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class DoomApp : IDesktopAppSession, IDesktopAppVisibilitySession
{
    private const float TicInterval = 1f / 35f;
    private readonly DesktopAppContext _context;
    private readonly GameObject _root;
    private GameContent? _content;
    private ManagedDoom.Doom? _doom;
    private UnityDoomVideo? _video;
    private UnityDoomInput? _input;
    private S1Text? _status;
    private float _accumulator;
    private bool _disposed;
    private bool _completed;

    internal DoomApp(DesktopAppContext context)
    {
        _context = context;
        _root = new GameObject("DoomAppRoot");
        _root.transform.SetParent(context.Container, false);
        RectTransform rootRect = _root.AddComponent<RectTransform>();
        UiFactory.Stretch(rootRect, Vector2.zero);
        Build();
    }

    internal DoomState? CurrentState => _doom?.State;

    public void OnOpened()
    {
    }

    public void OnClosed() => SetFocused(false);

    public void OnVisibilityChanged(bool visible)
    {
        if (!visible)
            SetFocused(false);
    }

    public void OnTick()
    {
        if (_disposed || _doom == null || _video == null || _input == null || _completed)
            return;

        Keyboard? keyboard = Keyboard.current;
        if (keyboard?.f10Key.wasPressedThisFrame == true)
            SetFocused(false);

        _input.PostEvents(_doom);
        _accumulator = Mathf.Min(_accumulator + Time.unscaledDeltaTime, TicInterval * 4f);
        bool updated = false;
        while (_accumulator >= TicInterval)
        {
            _accumulator -= TicInterval;
            updated = true;
            if (_doom.Update() != UpdateResult.Completed)
                continue;

            _completed = true;
            SetFocused(false);
            if (_status != null)
                _status.text = "Doom exited. Close and reopen the app to restart.";
            break;
        }

        if (updated && !_completed)
            _video.Render(_doom);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        SetFocused(false);
        _doom = null;
        _input = null;
        _video?.Dispose();
        _video = null;
        _content?.Dispose();
        _content = null;
    }

    internal void InjectKey(DoomKey key)
    {
        if (_doom == null)
            return;

        _doom.PostEvent(new DoomEvent(EventType.KeyDown, key));
        _doom.PostEvent(new DoomEvent(EventType.KeyUp, key));
    }

    private void Build()
    {
        string? wadPath = DoomWadLocator.FindIwad();
        if (wadPath == null)
        {
            BuildMissingWadScreen();
            return;
        }

        try
        {
            StartDoom(wadPath);
        }
        catch (Exception exception)
        {
            DisposeEngine();
            BuildErrorScreen(exception.Message);
            MelonLoader.MelonLogger.Error(
                $"[{Constants.ModName}] Doom could not start with '{wadPath}': {exception}");
        }
    }

    private void StartDoom(string wadPath)
    {
        var args = new CommandLineArgs(new[] { "-iwad", wadPath, "-nosound", "-nomusic" });
        var config = new Config
        {
            video_highresolution = true,
            video_fullscreen = false,
            video_gamescreensize = 7,
        };
        _content = new GameContent(args);
        _video = new UnityDoomVideo(config, _content);
        _input = new UnityDoomInput(config);
        _doom = new ManagedDoom.Doom(
            args,
            config,
            _content,
            _video,
            NullSound.GetInstance(),
            NullMusic.GetInstance(),
            _input);

        var display = new GameObject("Display");
        display.transform.SetParent(_root.transform, false);
        RectTransform displayRect = display.AddComponent<RectTransform>();
        displayRect.anchorMin = new Vector2(0.5f, 0.5f);
        displayRect.anchorMax = new Vector2(0.5f, 0.5f);
        displayRect.pivot = new Vector2(0.5f, 0.5f);
        displayRect.sizeDelta = new Vector2(400f, 640f);
        displayRect.anchoredPosition = new Vector2(0f, 15f);
        displayRect.localEulerAngles = new Vector3(0f, 0f, -90f);

        var image = display.AddComponent<RawImage>();
        image.texture = _video.Texture;
        image.color = Color.white;
        image.raycastTarget = true;

        var trigger = display.AddComponent<EventTrigger>();
        _context.Listeners.AddTrigger(trigger, EventTriggerType.PointerClick, _ => SetFocused(true));

        _status = UiFactory.CreateText(
            _root.transform,
            "Controls",
            "Click the game to focus · WASD move · arrows turn · Ctrl/click fire · Space/right-click use · F10 release",
            12f,
            UiFactory.TextMuted,
            GetCenterAlignment());
        RectTransform statusRect = _status.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.sizeDelta = new Vector2(-12f, 28f);
        statusRect.anchoredPosition = new Vector2(0f, 1f);
    }

    private void BuildMissingWadScreen()
    {
        S1Text heading = UiFactory.CreateText(
            _root.transform,
            "Heading",
            "Doom needs an IWAD",
            22f,
            UiFactory.TextPrimary,
            GetCenterAlignment(),
            bold: true);
        SetMessageRect(heading.rectTransform, -82f, 42f);

        S1Text body = UiFactory.CreateText(
            _root.transform,
            "Instructions",
            "Copy a Doom IWAD you own, or a compatible free IWAD such as Freedoom, into:\n" +
            DoomWadLocator.WadDirectory +
            "\n\nRecognized names: doom.wad, doom1.wad, doom2.wad, freedoom1.wad, freedoom2.wad",
            15f,
            UiFactory.TextMuted,
            GetCenterAlignment());
        SetMessageRect(body.rectTransform, -138f, 120f);

        UiButton retry = UiFactory.CreateButton(
            _root.transform,
            "Retry",
            "Rescan",
            UiFactory.Accent,
            out _);
        RectTransform retryRect = retry.GetComponent<RectTransform>();
        UiFactory.SetRect(
            retryRect,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(120f, 38f),
            new Vector2(0f, -105f));
        _context.Bind(retry, Rebuild);
    }

    private void BuildErrorScreen(string message)
    {
        S1Text heading = UiFactory.CreateText(
            _root.transform,
            "ErrorHeading",
            "Doom could not start",
            22f,
            UiFactory.Danger,
            GetCenterAlignment(),
            bold: true);
        SetMessageRect(heading.rectTransform, -82f, 42f);

        S1Text body = UiFactory.CreateText(
            _root.transform,
            "Error",
            message + "\n\nCheck that the file is a valid Doom-compatible IWAD.",
            15f,
            UiFactory.TextMuted,
            GetCenterAlignment());
        SetMessageRect(body.rectTransform, -138f, 110f);
    }

    private void Rebuild()
    {
        DisposeEngine();
        for (int index = _root.transform.childCount - 1; index >= 0; index--)
            UnityEngine.Object.Destroy(_root.transform.GetChild(index).gameObject);
        Build();
    }

    private void DisposeEngine()
    {
        SetFocused(false);
        _doom = null;
        _input = null;
        _video?.Dispose();
        _video = null;
        _content?.Dispose();
        _content = null;
    }

    private void SetFocused(bool focused)
    {
        _video?.SetFocused(focused);
        _input?.SetFocused(focused);
        _context.SetTyping(focused);
        if (_status != null && !_completed)
        {
            _status.text = focused
                ? "DOOM input active · F10 releases keyboard and mouse"
                : "Click the game to focus · WASD move · arrows turn · Ctrl/click fire · Space/right-click use · F10 release";
        }
    }

    private static void SetMessageRect(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-40f, height);
        rect.anchoredPosition = new Vector2(0f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetCenterAlignment() => Il2CppTMPro.TextAlignmentOptions.Center;
#else
    private static TMPro.TextAlignmentOptions GetCenterAlignment() => TMPro.TextAlignmentOptions.Center;
#endif
}

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UsableComputer.API;
using UsableComputer.NestedGame;
using UiButton = UnityEngine.UI.Button;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class NestedGameApp : IDesktopAppSession, IDesktopAppVisibilitySession
{
    private const int F10 = 0x79;
    private readonly DesktopAppContext _context;
    private readonly GameObject _root;
    private NestedGameProcess? _nestedGame;
    private Texture2D? _texture;
    private RawImage? _display;
    private S1Text? _status;
    private bool _focused;
    private bool _f10WasDown;
    private bool _previousRunInBackground;
    private bool _disposed;

    internal NestedGameApp(DesktopAppContext context)
    {
        _context = context;
        _root = new GameObject("NestedGameAppRoot");
        _root.transform.SetParent(context.Container, false);
        RectTransform rootRect = _root.AddComponent<RectTransform>();
        UiFactory.Stretch(rootRect, Vector2.zero);
        BuildStartScreen();
    }

    internal bool HasFrame => _nestedGame?.HasFrame == true;
    internal bool HasVisibleFrame => _nestedGame?.HasVisibleFrame == true;

    public void OnOpened()
    {
    }

    public void OnClosed() => ReleaseFocus();

    public void OnVisibilityChanged(bool visible)
    {
        if (!visible)
            ReleaseFocus();
    }

    public void OnTick()
    {
        if (_disposed || _nestedGame == null)
            return;

        bool f10Down = (GetAsyncKeyState(F10) & 0x8000) != 0;
        if (_focused && f10Down && !_f10WasDown)
            ReleaseFocus();
        _f10WasDown = f10Down;

        if (_nestedGame.TryReadFrame(out byte[] frame))
            UpdateFrame(frame);
        if (_status != null)
            _status.text = _nestedGame.Status;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopGame();
    }

    internal void StartForTesting() => StartGame();

    private void BuildStartScreen()
    {
        ClearUi();
        S1Text heading = UiFactory.CreateText(
            _root.transform, "Heading", "Schedule I", 24f, UiFactory.TextPrimary, GetCenterAlignment(), bold: true);
        SetCenteredRect(heading.rectTransform, 76f, 44f);

        S1Text body = UiFactory.CreateText(
            _root.transform,
            "Description",
            "Run a real second instance of Schedule I inside this computer.\n\n" +
            "The nested game uses its own save profile and does not load your normal mods. " +
            "It needs additional CPU, GPU, and memory.",
            15f,
            UiFactory.TextMuted,
            GetCenterAlignment());
        SetCenteredRect(body.rectTransform, 4f, 112f);

        UiButton start = UiFactory.CreateButton(_root.transform, "Start", "Start Schedule I", UiFactory.StartGreen, out _);
        UiFactory.SetRect(
            start.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(180f, 40f), new Vector2(0f, -86f));
        _context.Bind(start, StartGame);
    }

    private void StartGame()
    {
        if (_nestedGame != null)
            return;
        try
        {
            _nestedGame = NestedGameProcess.Start();
            BuildRunningScreen();
        }
        catch (Exception exception)
        {
            _nestedGame?.Dispose();
            _nestedGame = null;
            MelonLoader.MelonLogger.Error($"[{Constants.ModName}] Nested Schedule I could not start: {exception}");
            BuildErrorScreen(exception.Message);
        }
    }

    private void BuildRunningScreen()
    {
        ClearUi();
        GameObject displayArea = UiFactory.CreatePanel(_root.transform, "DisplayArea", Color.black);
        RectTransform displayAreaRect = displayArea.GetComponent<RectTransform>();
        displayAreaRect.anchorMin = new Vector2(0f, 0f);
        displayAreaRect.anchorMax = Vector2.one;
        displayAreaRect.offsetMin = new Vector2(0f, 27f);
        displayAreaRect.offsetMax = Vector2.zero;

        var displayObject = new GameObject("NestedDisplay");
        displayObject.transform.SetParent(displayArea.transform, false);
        RectTransform displayRect = displayObject.AddComponent<RectTransform>();
        UiFactory.Stretch(displayRect, Vector2.zero);
        var aspect = displayObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;
        _display = displayObject.AddComponent<RawImage>();
        _display.color = Color.white;
        _display.raycastTarget = true;
        var trigger = displayObject.AddComponent<EventTrigger>();
        _context.Listeners.AddTrigger(trigger, EventTriggerType.PointerClick, _ => FocusGame());

        GameObject controls = UiFactory.CreatePanel(_root.transform, "Controls", UiFactory.SurfaceRaised);
        RectTransform controlsRect = controls.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(0f, 0f);
        controlsRect.anchorMax = new Vector2(1f, 0f);
        controlsRect.pivot = new Vector2(0.5f, 0f);
        controlsRect.sizeDelta = new Vector2(0f, 27f);
        controlsRect.anchoredPosition = Vector2.zero;

        _status = UiFactory.CreateText(
            controls.transform, "Status", "Starting Schedule I...", 11f, UiFactory.TextMuted, GetLeftAlignment());
        _status.rectTransform.anchorMin = Vector2.zero;
        _status.rectTransform.anchorMax = Vector2.one;
        _status.rectTransform.offsetMin = new Vector2(7f, 0f);
        _status.rectTransform.offsetMax = new Vector2(-68f, 0f);

        UiButton stop = UiFactory.CreateButton(controls.transform, "Stop", "Stop", UiFactory.Danger, out _);
        UiFactory.SetRect(
            stop.GetComponent<RectTransform>(),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(58f, 22f), new Vector2(-3f, 0f));
        _context.Bind(stop, StopAndReturn);
    }

    private void BuildErrorScreen(string message)
    {
        ClearUi();
        S1Text heading = UiFactory.CreateText(
            _root.transform, "ErrorHeading", "Schedule I could not start", 22f, UiFactory.Danger, GetCenterAlignment(), bold: true);
        SetCenteredRect(heading.rectTransform, 58f, 42f);
        S1Text error = UiFactory.CreateText(
            _root.transform, "Error", message, 14f, UiFactory.TextMuted, GetCenterAlignment());
        SetCenteredRect(error.rectTransform, -4f, 90f);
        UiButton retry = UiFactory.CreateButton(_root.transform, "Retry", "Try again", UiFactory.Accent, out _);
        UiFactory.SetRect(
            retry.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(130f, 38f), new Vector2(0f, -86f));
        _context.Bind(retry, StartGame);
    }

    private void UpdateFrame(byte[] frame)
    {
        if (_nestedGame == null || _display == null)
            return;
        if (_texture == null || _texture.width != _nestedGame.Width || _texture.height != _nestedGame.Height)
        {
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = new Texture2D(_nestedGame.Width, _nestedGame.Height, TextureFormat.RGBA32, mipChain: false)
            {
                name = "UsableComputer_NestedScheduleOneFrame",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _display.texture = _texture;
        }
        _texture.LoadRawTextureData(frame);
        _texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
    }

    private void FocusGame()
    {
        if (_nestedGame?.FocusChild() != true)
            return;
        if (!_focused)
        {
            _previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }
        _focused = true;
        _context.SetTyping(true);
        if (_status != null)
            _status.text = "Nested-game input active · press F10 to return to the desktop";
    }

    private void ReleaseFocus()
    {
        if (!_focused)
            return;
        _focused = false;
        _context.SetTyping(false);
        Application.runInBackground = _previousRunInBackground;
        _nestedGame?.ReleaseInput();
        NestedGameProcess.FocusParent();
    }

    private void StopAndReturn()
    {
        StopGame();
        BuildStartScreen();
    }

    private void StopGame()
    {
        ReleaseFocus();
        _nestedGame?.Dispose();
        _nestedGame = null;
        if (_texture != null)
            UnityEngine.Object.Destroy(_texture);
        _texture = null;
        _display = null;
        _status = null;
    }

    private void ClearUi()
    {
        for (int index = _root.transform.childCount - 1; index >= 0; index--)
            UnityEngine.Object.Destroy(_root.transform.GetChild(index).gameObject);
    }

    private static void SetCenteredRect(RectTransform rect, float y, float height)
    {
        UiFactory.SetRect(
            rect,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-50f, height), new Vector2(0f, y));
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetCenterAlignment() => Il2CppTMPro.TextAlignmentOptions.Center;
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
#else
    private static TMPro.TextAlignmentOptions GetCenterAlignment() => TMPro.TextAlignmentOptions.Center;
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
#endif

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}

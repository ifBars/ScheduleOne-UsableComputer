using System;
using System.Reflection;
using UsableComputer.API;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.InputSystem;

#if IL2CPPMELON
using NativeSnake = Il2CppScheduleOne.TV.Snake;
using NativeAudio = Il2CppScheduleOne.Audio.AudioSourceController;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using TextAlignment = Il2CppTMPro.TextAlignmentOptions;
#else
using NativeSnake = ScheduleOne.TV.Snake;
using NativeAudio = ScheduleOne.Audio.AudioSourceController;
using S1Text = TMPro.TextMeshProUGUI;
using TextAlignment = TMPro.TextAlignmentOptions;
#endif

namespace UsableComputer.Apps.Games.Noodle;

internal sealed class NoodleApp : IDesktopAppSession
{
    private readonly DesktopAppContext _context;
    private readonly GameObject _host;
    private readonly RectTransform _viewport;
    private readonly S1Text _status;
    private NativeSnake? _game;
    private MethodInfo? _start;
    private MethodInfo? _move;
    private MethodInfo? _direction;
    private RectTransform? _gameRect;
    private Vector2 _nativeSize;
    private bool _disposed;

    internal NoodleApp(DesktopAppContext context)
    {
        _context = context;
        _host = UiFactory.CreatePanel(context.Container, "NoodleHost", Color.black);
        _viewport = _host.GetComponent<RectTransform>();
        _host.transform.SetParent(context.Container, false);
        UiFactory.Stretch(_viewport, Vector2.zero);
        _viewport.offsetMin = new Vector2(8f, 72f);
        _viewport.offsetMax = new Vector2(-8f, -8f);
        _host.AddComponent<UnityEngine.UI.RectMask2D>();
        _host.SetActive(false);
        _status = UiFactory.CreateText(context.Container, "NoodleStatus", "Use arrow keys or the direction buttons.",
            14f, UiFactory.TextPrimary, TextAlignment.Center);
        _status.richText = false;
        _status.rectTransform.anchorMin = new Vector2(0f, 0f);
        _status.rectTransform.anchorMax = new Vector2(1f, 0f);
        _status.rectTransform.pivot = new Vector2(0.5f, 0f);
        _status.rectTransform.sizeDelta = new Vector2(0f, 24f);
        _status.rectTransform.anchoredPosition = new Vector2(0f, 42f);
        AddButton("Left", 0, Vector2.left);
        AddButton("Up", 1, Vector2.up);
        AddButton("Down", 2, Vector2.down);
        AddButton("Right", 3, Vector2.right);
    }

    private void AddButton(string label, int index, Vector2 direction)
    {
        var button = UiFactory.CreateButton(_context.Container, "Noodle" + label, label, UiFactory.Accent, out _);
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(72f, 30f);
        rect.anchoredPosition = new Vector2((index - 1.5f) * 78f, 6f);
        _context.Bind(button, () => Steer(direction));
    }

    public void OnOpened()
    {
        try
        {
            NativeSnake source = NoodleNativeAdapter.FindSource()
                ?? throw new InvalidOperationException("Noodle is unavailable in this game version.");
            _start = NoodleNativeAdapter.Method("StartGame");
            _move = NoodleNativeAdapter.Method("UpdateMovement");
            _direction = NoodleNativeAdapter.DirectionSetter();
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, _host.transform, false);
            clone.name = "UsableComputer_Noodle";
            _game = clone.GetComponent<NativeSnake>();
            // The desktop steps native movement itself. Never open the TV or push its UI state.
            _game.enabled = false;
            _game.PreviousScreen = null;
            _host.SetActive(true);
            clone.SetActive(true);
            // Prefab canvas roots can have zero size until attached to a TV. Host the native
            // playfield directly so its tile grid has a defined size independent of that TV.
            _gameRect = _game.PlaySpace;
            _nativeSize = new Vector2(_game.Tiles[0].RectTransform.rect.width * 20f,
                _game.Tiles[0].RectTransform.rect.height * 12f);
            if (_nativeSize.x <= 0f || _nativeSize.y <= 0f)
                throw new InvalidOperationException("The native Noodle tile layout is unavailable.");
            _gameRect.SetParent(_host.transform, false);
            _gameRect.anchorMin = _gameRect.anchorMax = new Vector2(0.5f, 0.5f);
            _gameRect.pivot = new Vector2(0.5f, 0.5f);
            _gameRect.sizeDelta = _nativeSize;
            _gameRect.localRotation = Quaternion.identity;
            var board = _gameRect.GetComponent<UnityEngine.UI.Image>() ?? _gameRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            board.color = new Color(0.06f, 0.08f, 0.11f, 1f);
            board.raycastTarget = false;
            foreach (var tile in _game.Tiles)
            {
                tile.RectTransform.anchorMin = tile.RectTransform.anchorMax = Vector2.zero;
                tile.RectTransform.pivot = new Vector2(0.5f, 0.5f);
                tile.RectTransform.localScale = Vector3.one;
                tile.RectTransform.localRotation = Quaternion.identity;
                tile.SetPosition(tile.Position, _nativeSize.x / 20f);
            }
            _gameRect.gameObject.SetActive(true);
            foreach (NativeAudio sound in clone.GetComponentsInChildren<NativeAudio>(true))
            {
                sound.transform.SetParent(_host.transform, false);
                sound.gameObject.SetActive(true);
            }
            clone.SetActive(false);
            UiFactory.SetLayerRecursively(_host, Constants.UiLayer);
            Fit();
        }
        catch (Exception error)
        {
            ShowFailure(error);
        }
    }

    private void Fit()
    {
        if (_gameRect == null || _nativeSize.x <= 0f || _nativeSize.y <= 0f) return;
        Vector2 available = _viewport.rect.size - Vector2.one * 4f;
        float scale = Mathf.Max(0.01f, Mathf.Min(available.x / _nativeSize.x, available.y / _nativeSize.y));
        _gameRect.localScale = Vector3.one * scale;
        _gameRect.anchoredPosition3D = Vector3.zero;
    }

    private void Steer(Vector2 direction)
    {
        if (_game == null || !_context.IsFocused()) return;
        try
        {
            if (_game.GameState == NativeSnake.EGameState.Ready)
                _start!.Invoke(_game, new object[] { direction });
            else if (direction != -_game.Direction)
                _direction!.Invoke(_game, new object[] { direction });
        }
        catch (Exception error) { ShowFailure(error); }
    }

    public void OnTick()
    {
        if (_game == null || _disposed) return;
        try { TickGame(); }
        catch (Exception error) { ShowFailure(error); }
    }

    private void TickGame()
    {
        Fit();
        if (!_context.IsFocused() || !_context.Content.gameObject.activeInHierarchy) return;
        Keyboard? keyboard = Keyboard.current;
        if (keyboard?.leftArrowKey.wasPressedThisFrame == true) Steer(Vector2.left);
        else if (keyboard?.rightArrowKey.wasPressedThisFrame == true) Steer(Vector2.right);
        else if (keyboard?.upArrowKey.wasPressedThisFrame == true) Steer(Vector2.up);
        else if (keyboard?.downArrowKey.wasPressedThisFrame == true) Steer(Vector2.down);
        if (_game == null) return;
        _move!.Invoke(_game, null);
        _status.text = _game.GameState == NativeSnake.EGameState.Ready
            ? $"Score: {_game.Tail.Count}. Choose a direction to start."
            : $"Score: {_game.Tail.Count}. Arrow keys to turn; switching windows pauses.";
    }

    private void ShowFailure(Exception error)
    {
        _host.SetActive(false);
        _game = null;
        string message = error.GetBaseException().Message;
        _status.text = "Noodle unavailable: " + (message.Length > 180 ? message.Substring(0, 180) : message);
        MelonLoader.MelonLogger.Warning("[Usable Computer] " + _status.text);
    }

    public void OnClosed() => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _game = null;
        _host.SetActive(false);
        UnityEngine.Object.Destroy(_host);
    }
}

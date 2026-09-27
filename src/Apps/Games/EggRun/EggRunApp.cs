using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if IL2CPPMELON
using NativeRunner = Il2CppScheduleOne.TV.RunnerGame;
using NativeCharacter = Il2CppScheduleOne.TV.RunnerGameCharacter;
using NativeSpawner = Il2CppScheduleOne.UI.UISpawner;
using NativeMover = Il2CppScheduleOne.UI.UIMover;
using NativeGround = Il2CppScheduleOne.UI.SlidingRect;
using NativeFlipboard = Il2CppScheduleOne.UI.Flipboard;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using TextAlignment = Il2CppTMPro.TextAlignmentOptions;
#else
using NativeRunner = ScheduleOne.TV.RunnerGame;
using NativeCharacter = ScheduleOne.TV.RunnerGameCharacter;
using NativeSpawner = ScheduleOne.UI.UISpawner;
using NativeMover = ScheduleOne.UI.UIMover;
using NativeGround = ScheduleOne.UI.SlidingRect;
using NativeFlipboard = ScheduleOne.UI.Flipboard;
using S1Text = TMPro.TextMeshProUGUI;
using TextAlignment = TMPro.TextAlignmentOptions;
#endif

namespace UsableComputer.Apps.Games.EggRun;

internal sealed class EggRunApp : IDesktopAppSession, IDesktopAppVisibilitySession
{
    private readonly DesktopAppContext _context;
    private readonly GameObject _host;
    private readonly RectTransform _viewport;
    private readonly S1Text _status;
    private readonly List<NativeMover> _clouds = new();
    private readonly List<NativeMover> _obstacles = new();
    private readonly Dictionary<int, BoxCollider> _obstacleHitboxes = new();
    private RectTransform? _playfield;
    private RectTransform? _character;
    private CapsuleCollider? _characterCollider;
    private NativeFlipboard? _flipboard;
    private NativeGround? _ground;
    private NativeSpawner? _cloudSpawner;
    private NativeSpawner? _obstacleSpawner;
    private S1Text? _scoreLabel;
    private S1Text? _highScoreLabel;
    private GameObject? _startScreen;
    private GameObject? _gameOverScreen;
    private Sprite? _jumpSprite;
    private Vector2 _nativeSize;
    private float _characterY;
    private float _velocity;
    private float _speed;
    private float _score;
    private float _minSpeed;
    private float _maxSpeed;
    private float _speedIncrease;
    private float _gravity;
    private float _jumpForce;
    private float _forceMultiplier;
    private float _dropForce;
    private int _scoreRate;
    private int _bestScore;
    private int _baseCloudCount;
    private int _baseObstacleCount;
    private bool _ready = true;
    private bool _grounded = true;
    private bool _available;
    private bool _disposed;

    internal EggRunApp(DesktopAppContext context)
    {
        _context = context;
        _host = UiFactory.CreatePanel(context.Container, "EggRunHost", Color.black);
        _viewport = _host.GetComponent<RectTransform>();
        UiFactory.Stretch(_viewport, Vector2.zero);
        _viewport.offsetMin = new Vector2(8f, 54f);
        _viewport.offsetMax = new Vector2(-8f, -8f);
        _host.AddComponent<RectMask2D>();
        _host.SetActive(false);
        _status = UiFactory.CreateText(context.Container, "EggRunStatus",
            "Space or Up to start and jump. Down to drop.", 14f, UiFactory.TextPrimary, TextAlignment.Center);
        _status.richText = false;
        UiFactory.SetRect(_status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(0f, 28f));
        AddButton("Jump", -54f, JumpOrStart);
        AddButton("Drop", 54f, Drop);
    }

    private void AddButton(string label, float x, Action action)
    {
        Button button = UiFactory.CreateButton(_context.Container, "EggRun" + label, label, UiFactory.Accent, out _);
        UiFactory.SetRect(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(100f, 30f), new Vector2(x, 3f));
        _context.Bind(button, action);
    }

    public void OnOpened()
    {
        GameObject? staging = null;
        try
        {
            NativeRunner source = EggRunNativeAdapter.FindSource()
                ?? throw new InvalidOperationException("Egg Run's native TV prefab is unavailable.");
            _minSpeed = source.MinGameSpeed;
            _maxSpeed = source.MaxGameSpeed;
            _speedIncrease = source.SpeedIncreaseRate;
            _gravity = source.Gravity;
            _jumpForce = source.JumpForce;
            _forceMultiplier = source.GlobalForceMultiplier;
            _dropForce = source.DropForce;
            _scoreRate = source.ScoreRate;
            if (EggRunNativeAdapter.TryGetHighScore(out float savedScore, out bool canSaveScore))
            {
                _bestScore = Mathf.Max(0, (int)savedScore);
                if (!canSaveScore)
                    _status.text = "Space or Up to jump. Scores here are local until hosting a game.";
            }

            // The TV canvas is also the RunnerGame root. Keep that root inactive, move
            // only its child visuals, and destroy it before it can register TV listeners.
            staging = new GameObject("EggRunInactiveStaging");
            staging.SetActive(false);
            staging.transform.SetParent(_host.transform, false);
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, staging.transform, false);
            NativeRunner copied = clone.GetComponent<NativeRunner>()
                ?? throw new InvalidOperationException("The native Egg Run component was not cloned.");
            _character = copied.Character;
            _characterCollider = _character.GetComponent<CapsuleCollider>()
                ?? throw new InvalidOperationException("The native Egg Run character collider is unavailable.");
            _flipboard = copied.CharacterFlipboard;
            _ground = copied.Ground;
            _cloudSpawner = copied.CloudSpawner;
            _obstacleSpawner = copied.ObstacleSpawner;
            _scoreLabel = copied.ScoreLabel;
            _highScoreLabel = copied.HighScoreLabel;
            _startScreen = copied.StartScreen;
            _gameOverScreen = copied.GameOverScreen;
            _jumpSprite = copied.JumpSprite;
            _characterY = _character.anchoredPosition.y;
            RectTransform sourceRoot = source.Canvas.GetComponent<RectTransform>();
            _nativeSize = sourceRoot.rect.size;
            if (_nativeSize.x <= 0f || _nativeSize.y <= 0f)
                _nativeSize = sourceRoot.parent?.GetComponent<RectTransform>()?.rect.size ?? Vector2.zero;
            if (_nativeSize.x <= 0f || _nativeSize.y <= 0f)
                throw new InvalidOperationException("The native Egg Run canvas has no usable size.");

            GameObject field = UiFactory.CreatePanel(_host.transform, "EggRunPlayfield", Color.black);
            _playfield = field.GetComponent<RectTransform>();
            _playfield.anchorMin = _playfield.anchorMax = new Vector2(0.5f, 0.5f);
            _playfield.pivot = new Vector2(0.5f, 0.5f);
            _playfield.sizeDelta = _nativeSize;
            _playfield.anchoredPosition = Vector2.zero;
            while (clone.transform.childCount > 0)
                clone.transform.GetChild(0).SetParent(_playfield, false);

            foreach (Collider collider in field.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (NativeCharacter character in field.GetComponentsInChildren<NativeCharacter>(true))
                character.enabled = false;
            _baseCloudCount = _cloudSpawner.transform.childCount;
            _baseObstacleCount = _obstacleSpawner.transform.childCount;
            _context.Listeners.Add<GameObject>(cloud => TrackSpawn(cloud, _clouds), _cloudSpawner.OnSpawn);
            _context.Listeners.Add<GameObject>(obstacle => TrackSpawn(obstacle, _obstacles), _obstacleSpawner.OnSpawn);
            _startScreen.SetActive(true);
            _gameOverScreen.SetActive(false);
            _highScoreLabel.text = _bestScore.ToString("00000");
            _scoreLabel.text = "00000";
            SetSimulation(false);
            UnityEngine.Object.Destroy(staging);
            staging = null;
            UiFactory.SetLayerRecursively(_host, Constants.UiLayer);
            _host.SetActive(true);
            Fit();
            _available = true;
        }
        catch (Exception error)
        {
            if (staging != null) UnityEngine.Object.Destroy(staging);
            ShowFailure(error);
        }
    }

    private void Fit()
    {
        if (_playfield == null) return;
        Vector2 available = _viewport.rect.size - Vector2.one * 4f;
        float scale = Mathf.Max(0.01f, Mathf.Min(available.x / _nativeSize.x, available.y / _nativeSize.y));
        _playfield.localScale = Vector3.one * scale;
        _playfield.anchoredPosition3D = Vector3.zero;
    }

    private bool Active => !_disposed && _available && _host.activeInHierarchy &&
                           _playfield != null && _context.IsFocused() &&
                           _context.Content.gameObject.activeInHierarchy;

    private void JumpOrStart()
    {
        if (!Active) return;
        if (_ready)
        {
            StartGame();
            return;
        }
        if (!_grounded) return;
        _grounded = false;
        _velocity = _jumpForce * _forceMultiplier;
    }

    private void Drop()
    {
        if (Active && !_grounded)
            _velocity -= _dropForce * _forceMultiplier;
    }

    private void StartGame()
    {
        ClearSpawned();
        _score = 0f;
        _speed = _minSpeed;
        _velocity = 0f;
        _grounded = true;
        _ready = false;
        _character!.anchoredPosition = new Vector2(_character.anchoredPosition.x, _characterY);
        _flipboard!.SetIndex(0);
        _scoreLabel!.text = "00000";
        _startScreen!.SetActive(false);
        _gameOverScreen!.SetActive(false);
    }

    public void OnTick()
    {
        if (_disposed || !_available || _playfield == null) return;
        try
        {
            Fit();
            bool active = Active;
            SetSimulation(active && !_ready);
            if (!active) return;
            Keyboard? keyboard = Keyboard.current;
            if (keyboard?.spaceKey.wasPressedThisFrame == true ||
                keyboard?.upArrowKey.wasPressedThisFrame == true || keyboard?.wKey.wasPressedThisFrame == true)
                JumpOrStart();
            if (_ready) return;
            if (keyboard?.downArrowKey.isPressed == true || keyboard?.sKey.isPressed == true)
                _velocity -= _dropForce * _forceMultiplier * Time.deltaTime;
            TickGame(Time.deltaTime);
        }
        catch (Exception error) { ShowFailure(error); }
    }

    private void TickGame(float deltaTime)
    {
        _score += _scoreRate * deltaTime;
        _scoreLabel!.text = ((int)_score).ToString("00000");
        _speed = Mathf.Clamp(_speed + _speedIncrease * deltaTime, _minSpeed, _maxSpeed);
        _ground!.SpeedMultiplier = _speed;
        _cloudSpawner!.SpawnRateMultiplier = Mathf.Sqrt(_speed);
        _obstacleSpawner!.SpawnRateMultiplier = Mathf.Sqrt(_speed);
        _velocity -= _gravity * _forceMultiplier * deltaTime;
        float nextY = _character!.anchoredPosition.y + _velocity * deltaTime;
        if (nextY <= _characterY)
        {
            if (!_grounded) _flipboard!.SetIndex(0);
            nextY = _characterY;
            _velocity = 0f;
            _grounded = true;
        }
        _character.anchoredPosition = new Vector2(_character.anchoredPosition.x, nextY);
        _flipboard!.enabled = _grounded;
        _flipboard.SpeedMultiplier = _speed;
        if (!_grounded && nextY - _characterY > 10f && _jumpSprite != null)
            _flipboard.Image.sprite = _jumpSprite;

        CollectMovers(_cloudSpawner, _baseCloudCount, _clouds);
        CollectMovers(_obstacleSpawner, _baseObstacleCount, _obstacles);
        for (int index = _clouds.Count - 1; index >= 0; index--)
        {
            if (_clouds[index] == null) _clouds.RemoveAt(index);
            else _clouds[index].SpeedMultiplier = _speed;
        }
        Rect player = CapsuleBoundsInPlayfield(_characterCollider!);
        for (int index = _obstacles.Count - 1; index >= 0; index--)
        {
            NativeMover obstacle = _obstacles[index];
            if (obstacle == null) { _obstacles.RemoveAt(index); continue; }
            obstacle.SpeedMultiplier = _speed;
            if (obstacle.gameObject.activeInHierarchy &&
                _obstacleHitboxes.TryGetValue(obstacle.GetInstanceID(), out BoxCollider? hitbox) && hitbox != null &&
                player.Overlaps(BoxBoundsInPlayfield(hitbox)))
            {
                EndGame();
                break;
            }
        }
    }

    private void CollectMovers(NativeSpawner spawner, int baseCount, List<NativeMover> movers)
    {
        for (int index = baseCount; index < spawner.transform.childCount; index++)
        {
            Transform child = spawner.transform.GetChild(index);
            if (!child.gameObject.activeSelf) continue;
            TrackSpawn(child.gameObject, movers);
        }
    }

    private void TrackSpawn(GameObject spawned, List<NativeMover> movers)
    {
        if (spawned == null) return;
        NativeMover mover = spawned.GetComponent<NativeMover>();
        if (mover == null || movers.Contains(mover)) return;
        foreach (Collider collider in spawned.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (NativeCharacter character in spawned.GetComponentsInChildren<NativeCharacter>(true))
            character.enabled = false;
        mover.SpeedMultiplier = _speed;
        movers.Add(mover);
        if (ReferenceEquals(movers, _obstacles))
        {
            BoxCollider? hitbox = spawned.GetComponentInChildren<BoxCollider>(true);
            if (hitbox != null) _obstacleHitboxes[mover.GetInstanceID()] = hitbox;
        }
    }

    private Rect CapsuleBoundsInPlayfield(CapsuleCollider collider)
    {
        Vector3 halfSize = collider.direction == 1
            ? new Vector3(collider.radius, collider.height * 0.5f, 0f)
            : new Vector3(collider.height * 0.5f, collider.radius, 0f);
        return LocalHitboxBounds(collider.transform, collider.center, halfSize);
    }

    private Rect BoxBoundsInPlayfield(BoxCollider collider) =>
        LocalHitboxBounds(collider.transform, collider.center, collider.size * 0.5f);

    private Rect LocalHitboxBounds(Transform transform, Vector3 center, Vector3 halfSize)
    {
        Vector3 low = _playfield!.InverseTransformPoint(transform.TransformPoint(center - halfSize));
        Vector3 high = _playfield.InverseTransformPoint(transform.TransformPoint(center + halfSize));
        return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y),
            Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
    }

    private void SetSimulation(bool enabled)
    {
        if (_ground != null) _ground.enabled = enabled;
        if (_flipboard != null) _flipboard.enabled = enabled && _grounded;
        if (_cloudSpawner != null) _cloudSpawner.enabled = enabled;
        if (_obstacleSpawner != null) _obstacleSpawner.enabled = enabled;
        foreach (NativeMover mover in _clouds) if (mover != null) mover.enabled = enabled;
        foreach (NativeMover mover in _obstacles) if (mover != null) mover.enabled = enabled;
    }

    private void EndGame()
    {
        _ready = true;
        _speed = 0f;
        SetSimulation(false);
        int score = (int)_score;
        if (score > _bestScore) _bestScore = score;
        if (_score > 0f)
            EggRunNativeAdapter.TrySaveHighScore(_score);
        _highScoreLabel!.text = _bestScore.ToString("00000");
        _gameOverScreen!.SetActive(true);
    }

    private void ClearSpawned()
    {
        foreach (NativeMover mover in _clouds)
        {
            if (mover == null) continue;
            mover.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(mover.gameObject);
        }
        foreach (NativeMover mover in _obstacles)
        {
            if (mover == null) continue;
            mover.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(mover.gameObject);
        }
        _clouds.Clear();
        _obstacles.Clear();
        _obstacleHitboxes.Clear();
    }

    private void ShowFailure(Exception error)
    {
        _available = false;
        SetSimulation(false);
        _host.SetActive(false);
        string message = error.GetBaseException().Message;
        _status.text = "Egg Run unavailable: " + (message.Length > 180 ? message.Substring(0, 180) : message);
        MelonLoader.MelonLogger.Warning("[Usable Computer] " + _status.text);
    }

    public void OnClosed() => Dispose();

    public void OnVisibilityChanged(bool visible)
    {
        if (!visible) SetSimulation(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SetSimulation(false);
        _clouds.Clear();
        _obstacles.Clear();
        _obstacleHitboxes.Clear();
        _host.SetActive(false);
        UnityEngine.Object.Destroy(_host);
    }
}

using System;
using S1API.Utils;
using UsableComputer.UI;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPPMELON
using Il2CppInterop.Runtime;
using S1BuildableItem = Il2CppScheduleOne.EntityFramework.BuildableItem;
using S1ExitAction = Il2CppScheduleOne.ExitAction;
using S1ExitDelegate = Il2CppScheduleOne.GameInput.ExitDelegate;
using S1GameInput = Il2CppScheduleOne.GameInput;
using S1Interactable = Il2CppScheduleOne.Interaction.InteractableObject;
using S1MonoState = Il2CppScheduleOne.State.MonoState;
using S1PlayerCamera = Il2CppScheduleOne.PlayerScripts.PlayerCamera;
using S1CameraSingleton = Il2CppScheduleOne.DevUtilities.PlayerSingleton<Il2CppScheduleOne.PlayerScripts.PlayerCamera>;
using S1SceneState = Il2CppScheduleOne.SceneState;
using S1StateProperties = Il2CppScheduleOne.State.StateProperties;
#elif MONOMELON
using S1BuildableItem = ScheduleOne.EntityFramework.BuildableItem;
using S1ExitAction = ScheduleOne.ExitAction;
using S1ExitDelegate = ScheduleOne.GameInput.ExitDelegate;
using S1GameInput = ScheduleOne.GameInput;
using S1Interactable = ScheduleOne.Interaction.InteractableObject;
using S1MonoState = ScheduleOne.State.MonoState;
using S1PlayerCamera = ScheduleOne.PlayerScripts.PlayerCamera;
using S1CameraSingleton = ScheduleOne.DevUtilities.PlayerSingleton<ScheduleOne.PlayerScripts.PlayerCamera>;
using S1SceneState = ScheduleOne.SceneState;
using S1StateProperties = ScheduleOne.State.StateProperties;
#endif

namespace UsableComputer.Runtime;

internal sealed class UsableComputerController : IDisposable
{
    private readonly GameObject _builtObject;
    private readonly string _interactionToken;
    private S1Interactable? _interactable;
    private S1MonoState? _state;
    private GameObject? _stateHost;
    private Transform? _visual;
    private Transform? _cameraAnchor;
    private Transform? _screenAnchor;
    private DesktopShell? _desktop;
    private S1PlayerCamera? _playerCamera;
    private S1ExitDelegate? _exitDelegate;
    private Action? _openListener;
    private bool _ownsInteractable;
    private bool _listenerAttached;
    private bool _exitRegistered;
    private bool _statePushed;
    private bool _uiElementActive;
    private bool _cameraOverridden;
    private bool _fovOverridden;
    private bool _isOpen;
    private bool _isReady;
    private bool _disposed;
    private float _nextInitializationAttempt;
    private float _viewAspect;

    internal UsableComputerController(GameObject builtObject)
    {
        _builtObject = builtObject;
        _interactionToken = Constants.InteractionTokenPrefix + builtObject.GetInstanceID();
    }

    internal bool IsReady => _isReady;

    internal bool IsOpen => _isOpen;

    internal bool IsDisposed => _disposed;

    internal bool IsSame(GameObject builtObject) => !_disposed && _builtObject == builtObject;

    internal void Tick(float unscaledTime)
    {
        if (_disposed)
            return;

        if (_builtObject == null)
        {
            Dispose();
            return;
        }

        if (!_isReady)
        {
            if (unscaledTime < _nextInitializationAttempt)
                return;

            _nextInitializationAttempt = unscaledTime + 1f;
            TryInitialize();
        }

        if (_isOpen && _playerCamera != null && _playerCamera.Camera != null &&
            Math.Abs(_playerCamera.Camera.aspect - _viewAspect) > 0.001f)
            UpdateInteractionView(0f);

        _desktop?.Tick();
    }

    internal void TryInitialize()
    {
        if (_disposed || _isReady)
            return;

        try
        {
            _visual = FindVisual(_builtObject.transform);
            _cameraAnchor = RequireAnchor(_visual, Constants.CameraAnchorName);
            _screenAnchor = RequireAnchor(_visual, Constants.ScreenAnchorName);

            if (_visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException("The composed computer visual has no renderers.");

            _interactable = _visual.GetComponent<S1Interactable>();
            if (_interactable == null)
            {
                _interactable = _visual.gameObject.AddComponent<S1Interactable>();
                _ownsInteractable = true;
            }

            _interactable.MaxInteractionRange = Constants.InteractionRange;
            _interactable.RequiresUniqueClick = true;
            _interactable.Priority = 1;
            _interactable.displayLocationPoint = _screenAnchor;
            _interactable.SetInteractionType(S1Interactable.EInteractionType.Key_Press);
            _interactable.SetInteractableState(S1Interactable.EInteractableState.Default);
            _interactable.SetMessage(Constants.InteractionMessage);

            _openListener = Open;
            EventHelper.AddListener(_openListener, _interactable.onInteractStart);
            _listenerAttached = true;

            _stateHost = new GameObject(Constants.StateHostName);
            _stateHost.transform.SetParent(_builtObject.transform, false);
            _state = _stateHost.AddComponent<S1MonoState>();
            _state.Properties = S1StateProperties.UIDefault;

            S1SceneState? sceneState = S1SceneState.Current;
            if (sceneState == null || sceneState.InGame == null)
                throw new InvalidOperationException("The in-game state machine is not available yet.");

            _state.InitializeDefaultParent(sceneState.InGame);
            _desktop = new DesktopShell(_screenAnchor, null, Close);
            _isReady = true;
            MelonLoader.MelonLogger.Msg(
                $"[{Constants.ModName}] Attached native computer lifecycle to '{_builtObject.name}'.");
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Computer setup is waiting for native prerequisites: {exception.Message}");
            CleanupPartialInitialization();
        }
    }

    internal void Open()
    {
        if (_disposed || !_isReady || _isOpen || _cameraAnchor == null || _desktop == null)
            return;

        if (!S1CameraSingleton.InstanceExists)
            return;

        S1PlayerCamera? playerCamera = S1CameraSingleton.Instance;
        if (playerCamera == null || playerCamera.Camera == null)
            return;

        UsableComputerRuntime.CloseOther(this);
        _isOpen = true;
        _playerCamera = playerCamera;
        S1GameInput.IsTyping = false;

        try
        {
            _interactable?.SetInteractableState(S1Interactable.EInteractableState.Disabled);

            UpdateInteractionView(Constants.CameraTransitionSeconds);

            _playerCamera.AddActiveUIElement(_interactionToken);
            _uiElementActive = true;

            _state!.PushToDefaultParent();
            _statePushed = true;

#if IL2CPPMELON
            _exitDelegate = DelegateSupport.ConvertDelegate<S1ExitDelegate>(new Action<S1ExitAction>(OnExit));
#else
            _exitDelegate = new S1ExitDelegate(OnExit);
#endif
            S1GameInput.RegisterExitListener(_exitDelegate, 1);
            _exitRegistered = true;

            _desktop.SetEventCamera(_playerCamera.Camera);
            _desktop.Show();
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Error($"[{Constants.ModName}] Could not open computer: {exception}");
            Close();
        }
    }

    private void UpdateInteractionView(float transitionSeconds)
    {
        if (_playerCamera == null || _cameraAnchor == null || _screenAnchor == null)
            return;
        _viewAspect = _playerCamera.Camera.aspect;
        Quaternion rotation = Quaternion.LookRotation(_screenAnchor.position - _cameraAnchor.position, _screenAnchor.up);
        _playerCamera.OverrideTransform(_cameraAnchor.position, rotation, transitionSeconds);
        _cameraOverridden = true;
        _playerCamera.OverrideFOV(DisplayProfile.GetInteractionFieldOfView(_viewAspect), transitionSeconds);
        _fovOverridden = true;
    }

    internal void Close()
    {
        if (!_isOpen &&
            !_exitRegistered &&
            !_statePushed &&
            !_uiElementActive &&
            !_cameraOverridden &&
            !_fovOverridden)
            return;

        _isOpen = false;
        S1GameInput.IsTyping = false;
        _desktop?.Hide();

        if (_exitRegistered && _exitDelegate != null)
        {
            try
            {
                S1GameInput.DeregisterExitListener(_exitDelegate);
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Exit listener cleanup failed: {exception.Message}");
            }
        }

        _exitRegistered = false;
        _exitDelegate = null;

        if (_statePushed && _state != null)
        {
            try
            {
                if (_state.IsActive)
                    _state.PopFromDefaultParent();
                else
                    _state.RemoveFromDefaultParent();
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] State cleanup failed: {exception.Message}");
            }
        }

        _statePushed = false;

        if (_uiElementActive && _playerCamera != null)
        {
            try
            {
                _playerCamera.RemoveActiveUIElement(_interactionToken);
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Active UI cleanup failed: {exception.Message}");
            }
        }

        _uiElementActive = false;

        if (_fovOverridden && _playerCamera != null)
        {
            try
            {
                _playerCamera.StopFOVOverride(Constants.CameraTransitionSeconds);
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] FOV cleanup failed: {exception.Message}");
            }
        }

        _fovOverridden = false;

        if (_cameraOverridden && _playerCamera != null)
        {
            try
            {
                _playerCamera.StopTransformOverride(Constants.CameraTransitionSeconds);
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[{Constants.ModName}] Camera cleanup failed: {exception.Message}");
            }
        }

        _cameraOverridden = false;
        _playerCamera = null;
        _interactable?.SetInteractableState(S1Interactable.EInteractableState.Default);
        UsableComputerRuntime.NotifyClosed(this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Close();
        _disposed = true;
        _isReady = false;

        if (_listenerAttached && _openListener != null && _interactable != null)
        {
            try
            {
                EventHelper.RemoveListener(_openListener, _interactable.onInteractStart);
            }
            catch
            {
                // Unity may have destroyed the event source during scene teardown.
            }
        }

        _listenerAttached = false;
        _openListener = null;
        _desktop?.Dispose();
        _desktop = null;

        if (_stateHost != null)
            Object.Destroy(_stateHost);
        _stateHost = null;
        _state = null;

        if (_ownsInteractable && _interactable != null)
            Object.Destroy(_interactable);
        _interactable = null;
    }

    private void OnExit(S1ExitAction exitAction)
    {
        if (!_isOpen)
            return;

        exitAction.Use();
        Close();
    }

    private static Transform FindVisual(Transform builtRoot)
    {
        Transform? direct = builtRoot.Find("FurnitureVisual");
        if (direct != null)
            return direct;

        foreach (Transform candidate in builtRoot.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(candidate.name, "FurnitureVisual", StringComparison.Ordinal))
                return candidate;
        }

        throw new InvalidOperationException("Composed furniture is missing its FurnitureVisual root.");
    }

    private static Transform RequireAnchor(Transform visual, string name)
    {
        Transform? anchor = visual.Find(name);
        if (anchor == null)
        {
            foreach (Transform candidate in visual.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                {
                    anchor = candidate;
                    break;
                }
            }
        }

        return anchor ?? throw new InvalidOperationException($"Computer visual is missing anchor '{name}'.");
    }

    private void CleanupPartialInitialization()
    {
        if (_listenerAttached && _openListener != null && _interactable != null)
        {
            try
            {
                EventHelper.RemoveListener(_openListener, _interactable.onInteractStart);
            }
            catch
            {
            }
        }

        _listenerAttached = false;
        _openListener = null;
        _desktop?.Dispose();
        _desktop = null;

        if (_stateHost != null)
            Object.Destroy(_stateHost);
        _stateHost = null;
        _state = null;

        if (_ownsInteractable && _interactable != null)
            Object.Destroy(_interactable);
        _interactable = null;
        _ownsInteractable = false;
        _visual = null;
        _cameraAnchor = null;
        _screenAnchor = null;
    }
}

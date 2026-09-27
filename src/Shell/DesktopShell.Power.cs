using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UsableComputer.UI;

#if IL2CPPMELON
using S1GameInput = Il2CppScheduleOne.GameInput;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#else
using S1GameInput = ScheduleOne.GameInput;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.Shell;

internal sealed partial class DesktopShell
{
    private enum ComputerPowerState { Running, Off, Booting }

    private ComputerPowerState _powerState;
    private GameObject _powerScreen = null!;
    private S1Text _powerTitle = null!;
    private S1Text _powerStatus = null!;
    private Button _powerButton = null!;
    private float _bootStarted;
    private float _powerInputReady;
    private int _bootStage = -1;

    private void InitializePowerScreen()
    {
        _powerScreen = UiFactory.CreatePanel(_root.transform, "ComputerPowerScreen", new Color(0.015f, 0.02f, 0.035f));
        UiFactory.Stretch(_powerScreen.GetComponent<RectTransform>(), Vector2.zero);
        _powerTitle = UiFactory.CreateText(_powerScreen.transform, "PowerTitle", "", 30f, Color.white, GetCenterAlignment(), bold: true);
        PlacePowerElement(_powerTitle.rectTransform, 100f, 680f, 64f);
        _powerStatus = UiFactory.CreateText(_powerScreen.transform, "PowerStatus", "", 18f, new Color(0.65f, 0.73f, 0.82f), GetCenterAlignment());
        PlacePowerElement(_powerStatus.rectTransform, 0f, 680f, 120f);
        _powerButton = UiFactory.CreateButton(_powerScreen.transform, "ComputerPowerOn", "Power on", new Color(0.12f, 0.24f, 0.39f), out S1Text label);
        label.color = Color.white;
        PlacePowerElement(_powerButton.GetComponent<RectTransform>(), -112f, 180f, 44f);
        _listeners.Add(BeginBoot, _powerButton.onClick);
        _powerScreen.SetActive(false);
    }

    private static void PlacePowerElement(RectTransform rect, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void ShutdownComputer()
    {
        if (_disposed || _powerState != ComputerPowerState.Running) return;
        _powerState = ComputerPowerState.Off;
        HideStartMenu();
        HideStartTooltip();
        _files.ClosePopup();
        _files.CancelDrag();
        ClearEventSystemSelection();
        _windows.CloseAll();
        S1GameInput.IsTyping = false;
        _powerTitle.text = "Computer is off";
        _powerStatus.text = "Press Enter or click Power on to start.\nPress Escape to leave the desk.";
        _powerButton.gameObject.SetActive(true);
        _powerScreen.SetActive(true);
        _powerScreen.transform.SetAsLastSibling();
        _powerInputReady = Time.realtimeSinceStartup + 0.25f;
    }

    private void RestartComputer()
    {
        if (_disposed || _powerState != ComputerPowerState.Running) return;
        ShutdownComputer();
        BeginBoot();
    }

    private void BeginBoot()
    {
        if (_disposed || _powerState != ComputerPowerState.Off) return;
        _powerState = ComputerPowerState.Booting;
        _bootStarted = Time.realtimeSinceStartup;
        _bootStage = -1;
        _powerButton.gameObject.SetActive(false);
        ClearEventSystemSelection();
        TickPower();
    }

    private void TickPower()
    {
        if (_powerState == ComputerPowerState.Off)
        {
            if (_root.activeSelf && Time.realtimeSinceStartup >= _powerInputReady &&
                Keyboard.current?.enterKey.wasPressedThisFrame == true)
                BeginBoot();
            return;
        }
        if (_powerState != ComputerPowerState.Booting) return;
        float elapsed = Time.realtimeSinceStartup - _bootStarted;
        if (elapsed >= 4f)
        {
            _powerState = ComputerPowerState.Running;
            _powerScreen.SetActive(false);
            UpdateClock();
            return;
        }
        int stage = (int)elapsed;
        if (stage == _bootStage) return;
        _bootStage = stage;
        _powerTitle.text = stage < 2 ? "HYLAND SYSTEM BIOS" : "Starting Desktop OS";
        _powerStatus.text = stage switch
        {
            0 => "Display adapter . . . OK\nKeyboard controller . . . OK",
            1 => "System check complete\nBoot device: Desktop system volume",
            2 => "Connecting desktop services . . .\nOpening virtual filesystem . . .",
            _ => "Preparing your desktop . . .\nWelcome back."
        };
    }
}

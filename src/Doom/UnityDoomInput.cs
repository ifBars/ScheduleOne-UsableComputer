using System;
using ManagedDoom;
using ManagedDoom.UserInput;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UsableComputer.Doom;

internal sealed class UnityDoomInput : IUserInput
{
    private readonly Config _config;
    private bool _focused;
    private bool _mouseGrabbed;
    private int _turnHeld;

    internal UnityDoomInput(Config config)
    {
        _config = config;
    }

    internal void SetFocused(bool focused)
    {
        _focused = focused;
        if (!focused)
            ReleaseMouse();
    }

    internal void PostEvents(ManagedDoom.Doom doom)
    {
        Keyboard? keyboard = Keyboard.current;
        if (!_focused || keyboard == null)
            return;

        Post(doom, keyboard.aKey, DoomKey.A);
        Post(doom, keyboard.dKey, DoomKey.D);
        Post(doom, keyboard.nKey, DoomKey.N);
        Post(doom, keyboard.sKey, DoomKey.S);
        Post(doom, keyboard.wKey, DoomKey.W);
        Post(doom, keyboard.yKey, DoomKey.Y);
        Post(doom, keyboard.digit1Key, DoomKey.Num1);
        Post(doom, keyboard.digit2Key, DoomKey.Num2);
        Post(doom, keyboard.digit3Key, DoomKey.Num3);
        Post(doom, keyboard.digit4Key, DoomKey.Num4);
        Post(doom, keyboard.digit5Key, DoomKey.Num5);
        Post(doom, keyboard.digit6Key, DoomKey.Num6);
        Post(doom, keyboard.digit7Key, DoomKey.Num7);
        Post(doom, keyboard.escapeKey, DoomKey.Escape);
        Post(doom, keyboard.enterKey, DoomKey.Enter);
        Post(doom, keyboard.spaceKey, DoomKey.Space);
        Post(doom, keyboard.backspaceKey, DoomKey.Backspace);
        Post(doom, keyboard.tabKey, DoomKey.Tab);
        Post(doom, keyboard.leftArrowKey, DoomKey.Left);
        Post(doom, keyboard.rightArrowKey, DoomKey.Right);
        Post(doom, keyboard.upArrowKey, DoomKey.Up);
        Post(doom, keyboard.downArrowKey, DoomKey.Down);
        Post(doom, keyboard.leftCtrlKey, DoomKey.LControl);
        Post(doom, keyboard.rightCtrlKey, DoomKey.RControl);
        Post(doom, keyboard.leftShiftKey, DoomKey.LShift);
        Post(doom, keyboard.rightShiftKey, DoomKey.RShift);
        Post(doom, keyboard.leftAltKey, DoomKey.LAlt);
        Post(doom, keyboard.rightAltKey, DoomKey.RAlt);
        Post(doom, keyboard.f1Key, DoomKey.F1);
        Post(doom, keyboard.f2Key, DoomKey.F2);
        Post(doom, keyboard.f3Key, DoomKey.F3);
        Post(doom, keyboard.f4Key, DoomKey.F4);
        Post(doom, keyboard.f5Key, DoomKey.F5);
        Post(doom, keyboard.f6Key, DoomKey.F6);
        Post(doom, keyboard.f7Key, DoomKey.F7);
        Post(doom, keyboard.f8Key, DoomKey.F8);
        Post(doom, keyboard.f9Key, DoomKey.F9);
        Post(doom, keyboard.f11Key, DoomKey.F11);
        Post(doom, keyboard.f12Key, DoomKey.F12);
    }

    public void BuildTicCmd(TicCmd cmd)
    {
        cmd.Clear();
        Keyboard? keyboard = Keyboard.current;
        if (!_focused || keyboard == null)
            return;

        bool run = IsPressed(keyboard.leftShiftKey) || IsPressed(keyboard.rightShiftKey);
        int speed = _config.game_alwaysrun ? (run ? 0 : 1) : (run ? 1 : 0);
        int forward = 0;
        int side = 0;
        bool turnLeft = IsPressed(keyboard.leftArrowKey);
        bool turnRight = IsPressed(keyboard.rightArrowKey);

        _turnHeld = turnLeft || turnRight ? _turnHeld + 1 : 0;
        int turnSpeed = _turnHeld < PlayerBehavior.SlowTurnTics ? 2 : speed;
        if (turnRight)
            cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
        if (turnLeft)
            cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
        if (IsPressed(keyboard.wKey) || IsPressed(keyboard.upArrowKey))
            forward += PlayerBehavior.ForwardMove[speed];
        if (IsPressed(keyboard.sKey) || IsPressed(keyboard.downArrowKey))
            forward -= PlayerBehavior.ForwardMove[speed];
        if (IsPressed(keyboard.aKey))
            side -= PlayerBehavior.SideMove[speed];
        if (IsPressed(keyboard.dKey))
            side += PlayerBehavior.SideMove[speed];

        Mouse? mouse = Mouse.current;
        if (IsPressed(keyboard.leftCtrlKey) || IsPressed(keyboard.rightCtrlKey) ||
            (_mouseGrabbed && mouse?.leftButton.isPressed == true))
        {
            cmd.Buttons |= TicCmdButtons.Attack;
        }
        if (IsPressed(keyboard.spaceKey) || (_mouseGrabbed && mouse?.rightButton.isPressed == true))
            cmd.Buttons |= TicCmdButtons.Use;

        KeyControl[] weapons =
        {
            keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
            keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key,
        };
        for (int index = 0; index < weapons.Length; index++)
        {
            if (!IsPressed(weapons[index]))
                continue;

            cmd.Buttons |= TicCmdButtons.Change;
            cmd.Buttons |= (byte)(index << TicCmdButtons.WeaponShift);
            break;
        }

        if (_mouseGrabbed && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float scale = 0.5f * _config.mouse_sensitivity;
            cmd.AngleTurn -= (short)(Mathf.RoundToInt(scale * delta.x) * 8);
            if (!_config.mouse_disableyaxis)
                forward -= Mathf.RoundToInt(scale * delta.y);
        }

        cmd.ForwardMove = (sbyte)Math.Clamp(forward, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
        cmd.SideMove = (sbyte)Math.Clamp(side, -PlayerBehavior.MaxMove, PlayerBehavior.MaxMove);
    }

    public void Reset() => _turnHeld = 0;

    public void GrabMouse() => _mouseGrabbed = _focused;

    public void ReleaseMouse() => _mouseGrabbed = false;

    public int MaxMouseSensitivity => 15;

    public int MouseSensitivity
    {
        get => _config.mouse_sensitivity;
        set => _config.mouse_sensitivity = Math.Clamp(value, 0, MaxMouseSensitivity);
    }

    private static bool IsPressed(KeyControl key) => key.isPressed;

    private static void Post(ManagedDoom.Doom doom, KeyControl key, DoomKey doomKey)
    {
        if (key.wasPressedThisFrame)
            doom.PostEvent(new DoomEvent(EventType.KeyDown, doomKey));
        if (key.wasReleasedThisFrame)
            doom.PostEvent(new DoomEvent(EventType.KeyUp, doomKey));
    }
}

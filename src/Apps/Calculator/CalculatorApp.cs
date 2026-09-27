using System;
using UsableComputer.Logic;
using UsableComputer.API;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using S1GameInput = Il2CppScheduleOne.GameInput;
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1GameInput = ScheduleOne.GameInput;
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class CalculatorApp : IDesktopAppSession
{
    private readonly CalculatorModel _model = new();
    private S1Input? _display;
    private S1Text? _status;
    private bool _syncing;

    internal CalculatorApp(DesktopAppContext context)
    {
        Build(context.Container, context.Listeners);
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void OnTick()
    {
    }

    public void Dispose()
    {
    }

    internal void Build(Transform parent, UiListenerRegistry listeners)
    {
        S1Text heading = UiFactory.CreateText(
            parent,
            "Heading",
            "Calculator",
            21f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetHeadingRect(heading.rectTransform);

        _display = UiFactory.CreateInputField(
            parent,
            "Display",
            _model.DisplayText,
            "",
            multiline: false,
            out _);
        RectTransform displayRect = _display.GetComponent<RectTransform>();
        displayRect.anchorMin = new Vector2(0f, 1f);
        displayRect.anchorMax = new Vector2(1f, 1f);
        displayRect.pivot = new Vector2(0.5f, 1f);
        displayRect.sizeDelta = new Vector2(-24f, 52f);
        displayRect.anchoredPosition = new Vector2(0f, -48f);
        _display.contentType = S1Input.ContentType.Standard;
        _display.lineType = S1Input.LineType.SingleLine;

        _status = UiFactory.CreateText(
            parent,
            "Status",
            "Enter a number",
            13f,
            UiFactory.TextMuted,
            GetStatusAlignment());
        RectTransform statusRect = _status.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(-24f, 22f);
        statusRect.anchoredPosition = new Vector2(0f, -104f);

        listeners.Add<string>(value => OnDisplayChanged(value), _display.onValueChanged);
        var trigger = _display.gameObject.AddComponent<EventTrigger>();
        listeners.AddTrigger(trigger, EventTriggerType.Select, _ => S1GameInput.IsTyping = true);
        listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => S1GameInput.IsTyping = false);
        listeners.Add<string>(_ => S1GameInput.IsTyping = false, _display.onEndEdit);

        string[][] rows =
        {
            new[] { "7", "8", "9", "÷" },
            new[] { "4", "5", "6", "×" },
            new[] { "1", "2", "3", "−" },
            new[] { "0", ".", "C", "+" },
        };

        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < rows[row].Length; column++)
            {
                string token = rows[row][column];
                Button button = UiFactory.CreateButton(
                    parent,
                    $"Key_{token}_{row}_{column}",
                    token,
                    token == "C" ? new Color(0.34f, 0.19f, 0.22f, 1f) : UiFactory.SurfaceRaised,
                    out _);
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(0f, 1f);
                buttonRect.pivot = new Vector2(0f, 1f);
                buttonRect.sizeDelta = new Vector2(72f, 46f);
                buttonRect.anchoredPosition = new Vector2(12f + column * 78f, -134f - row * 51f);
                listeners.Add(() => Press(token), button.onClick);
            }
        }

        Button equalsButton = UiFactory.CreateButton(
            parent,
            "Key_Equals",
            "=",
            UiFactory.Accent,
            out _);
        RectTransform equalsRect = equalsButton.GetComponent<RectTransform>();
        equalsRect.anchorMin = new Vector2(0f, 1f);
        equalsRect.anchorMax = new Vector2(1f, 1f);
        equalsRect.pivot = new Vector2(0.5f, 1f);
        equalsRect.sizeDelta = new Vector2(-24f, 44f);
        equalsRect.anchoredPosition = new Vector2(0f, -342f);
        listeners.Add(PressEquals, equalsButton.onClick);
    }

    private void OnDisplayChanged(string value)
    {
        if (_syncing)
            return;

        _model.SetEntryText(value);
        UpdateDisplay();
    }

    private void Press(string token)
    {
        if (token == "C")
        {
            _model.Clear();
        }
        else if (token == ".")
        {
            _model.InputDecimal();
        }
        else if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
        {
            _model.InputDigit(token[0] - '0');
        }
        else if (TryGetOperator(token, out CalculatorOperator calculatorOperator))
        {
            _model.ApplyOperator(calculatorOperator);
        }

        UpdateDisplay();
    }

    private void PressEquals()
    {
        _model.PressEquals();
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_display == null || _status == null)
            return;

        _syncing = true;
        _display.text = _model.DisplayText;
        _syncing = false;
        _status.text = _model.ErrorMessage ?? "Enter a number";
        _status.color = _model.HasError ? UiFactory.Danger : UiFactory.TextMuted;
    }

    private static bool TryGetOperator(string token, out CalculatorOperator calculatorOperator)
    {
        calculatorOperator = token switch
        {
            "+" => CalculatorOperator.Add,
            "−" => CalculatorOperator.Subtract,
            "×" => CalculatorOperator.Multiply,
            "÷" => CalculatorOperator.Divide,
            _ => default,
        };
        return token is "+" or "−" or "×" or "÷";
    }

    private static void SetHeadingRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(-24f, 32f);
        rectTransform.anchoredPosition = new Vector2(0f, -10f);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetHeadingAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetStatusAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
#else
    private static TMPro.TextAlignmentOptions GetHeadingAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetStatusAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
#endif
}

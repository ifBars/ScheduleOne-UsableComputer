using System;
using MoonSharp.Interpreter;
using UsableComputer.API;
using UsableComputer.Scripting;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class LuaDesktopAppSession : IDesktopAppSession
{
    private const int MaximumRows = 64;
    private const int MaximumTextLength = 500;
    private readonly LuaAppDefinition _definition;
    private readonly RectTransform _content;
    private UiListenerRegistry _rowListeners = new();
    private float _nextRefresh;
    private bool _disposed;

    internal LuaDesktopAppSession(DesktopAppContext context, LuaAppDefinition definition)
    {
        _definition = definition;
        GameObject viewport = new("LuaViewport");
        viewport.transform.SetParent(context.Container, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        UiFactory.Stretch(viewportRect, new Vector2(8f, 8f));
        viewport.AddComponent<RectMask2D>();

        GameObject content = new("LuaContent");
        content.transform.SetParent(viewport.transform, false);
        _content = content.AddComponent<RectTransform>();
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = context.Container.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = _content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
    }

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    public void OnTick()
    {
        if (_disposed || Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _rowListeners.Dispose();
    }

    private void Refresh()
    {
        if (_disposed)
            return;

        ClearRows();
        try
        {
            DynValue result = LuaExecutionBudget.RunFunction(
                _definition.Script,
                _definition.Render,
                $"{_definition.Title} render");
            if (result.Type != DataType.Table)
                throw new ScriptRuntimeException("render must return an array of row tables.");

            float y = 0f;
            for (int index = 1; index <= MaximumRows; index++)
            {
                DynValue value = result.Table.Get(index);
                if (value.IsNil())
                    break;
                if (value.Type != DataType.Table)
                    throw new ScriptRuntimeException($"render row {index} must be a table.");
                y += BuildRow(value.Table, index, y);
            }

            _content.sizeDelta = new Vector2(0f, Mathf.Max(1f, y + 8f));
        }
        catch (Exception exception)
        {
            BuildError(exception is ScriptRuntimeException runtime
                ? runtime.DecoratedMessage ?? runtime.Message
                : exception.Message);
        }
    }

    private float BuildRow(Table row, int index, float y)
    {
        string kind = ReadString(row, "kind", "text", 20).ToLowerInvariant();
        string text = ReadString(row, "text", string.Empty, MaximumTextLength);
        switch (kind)
        {
            case "heading":
                CreateTextRow($"Heading_{index}", text, 22f, UiFactory.TextPrimary, y, 38f, bold: true);
                return 42f;
            case "stat":
                string label = ReadString(row, "label", string.Empty, 80);
                string value = ReadString(row, "value", string.Empty, 120);
                CreateStatRow(index, label, value, y);
                return 42f;
            case "spacer":
                return 16f;
            case "button":
                CreateButtonRow(index, text, row.Get("action"), y);
                return 42f;
            case "text":
                CreateTextRow($"Text_{index}", text, 15f, UiFactory.TextPrimary, y, 32f, bold: false);
                return 36f;
            default:
                throw new ScriptRuntimeException($"Unknown row kind '{kind}'.");
        }
    }

    private void CreateTextRow(
        string name,
        string text,
        float fontSize,
        Color color,
        float y,
        float height,
        bool bold)
    {
        S1Text label = UiFactory.CreateText(
            _content,
            name,
            text,
            fontSize,
            color,
            GetLeftAlignment(),
            bold);
        SetRowRect(label.rectTransform, y, height);
    }

    private void CreateStatRow(int index, string labelText, string valueText, float y)
    {
        GameObject panel = UiFactory.CreatePanel(_content, $"Stat_{index}", UiFactory.SurfaceRaised);
        SetRowRect(panel.GetComponent<RectTransform>(), y, 36f);
        S1Text label = UiFactory.CreateText(
            panel.transform,
            "Label",
            labelText,
            15f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        UiFactory.Stretch(label.rectTransform, new Vector2(10f, 3f));
        S1Text value = UiFactory.CreateText(
            panel.transform,
            "Value",
            valueText,
            17f,
            UiFactory.TextPrimary,
            GetRightAlignment(),
            bold: true);
        UiFactory.Stretch(value.rectTransform, new Vector2(10f, 3f));
    }

    private void CreateButtonRow(int index, string text, DynValue action, float y)
    {
        if (action.Type != DataType.Function && action.Type != DataType.ClrFunction)
            throw new ScriptRuntimeException($"button row {index} requires an action function.");

        Button button = UiFactory.CreateButton(
            _content,
            $"Button_{index}",
            text,
            UiFactory.Accent,
            out _);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(190f, 34f);
        rect.anchoredPosition = new Vector2(4f, -y);
        _rowListeners.Add(() => InvokeAction(action), button.onClick);
    }

    private void InvokeAction(DynValue action)
    {
        try
        {
            LuaExecutionBudget.RunFunction(_definition.Script, action, $"{_definition.Title} button action");
            Refresh();
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Lua app '{_definition.Id}' action failed: {exception.Message}");
        }
    }

    private void BuildError(string message)
    {
        S1Text error = UiFactory.CreateText(
            _content,
            "Error",
            $"App error\n{message}",
            15f,
            UiFactory.Danger,
            GetLeftAlignment(),
            bold: true);
        SetRowRect(error.rectTransform, 0f, 120f);
        _content.sizeDelta = new Vector2(0f, 128f);
    }

    private void ClearRows()
    {
        _rowListeners.Dispose();
        _rowListeners = new UiListenerRegistry();
        for (int index = _content.childCount - 1; index >= 0; index--)
        {
            Transform child = _content.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static string ReadString(Table table, string field, string fallback, int maxLength)
    {
        DynValue value = table.Get(field);
        if (value.IsNil())
            return fallback;
        if (value.Type != DataType.String)
            throw new ScriptRuntimeException($"{field} must be text.");
        string text = value.String ?? string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }

    private static void SetRowRect(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-8f, height);
        rect.anchoredPosition = new Vector2(0f, -y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetRightAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineRight;
#else
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetRightAlignment() => TMPro.TextAlignmentOptions.MidlineRight;
#endif
}

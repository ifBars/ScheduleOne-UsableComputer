using System;
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

internal sealed class NotesApp : IDesktopAppSession
{
    internal NotesApp(DesktopAppContext context)
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
            "Notes",
            21f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetHeadingRect(heading.rectTransform);

        S1Input input = UiFactory.CreateInputField(
            parent,
            "NoteInput",
            PreferencesStore.Note,
            "Write something worth remembering...",
            multiline: true,
            out _);
        RectTransform inputRect = input.GetComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(12f, 58f);
        inputRect.offsetMax = new Vector2(-12f, -48f);

        S1Text status = UiFactory.CreateText(
            parent,
            "Status",
            "Saved globally",
            13f,
            UiFactory.TextMuted,
            GetStatusAlignment());
        RectTransform statusRect = status.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(-136f, 32f);
        statusRect.anchoredPosition = new Vector2(-62f, 25f);

        Button saveButton = UiFactory.CreateButton(
            parent,
            "Save",
            "Save note",
            UiFactory.Accent,
            out _);
        RectTransform saveRect = saveButton.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(1f, 0f);
        saveRect.anchorMax = new Vector2(1f, 0f);
        saveRect.pivot = new Vector2(1f, 0.5f);
        saveRect.sizeDelta = new Vector2(112f, 32f);
        saveRect.anchoredPosition = new Vector2(-12f, 25f);

        listeners.Add(() =>
        {
            PreferencesStore.SetNote(input.text);
            status.text = "Saved globally";
        }, saveButton.onClick);

        var trigger = input.gameObject.AddComponent<EventTrigger>();
        listeners.AddTrigger(trigger, EventTriggerType.Select, _ => S1GameInput.IsTyping = true);
        listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => S1GameInput.IsTyping = false);
        listeners.Add<string>(
            _ => S1GameInput.IsTyping = false,
            input.onEndEdit);
        listeners.Add<string>(
            _ =>
            {
                if (input.isFocused)
                    S1GameInput.IsTyping = true;
            },
            input.onValueChanged);
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

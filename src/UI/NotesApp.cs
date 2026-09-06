using System;
using UsableComputer.API;
using UsableComputer.FileSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using S1Alignment = Il2CppTMPro.TextAlignmentOptions;
#else
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
using S1Alignment = TMPro.TextAlignmentOptions;
#endif

namespace UsableComputer.UI;

internal sealed class NotesApp : IDesktopAppSession
{
    private readonly DesktopAppContext _context;
    private readonly NotesDocument _document;
    private readonly S1Input _input;
    private readonly S1Input _path;
    private readonly S1Text _status;

    internal NotesApp(DesktopAppContext context)
    {
        _context = context;
        _document = VirtualFileSystemService.Notes;
        Transform parent = context.Container;
        AddButton("New", "New", 12f, 54f, () => Change(() => _document.New()));
        AddButton("Open", "Open", 72f, 54f, () => Change(_document.OpenPath));
        AddButton("Save", "Save", 132f, 54f, () => Save(false));
        AddButton("SaveAs", "Save as", 192f, 70f, () => Save(true));
        AddButton("Discard", "Discard", 268f, 66f, () => Change(() => _document.New(discard: true)));
        Button import = AddButton("ImportNote", "Import old note", 340f, 142f,
            () => Change(() => _document.Import(PreferencesStore.Note)));
        import.interactable = !string.IsNullOrEmpty(PreferencesStore.Note);

        _path = UiFactory.CreateInputField(parent, "NotePath", _document.Path,
            "/Desktop/Note.txt", false, out _);
        TopRect(_path.GetComponent<RectTransform>(), 12f, 46f, -24f, 34f, stretch: true);
        BindTyping(_path);
        context.Listeners.Add<string>(value => _document.Path = value, _path.onValueChanged);

        _input = UiFactory.CreateInputField(parent, "NoteInput", _document.Text,
            "Write something worth remembering...", true, out S1Text bodyText);
        bodyText.richText = false;
        RectTransform inputRect = _input.GetComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(12f, 54f);
        inputRect.offsetMax = new Vector2(-12f, -88f);
        BindTyping(_input);

        _status = UiFactory.CreateText(parent, "NoteStatus", string.Empty, 12f,
            UiFactory.TextMuted, S1Alignment.MidlineLeft);
        RectTransform statusRect = _status.rectTransform;
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.sizeDelta = new Vector2(-24f, 46f);
        statusRect.anchoredPosition = new Vector2(0f, 4f);
        _status.richText = false;
        context.Listeners.Add<string>(value =>
        {
            _document.Text = value;
            ShowStatus();
        }, _input.onValueChanged);
        ShowStatus();
    }

    internal void OpenFile(string fileId) => Change(() => _document.Open(fileId));

    public void OnOpened() { }
    public void OnClosed() => _context.SetTyping(false);
    public void OnTick() { }
    public void Dispose() { }

    private void Save(bool saveAs) => Change(() => _document.Save(saveAs));

    private void Change(Action action)
    {
        try
        {
            action();
            _path.text = _document.Path;
            _input.text = _document.Text;
            ShowStatus();
        }
        catch (Exception exception)
        {
            _status.text = exception.Message;
        }
    }

    private void ShowStatus()
    {
        _status.text = _document.IsDirty
            ? "Unsaved draft. Save before leaving this game save."
            : _document.FileId == null
                ? "Enter a file path above. Save as creates a new file."
                : "Saved to virtual disk. Stored on disk with your next game save.";
    }

    private Button AddButton(string name, string label, float x, float width, Action action)
    {
        Button button = UiFactory.CreateButton(_context.Container, name, label, UiFactory.SurfaceRaised, out S1Text text);
        text.fontSize = 12f;
        TopRect(button.GetComponent<RectTransform>(), x, 8f, width, 30f);
        _context.Bind(button, action);
        return button;
    }

    private void BindTyping(S1Input input)
    {
        var trigger = input.gameObject.AddComponent<EventTrigger>();
        _context.Listeners.AddTrigger(trigger, EventTriggerType.Select, _ => _context.SetTyping(true));
        _context.Listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => _context.SetTyping(false));
        _context.Listeners.Add<string>(_ => _context.SetTyping(false), input.onEndEdit);
        _context.Listeners.Add<string>(_ =>
        {
            if (input.isFocused) _context.SetTyping(true);
        }, input.onValueChanged);
    }

    private static void TopRect(RectTransform rect, float x, float y, float width, float height, bool stretch = false)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(stretch ? 1f : 0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }
}

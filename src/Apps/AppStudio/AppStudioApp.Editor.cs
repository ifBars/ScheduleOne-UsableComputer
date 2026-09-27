using System.Text;
using UsableComputer.API;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UsableComputer.Apps.AppStudio;

internal sealed partial class AppStudioApp
{
    private readonly DesktopAppContext _context;
    private static StudioWorkspace Workspace = new();
    private StudioDraft _document = null!;
    private Button _errorButton = null!;
    private string _savedSource = "";
    private int _diagnosticLine;
    private bool _layoutDirty = true;
    private float _layoutWidth;
    private int? _pendingCaret;

    private void BuildEditorTools(DesktopAppContext context)
    {
        Transform toolbar = _fileName.transform.parent;
        foreach (int direction in new[] { -1, 1 })
        {
            Button change = UiFactory.CreateButton(toolbar, direction < 0 ? "PreviousDraft" : "NextDraft", direction < 0 ? "<" : ">", UiFactory.Surface, out _);
            RectTransform buttonRect = change.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 0.5f);
            buttonRect.pivot = new Vector2(0f, 0.5f);
            buttonRect.sizeDelta = new Vector2(24f, 24f);
            buttonRect.anchoredPosition = new Vector2(direction < 0 ? 4f : 32f, 0f);
            context.Bind(change, () => SelectDraft(Workspace.Next(_document, direction)));
        }
        _fileName.rectTransform.offsetMin = new Vector2(64f, 3f);
        _fileName.rectTransform.offsetMax = new Vector2(-160f, -3f);
        _errorButton = UiFactory.CreateButton(context.Container, "GoToError", "Go to error", UiFactory.SurfaceRaised, out _);
        RectTransform rect = _errorButton.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(126f, 32f);
        rect.anchoredPosition = new Vector2(288f, 9f);
        _errorButton.interactable = false;
        context.Bind(_errorButton, GoToError);

        GameObject track = UiFactory.CreatePanel(_editor.transform, "SourceScrollbar", UiFactory.Surface);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(-14f, 4f);
        trackRect.offsetMax = new Vector2(-3f, -4f);
        GameObject thumb = UiFactory.CreatePanel(track.transform, "Handle", UiFactory.SurfaceRaised);
        UiFactory.Stretch(thumb.GetComponent<RectTransform>(), Vector2.zero);
        var scrollbar = track.AddComponent<Scrollbar>();
        scrollbar.handleRect = thumb.GetComponent<RectTransform>();
        scrollbar.targetGraphic = thumb.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _editor.verticalScrollbar = scrollbar;
        _editor.onFocusSelectAll = false;
        _editor.textViewport.offsetMax = new Vector2(-20f, -8f);
        // Match the code's metrics, including blank and wrapped visual lines.
        _lineNumbers.font = _editor.textComponent.font;
        _lineNumbers.fontSize = _editor.textComponent.fontSize;
        _lineNumbers.richText = false;
    }

    private void TickEditor()
    {
        if (_editor.text != _document.Source) _editor.text = _document.Source;
        if (_pendingCaret.HasValue && _editor.isFocused)
        {
            _editor.stringPosition = _pendingCaret.Value;
            _pendingCaret = null;
        }
        float width = _editor.textViewport.rect.width;
        if (_layoutDirty || Mathf.Abs(width - _layoutWidth) > 0.5f)
        {
            _layoutDirty = false;
            _layoutWidth = width;
            _editor.textComponent.ForceMeshUpdate();
            var info = _editor.textComponent.textInfo;
            var numbers = new StringBuilder();
            int previous = 0;
            int scanned = 0;
            int sourceLine = 1;
            for (int index = 0; index < info.lineCount; index++)
            {
                int first = info.lineInfo[index].firstCharacterIndex;
                int offset = first < info.characterCount ? info.characterInfo[first].index : _editor.text.Length;
                while (scanned < offset && scanned < _editor.text.Length)
                    if (_editor.text[scanned++] == '\n') sourceLine++;
                int line = sourceLine;
                if (line != previous) numbers.Append(line);
                numbers.Append('\n');
                previous = line;
            }
            _lineNumbers.text = numbers.ToString();
        }
        Vector2 position = _lineNumbers.rectTransform.anchoredPosition;
        position.y = _editor.textComponent.rectTransform.anchoredPosition.y;
        _lineNumbers.rectTransform.anchoredPosition = position;

        Keyboard? keyboard = Keyboard.current;
        if (!_context.IsFocused() || keyboard == null) return;
        bool control = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        if (control && (keyboard.sKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Save();
    }

    private void UpdateDocumentLabel()
    {
        _document.Source = _editor.text;
        _fileName.text = _document.Name + (_document.IsModified ? " *" : "");
    }

    private void SelectDraft(StudioDraft draft)
    {
        _document = draft;
        _savedSource = draft.SavedSource;
        _editor.text = draft.Source;
        _layoutDirty = true;
        UpdateDocumentLabel();
    }

    internal static void ClearDrafts() => Workspace = new StudioWorkspace();

    private void GoToError()
    {
        if (_diagnosticLine <= 0) return;
        int start = StudioSource.LineStart(_editor.text, _diagnosticLine);
        _pendingCaret = start;
        _editor.ActivateInputField();
        _editor.stringPosition = start;
        _context.SetTyping(true);
    }

    private static char ValidateCodeCharacter(string text, int position, char character)
    {
        Keyboard? keyboard = Keyboard.current;
        bool control = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        return control && (character == '\n' || character == '\r') ? '\0' : character;
    }
}

using UsableComputer.API;
using UsableComputer.Subsystems.Lua;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.Apps.AppStudio;

internal sealed partial class AppStudioApp : IDesktopAppSession
{
    private readonly S1Input _editor;
    private readonly S1Text _lineNumbers;
    private readonly S1Text _status;
    private readonly S1Text _fileName;
    private readonly GameObject _templateMenu;

    internal AppStudioApp(DesktopAppContext context)
    {
        _context = context;
        _document = Workspace.Open("desktop-app.lua", LuaAppManager.GetEditorSource());
        GameObject toolbar = UiFactory.CreatePanel(
            context.Container,
            "EditorToolbar",
            UiFactory.SurfaceRaised);
        RectTransform toolbarRect = toolbar.GetComponent<RectTransform>();
        toolbarRect.anchorMin = new Vector2(0f, 1f);
        toolbarRect.anchorMax = new Vector2(1f, 1f);
        toolbarRect.pivot = new Vector2(0.5f, 1f);
        toolbarRect.sizeDelta = new Vector2(-16f, 32f);
        toolbarRect.anchoredPosition = new Vector2(0f, -8f);

        _fileName = UiFactory.CreateText(
            toolbar.transform,
            "FileName",
            "●  desktop-app.lua",
            13f,
            UiFactory.TextPrimary,
            GetLeftAlignment(),
            bold: true);
        UiFactory.Stretch(_fileName.rectTransform, new Vector2(10f, 3f));

        S1Text mode = UiFactory.CreateText(
            toolbar.transform,
            "Mode",
            "Lua  ·  sandboxed",
            12f,
            UiFactory.TextMuted,
            GetRightAlignment());
        UiFactory.Stretch(mode.rectTransform, new Vector2(10f, 3f));

        S1Text help = UiFactory.CreateText(
            context.Container,
            "Help",
            "Ctrl+S / Ctrl+Enter: save and run. Use < > to switch drafts.",
            12f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        SetTopRect(help.rectTransform, 24f, -44f);

        GameObject gutter = UiFactory.CreatePanel(
            context.Container,
            "LineGutter",
            UiFactory.Surface);
        RectTransform gutterRect = gutter.GetComponent<RectTransform>();
        gutterRect.anchorMin = new Vector2(0f, 0f);
        gutterRect.anchorMax = new Vector2(0f, 1f);
        gutterRect.offsetMin = new Vector2(8f, 94f);
        gutterRect.offsetMax = new Vector2(64f, -74f);
        GameObject gutterViewport = UiFactory.CreatePanel(gutter.transform, "GutterViewport", Color.clear);
        UiFactory.Stretch(gutterViewport.GetComponent<RectTransform>(), new Vector2(0f, 8f));
        gutterViewport.AddComponent<RectMask2D>();

        _lineNumbers = UiFactory.CreateText(
            gutterViewport.transform,
            "LineNumbers",
            string.Empty,
            14f,
            UiFactory.TextMuted,
            GetRightTopAlignment());
        UiFactory.Stretch(_lineNumbers.rectTransform, new Vector2(5f, 0f));

        _editor = UiFactory.CreateInputField(
            context.Container,
            "Source",
            _document.Source,
            "return { id = \"my-app\", title = \"My App\", render = function() return {} end }",
            multiline: true,
            out S1Text codeText);
        _editor.GetComponent<Image>().color = UiFactory.SurfaceInset;
        codeText.color = UiFactory.TextPrimary;
        codeText.fontSize = 14f;
        codeText.richText = false;
        _editor.characterLimit = 65536;
#if IL2CPPMELON
        _editor.onValidateInput = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<S1Input.OnValidateInput>(
            new System.Func<string, int, char, char>(ValidateCodeCharacter));
#else
        _editor.onValidateInput = ValidateCodeCharacter;
#endif
        RectTransform editorRect = _editor.GetComponent<RectTransform>();
        editorRect.anchorMin = new Vector2(0f, 0f);
        editorRect.anchorMax = new Vector2(1f, 1f);
        editorRect.offsetMin = new Vector2(64f, 94f);
        editorRect.offsetMax = new Vector2(-8f, -74f);
        context.Listeners.Add<string>(_ => context.SetTyping(true), _editor.onSelect);
        context.Listeners.Add<string>(_ => context.SetTyping(false), _editor.onDeselect);
        context.Listeners.Add<string>(UpdateLineNumbers, _editor.onValueChanged);

        Button save = UiFactory.CreateButton(
            context.Container,
            "Save",
            "Save & run",
            UiFactory.Accent,
            out _);
        RectTransform saveRect = save.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(0f, 0f);
        saveRect.anchorMax = new Vector2(0f, 0f);
        saveRect.pivot = new Vector2(0f, 0f);
        saveRect.sizeDelta = new Vector2(132f, 32f);
        saveRect.anchoredPosition = new Vector2(8f, 9f);
        context.Bind(save, Save);

        Button template = UiFactory.CreateButton(
            context.Container,
            "Templates",
            "Templates",
            UiFactory.SurfaceRaised,
            out _);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(0f, 0f);
        templateRect.pivot = new Vector2(0f, 0f);
        templateRect.sizeDelta = new Vector2(132f, 32f);
        templateRect.anchoredPosition = new Vector2(148f, 9f);
        context.Bind(template, ToggleTemplates);

        int templateCount = LuaAppTemplates.All.Count + 1;
        _templateMenu = UiFactory.CreatePanel(
            context.Container,
            "TemplateMenu",
            UiFactory.SurfaceRaised);
        RectTransform menuRect = _templateMenu.GetComponent<RectTransform>();
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.zero;
        menuRect.pivot = Vector2.zero;
        menuRect.sizeDelta = new Vector2(210f, (templateCount * 28f) + 8f);
        menuRect.anchoredPosition = new Vector2(148f, 46f);
        AddTemplateOption(context, "Blank starter", LuaAppManager.StarterSource, 0);
        for (int index = 0; index < LuaAppTemplates.All.Count; index++)
        {
            LuaAppTemplate appTemplate = LuaAppTemplates.All[index];
            AddTemplateOption(context, appTemplate.Name, appTemplate.Source, index + 1);
        }
        _templateMenu.SetActive(false);

        _status = UiFactory.CreateText(
            context.Container,
            "Status",
            "Ready",
            12f,
            UiFactory.TextMuted,
            GetLeftAlignment());
        _status.rectTransform.anchorMin = new Vector2(0f, 0f);
        _status.rectTransform.anchorMax = new Vector2(1f, 0f);
        _status.rectTransform.pivot = new Vector2(0.5f, 0f);
        _status.rectTransform.sizeDelta = new Vector2(-24f, 44f);
        _status.rectTransform.anchoredPosition = new Vector2(0f, 46f);
        _status.richText = false;
        _savedSource = _document.SavedSource;
        BuildEditorTools(context);
        UpdateLineNumbers(_editor.text);
    }

    public void OnOpened()
    {
    }

    public void OnClosed() => _context.SetTyping(false);

    public void OnTick() => TickEditor();

    public void Dispose()
    {
    }

    private void Save()
    {
        try
        {
            bool saved = LuaAppManager.TrySaveAndRegister(_editor.text, out string message, out string appId);
            _status.text = saved ? message : "Error: " + StudioSource.DescribeDiagnostic(message);
            _status.color = saved ? UiFactory.Success : UiFactory.TextPrimary;
            _diagnosticLine = saved ? 0 : StudioSource.DiagnosticLine(message);
            if (saved) _document.SavedSource = _savedSource = _editor.text;
            UpdateDocumentLabel();
            _errorButton.interactable = _diagnosticLine > 0;
            if (saved) _context.OpenApp(appId);
        }
        catch (System.Exception exception)
        {
            _status.text = "Could not save: " + exception.Message;
            _status.color = UiFactory.TextPrimary;
            _diagnosticLine = 0;
            _errorButton.interactable = false;
        }
    }

    private void ToggleTemplates()
    {
        _templateMenu.SetActive(!_templateMenu.activeSelf);
    }

    private void AddTemplateOption(
        DesktopAppContext context,
        string name,
        string source,
        int index)
    {
        Button option = UiFactory.CreateButton(
            _templateMenu.transform,
            $"Template_{index}",
            name,
            index % 2 == 0 ? UiFactory.Surface : UiFactory.SurfaceInset,
            out _);
        RectTransform rect = option.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-8f, 26f);
        rect.anchoredPosition = new Vector2(0f, -4f - (index * 28f));
        context.Bind(option, () => LoadTemplate(name, source));
    }

    private void LoadTemplate(string name, string source)
    {
        SelectDraft(Workspace.Open(name.ToLowerInvariant().Replace(' ', '-') + ".lua", source));
        _templateMenu.SetActive(false);
        _status.text = $"{name} template loaded. Save when ready.";
        _status.color = UiFactory.TextMuted;
    }

    private void UpdateLineNumbers(string source)
    {
        _layoutDirty = true;
        _diagnosticLine = 0;
        if (_errorButton != null) _errorButton.interactable = false;
        UpdateDocumentLabel();
    }

    private static void SetTopRect(RectTransform rect, float height, float y)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-16f, height);
        rect.anchoredPosition = new Vector2(0f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetLeftAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetRightAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineRight;
    private static Il2CppTMPro.TextAlignmentOptions GetRightTopAlignment() => Il2CppTMPro.TextAlignmentOptions.TopRight;
#else
    private static TMPro.TextAlignmentOptions GetLeftAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetRightAlignment() => TMPro.TextAlignmentOptions.MidlineRight;
    private static TMPro.TextAlignmentOptions GetRightTopAlignment() => TMPro.TextAlignmentOptions.TopRight;
#endif
}

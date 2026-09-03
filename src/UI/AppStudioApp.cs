using UsableComputer.API;
using UsableComputer.Scripting;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Input = Il2CppTMPro.TMP_InputField;
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Input = TMPro.TMP_InputField;
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class AppStudioApp : IDesktopAppSession
{
    private readonly S1Input _editor;
    private readonly S1Text _lineNumbers;
    private readonly S1Text _status;
    private readonly S1Text _fileName;
    private readonly GameObject _templateMenu;

    internal AppStudioApp(DesktopAppContext context)
    {
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
            "Return an app definition and render rows. Save validates and hot-loads it.",
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
        gutterRect.offsetMin = new Vector2(8f, 50f);
        gutterRect.offsetMax = new Vector2(50f, -74f);
        gutter.AddComponent<RectMask2D>();

        _lineNumbers = UiFactory.CreateText(
            gutter.transform,
            "LineNumbers",
            string.Empty,
            14f,
            UiFactory.TextMuted,
            GetRightTopAlignment());
        UiFactory.Stretch(_lineNumbers.rectTransform, new Vector2(5f, 8f));

        _editor = UiFactory.CreateInputField(
            context.Container,
            "Source",
            LuaAppManager.GetEditorSource(),
            "return { id = \"my-app\", title = \"My App\", render = function() return {} end }",
            multiline: true,
            out S1Text codeText);
        _editor.GetComponent<Image>().color = UiFactory.SurfaceInset;
        codeText.color = UiFactory.TextPrimary;
        codeText.fontSize = 14f;
        RectTransform editorRect = _editor.GetComponent<RectTransform>();
        editorRect.anchorMin = new Vector2(0f, 0f);
        editorRect.anchorMax = new Vector2(1f, 1f);
        editorRect.offsetMin = new Vector2(50f, 50f);
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
            "Templates ▾",
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
        _status.rectTransform.sizeDelta = new Vector2(-304f, 32f);
        _status.rectTransform.anchoredPosition = new Vector2(146f, 9f);
        UpdateLineNumbers(_editor.text);
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

    private void Save()
    {
        bool saved = LuaAppManager.TrySaveAndRegister(_editor.text, out string message);
        _status.text = message;
        _status.color = saved ? UiFactory.Success : UiFactory.Danger;
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
        _editor.text = source;
        _fileName.text = $"●  {name.ToLowerInvariant().Replace(' ', '-')}.lua";
        _templateMenu.SetActive(false);
        _status.text = $"{name} template loaded. Save when ready.";
        _status.color = UiFactory.TextMuted;
    }

    private void UpdateLineNumbers(string source)
    {
        int lineCount = 1;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
                lineCount++;
        }

        lineCount = Mathf.Min(lineCount, 99);
        var lines = new System.Text.StringBuilder(lineCount * 3);
        for (int line = 1; line <= lineCount; line++)
            lines.Append(line).Append('\n');
        _lineNumbers.text = lines.ToString();
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

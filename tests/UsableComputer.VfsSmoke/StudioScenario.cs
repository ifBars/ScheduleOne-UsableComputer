using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
#if IL2CPP
using StudioInput = Il2CppTMPro.TMP_InputField;
using StudioText = Il2CppTMPro.TextMeshProUGUI;
#else
using StudioInput = TMPro.TMP_InputField;
using StudioText = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunStudioScenario()
    {
        StudioInput editor;
        object session;
        string source = string.Join("\n", Enumerable.Range(1, 150).Select(i => $"-- line {i}" + (i == 140 ? new string('x', 250) : ""))) + "\nreturn {";
        try
        {
            CloseAllWindows();
            Type preferences = GetUsableComputerAssembly().GetType("UsableComputer.PreferencesStore", true)!;
            MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, _phase == "seed" ? "Light" : "Dark") });
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "app-studio" });
            session = GetAppSession("app-studio");
            editor = GameObject.Find("Source").GetComponent<StudioInput>();
            editor.text = source;
        }
        catch (Exception exception) { Fail("Studio setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.4f);
        try
        {
            Require(GameObject.Find("LineNumbers").GetComponent<StudioText>().text.Contains("150"), "Gutter stopped before line 150.");
            editor.verticalScrollbar.value = 0f;
            GameObject.Find("Save").GetComponent<Button>().onClick.Invoke();
            Require(GameObject.Find("GoToError").GetComponent<Button>().interactable, "Syntax error did not expose a line location.");
            GameObject.Find("GoToError").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Studio diagnostics failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.4f);
        try
        {
            Require(editor.stringPosition >= source.LastIndexOf("return", StringComparison.Ordinal), "Go to error did not move to the failing line.");
            var gutter = GameObject.Find("LineNumbers").GetComponent<StudioText>();
            Require(Math.Abs(gutter.rectTransform.anchoredPosition.y - editor.textComponent.rectTransform.anchoredPosition.y) < 0.1f, "Gutter and editor scroll diverged.");
            gutter.ForceMeshUpdate();
            Require(editor.textComponent.textInfo.lineCount > 151, "Fixture did not exercise word wrapping.");
            for (int line = 0; line < editor.textComponent.textInfo.lineCount; line++)
                Require(Math.Abs(gutter.textInfo.lineInfo[line].baseline - editor.textComponent.textInfo.lineInfo[line].baseline) < 0.5f,
                    $"Gutter baseline diverged at visual line {line}.");
            ValidateStudioGutter(editor);
        }
        catch (Exception exception) { Fail("Studio scroll failed", Unwrap(exception)); yield break; }
        string screenshot = Path.Combine(_outputDirectory, "studio-error.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            GameObject.Find("Templates").GetComponent<Button>().onClick.Invoke();
            Require(GameObject.Find("TemplateMenu").transform.childCount == 9, "Not all templates are discoverable.");
            GameObject.Find("Template_1").GetComponent<Button>().onClick.Invoke();
            Require(editor.text != source, "Template did not load.");
            GameObject.Find("PreviousDraft").GetComponent<Button>().onClick.Invoke();
            Require(editor.text == source, "Switching templates discarded edits.");
            object[] args = { "" };
            Type manager = GetUsableComputerAssembly().GetType("UsableComputer.Scripting.LuaAppManager", true)!;
            Require((bool)manager.GetMethod("TryValidateBundledTemplatesAtRuntime", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!, "Bundled template failed: " + args[0]);
            GameObject.Find("Window_app-studio").transform.Find("TitleBar/Maximize").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Studio templates failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.4f);
        try { ValidateStudioGutter(editor); }
        catch (Exception exception) { Fail("Studio resize failed", Unwrap(exception)); yield break; }
        screenshot = Path.Combine(_outputDirectory, "studio-maximized.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try { VerifyStudioSave(editor); }
        catch (Exception exception) { Fail("Studio Save and Run failed", Unwrap(exception)); yield break; }
        LoggerInstance.Msg($"[UsableComputerStudioSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} LongSource=True Scroll=True ErrorLocation=True Drafts=True Templates=True Resize=True SaveAndRun=True");
    }

    private static void ValidateStudioGutter(StudioInput editor)
    {
        var gutter = GameObject.Find("LineNumbers").GetComponent<StudioText>();
        gutter.ForceMeshUpdate();
        for (int line = 0; line < editor.textComponent.textInfo.lineCount; line++)
        {
            Vector3 world = gutter.rectTransform.TransformPoint(new Vector3(0, gutter.textInfo.lineInfo[line].baseline, 0));
            float actual = editor.textComponent.rectTransform.InverseTransformPoint(world).y;
            Require(Math.Abs(actual - editor.textComponent.textInfo.lineInfo[line].baseline) < 0.5f, "Rendered gutter baseline differs.");
        }
        Vector3[] corners = GetWorldCorners(GameObject.Find("GutterViewport").GetComponent<RectTransform>());
        Require(Math.Abs(editor.textViewport.InverseTransformPoint(corners[1]).y - editor.textViewport.rect.yMax) < 0.1f, "Gutter clipping differs from source viewport.");
    }

    private void VerifyStudioSave(StudioInput editor)
    {
        string id = "smoke-studio-" + Guid.NewGuid().ToString("N");
        Type manager = GetUsableComputerAssembly().GetType("UsableComputer.Scripting.LuaAppManager", true)!;
        string root = (string)manager.GetProperty("AppsDirectory", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        string path = Path.Combine(root, id + ".lua");
        Require(!File.Exists(path), "Unique Studio fixture already exists.");
        string original = editor.text;
        try
        {
            editor.text = "return { id = '" + id + "', title = 'Studio Smoke', render = function() return {{kind='text', text='Saved'}} end }";
            GameObject.Find("Save").GetComponent<Button>().onClick.Invoke();
            Require(File.Exists(path) && File.ReadAllText(path) == editor.text, "Save did not write the exact source.");
            RequireWindow("lua." + id);
            object first = GetAppSession("lua." + id);
            editor.text = editor.text.Replace("text='Saved'", "text='Updated'");
            GameObject.Find("Save").GetComponent<Button>().onClick.Invoke();
            Require(File.ReadAllText(path) == editor.text, "Rerun did not replace the saved source.");
            Require(!ReferenceEquals(first, GetAppSession("lua." + id)), "Rerun kept the previous app session.");
        }
        finally
        {
            object windows = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
            windows.GetType().GetMethod("CloseWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(windows, new object[] { "lua." + id });
            UsableComputer.API.DesktopAppRegistry.Unregister("lua." + id);
            var definitions = (IDictionary)manager.GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            (definitions[id] as IDisposable)?.Dispose();
            definitions.Remove(id);
            object? shortcut = FindChild(GetServiceType(), "desktop", null, "lua." + id);
            if (shortcut != null) Invoke(GetServiceType(), "Delete", ReadString(shortcut, "Id"));
            if (File.Exists(path)) File.Delete(path);
            editor.text = original;
        }
    }
}

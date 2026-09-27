using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunAppIconsScenario()
    {
        const string id = "smoke-custom-icon";
        Type manager = GetUsableComputerAssembly().GetType("UsableComputer.Scripting.LuaAppManager", true)!;
        Type registry = GetUsableComputerAssembly().GetType("UsableComputer.API.DesktopAppRegistry", true)!;
        object? definition = null;
        Sprite? first = null;
        Texture2D? firstTexture = null;
        try
        {
            CloseAllWindows();
            definition = CompileIcon(manager);
            Invoke(manager, "Register", definition);
            first = ResolveIcon(definition);
            firstTexture = first.texture;
            Require(first.rect.width == 8 && first.rect.height == 8, "Custom sprite dimensions changed.");
            Require(ResolveIcon(definition) == first, "Custom icon was not cached.");
            Require(firstTexture.filterMode == FilterMode.Point, "Pixel icon lost point filtering.");
            Require(_desktop != null, "Desktop unavailable.");
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "lua." + id });
            Require(UnityEngine.Object.FindObjectsOfType<Image>().Any(image => image.sprite == first), "Custom icon did not reach desktop UI.");
        }
        catch (Exception exception)
        {
            Fail("App icon setup failed", Unwrap(exception));
            yield break;
        }

        yield return new WaitForSecondsRealtime(1.2f);
        try
        {
            Transform content = GameObject.Find("LuaContent").transform;
            Require(content.childCount > 0, "Lua app has no content after refresh.");
            int uiLayer = (int)GetUsableComputerAssembly().GetType("UsableComputer.Constants", true)!
                .GetField("UiLayer", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            for (int index = 0; index < content.childCount; index++)
                Require(content.GetChild(index).gameObject.layer == uiLayer,
                    "Refreshed Lua content is invisible to the desktop camera.");
        }
        catch (Exception exception)
        {
            Fail("App content refresh failed", Unwrap(exception));
            yield break;
        }
        string screenshot = Path.Combine(_outputDirectory, "custom-app-icon.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;

        try
        {
            definition = CompileIcon(manager);
            Invoke(manager, "Register", definition);
            Require(ResolveIcon(definition) != first, "Hot reload retained the old icon.");
        }
        catch (Exception exception)
        {
            Fail("App icon replacement failed", Unwrap(exception));
            yield break;
        }
        yield return null;
        try
        {
            Require(first == null && firstTexture == null, "Hot reload leaked the previous sprite or texture.");
            Sprite replacement = ResolveIcon(definition!);
            Texture2D replacementTexture = replacement.texture;
            registry.GetMethod("Unregister")!.Invoke(null, new object[] { "lua." + id });
            ((IDisposable)definition!).Dispose();
            var definitions = (IDictionary)manager.GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            definitions.Remove(id);
            first = replacement;
            firstTexture = replacementTexture;
        }
        catch (Exception exception)
        {
            Fail("App icon cleanup failed", Unwrap(exception));
            yield break;
        }
        yield return null;
        try
        {
            Require(first == null && firstTexture == null, "Icon cleanup leaked Unity resources.");
            LoggerInstance.Msg($"[UsableComputerAppIconsSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Render=True Cached=True Replaced=True Disposed=True");
        }
        catch (Exception exception) { Fail("App icon disposal failed", Unwrap(exception)); }
    }

    private static object CompileIcon(Type manager)
    {
        const string source = """
            return {
              id = "smoke-custom-icon", title = "Custom Icon",
              icon_pixels = {
                "........", ".cccccc.", ".cffffc.", ".cfccfc.",
                ".cfccfc.", ".cffffc.", ".cccccc.", "........"
              },
              render = function() return { { kind = "heading", text = "Custom pixel icon" } } end
            }
            """;
        object?[] args = { source, "smoke-icon.lua", null, null };
        bool compiled = (bool)manager.GetMethod("TryCompile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        Require(compiled, "Icon app failed validation: " + args[3]);
        return args[2]!;
    }

    private static Sprite ResolveIcon(object definition) => (Sprite)definition.GetType()
        .GetMethod("ResolveIcon", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(definition, null)!;
}

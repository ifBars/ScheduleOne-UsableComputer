using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UsableComputer.Tests.Shared;

#if IL2CPP
using LuaErrorText = Il2CppTMPro.TextMeshProUGUI;
#else
using LuaErrorText = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunLuaStorageScenario()
    {
        const string id = "smoke-storage";
        Type manager = GetUsableComputerAssembly().GetType(ModTypeNames.LuaAppManager, true)!;
        object definition;
        try
        {
            Type preferences = GetUsableComputerAssembly().GetType(ModTypeNames.PreferencesStore, true)!;
            MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, _phase == "seed" ? "Light" : "Dark") });
            string source = "return {api_version=1,id='" + id + "',title='Storage Smoke',render=function() " +
                "assert(computer.api_version==1); local value=computer.storage.get('value'); " +
                "return {{kind='text',text=value or 'empty'},{kind='button',text='Remember',action=function() computer.storage.set('value','remembered') end}," +
                "{kind='checkbox',text='Supplies checked',value=computer.storage.get('checked')=='yes',action=function(checked) " +
                "assert(type(checked)=='boolean'); computer.storage.set('checked',checked and 'yes' or 'no'); " +
                "computer.storage.set('calls',tostring((tonumber(computer.storage.get('calls')) or 0)+1)) end}} end}";
            object[] args = { source, "smoke-storage.lua", null!, "" };
            Require((bool)manager.GetMethod("TryCompile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!, "Storage compile failed: " + args[3]);
            definition = args[2];
            // Execute through the same bounded render callback used by the UI.
            Type service = GetUsableComputerAssembly().GetType(ModTypeNames.LuaStorageService, true)!;
            string? value = (string?)service.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { id, "value" });
            Require(value == (_phase == "seed" ? null : "remembered"), "Storage did not round-trip through the game save.");
            Require(service.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { "other-app", "value" }) == null, "Storage leaked between app IDs.");
            Invoke(manager, "Register", definition);
            CloseAllWindows();
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "lua." + id });
            if (_phase == "seed") GameObject.Find("Button_2").GetComponent<Button>().onClick.Invoke();
            Require((string?)service.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { id, "value" }) == "remembered", "Lua callback did not store its value.");
            string? calls = (string?)service.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { id, "calls" });
            Require(calls == (_phase == "seed" ? null : "3"), "Rendering invoked the checkbox action or lost its saved callback count.");
            if (_phase == "seed")
            {
                GameObject.Find("Checkbox_3").GetComponent<Toggle>().isOn = true;
                GameObject.Find("Checkbox_3").GetComponent<Toggle>().isOn = false;
                GameObject.Find("Checkbox_3").GetComponent<Toggle>().isOn = true;
            }
            Require(GameObject.Find("Checkbox_3").GetComponent<Toggle>().isOn, "Checkbox did not render its saved state.");
            Require((string?)service.GetMethod("Get", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { id, "calls" }) == "3", "Checkbox callbacks fired more than once per change.");
        }
        catch (Exception exception) { Fail("Lua storage failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.4f);
        string screenshot = Path.Combine(_outputDirectory, "lua-storage.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            VerifyCheckboxError(manager, "'yes'", "function() end", false, "boolean value");
            VerifyCheckboxError(manager, "false", "function(checked) error('Checkbox action failed') end", true, "Checkbox action failed");
        }
        catch (Exception exception) { Fail("Checkbox error isolation failed", Unwrap(exception)); yield break; }
        LoggerInstance.Msg($"[UsableComputerLuaStorageSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} AppScope=True CallbackWrite=True Checkbox=True Persisted=True");
    }

    private void VerifyCheckboxError(Type manager, string value, string action, bool click, string expected)
    {
        string source = "return {id='smoke-checkbox-error',title='Checkbox Error',render=function() return {{kind='checkbox',text='Test',value=" + value + ",action=" + action + "}} end}";
        object[] args = { source, "smoke-checkbox-error.lua", null!, "" };
        Require((bool)manager.GetMethod("TryCompile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!, "Checkbox error fixture failed compilation.");
        Invoke(manager, "Register", args[2]);
        CloseAllWindows();
        _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "lua.smoke-checkbox-error" });
        if (click) GameObject.Find("Checkbox_1").GetComponent<Toggle>().isOn = true;
        Require(GetWindowList().Length == 1, "Checkbox error escaped its window.");
        object windows = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        object window = ((IList)windows.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(windows)!)[0]!;
        var content = (RectTransform)window.GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var error = content.Find("LuaViewport/LuaContent/Error");
        Require(error != null && error.GetComponent<LuaErrorText>().text.Contains(expected), "Checkbox failure did not show its app error.");
    }
}

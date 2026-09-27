using System.Collections;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UsableComputer.Tests.Shared;

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunDoomRuntimeScenario()
    {
        object session;
        Texture2D texture;
        try
        {
            Type locator = GetUsableComputerAssembly().GetType(ModTypeNames.DoomWadLocator, true)!;
            string? wad = (string?)locator.GetMethod("FindIwad", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null);
            if (wad == null || !Path.GetFileName(wad).StartsWith("freedoom", StringComparison.OrdinalIgnoreCase))
                throw new FileNotFoundException("Doom runtime check requires a local Freedoom IWAD in a DoomWadLocator search directory.");
            CloseAllWindows();
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "doom" });
            session = GetAppSession("doom");
            Require(GameAppField(session, "_doom") != null, "Doom engine did not start from the IWAD.");
            object video = GameAppField(session, "_video")
                ?? throw new InvalidOperationException("Doom video adapter did not start.");
            texture = (Texture2D)video.GetType().GetProperty("Texture", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(video)!;
            Require(texture != null && texture.width > 0 && texture.height > 0,
                "Doom video texture is unavailable.");
        }
        catch (Exception error) { Fail("Doom runtime setup failed", Unwrap(error)); yield break; }

        bool visible = false;
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline && !visible)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            try
            {
                visible = HasVisibleDoomPixels(texture ?? throw new InvalidOperationException("Doom texture was lost."));
            }
            catch (Exception error) { Fail("Doom frame read failed", Unwrap(error)); yield break; }
        }
        try
        {
            Require(visible, "Doom did not render a visible frame within six seconds.");
            RawImage display = GameObject.Find("DoomAppRoot").transform.Find("Display").GetComponent<RawImage>();
            Require(display.texture == texture, "Doom frame is not attached to its desktop display.");
        }
        catch (Exception error) { Fail("Doom frame validation failed", Unwrap(error)); yield break; }

        string screenshot = Path.Combine(_outputDirectory, "doom-runtime.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try { CloseAllWindows(); }
        catch (Exception error) { Fail("Doom close failed", Unwrap(error)); yield break; }
        yield return null;
        try
        {
            Require(texture == null, "Doom retained its frame texture after close.");
            LoggerInstance.Msg($"[UsableComputerDoomRuntime] PASS Runtime={ConstantsRuntime()} Phase={_phase} VisibleFrame=True TextureDisposed=True");
        }
        catch (Exception error) { Fail("Doom cleanup failed", Unwrap(error)); }
    }

    private IEnumerator RunNestedGameRuntimeScenario()
    {
        object session;
        object child;
        int childPid;
        try
        {
            CloseAllWindows();
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "schedule-one" });
            session = GetAppSession("schedule-one");
            GameObject.Find("NestedGameAppRoot").transform.Find("Start").GetComponent<Button>().onClick.Invoke();
            child = GameAppField(session, "_nestedGame")
                ?? throw new InvalidOperationException("Nested Schedule I did not start; inspect the desktop error screen and nested loader prerequisites.");
            Process process = (Process)GameAppField(child, "_process")!;
            childPid = process.Id;
            Require(childPid != Process.GetCurrentProcess().Id && ReadGameAppBool(child, "IsRunning"),
                "Nested Schedule I did not own a separate running process.");
        }
        catch (Exception error) { Fail("Nested-game start failed", Unwrap(error)); yield break; }

        float deadline = Time.realtimeSinceStartup + 80f;
        while (Time.realtimeSinceStartup < deadline && !ReadGameAppBool(session, "HasVisibleFrame"))
        {
            if (!ReadGameAppBool(child, "IsRunning"))
            {
                Fail("Nested-game process exited before a visible frame", new InvalidOperationException(ReadGameAppString(child, "Status")));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
        try
        {
            Require(ReadGameAppBool(session, "HasVisibleFrame"),
                "Nested Schedule I did not produce a visible frame within 80 seconds. Status: " + ReadGameAppString(child, "Status"));
            RawImage display = GameObject.Find("NestedDisplay").GetComponent<RawImage>();
            Require(display.texture != null && display.texture.width > 0 && display.texture.height > 0,
                "The nested frame was not rendered in the desktop window.");
        }
        catch (Exception error) { Fail("Nested-game frame validation failed", Unwrap(error)); yield break; }

        string screenshot = Path.Combine(_outputDirectory, "nested-game-runtime.png");
        ScreenCapture.CaptureScreenshot(screenshot);
        yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            GameObject.Find("NestedGameAppRoot").transform.Find("Controls/Stop")
                .GetComponent<Button>().onClick.Invoke();
            Require(GameAppField(session, "_nestedGame") == null,
                "The nested-game Stop button retained its owned process.");
        }
        catch (Exception error) { Fail("Nested-game stop failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.25f);
        try
        {
            Require(!IsProcessRunning(childPid), "The nested Schedule I process survived Stop.");
            CloseAllWindows();
            LoggerInstance.Msg($"[UsableComputerNestedRuntime] PASS Runtime={ConstantsRuntime()} Phase={_phase} VisibleFrame=True ChildPid={childPid} StopTerminated=True");
        }
        catch (Exception error) { Fail("Nested-game cleanup failed", Unwrap(error)); }
    }

    private static object? GameAppField(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);

    private static bool ReadGameAppBool(object target, string name) => (bool)target.GetType()
        .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(target)!;

    private static string ReadGameAppString(object target, string name) => target.GetType()
        .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(target)?.ToString() ?? string.Empty;

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool HasVisibleDoomPixels(Texture2D texture)
    {
        var pixels = texture.GetPixels32();
        int visible = 0;
        for (int index = 0; index < pixels.Length; index += 64)
        {
            Color32 pixel = pixels[index];
            if (pixel.r + pixel.g + pixel.b > 48 && ++visible >= 16)
                return true;
        }
        return false;
    }
}

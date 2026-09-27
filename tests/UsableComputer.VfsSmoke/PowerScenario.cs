using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunPowerScenario()
    {
        try
        {
            OpenSettings();
            PowerInvoke("ToggleStartMenu");
            GameObject.Find("Start_Shut down").GetComponent<Button>().onClick.Invoke();
            Require(PowerState() == "Off", "Shutdown did not power off.");
            Require(GetWindowList().Length == 0, "Shutdown retained app windows.");
            ControllerPowerInvoke("Close");
            ControllerPowerInvoke("Open");
            Require(PowerState() == "Off", "Re-entering powered on an off computer.");
        }
        catch (Exception exception) { Fail("Power-off scenario failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.5f);
        string screenshot = Path.Combine(_outputDirectory, "power-off.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            GameObject.Find("ComputerPowerOn").GetComponent<Button>().onClick.Invoke();
            Require(PowerState() == "Booting", "Power button did not start boot.");
            OpenSettings();
            Require(GetWindowList().Length == 0, "App opened during boot.");
        }
        catch (Exception exception) { Fail("Boot scenario failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(1.2f);
        screenshot = Path.Combine(_outputDirectory, "power-boot.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try { ControllerPowerInvoke("Close"); }
        catch (Exception exception) { Fail("Boot exit failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(4.2f);
        try
        {
            ControllerPowerInvoke("Open");
            Require(PowerState() == "Running", "Boot did not finish while away.");
            OpenSettings();
            ControllerPowerInvoke("Close"); ControllerPowerInvoke("Open");
            Require(GetWindowList().Length == 1, "Leaving desk closed the running session.");
            PowerInvoke("ToggleStartMenu");
            GameObject.Find("Start_Restart").GetComponent<Button>().onClick.Invoke();
            Require(PowerState() == "Booting" && GetWindowList().Length == 0, "Restart did not close apps and boot.");
        }
        catch (Exception exception) { Fail("Restart scenario failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(4.2f);
        try
        {
            Require(PowerState() == "Running", "Restart did not finish.");
            OpenSettings();
            Require(GetWindowList().Length == 1, "Apps unavailable after restart.");
            LoggerInstance.Msg($"[UsableComputerPowerSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Shutdown=True Boot=True Restart=True LeaveAndReturn=True");
        }
        catch (Exception exception) { Fail("Restart completion failed", Unwrap(exception)); }
    }

    private void PowerInvoke(string method) => _desktop!.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, null);
    private void ControllerPowerInvoke(string method) => _controller!.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_controller, null);
    private string PowerState() => _desktop!.GetType().GetField("_powerState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!.ToString()!;
    private object[] GetWindowList()
    {
        object manager = _desktop!.GetType().GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_desktop)!;
        return ((IEnumerable)manager.GetType().GetMethod("GetAppIds", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null)!).Cast<object>().ToArray();
    }
}

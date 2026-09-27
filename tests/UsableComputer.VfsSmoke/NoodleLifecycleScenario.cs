using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using NativeSnake = Il2CppScheduleOne.TV.Snake;
using NativeAudio = Il2CppScheduleOne.Audio.AudioSourceController;
#else
using NativeSnake = ScheduleOne.TV.Snake;
using NativeAudio = ScheduleOne.Audio.AudioSourceController;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunNoodleLifecycle(NativeSnake game)
    {
        GameObject host;
        RectTransform grid;
        NativeAudio[] sounds;
        Vector2 head;
        try
        {
            grid = game.PlaySpace;
            host = grid.parent.gameObject;
            sounds = host.GetComponentsInChildren<NativeAudio>(true).ToArray();
            Require(sounds.Length > 0 && sounds.All(sound => sound.gameObject.activeInHierarchy), "Noodle sound objects are not owned and active.");
            RestartNoodleAtWall(game);
            head = game.HeadPosition;
            GameObject.Find("Window_native-tv.noodle").transform.Find("TitleBar/Minimize").GetComponent<Button>().onClick.Invoke();
            Require(!grid.gameObject.activeInHierarchy, "Minimized Noodle kept its playfield visible.");
        }
        catch (Exception error) { Fail("Noodle minimize setup failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.45f);
        try
        {
            if (game == null || game.HeadPosition != head) throw new InvalidOperationException("Minimized Noodle advanced or was destroyed.");
            OpenNoodle();
        }
        catch (Exception error) { Fail("Noodle minimize pause failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.3f);
        try
        {
            Require(grid.gameObject.activeInHierarchy && game.HeadPosition != head, "Restored Noodle did not resume.");
            RestartNoodleAtWall(game);
            head = game.HeadPosition;
            ControllerPowerInvoke("Close");
            Require(!grid.gameObject.activeInHierarchy, "Leaving the desk retained visible Noodle content.");
        }
        catch (Exception error) { Fail("Noodle desk exit failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.45f);
        try
        {
            if (game == null || game.HeadPosition != head) throw new InvalidOperationException("Noodle advanced while away from the desk.");
            ControllerPowerInvoke("Open");
            OpenNoodle();
        }
        catch (Exception error) { Fail("Noodle desk return failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(0.3f);
        try
        {
            Require(grid.gameObject.activeInHierarchy && game.HeadPosition != head, "Noodle did not resume after returning.");
            PowerInvoke("ToggleStartMenu");
            GameObject.Find("Start_Shut down").GetComponent<Button>().onClick.Invoke();
            Require(PowerState() == "Off" && GetWindowList().Length == 0, "Shutdown did not close Noodle.");
        }
        catch (Exception error) { Fail("Noodle shutdown failed", Unwrap(error)); yield break; }
        yield return null;
        try
        {
            Require(game == null && grid == null && host == null && sounds.All(sound => sound == null),
                "Shutdown retained Noodle's native game, playfield, or sounds.");
            GameObject.Find("ComputerPowerOn").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception error) { Fail("Noodle resource cleanup failed", Unwrap(error)); yield break; }
        yield return new WaitForSecondsRealtime(4.2f);
        try
        {
            Require(PowerState() == "Running", "Computer did not boot after Noodle shutdown.");
            OpenNoodle();
            object session = GetAppSession("native-tv.noodle");
            NativeSnake fresh = (NativeSnake)session.GetType().GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            Require(fresh != null && fresh.GameState == NativeSnake.EGameState.Ready && fresh.Tail.Count == 0,
                "Reopening Noodle did not create a fresh round.");
            LoggerInstance.Msg($"[UsableComputerNoodleLifecycle] PASS Runtime={ConstantsRuntime()} Phase={_phase} Wall=True Minimize=True DeskExit=True Shutdown=True SoundObjectsDisposed=True FreshRound=True");
        }
        catch (Exception error) { Fail("Noodle fresh round failed", Unwrap(error)); }
    }

    private void OpenNoodle() => _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(_desktop, new object[] { "native-tv.noodle" });

    private static void RestartNoodleAtWall(NativeSnake game)
    {
        MethodInfo move = typeof(NativeSnake).GetMethod("MoveSnake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        for (int step = 0; step < 25 && game.GameState == NativeSnake.EGameState.Playing; step++) move.Invoke(game, null);
        Require(game.GameState == NativeSnake.EGameState.Ready, "Noodle did not end the round at a wall.");
        game.TimePerTile = 0.2f;
        GameObject.Find("NoodleUp").GetComponent<Button>().onClick.Invoke();
        Require(game.GameState == NativeSnake.EGameState.Playing, "Noodle did not restart from its direction button.");
    }
}

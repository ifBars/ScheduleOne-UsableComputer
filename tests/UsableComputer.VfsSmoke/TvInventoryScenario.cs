using System.Collections;
using System.Text;
using UnityEngine;

#if IL2CPP
using NativeHome = Il2CppScheduleOne.TV.TVHomeScreen;
#else
using NativeHome = ScheduleOne.TV.TVHomeScreen;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunTvInventoryScenario()
    {
        try
        {
            var report = new StringBuilder();
            int homes = 0;
            int apps = 0;
            int sceneHomes = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeHome home in Resources.FindObjectsOfTypeAll<NativeHome>())
            {
                bool loaded = home.gameObject.scene.IsValid() && home.gameObject.scene.isLoaded;
                if (loaded) sceneHomes++;
                homes++;
                if (home.Apps == null) throw new InvalidOperationException("TV registry is missing.");
                report.AppendLine($"Home={home.name} Scene={home.gameObject.scene.name} Loaded={loaded} Open={home.IsOpen} Apps={home.Apps.Length}");
                foreach (var app in home.Apps)
                {
                    if (app == null || app.Canvas == null || app.CanvasGroup == null)
                        throw new InvalidOperationException("Native TV content root is missing.");
                    if (app.Icon == null) throw new InvalidOperationException("Native TV icon is missing for " + app.AppName);
                    apps++;
                    names.Add(app.name);
                    report.AppendLine($"App={app.AppName} Object={app.name} Icon={app.Icon.name} Open={app.IsOpen} Canvas={app.Canvas.name} Mode={app.Canvas.renderMode} Root={app.transform.parent?.name} CanvasRoot={app.Canvas.transform.parent?.name}");
                    foreach (Component component in app.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) continue;
#if IL2CPP
                        string typeName = component.GetIl2CppType().FullName;
#else
                        string typeName = component.GetType().FullName ?? "unknown";
#endif
                        if (typeName.Contains("ScheduleOne."))
                            report.AppendLine($"  {component.name}: {typeName}");
                    }
                }
            }
            File.WriteAllText(Path.Combine(_outputDirectory, "tv-inventory.txt"), report.ToString());
            Require(homes > 0 && names.Contains("RunnerGame") && names.Contains("Snake"),
                "Expected Egg Run and Noodle TV assets were not found; inspect tv-inventory.txt.");
            LoggerInstance.Msg($"[UsableComputerTvInventory] PASS Runtime={ConstantsRuntime()} Phase={_phase} Homes={homes} SceneHomes={sceneHomes} Apps={apps}");
        }
        catch (Exception error) { Fail("TV inventory failed", Unwrap(error)); }
        yield break;
    }
}

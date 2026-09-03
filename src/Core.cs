using MelonLoader;
using S1API.Building;
using S1API.Lifecycle;
using UsableComputer.Content;
using UsableComputer.Runtime;
using UsableComputer.Scripting;
using UsableComputer.UI;
using UnityEngine;

[assembly: MelonInfo(
    typeof(UsableComputer.Core),
    UsableComputer.Constants.ModName,
    UsableComputer.Constants.ModVersion,
    UsableComputer.Constants.ModAuthor)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer;

public sealed class Core : MelonMod
{
    private float _nextShopAttempt;
    private bool _subscribed;

    public override void OnInitializeMelon()
    {
        PreferencesStore.Initialize();
        BuiltInDesktopApps.RegisterAll();
        LuaAppManager.Initialize();
        GameLifecycle.OnPreLoad += ComputerContentRegistrar.Register;
        GameLifecycle.OnLoadComplete += ComputerContentRegistrar.AddToShops;
        GameLifecycle.OnPreSceneChange += UsableComputerRuntime.DisposeAll;
        BuildEvents.OnBuildableItemInitialized += UsableComputerRuntime.Attach;
        _subscribed = true;
        MelonLogger.Msg(
            $"[{Constants.ModName}] {Constants.ModVersion} initialized for {Constants.RuntimeName}.");
    }

    public override void OnUpdate()
    {
        UsableComputerRuntime.Update();

        if (ComputerContentRegistrar.IsRegistered &&
            !ComputerContentRegistrar.ShopsAdded &&
            Time.unscaledTime >= _nextShopAttempt)
        {
            _nextShopAttempt = Time.unscaledTime + 2f;
            ComputerContentRegistrar.AddToShops();
        }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        _nextShopAttempt = 0f;
        ComputerContentRegistrar.ResetShopRegistration();
    }

    public override void OnApplicationQuit()
    {
        Shutdown();
    }

    public override void OnDeinitializeMelon()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        if (_subscribed)
        {
            GameLifecycle.OnPreLoad -= ComputerContentRegistrar.Register;
            GameLifecycle.OnLoadComplete -= ComputerContentRegistrar.AddToShops;
            GameLifecycle.OnPreSceneChange -= UsableComputerRuntime.DisposeAll;
            BuildEvents.OnBuildableItemInitialized -= UsableComputerRuntime.Attach;
            _subscribed = false;
        }

        UsableComputerRuntime.DisposeAll();
        LuaAppManager.Shutdown();
        BuiltInDesktopApps.UnregisterAll();
    }
}

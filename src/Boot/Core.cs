using MelonLoader;
using UsableComputer.API;
using UsableComputer.Apps;
using UsableComputer.Apps.AppStudio;
using UsableComputer.Apps.Reports;
using UsableComputer.FileSystem;
using UsableComputer.Hardware;
using UsableComputer.Shell;
using UsableComputer.Subsystems.Lua;
using UsableComputer.Subsystems.S1Api;
using S1API.Building;
using S1API.Lifecycle;
using UnityEngine;

[assembly: MelonInfo(
    typeof(UsableComputer.Boot.Core),
    UsableComputer.Constants.ModName,
    UsableComputer.Constants.ModVersion,
    UsableComputer.Constants.ModAuthor)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.Boot;

public sealed class Core : MelonMod
{
    private float _nextShopAttempt;
    private bool _subscribed;

    public override void OnInitializeMelon()
    {
        PreferencesStore.Initialize();
        VirtualFileSystemService.Initialize();
        BuiltInDesktopApps.RegisterAll();
        S1ApiDesktopBridge.Start();
        LuaAppManager.Initialize();
        DesktopKernel.Boot();
        GameLifecycle.OnLoadComplete += DesktopKernel.SaveLoaded;
        GameLifecycle.OnLoadComplete += S1ApiDesktopBridge.ResumeForSave;
        GameLifecycle.OnPreSceneChange += DesktopKernel.SaveLeaving;
        GameLifecycle.OnPreLoad += ComputerContentRegistrar.Register;
        GameLifecycle.OnPreLoad += VirtualFileSystemService.PrepareForLoad;
        GameLifecycle.OnPreLoad += BankReportsService.PrepareForLoad;
        GameLifecycle.OnPreLoad += LuaStorageService.Prepare;
        GameLifecycle.OnLoadComplete += LuaStorageService.Start;
        GameLifecycle.OnPreSceneChange += LuaStorageService.Stop;
        GameLifecycle.OnLoadComplete += BankReportsService.Start;
        GameLifecycle.OnSaveStart += BankReportsService.Flush;
        GameLifecycle.OnPreSceneChange += BankReportsService.Stop;
        GameLifecycle.OnLoadComplete += ComputerContentRegistrar.AddToShops;
        GameLifecycle.OnPreSceneChange += S1ApiDesktopBridge.ClearForSceneChange;
        GameLifecycle.OnPreSceneChange += PlacedComputers.DisposeAll;
        BuildEvents.OnBuildableItemInitialized += PlacedComputers.Attach;
        _subscribed = true;
        MelonLogger.Msg(
            $"[{Constants.ModName}] {Constants.ModVersion} initialized for {Constants.RuntimeName}.");
    }

    public override void OnUpdate()
    {
        PlacedComputers.Update();
        S1ApiDesktopBridge.Update();
        BankReportsService.Update();
        DesktopKernel.Tick(Time.unscaledDeltaTime);

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
            GameLifecycle.OnLoadComplete -= DesktopKernel.SaveLoaded;
            GameLifecycle.OnLoadComplete -= S1ApiDesktopBridge.ResumeForSave;
            GameLifecycle.OnPreSceneChange -= DesktopKernel.SaveLeaving;
            GameLifecycle.OnPreLoad -= ComputerContentRegistrar.Register;
            GameLifecycle.OnPreLoad -= VirtualFileSystemService.PrepareForLoad;
            GameLifecycle.OnPreLoad -= BankReportsService.PrepareForLoad;
            GameLifecycle.OnPreLoad -= LuaStorageService.Prepare;
            GameLifecycle.OnLoadComplete -= LuaStorageService.Start;
            GameLifecycle.OnPreSceneChange -= LuaStorageService.Stop;
            GameLifecycle.OnLoadComplete -= BankReportsService.Start;
            GameLifecycle.OnSaveStart -= BankReportsService.Flush;
            GameLifecycle.OnPreSceneChange -= BankReportsService.Stop;
            GameLifecycle.OnLoadComplete -= ComputerContentRegistrar.AddToShops;
            GameLifecycle.OnPreSceneChange -= S1ApiDesktopBridge.ClearForSceneChange;
            GameLifecycle.OnPreSceneChange -= PlacedComputers.DisposeAll;
            BuildEvents.OnBuildableItemInitialized -= PlacedComputers.Attach;
            _subscribed = false;
        }

        S1ApiDesktopBridge.Stop();
        PlacedComputers.DisposeAll();
        DesktopKernel.Shutdown();
        BankReportsService.Stop();
        LuaAppManager.Shutdown();
        AppStudioApp.ClearDrafts();
        LuaStorageService.Stop();
        BuiltInDesktopApps.UnregisterAll();
        VirtualFileSystemService.Shutdown();
    }
}

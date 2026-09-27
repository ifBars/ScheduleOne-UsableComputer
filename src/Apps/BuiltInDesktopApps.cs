using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Apps.About;
using UsableComputer.Apps.AppStudio;
using UsableComputer.Apps.Calculator;
using UsableComputer.Apps.Dealers;
using UsableComputer.Apps.Deliveries;
using UsableComputer.Apps.Files;
using UsableComputer.Apps.Games.Doom;
using UsableComputer.Apps.Games.EggRun;
using UsableComputer.Apps.Games.NestedGame;
using UsableComputer.Apps.Games.Noodle;
using UsableComputer.Apps.Journal;
using UsableComputer.Apps.Notes;
using UsableComputer.Apps.Products;
using UsableComputer.Apps.Reports;
using UsableComputer.Apps.Settings;
using UsableComputer.Apps.SystemMonitor;
using UsableComputer.Native;
using UsableComputer.UI;
using UnityEngine;

namespace UsableComputer.Apps;

internal static class BuiltInDesktopApps
{
    private static readonly string[] AppIds =
    {
        NoodleNativeAdapter.AppId,
        EggRunNativeAdapter.AppId,
        Constants.NotesAppId,
        Constants.FilesAppId,
        Constants.CalculatorAppId,
        Constants.AboutAppId,
        Constants.JournalAppId,
        Constants.ReportsAppId,
        Constants.DeliveriesAppId,
        Constants.DealersAppId,
        Constants.SystemMonitorAppId,
        Constants.ProductManagerAppId,
        Constants.AppStudioAppId,
        Constants.SettingsAppId,
        Constants.DoomAppId,
        Constants.NestedGameAppId,
    };

    internal static void RegisterAll()
    {
        Register(new DesktopAppDescriptor(EggRunNativeAdapter.AppId, "Egg Run", "", new Vector2(700f, 470f),
            Vector2.zero, context => new EggRunApp(context), EggRunNativeAdapter.Icon));
        Register(new DesktopAppDescriptor(NoodleNativeAdapter.AppId, "Noodle", "", new Vector2(700f, 470f),
            Vector2.zero, context => new NoodleApp(context), NoodleNativeAdapter.Icon));
        Register(new DesktopAppDescriptor(Constants.SystemMonitorAppId, "System Monitor", "", new Vector2(700f, 470f),
            Vector2.zero, context => new SystemMonitorApp(context), () => RuntimeAppIcons.Get(BuiltInIcon.Settings)));
        Register(new DesktopAppDescriptor(Constants.DealersAppId, "Dealers", "", new Vector2(700f, 470f),
            Vector2.zero, context => new DealersApp(context), NativePhoneAppAssets.GetDealerIcon));
        Register(new DesktopAppDescriptor(
            Constants.DeliveriesAppId,
            "Deliveries",
            "D",
            new Vector2(700f, 470f),
            Vector2.zero,
            context => new DeliveriesApp(context),
            NativePhoneAppAssets.GetDeliveryIcon));
        Register(new DesktopAppDescriptor(
            Constants.ReportsAppId,
            "Reports",
            string.Empty,
            new Vector2(700f, 470f),
            Vector2.zero,
            context => new BankReportsApp(context),
            () => RuntimeAppIcons.Get(BuiltInIcon.Reports)));
        Register(
            new DesktopAppDescriptor(
                Constants.NotesAppId,
                "Notes",
                "N",
                new Vector2(520f, 360f),
                new Vector2(-90f, 24f),
                context => new NotesApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Notes)));
        Register(
            new DesktopAppDescriptor(
                Constants.FilesAppId,
                "Files",
                string.Empty,
                new Vector2(700f, 470f),
                Vector2.zero,
                context => new FileExplorerApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Folder)));
        Register(
            new DesktopAppDescriptor(
                Constants.CalculatorAppId,
                "Calculator",
                "÷",
                new Vector2(360f, 440f),
                new Vector2(150f, -8f),
                context => new CalculatorApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Calculator)));
        Register(
            new DesktopAppDescriptor(
                Constants.AboutAppId,
                "About",
                "i",
                new Vector2(430f, 300f),
                new Vector2(0f, 80f),
                context => new AboutApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.About)));
        Register(
            new DesktopAppDescriptor(
                Constants.JournalAppId,
                "Journal",
                "J",
                new Vector2(620f, 430f),
                new Vector2(-10f, 20f),
                context => new JournalApp(context),
                NativePhoneAppAssets.GetJournalIcon));
        Register(
            new DesktopAppDescriptor(
                Constants.ProductManagerAppId,
                "Products",
                "P",
                new Vector2(700f, 470f),
                new Vector2(20f, 12f),
                context => new ProductManagerApp(context),
                NativePhoneAppAssets.GetProductManagerIcon));
        Register(
            new DesktopAppDescriptor(
                Constants.AppStudioAppId,
                "App Studio",
                string.Empty,
                new Vector2(700f, 470f),
                Vector2.zero,
                context => new AppStudioApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Studio)));
        Register(
            new DesktopAppDescriptor(
                Constants.SettingsAppId,
                "Settings",
                string.Empty,
                new Vector2(520f, 360f),
                new Vector2(20f, 40f),
                context => new SettingsApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Settings)));
        Register(
            new DesktopAppDescriptor(
                Constants.DoomAppId,
                "Doom",
                string.Empty,
                new Vector2(720f, 470f),
                Vector2.zero,
                context => new DoomApp(context),
                () => RuntimeAppIcons.Get(BuiltInIcon.Doom)));
        Register(
            new DesktopAppDescriptor(
                Constants.NestedGameAppId,
                "Schedule I",
                string.Empty,
                new Vector2(720f, 470f),
                Vector2.zero,
                context => new NestedGameApp(context),
                () => NativeGameBrandAssets.GetScheduleOneLogo()
                    ?? RuntimeAppIcons.Get(BuiltInIcon.ScheduleOne)));
    }

    internal static void UnregisterAll()
    {
        foreach (string id in AppIds)
            DesktopAppRegistry.Unregister(id);
        RuntimeAppIcons.Dispose();
        NativeGameBrandAssets.Reset();
    }

    private static void Register(DesktopAppDescriptor descriptor)
    {
        try
        {
            DesktopAppRegistry.Register(descriptor);
        }
        catch (InvalidOperationException exception)
        {
            // A second initialization pass should not prevent the rest of the mod from loading.
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Built-in app '{descriptor.Id}' was already registered: {exception.Message}");
        }
    }
}

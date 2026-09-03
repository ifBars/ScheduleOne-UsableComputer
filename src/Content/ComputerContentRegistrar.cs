using System;
using MelonLoader;
using S1API.Building;
using S1API.Items.Buildable;
using S1API.Lifecycle;
using S1API.Shops;
using UnityEngine;
using Object = UnityEngine.Object;

#if IL2CPPMELON
using Il2CppInterop.Runtime;
using S1BuildableDefinition = Il2CppScheduleOne.ItemFramework.BuildableItemDefinition;
using S1Registry = Il2CppScheduleOne.Registry;
#elif MONOMELON
using S1BuildableDefinition = ScheduleOne.ItemFramework.BuildableItemDefinition;
using S1Registry = ScheduleOne.Registry;
#endif

namespace UsableComputer.Content;

internal static class ComputerContentRegistrar
{
    private static bool _registered;
    private static bool _shopsAdded;

    internal static bool IsRegistered => _registered;

    internal static bool ShopsAdded => _shopsAdded;

    internal static void ResetShopRegistration()
    {
        _shopsAdded = false;
    }

    internal static void Register()
    {
        if (_registered)
            return;

        GameObject? model = null;
        try
        {
            var donorItem = S1Registry.GetItem(Constants.DonorItemId);
            if (donorItem == null)
            {
                MelonLogger.Error(
                    $"[{Constants.ModName}] Native donor '{Constants.DonorItemId}' is unavailable.");
                return;
            }

#if IL2CPPMELON
            S1BuildableDefinition? donor = donorItem.TryCast<S1BuildableDefinition>();
#else
            S1BuildableDefinition? donor = donorItem as S1BuildableDefinition;
#endif
            if (donor == null || donor.BuiltItem == null)
            {
                MelonLogger.Error(
                    $"[{Constants.ModName}] Donor '{Constants.DonorItemId}' has no native built item.");
                return;
            }

            if (!string.Equals(donor.BuiltItem.name, Constants.DonorBuiltRootName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Donor built root is '{donor.BuiltItem.name}', expected '{Constants.DonorBuiltRootName}'.");
            }

            model = NativeComputerModelFactory.Create(donor.BuiltItem.gameObject);
            FurnitureCreator.CreateBuilder()
                .WithBasicInfo(
                    Constants.ItemId,
                    "Usable Computer",
                    "A compact computer with a notes app and calculator.")
                .WithModel(model)
                .WithPlacement(FurniturePlacementMode.Grid)
                .WithFootprint(4, 2)
                .WithBuildSound(BuildSoundType.Metal)
                .WithPricing(750f, 0.5f)
                .WithStackLimit(1)
                .WithGeneratedIcon(512)
                .Build();

            _registered = true;
            MelonLogger.Msg(
                $"[{Constants.ModName}] Registered '{Constants.ItemId}' from runtime native visuals.");
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[{Constants.ModName}] Furniture registration failed: {exception}");
        }
        finally
        {
            if (model != null)
                Object.Destroy(model);
        }
    }

    internal static void AddToShops()
    {
        if (_shopsAdded || !_registered)
            return;

        try
        {
            S1API.Items.ItemDefinition? item = S1API.Items.ItemManager.GetDefinition(Constants.ItemId);
            if (item == null)
            {
                MelonLogger.Warning($"[{Constants.ModName}] Registered item could not be resolved for shops.");
                return;
            }

            ShopManager.AddToShops(item, Constants.HardwareShop, Constants.DanHardwareShop);

            bool hardwareReady = ShopManager.GetShopByName(Constants.HardwareShop)?.HasItem(Constants.ItemId) == true;
            bool danHardwareReady = ShopManager.GetShopByName(Constants.DanHardwareShop)?.HasItem(Constants.ItemId) == true;
            _shopsAdded = hardwareReady && danHardwareReady;

            if (_shopsAdded)
            {
                MelonLogger.Msg(
                    $"[{Constants.ModName}] Added the computer to both hardware shops.");
            }
            else
            {
                MelonLogger.Warning(
                    $"[{Constants.ModName}] Hardware shops are not ready; shop registration will be retried.");
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[{Constants.ModName}] Shop registration failed: {exception}");
        }
    }
}

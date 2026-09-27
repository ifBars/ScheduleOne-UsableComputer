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
    private static bool _laptopRegistered;
    private static bool _shopsAdded;

    internal static bool IsRegistered => _registered;

    internal static bool ShopsAdded => _shopsAdded;

    internal static void ResetShopRegistration()
    {
        _shopsAdded = false;
    }

    internal static void Register()
    {
        if (_registered && _laptopRegistered)
            return;

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

            if (!_registered)
            {
                RegisterItem(Constants.ItemId, "Usable Computer",
                    "A desktop computer with a working XP-inspired interface.",
                    donor.BuiltItem.gameObject, null);
                _registered = true;
            }

            if (!_laptopRegistered)
            {
                GameObject? laptop = FindNativeLaptop();
                if (laptop == null)
                {
                    MelonLogger.Warning($"[{Constants.ModName}] The special-customer laptop is unavailable in this scene.");
                    return;
                }

                RegisterItem(Constants.LaptopItemId, "Usable Laptop",
                    "A laptop with a working XP-inspired interface.",
                    donor.BuiltItem.gameObject, laptop);
                _laptopRegistered = true;
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"[{Constants.ModName}] Furniture registration failed: {exception}");
        }
    }

    private static void RegisterItem(string id, string name, string description,
        GameObject donorBuiltItem, GameObject? laptopSource)
    {
        GameObject model = laptopSource == null
            ? NativeComputerModelFactory.Create(donorBuiltItem)
            : NativeComputerModelFactory.CreateLaptop(donorBuiltItem, laptopSource);
        try
        {
            FurnitureCreator.CreateBuilder()
                .WithBasicInfo(id, name, description)
                .WithModel(model)
                .WithPlacement(FurniturePlacementMode.Grid)
                .WithFootprint(4, 2)
                .WithBuildSound(BuildSoundType.Metal)
                .WithPricing(750f, 0.5f)
                .WithStackLimit(1)
                .WithGeneratedIcon(512)
                .Build();

            MelonLogger.Msg($"[{Constants.ModName}] Registered '{id}' from runtime native visuals.");
        }
        finally
        {
            Object.Destroy(model);
        }
    }

    private static GameObject? FindNativeLaptop()
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!string.Equals(candidate.name, "Laptop", StringComparison.Ordinal) ||
                candidate.parent == null || candidate.parent.name != "ConferenceTable" ||
                candidate.parent.parent == null || candidate.parent.parent.name != "Props" ||
                candidate.parent.parent.parent == null || candidate.parent.parent.parent.name != "Container" ||
                candidate.parent.parent.parent.parent == null ||
                candidate.parent.parent.parent.parent.name != "BusinessmenCamp")
                continue;

            if (candidate.GetComponentsInChildren<Renderer>(true).Length > 0)
                return candidate.gameObject;
        }

        return null;
    }

    internal static void AddToShops()
    {
        if (_shopsAdded || !_registered)
            return;

        try
        {
            bool desktopReady = AddItemToShops(Constants.ItemId);
            bool laptopReady = !_laptopRegistered || AddItemToShops(Constants.LaptopItemId);
            _shopsAdded = desktopReady && laptopReady;

            if (_shopsAdded)
            {
                MelonLogger.Msg(
                    $"[{Constants.ModName}] Added the available computers to both hardware shops.");
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

    private static bool AddItemToShops(string id)
    {
        S1API.Items.ItemDefinition? item = S1API.Items.ItemManager.GetDefinition(id);
        if (item == null)
        {
            MelonLogger.Warning($"[{Constants.ModName}] Registered item '{id}' could not be resolved for shops.");
            return false;
        }

        ShopManager.AddToShops(item, Constants.HardwareShop, Constants.DanHardwareShop);
        return ShopManager.GetShopByName(Constants.HardwareShop)?.HasItem(id) == true &&
               ShopManager.GetShopByName(Constants.DanHardwareShop)?.HasItem(id) == true;
    }
}

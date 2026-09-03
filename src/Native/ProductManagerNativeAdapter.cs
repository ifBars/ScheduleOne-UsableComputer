using System;
using System.Collections.Generic;
using UsableComputer.Logic;
using UnityEngine;

#if IL2CPPMELON
using S1ProductDefinition = Il2CppScheduleOne.Product.ProductDefinition;
using S1ProductManager = Il2CppScheduleOne.Product.ProductManager;
#elif MONOMELON
using S1ProductDefinition = ScheduleOne.Product.ProductDefinition;
using S1ProductManager = ScheduleOne.Product.ProductManager;
#endif

namespace UsableComputer.Native;

/// <summary>
/// Narrow direct port of the native discovered/listed product surface.
/// </summary>
internal static class ProductManagerNativeAdapter
{
    internal static List<ProductViewModel> ReadDiscoveredProducts()
    {
        var result = new List<ProductViewModel>();
        try
        {
            var discoveredProducts = S1ProductManager.DiscoveredProducts;
            if (discoveredProducts == null)
                return result;

            var listedProducts = S1ProductManager.ListedProducts;
            var favouritedProducts = S1ProductManager.FavouritedProducts;
            foreach (S1ProductDefinition product in discoveredProducts)
            {
                if (product == null || string.IsNullOrWhiteSpace(product.ID))
                    continue;

                bool listed = false;
                if (listedProducts != null)
                {
                    foreach (S1ProductDefinition listedProduct in listedProducts)
                    {
                        if (listedProduct != null &&
                            string.Equals(listedProduct.ID, product.ID, StringComparison.Ordinal))
                        {
                            listed = true;
                            break;
                        }
                    }
                }

                bool favourited = false;
                if (favouritedProducts != null)
                {
                    foreach (S1ProductDefinition favouritedProduct in favouritedProducts)
                    {
                        if (favouritedProduct != null &&
                            string.Equals(favouritedProduct.ID, product.ID, StringComparison.Ordinal))
                        {
                            favourited = true;
                            break;
                        }
                    }
                }

                string productType = "Product";
                if (product.DrugTypes != null && product.DrugTypes.Count > 0)
                    productType = product.DrugTypes[0].DrugType.ToString();

                result.Add(new ProductViewModel(
                    product.ID,
                    product.Name,
                    productType,
                    product.MarketValue,
                    listed,
                    favourited));
            }
        }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Warning(
                $"[{Constants.ModName}] Product Manager read unavailable: {exception.Message}");
        }

        result.Sort((left, right) =>
        {
            int nameOrder = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return nameOrder != 0
                ? nameOrder
                : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    internal static Sprite? GetIcon(string productId)
    {
        try
        {
            var discoveredProducts = S1ProductManager.DiscoveredProducts;
            if (discoveredProducts == null)
                return null;

            foreach (S1ProductDefinition product in discoveredProducts)
            {
                if (product != null && string.Equals(product.ID, productId, StringComparison.Ordinal))
                    return product.Icon;
            }
        }
        catch
        {
        }

        return null;
    }

    internal static bool TrySetListed(string productId, bool listed, out string message)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            message = "Select a product first.";
            return false;
        }

        try
        {
            if (!S1ProductManager.InstanceExists || S1ProductManager.Instance == null)
            {
                message = "The native Product Manager service is unavailable.";
                return false;
            }

            S1ProductManager.Instance.SetProductListed(productId, listed);
            message = listed ? "Product listed." : "Product unlisted.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Native product listing failed: {exception.Message}";
            return false;
        }
    }

    internal static bool TrySetFavourited(string productId, bool favourited, out string message)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            message = "Select a product first.";
            return false;
        }

        try
        {
            if (!S1ProductManager.InstanceExists || S1ProductManager.Instance == null)
            {
                message = "The native Product Manager service is unavailable.";
                return false;
            }

            S1ProductManager.Instance.SetProductFavourited(productId, favourited);
            message = favourited ? "Product added to favourites." : "Product removed from favourites.";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Native product favourite update failed: {exception.Message}";
            return false;
        }
    }

}

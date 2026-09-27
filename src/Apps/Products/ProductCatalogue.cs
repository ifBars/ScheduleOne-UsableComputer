using System;
using System.Collections.Generic;
using UsableComputer.Native;

namespace UsableComputer.Apps.Products;

internal enum ProductFilter { All, Listed, Unlisted, Favourites }
internal enum ProductSort { Name, ValueHigh, ValueLow }

internal static class ProductCatalogue
{
    internal static List<ProductViewModel> Select(IEnumerable<ProductViewModel> products, string search,
        ProductFilter filter, string? type, ProductSort sort)
    {
        string query = search.Trim();
        var result = new List<ProductViewModel>();
        foreach (ProductViewModel product in products)
        {
            if (query.Length > 0 && product.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                product.ProductType.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (type != null && !string.Equals(type, product.ProductType, StringComparison.OrdinalIgnoreCase)) continue;
            if (filter == ProductFilter.Listed && !product.IsListed ||
                filter == ProductFilter.Unlisted && product.IsListed ||
                filter == ProductFilter.Favourites && !product.IsFavourited) continue;
            result.Add(product);
        }
        result.Sort((left, right) =>
        {
            int order = sort == ProductSort.ValueHigh ? right.MarketValue.CompareTo(left.MarketValue) :
                sort == ProductSort.ValueLow ? left.MarketValue.CompareTo(right.MarketValue) : 0;
            if (order == 0) order = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        });
        return result;
    }
}

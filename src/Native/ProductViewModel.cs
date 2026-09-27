using System.Globalization;

namespace UsableComputer.Native;

internal sealed class ProductViewModel
{
    internal ProductViewModel(
        string id,
        string name,
        string productType,
        float marketValue,
        bool isListed,
        bool isFavourited)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        ProductType = string.IsNullOrWhiteSpace(productType) ? "Product" : productType;
        MarketValue = marketValue;
        IsListed = isListed;
        IsFavourited = isFavourited;
    }

    internal string Id { get; }

    internal string Name { get; }

    internal string ProductType { get; }

    internal float MarketValue { get; }

    internal bool IsListed { get; }

    internal bool IsFavourited { get; }

    internal string ValueLabel => MarketValue.ToString("0.##", CultureInfo.InvariantCulture);
}

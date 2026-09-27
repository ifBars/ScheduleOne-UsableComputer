using System.Collections.Generic;
using UnityEngine;

namespace UsableComputer.Apps.Dealers;

internal sealed class DealerViewModel
{
    internal string Id { get; set; } = "";
    internal string Name { get; set; } = "";
    internal string Region { get; set; } = "";
    internal string Home { get; set; } = "";
    internal float Cash { get; set; }
    internal float Cut { get; set; }
    internal Sprite? Portrait { get; set; }
    internal List<DealerStockViewModel> Stock { get; } = new();
    internal List<DealerCustomerViewModel> Customers { get; } = new();
}

internal sealed class DealerStockViewModel
{
    internal string Name { get; set; } = "";
    internal string Quality { get; set; } = "";
    internal long Quantity { get; set; }
    internal Sprite? Icon { get; set; }
}

internal sealed class DealerCustomerViewModel
{
    internal string Id { get; set; } = "";
    internal string Name { get; set; } = "";
    internal string Region { get; set; } = "";
    internal string Standards { get; set; } = "";
    internal Sprite? Portrait { get; set; }
}

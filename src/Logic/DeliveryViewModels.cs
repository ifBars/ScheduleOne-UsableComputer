using System.Collections.Generic;

namespace UsableComputer.Logic;

internal sealed class DeliveryViewModel
{
    internal string Id { get; set; } = string.Empty;
    internal string Store { get; set; } = string.Empty;
    internal string Destination { get; set; } = string.Empty;
    internal int Dock { get; set; }
    internal bool IsActive { get; set; }
    internal string Status { get; set; } = string.Empty;
    internal List<DeliveryItemViewModel> Items { get; set; } = new();
}

internal sealed class DeliveryItemViewModel
{
    internal string Name { get; set; } = string.Empty;
    internal int Quantity { get; set; }
}

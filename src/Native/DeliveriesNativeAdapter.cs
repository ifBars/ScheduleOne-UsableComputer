using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.Logic;

#if IL2CPPMELON
using S1DeliveryManager = Il2CppScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = Il2CppScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = Il2CppScheduleOne.Delivery.DeliveryReceipt;
using S1PropertyManager = Il2CppScheduleOne.Property.PropertyManager;
using S1Registry = Il2CppScheduleOne.Registry;
using S1Status = Il2CppScheduleOne.Delivery.EDeliveryStatus;
#else
using S1DeliveryManager = ScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = ScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = ScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = ScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = ScheduleOne.Delivery.DeliveryReceipt;
using S1PropertyManager = ScheduleOne.Property.PropertyManager;
using S1Registry = ScheduleOne.Registry;
using S1Status = ScheduleOne.Delivery.EDeliveryStatus;
#endif

namespace UsableComputer.Native;

/// <summary>Reads native delivery records and delegates confirmed repeats to the existing order flow.</summary>
internal sealed class DeliveriesNativeAdapter
{
    private readonly Dictionary<string, (S1DeliveryShop Shop, S1ListingEntry[] Entries)> _shops = new();
    private readonly HashSet<string> _submitted = new(StringComparer.Ordinal);

    internal bool TryRead(out List<DeliveryViewModel> orders, out string message)
    {
        orders = new List<DeliveryViewModel>();
        message = string.Empty;
        try
        {
            var manager = S1DeliveryManager.Instance;
            if (manager == null || S1PropertyManager.Instance == null)
            {
                message = "Deliveries will appear when the game is ready.";
                return false;
            }
            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            var activeStores = new HashSet<string>(StringComparer.Ordinal);
            foreach (var delivery in manager.Deliveries)
            {
                if (delivery == null || string.IsNullOrEmpty(delivery.DeliveryID))
                    continue;
                activeIds.Add(delivery.DeliveryID);
                activeStores.Add(delivery.StoreName);
                DeliveryViewModel order = ReadReceipt(delivery.GetReceipt());
                order.IsActive = true;
                order.Status = delivery.Status switch
                {
                    S1Status.Arrived => "Arrived - unload the vehicle",
                    S1Status.Waiting => "Waiting for a loading dock",
                    S1Status.Completed => "Completed",
                    _ => $"In transit - {Math.Max(0, delivery.TimeUntilArrival)} min",
                };
                orders.Add(order);
            }
            var history = manager.DisplayedDeliveryHistory;
            for (int index = history.Count - 1; index >= 0; index--)
            {
                var receipt = history[index];
                if (receipt != null && activeStores.Contains(receipt.StoreName))
                    _submitted.Remove(receipt.DeliveryID);
                if (receipt == null || string.IsNullOrEmpty(receipt.DeliveryID) || activeIds.Contains(receipt.DeliveryID))
                    continue;
                DeliveryViewModel order = ReadReceipt(receipt);
                order.Status = "Previous order";
                orders.Add(order);
            }
            return true;
        }
        catch (Exception)
        {
            message = "Delivery data is temporarily unavailable.";
            return false;
        }
    }

    internal bool TryQuote(string receiptId, out decimal cost, out string reason)
    {
        cost = 0;
        reason = "This previous order is no longer available.";
        try
        {
            S1Receipt? receipt = FindReceipt(receiptId);
            var app = S1DeliveryApp.Instance;
            if (receipt == null || app == null)
                return false;
            if (_submitted.Contains(receiptId))
            {
                reason = "Reorder requested. Check active deliveries.";
                return false;
            }
            S1DeliveryShop shop = app.GetShop(receipt.StoreName);
            if (shop == null || shop.MatchingShop == null || shop.ListingContainer == null)
                return false;
            var property = S1PropertyManager.Instance.GetProperty(receipt.DestinationCode);
            if (property == null || !property.IsOwned || !property.CanDeliverToProperty() || receipt.LoadingDockIndex < 0 ||
                receipt.LoadingDockIndex >= property.LoadingDockCount)
            {
                reason = "The original destination or loading dock is unavailable.";
                return false;
            }
            if (!_shops.TryGetValue(receipt.StoreName, out var cached) || cached.Shop != shop)
            {
                var entries = new List<S1ListingEntry>();
                foreach (var entry in shop.ListingContainer.GetComponentsInChildren<S1ListingEntry>(true))
                    entries.Add(entry);
                cached = (shop, entries.ToArray());
                _shops[receipt.StoreName] = cached;
            }
            if (cached.Entries.Any(entry => entry != null && entry.SelectedQuantity != 0))
            {
                reason = "Finish or clear this shop's phone cart before reordering.";
                return false;
            }
            if (receipt.Items == null || receipt.Items.Length == 0)
                return false;
            long stacks = 0;
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in receipt.Items)
            {
                var entry = cached.Entries.FirstOrDefault(candidate => candidate != null && candidate.MatchingListing != null &&
                    candidate.MatchingListing.Item != null && candidate.MatchingListing.Item.ID == item.String);
                if (entry == null || item.Int <= 0 || item.Int > 999 || !itemIds.Add(item.String) ||
                    !entry.MatchingListing.Item.IsUnlocked || !entry.MatchingListing.CanBeDelivered ||
                    !entry.MatchingListing.ShouldShow() || entry.MatchingListing.Item.StackLimit <= 0)
                {
                    reason = "Some items are unavailable. Create a new order on your phone.";
                    return false;
                }
                stacks += ((long)item.Int + entry.MatchingListing.Item.StackLimit - 1) / entry.MatchingListing.Item.StackLimit;
            }
            if (shop.MatchingShop.DeliveryVehicle == null ||
                stacks > shop.MatchingShop.DeliveryVehicle.Vehicle.Storage.SlotCount)
            {
                reason = "This order will not fit in the delivery vehicle.";
                return false;
            }
            float nativeCost = app.GetDeliveryCost(receipt);
            if (float.IsNaN(nativeCost) || float.IsInfinity(nativeCost) || nativeCost <= 0 || nativeCost > 1_000_000_000f)
            {
                reason = "The current delivery price is unavailable.";
                return false;
            }
            cost = Math.Round((decimal)nativeCost, 2, MidpointRounding.AwayFromZero);
            return app.CanReorder(receipt, out reason);
        }
        catch (Exception)
        {
            reason = "This order cannot be repeated right now.";
            return false;
        }
    }

    internal bool TryReorder(string receiptId, decimal quotedCost, out string message)
    {
        if (!TryQuote(receiptId, out decimal currentCost, out message))
            return false;
        if (quotedCost != currentCost)
        {
            message = "The price changed. Review the new total before ordering.";
            return false;
        }
        S1Receipt? receipt = FindReceipt(receiptId);
        if (receipt == null)
        {
            message = "This previous order is no longer available.";
            return false;
        }
        _submitted.Add(receiptId);
        try
        {
            S1DeliveryApp.Instance.Reorder(receipt);
            message = "Reorder requested through the delivery service.";
            return true;
        }
        catch (Exception exception)
        {
            message = "Could not confirm the reorder. Check active deliveries before trying again.";
            MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] Delivery reorder returned an error: {exception.Message}");
            return false;
        }
    }

    private static S1Receipt? FindReceipt(string id)
    {
        var manager = S1DeliveryManager.Instance;
        if (manager == null || string.IsNullOrEmpty(id))
            return null;
        foreach (var delivery in manager.Deliveries)
            if (delivery.DeliveryID == id)
                return null;
        foreach (var receipt in manager.DisplayedDeliveryHistory)
            if (receipt != null && receipt.DeliveryID == id)
                return receipt;
        return null;
    }

    private static DeliveryViewModel ReadReceipt(S1Receipt receipt)
    {
        var property = S1PropertyManager.Instance.GetProperty(receipt.DestinationCode);
        var result = new DeliveryViewModel
        {
            Id = receipt.DeliveryID,
            Store = receipt.StoreName ?? "Unknown store",
            Destination = property != null ? property.PropertyName : "Unavailable destination",
            Dock = receipt.LoadingDockIndex + 1,
        };
        if (receipt.Items != null)
        {
            foreach (var item in receipt.Items)
            {
                var definition = S1Registry.GetItem(item.String);
                result.Items.Add(new DeliveryItemViewModel { Name = definition != null ? definition.Name : "Unavailable item", Quantity = item.Int });
            }
        }
        return result;
    }
}

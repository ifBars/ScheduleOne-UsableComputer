using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.Logic;

#if IL2CPPMELON
using Il2CppInterop.Runtime;
using Il2CppScheduleOne.Economy;
using S1Dealer = Il2CppScheduleOne.Economy.Dealer;
using S1Customer = Il2CppScheduleOne.Economy.Customer;
using S1Product = Il2CppScheduleOne.Product.ProductItemInstance;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
#else
using ScheduleOne.Economy;
using S1Dealer = ScheduleOne.Economy.Dealer;
using S1Customer = ScheduleOne.Economy.Customer;
using S1Product = ScheduleOne.Product.ProductItemInstance;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
#endif

namespace UsableComputer.Native;

internal sealed class DealersNativeAdapter
{
    internal static int MaximumCustomers => S1Dealer.MAX_CUSTOMERS;
    private readonly Dictionary<string, string?> _pending = new(StringComparer.Ordinal);

    private static bool Ready => S1LoadManager.Instance != null && S1LoadManager.Instance.IsGameLoaded && !S1LoadManager.Instance.IsLoading;

    internal bool TryRead(out List<DealerViewModel> dealers, out List<DealerCustomerViewModel> unassigned, out string message)
    {
        dealers = new();
        unassigned = new();
        message = "";
        try
        {
            if (!Ready) { message = "Dealers will appear when the game is ready."; return false; }
            foreach (var customer in S1Customer.UnlockedCustomers)
            {
                if (customer == null || customer.NPC == null) continue;
                if (_pending.TryGetValue(customer.NPC.ID, out string? expected) &&
                    (customer.AssignedDealer == null ? expected == null : customer.AssignedDealer.ID == expected))
                    _pending.Remove(customer.NPC.ID);
                if (customer.AssignedDealer == null) unassigned.Add(ReadCustomer(customer));
            }
            foreach (var dealer in S1Dealer.AllPlayerDealers)
            {
                if (dealer == null || !dealer.IsRecruited) continue;
                var view = new DealerViewModel
                {
                    Id = dealer.ID, Name = dealer.FullName, Region = dealer.Region.ToString(),
                    Cash = dealer.Cash, Cut = dealer.DealerData.SalesCutPercentage,
                    Home = dealer.DealerData.HomeName, Portrait = dealer.MugshotSprite,
                };
                var stock = new Dictionary<(string, string), DealerStockViewModel>();
                foreach (var slot in dealer.GetAllSlots())
                {
                    if (slot == null || slot.ItemInstance == null || slot.Quantity <= 0) continue;
                    var item = slot.ItemInstance;
#if IL2CPPMELON
                    S1Product? product = item.TryCast<S1Product>();
#else
                    S1Product? product = item as S1Product;
#endif
                    string quality = product == null ? "" : product.Quality.ToString();
                    var key = (item.ID, quality);
                    if (!stock.TryGetValue(key, out DealerStockViewModel? entry))
                    {
                        entry = new DealerStockViewModel { Name = item.Definition.Name, Quality = quality, Icon = item.Definition.Icon };
                        stock.Add(key, entry);
                    }
                    entry.Quantity += (long)slot.Quantity * (product == null ? 1 : product.Amount);
                }
                view.Stock.AddRange(stock.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Quality));
                foreach (var customer in dealer.AssignedCustomers)
                    if (customer != null && customer.NPC != null) view.Customers.Add(ReadCustomer(customer));
                view.Customers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                dealers.Add(view);
            }
            dealers = dealers.OrderBy(dealer => dealer.Region).ThenBy(dealer => dealer.Name, StringComparer.OrdinalIgnoreCase).ToList();
            unassigned.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        catch (Exception)
        {
            dealers.Clear(); unassigned.Clear();
            message = "Dealer data is temporarily unavailable. Reopen the app after the game finishes loading.";
            return false;
        }
    }

    internal bool TryChangeCustomer(string dealerId, string customerId, bool assign, out string message)
    {
        message = "";
        try
        {
            if (!Ready) { message = "Wait for the game to finish loading."; return false; }
            S1Dealer? dealer = null;
            foreach (var candidate in S1Dealer.AllPlayerDealers)
                if (candidate != null && candidate.ID == dealerId && candidate.IsRecruited) dealer = candidate;
            S1Customer? customer = null;
            foreach (var candidate in S1Customer.UnlockedCustomers)
                if (candidate != null && candidate.NPC != null && candidate.NPC.ID == customerId) customer = candidate;
            if (dealer == null || customer == null) { message = "That dealer or customer is no longer available."; return false; }
            if (_pending.ContainsKey(customerId)) { message = "Waiting for the previous customer change. Reopen the app if the connection was interrupted."; return false; }
            if (assign && (customer.AssignedDealer != null || dealer.AssignedCustomers.Count >= MaximumCustomers))
            { message = "The customer is already assigned or this dealer is full."; return false; }
            if (!assign && (customer.AssignedDealer == null || customer.AssignedDealer.ID != dealerId))
            { message = "This customer is no longer assigned to that dealer."; return false; }
            _pending[customerId] = assign ? dealerId : null;
            if (assign)
            {
                dealer.AddCustomer_Server(customerId);
                if (customer.OfferedContractInfo != null) customer.ExpireOffer();
            }
            else dealer.SendRemoveCustomer(customerId);
            message = assign ? "Customer assignment requested." : "Customer removal requested.";
            return true;
        }
        catch (Exception)
        {
            message = "Could not confirm the customer change. Check the dealer before trying again.";
            return false;
        }
    }

    private static DealerCustomerViewModel ReadCustomer(S1Customer customer) => new()
    {
        Id = customer.NPC.ID, Name = customer.NPC.FullName, Region = customer.NPC.Region.ToString(),
        Standards = customer.CustomerData.Standards.GetName(), Portrait = customer.NPC.MugshotSprite,
    };
}

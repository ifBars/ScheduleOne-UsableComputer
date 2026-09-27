using System;
using UnityEngine;

#if IL2CPPMELON
using S1JournalApp = Il2CppScheduleOne.UI.Phone.JournalApp;
using S1DeliveryApp = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DealerApp = Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp;
using S1ProductManagerApp = Il2CppScheduleOne.UI.Phone.ProductManagerApp.ProductManagerApp;
#elif MONOMELON
using S1JournalApp = ScheduleOne.UI.Phone.JournalApp;
using S1DeliveryApp = ScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DealerApp = ScheduleOne.UI.Phone.Messages.DealerManagementApp;
using S1ProductManagerApp = ScheduleOne.UI.Phone.ProductManagerApp.ProductManagerApp;
#endif

namespace UsableComputer.Native;

/// <summary>
/// Resolves game-owned phone artwork from the live player UI. No native assets are copied or packaged.
/// </summary>
internal static class NativePhoneAppAssets
{
    internal static Sprite? GetJournalIcon() => Resolve(() => S1JournalApp.Instance.AppIcon);
    internal static Sprite? GetDeliveryIcon() => Resolve(() => S1DeliveryApp.Instance.AppIcon);
    internal static Sprite? GetDealerIcon() => Resolve(() => S1DealerApp.Instance.AppIcon);

    internal static Sprite? GetProductManagerIcon() => Resolve(() => S1ProductManagerApp.Instance.AppIcon);

    private static Sprite? Resolve(Func<Sprite?> resolver)
    {
        try
        {
            return resolver();
        }
        catch
        {
            return null;
        }
    }
}

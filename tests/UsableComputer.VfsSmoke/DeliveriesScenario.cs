using System.Collections;
using System.Reflection;
using UnityEngine;

#if IL2CPP
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using S1DeliveryManager = Il2CppScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = Il2CppScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = Il2CppScheduleOne.Delivery.DeliveryReceipt;
using S1Property = Il2CppScheduleOne.Property.Property;
using S1ItemPair = Il2CppScheduleOne.DevUtilities.StringIntPair;
using S1MoneyManager = Il2CppScheduleOne.Money.MoneyManager;
#else
using S1Text = TMPro.TextMeshProUGUI;
using S1DeliveryManager = ScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = ScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = ScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = ScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = ScheduleOne.Delivery.DeliveryReceipt;
using S1Property = ScheduleOne.Property.Property;
using S1ItemPair = ScheduleOne.DevUtilities.StringIntPair;
using S1MoneyManager = ScheduleOne.Money.MoneyManager;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunDeliveriesScenario()
    {
        S1DeliveryShop? shop = null;
        S1ListingEntry? listing = null;
        S1Receipt? receipt = null;
        float startingBank = 0;
        float cost = 0;
        string storeFile = Path.Combine(Directory.GetParent(_outputDirectory)!.FullName, "delivery-store.txt");
        try
        {
            Require(S1DeliveryApp.Instance != null && S1DeliveryManager.Instance != null, "Native delivery services unavailable.");
            if (_phase == "seed")
            {
                foreach (var candidate in S1DeliveryApp.Instance!.GetComponentsInChildren<S1DeliveryShop>(true))
                {
                    if (candidate.MatchingShop == null || candidate.HasActiveDelivery()) continue;
                    foreach (var item in candidate.ListingContainer.GetComponentsInChildren<S1ListingEntry>(true))
                    {
                        if (item.MatchingListing.Item.IsUnlocked && item.MatchingListing.ShouldShow() &&
                            item.MatchingListing.CanBeDelivered && item.MatchingListing.Price > 0)
                        {
                            shop = candidate;
                            listing = item;
                            break;
                        }
                    }
                    if (shop != null) break;
                }
                Require(shop != null && listing != null, "No unlocked delivery listing was available in the fixture.");
                S1Property? destination = null;
                foreach (var property in S1Property.OwnedProperties)
                    if (property.CanDeliverToProperty() && property.LoadingDockCount > 0) { destination = property; break; }
                Require(destination != null, "No deliverable owned property in the fixture.");
                receipt = new S1Receipt(Guid.NewGuid().ToString(), shop!.MatchingShopInterfaceName,
                    destination!.PropertyCode, 0, new[] { new S1ItemPair(listing!.MatchingListing.Item.ID, 1) });
                S1DeliveryManager.Instance!.RecordDeliveryReceipt_Server(receipt);
                File.WriteAllText(storeFile, shop.MatchingShopInterfaceName);
                startingBank = S1MoneyManager.Instance.sync___get_value_onlineBalance();
                cost = S1DeliveryApp.Instance.GetDeliveryCost(receipt);
                Require(cost > 0 && cost < startingBank, "Fixture cannot afford the native quote.");
                listing.SetQuantity(1);
            }
            else
            {
                string store = File.ReadAllText(storeFile);
                shop = S1DeliveryApp.Instance!.GetShop(store);
                Require(shop != null && S1DeliveryManager.Instance!.GetActiveShopDelivery(shop) != null,
                    "The reordered native delivery did not survive process reload.");
            }
            CloseAllWindows();
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_desktop, new object[] { "deliveries" });
            RequireWindow("deliveries");
        }
        catch (Exception exception)
        {
            Fail("Delivery setup failed", Unwrap(exception));
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);

        if (_phase == "seed")
        {
            try
            {
                ClickDelivery("DeliveryHistoryTab");
                Require(!GameObject.Find("DeliveryReorder").GetComponent<UnityEngine.UI.Button>().interactable,
                    "Reorder ignored an existing phone cart.");
                Require(GameObject.Find("DeliveryPrice").GetComponent<S1Text>().text.Contains("phone cart"),
                    "Cart conflict was not explained.");
                Require(listing!.SelectedQuantity == 1, "Reading reports altered the phone cart.");
                listing.SetQuantity(0);
            }
            catch (Exception exception)
            {
                Fail("Delivery cart isolation failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.2f);
            try
            {
                Require(GameObject.Find("DeliveryReorder").GetComponent<UnityEngine.UI.Button>().interactable,
                    "Valid native receipt could not be reordered: " + GameObject.Find("DeliveryPrice").GetComponent<S1Text>().text);
                ClickDelivery("DeliveryReorder");
                Require(GameObject.Find("DeliveryReorder").GetComponentInChildren<S1Text>().text.StartsWith("Confirm"), "Reorder skipped confirmation.");
                Require(S1MoneyManager.Instance.sync___get_value_onlineBalance() == startingBank, "First click charged money.");
            }
            catch (Exception exception)
            {
                Fail("Delivery confirmation preview failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            string confirmationPath = Path.Combine(_outputDirectory, "delivery-confirmation.png");
            ScreenCapture.CaptureScreenshot(confirmationPath);
            yield return WaitForCapture(confirmationPath);
            if (_completed) yield break;
            try
            {
                ClickDelivery("DeliveryCancel");
                Require(S1MoneyManager.Instance.sync___get_value_onlineBalance() == startingBank, "Cancel charged money.");
                ClickDelivery("DeliveryReorder");
                ClickDelivery("DeliveryReorder");
            }
            catch (Exception exception)
            {
                Fail("Delivery confirmation failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            try
            {
                var active = S1DeliveryManager.Instance!.GetActiveShopDelivery(shop);
                Require(active != null && active.Items.Length == 1 && active.Items[0].Int == 1 &&
                    active.Items[0].String == listing!.MatchingListing.Item.ID, "Reorder changed the receipt items.");
                Require(active!.DestinationCode == receipt!.DestinationCode && active.LoadingDockIndex == receipt.LoadingDockIndex,
                    "Reorder changed destination or dock.");
                Require(Math.Abs(S1MoneyManager.Instance.sync___get_value_onlineBalance() - (startingBank - cost)) < 0.02f,
                    "Reorder did not charge the quoted amount exactly once.");
                Require(listing!.SelectedQuantity == 0, "Reorder left items in the phone cart.");
                ClickDelivery("DeliveryReorder");
                ClickDelivery("DeliveryReorder");
                Require(Math.Abs(S1MoneyManager.Instance.sync___get_value_onlineBalance() - (startingBank - cost)) < 0.02f,
                    "Repeated input created another charge.");
            }
            catch (Exception exception)
            {
                Fail("Native delivery result failed", Unwrap(exception));
                yield break;
            }
        }

        foreach (string theme in new[] { "Light", "Dark" })
        {
            try
            {
                OpenSettings();
                ClickChoice(theme);
                CloseAllWindows();
                _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(_desktop, new object[] { "deliveries" });
                RequireWindow("deliveries");
            }
            catch (Exception exception)
            {
                Fail("Delivery theme failed", Unwrap(exception));
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            string path = Path.Combine(_outputDirectory, $"deliveries-{theme}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitForCapture(path);
            if (_completed) yield break;
        }
        LoggerInstance.Msg($"[UsableComputerDeliveriesSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} CartIsolation=True Confirmation=True SingleCharge=True NativeOrder=True Themes=2");
    }

    private static void ClickDelivery(string name) =>
        (GameObject.Find(name) ?? throw new InvalidOperationException("Missing delivery control: " + name))
        .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
}

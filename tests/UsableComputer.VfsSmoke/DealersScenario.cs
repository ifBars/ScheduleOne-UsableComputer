using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UsableComputer.Tests.Shared;

#if IL2CPP
using S1Dealer = Il2CppScheduleOne.Economy.Dealer;
using S1Customer = Il2CppScheduleOne.Economy.Customer;
using S1DealerApp = Il2CppScheduleOne.UI.Phone.Messages.DealerManagementApp;
#else
using S1Dealer = ScheduleOne.Economy.Dealer;
using S1Customer = ScheduleOne.Economy.Customer;
using S1DealerApp = ScheduleOne.UI.Phone.Messages.DealerManagementApp;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunDealersScenario()
    {
        S1Dealer dealer = null!;
        S1Customer customer = null!;
        Transform phoneParent = null!;
        S1Dealer? phoneSelection = null;
        try
        {
            if (_phase == "seed")
            {
                foreach (var candidate in S1Dealer.AllPlayerDealers)
                    if (candidate != null && candidate.IsRecruited && candidate.AssignedCustomers.Count < S1Dealer.MAX_CUSTOMERS)
                    { dealer = candidate; break; }
                Require(dealer != null, "Fixture has no recruited dealer with customer capacity.");
                foreach (var candidate in S1Customer.UnlockedCustomers)
                    if (candidate != null && candidate.AssignedDealer == null) { customer = candidate; break; }
                Require(customer != null, "Fixture has no unlocked unassigned customer.");
                Invoke(GetServiceType(), "CreateTextFile", "desktop", "Dealer fixture.txt", dealer!.ID + "\n" + customer!.NPC.ID);
            }
            else
            {
                object marker = ((IEnumerable)Invoke(GetServiceType(), "GetChildren", "desktop")!).Cast<object>()
                    .Single(entry => ReadString(entry, "Name") == "Dealer fixture.txt");
                string[] ids = ((string)Invoke(GetServiceType(), "ReadText", ReadString(marker, "Id"))!).Split('\n');
                foreach (var candidate in S1Dealer.AllPlayerDealers) if (candidate != null && candidate.ID == ids[0]) dealer = candidate;
                foreach (var candidate in S1Customer.UnlockedCustomers) if (candidate != null && candidate.NPC.ID == ids[1]) customer = candidate;
                Require(dealer != null && customer != null && customer.AssignedDealer == dealer, "Customer assignment did not survive process reload.");
            }
            phoneParent = S1DealerApp.Instance.transform.parent;
            phoneSelection = S1DealerApp.Instance.SelectedDealer;
            CloseAllWindows();
            Type preferences = GetUsableComputerAssembly().GetType(ModTypeNames.PreferencesStore, true)!;
            MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, _phase == "seed" ? "Light" : "Dark") });
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "dealers" });
            object session = GetAppSession("dealers");
            session.GetType().GetField("_selectedId", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, dealer!.ID);
            session.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null);
            Require(GameObject.Find("DealerName") != null, "Dealers app did not render.");
        }
        catch (Exception exception) { Fail("Dealers setup failed", Unwrap(exception)); yield break; }
        yield return null;
        try
        {
            DealerClick(_phase == "seed" ? "DealerTab_2" : "DealerTab_1");
        }
        catch (Exception exception) { Fail("Dealers tab failed", Unwrap(exception)); yield break; }
        yield return null;
        try
        {
            string button = "DealerCustomer_" + customer!.NPC.ID;
            DealerClick(button);
            Require((_phase == "seed" && customer.AssignedDealer == null) || (_phase != "seed" && customer.AssignedDealer == dealer), "First click changed native assignment.");
            DealerClick("DealerCancel");
            Require((_phase == "seed" && customer.AssignedDealer == null) || (_phase != "seed" && customer.AssignedDealer == dealer), "Cancel changed native assignment.");
        }
        catch (Exception exception) { Fail("Dealers confirmation failed", Unwrap(exception)); yield break; }
        yield return null;
        try { DealerClick("DealerCustomer_" + customer.NPC.ID); }
        catch (Exception exception) { Fail("Dealers confirmation reopen failed", Unwrap(exception)); yield break; }
        yield return null;
        string confirmShot = Path.Combine(_outputDirectory, "dealers-confirmation.png");
        ScreenCapture.CaptureScreenshot(confirmShot); yield return WaitForCapture(confirmShot);
        if (_completed) yield break;
        try { DealerClick("DealerCustomer_" + customer.NPC.ID); }
        catch (Exception exception) { Fail("Dealers submit failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(1.2f);
        try
        {
            Require(_phase == "seed" ? customer.AssignedDealer == dealer : customer.AssignedDealer == null, "Native customer change was not applied.");
            int matches = 0;
            foreach (var assigned in dealer.AssignedCustomers) if (assigned.NPC.ID == customer.NPC.ID) matches++;
            Require(matches == (_phase == "seed" ? 1 : 0), "Dealer customer collection is inconsistent.");
            Require(S1DealerApp.Instance.transform.parent == phoneParent && S1DealerApp.Instance.SelectedDealer == phoneSelection, "Desktop mutated native phone UI.");
            DealerClick("DealerTab_0");
        }
        catch (Exception exception) { Fail("Dealers native result failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.3f);
        string stockShot = Path.Combine(_outputDirectory, "dealers-stock.png");
        ScreenCapture.CaptureScreenshot(stockShot); yield return WaitForCapture(stockShot);
        if (_completed) yield break;
        LoggerInstance.Msg($"[UsableComputerDealersSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Confirm=True Cancel=True NativeAssignment=True PhoneUnchanged=True");
    }

    private static void DealerClick(string name)
    {
        var target = GameObject.Find(name);
        Require(target != null, "Missing dealer control: " + name);
        Button button = target!.GetComponent<Button>();
        Require(button.interactable, "Dealer control is disabled: " + name);
        button.onClick.Invoke();
    }
}

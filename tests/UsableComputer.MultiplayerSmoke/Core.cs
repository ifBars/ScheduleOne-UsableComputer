using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime;
using S1DateTime = Il2CppSystem.DateTime;
using S1DateTimeData = Il2CppScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = Il2CppScheduleOne.Persistence.LoadManager;
using S1MetaData = Il2CppScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = Il2CppScheduleOne.Persistence.SaveInfo;
using S1Dealer = Il2CppScheduleOne.Economy.Dealer;
using S1Customer = Il2CppScheduleOne.Economy.Customer;
using S1DeliveryManager = Il2CppScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = Il2CppScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = Il2CppScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = Il2CppScheduleOne.Delivery.DeliveryReceipt;
using S1Property = Il2CppScheduleOne.Property.Property;
using S1ItemPair = Il2CppScheduleOne.DevUtilities.StringIntPair;
using S1MoneyManager = Il2CppScheduleOne.Money.MoneyManager;
using S1Lobby = Il2CppScheduleOne.Networking.Lobby;
using S1SteamLobbyService = Il2CppScheduleOne.Networking.SteamLobbyService;
#else
using S1DateTime = System.DateTime;
using S1DateTimeData = ScheduleOne.Persistence.Datas.DateTimeData;
using S1LoadManager = ScheduleOne.Persistence.LoadManager;
using S1MetaData = ScheduleOne.Persistence.Datas.MetaData;
using S1SaveInfo = ScheduleOne.Persistence.SaveInfo;
using S1Dealer = ScheduleOne.Economy.Dealer;
using S1Customer = ScheduleOne.Economy.Customer;
using S1DeliveryManager = ScheduleOne.Delivery.DeliveryManager;
using S1DeliveryApp = ScheduleOne.UI.Phone.Delivery.DeliveryApp;
using S1DeliveryShop = ScheduleOne.UI.Phone.Delivery.DeliveryShop;
using S1ListingEntry = ScheduleOne.UI.Phone.Delivery.ListingEntry;
using S1Receipt = ScheduleOne.Delivery.DeliveryReceipt;
using S1Property = ScheduleOne.Property.Property;
using S1ItemPair = ScheduleOne.DevUtilities.StringIntPair;
using S1MoneyManager = ScheduleOne.Money.MoneyManager;
using S1Lobby = ScheduleOne.Networking.Lobby;
using S1SteamLobbyService = ScheduleOne.Networking.SteamLobbyService;
#endif

[assembly: MelonInfo(typeof(UsableComputer.MultiplayerSmoke.Core), "Usable Computer Multiplayer Smoke", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UsableComputer.MultiplayerSmoke;

public sealed class Core : MelonMod
{
    private string _role = string.Empty;
    private string _root = string.Empty;
    private string _save = string.Empty;
    private bool _enabled;
    private bool _started;
    private bool _gameplayStarted;
    private bool _finished;

    public override void OnInitializeMelon()
    {
        string[] args = Environment.GetCommandLineArgs();
        _role = Arg(args, "--uc-mp-role");
        _root = Arg(args, "--uc-mp-root");
        _save = Arg(args, "--uc-mp-save");
        Instance = this;
        if (_role is not ("host" or "client") || string.IsNullOrWhiteSpace(_root) || string.IsNullOrWhiteSpace(_save))
            return;
        _enabled = true;
        Directory.CreateDirectory(_root);
        Write("boot", $"role={_role};runtime={RuntimeName};pid={System.Diagnostics.Process.GetCurrentProcess().Id}");
        LoggerInstance.Msg($"[UsableComputerMultiplayerSmoke] Enabled role={_role} runtime={RuntimeName} root={_root}");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!_enabled || _finished)
            return;
        if (sceneName == "Menu" && !_started)
        {
            _started = true;
            MelonCoroutines.Start(Guard(_role == "host" ? HostLobbyAndLoad() : ClientWaitForGame()));
        }
        else if (sceneName == "Main" && !_gameplayStarted)
        {
            _gameplayStarted = true;
            MelonCoroutines.Start(Guard(WaitForGameplay()));
        }
    }

    private IEnumerator HostLobbyAndLoad()
    {
        yield return WaitUntil(() => S1LoadManager.Instance != null && GetSteamLobbyService() != null, 30,
            "Host Steam lobby service did not initialize in Menu.");
        if (_finished) yield break;
        S1Lobby.Instance!.CreateLobby();
        yield return WaitUntil(() => S1Lobby.Instance != null && S1Lobby.Instance.IsInLobby &&
            GetNativeLobbyId() != 0, 120, "Host Steam lobby ID was not established.");
        if (_finished) yield break;
        Require(S1Lobby.Instance!.IsHost, "Lobby creator is not host.");
        Write("lobby", $"id={GetNativeLobbyId()};members={S1Lobby.Instance.GetLobbyMemberIDs().Count}");
        yield return WaitUntil(() => S1Lobby.Instance != null && S1Lobby.Instance.IsInLobby &&
            S1Lobby.Instance.PlayerCount >= 2 && S1Lobby.Instance.GetLobbyMemberIDs().Count >= 2, 120,
            "A second lobby member did not join before host load.");
        if (_finished) yield break;

        var load = S1LoadManager.Instance!;
        S1DateTime now = Now();
        var info = new S1SaveInfo(_save, -1, "Usable Computer Multiplayer Smoke", now, now, 0f,
            Application.version, new S1MetaData((S1DateTimeData?)null, (S1DateTimeData?)null,
                Application.version, Application.version, playTutorial: false));
        load.StartGame(info, allowLoadStacking: false, allowSaveBackup: false);
        Write("host-load-started", "true");
    }

    private IEnumerator ClientWaitForGame()
    {
        yield return WaitUntil(() => S1Lobby.Instance != null && S1Lobby.Instance.IsInLobby &&
            S1Lobby.Instance.PlayerCount >= 2 && S1Lobby.Instance.GetLobbyMemberIDs().Count >= 2, 150,
            "Client did not join the host lobby.");
        if (_finished) yield break;
        yield return WaitUntil(() => File.Exists(Path.Combine(_root, "host-load-started.txt")), 150,
            "Host did not start its save after lobby membership was established.");
    }

    private IEnumerator WaitForGameplay()
    {
        yield return WaitUntil(() => S1LoadManager.Instance != null && !S1LoadManager.Instance.IsLoading &&
            S1LoadManager.Instance.IsGameLoaded && S1LoadManager.Instance.IsInGameScene && S1Lobby.Instance != null &&
            S1Lobby.Instance.IsInLobby && S1Lobby.Instance.PlayerCount >= 2 && S1Lobby.Instance.GetLobbyMemberIDs().Count >= 2 &&
            S1MoneyManager.Instance != null,
            180, "Gameplay or two-peer network readiness did not arrive.");
        if (_finished) yield break;
        Require(S1Lobby.Instance!.IsHost == (_role == "host"), "Lobby role disagrees with the smoke role.");
        Require(S1Lobby.Instance.GetLobbyMemberIDs().Count >= 2, "Network membership was lost after loading.");
        Write("game-ready", $"scene=Main;loaded=true;host={S1Lobby.Instance.IsHost};members={S1Lobby.Instance.GetLobbyMemberIDs().Count}");
        if (_role == "host") yield return RunHostScenario();
        else yield return RunClientScenario();
    }

    private IEnumerator RunHostScenario()
    {
        string dealerId = string.Empty;
        string customerId = string.Empty;
        S1DeliveryShop? shop = null;
        S1ListingEntry? listing = null;
        S1Receipt? receipt = null;
        float startingBalance = 0;
        float price = 0;
            yield return WaitUntil(() => S1DeliveryApp.Instance != null && S1DeliveryManager.Instance != null, 60,
                "Native delivery services did not initialize.");
            if (_finished) yield break;
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "client-game-ready.txt")), 60,
                "Client gameplay was not ready before host delivery setup.");
            if (_finished) yield break;
            Require(SelectDealerAndCustomer(out dealerId, out customerId), "No recruited dealer with space and unlocked unassigned customer was found.");
            foreach (var candidate in S1DeliveryApp.Instance!.GetComponentsInChildren<S1DeliveryShop>(true))
            {
                if (candidate.MatchingShop == null || candidate.HasActiveDelivery()) continue;
                foreach (var entry in candidate.ListingContainer.GetComponentsInChildren<S1ListingEntry>(true))
                {
                    if (entry?.MatchingListing?.Item != null && entry.MatchingListing.Item.IsUnlocked &&
                        entry.MatchingListing.ShouldShow() && entry.MatchingListing.CanBeDelivered && entry.MatchingListing.Price > 0)
                    { shop = candidate; listing = entry; break; }
                }
                if (shop != null) break;
            }
            Require(shop != null && listing != null, "No available native delivery listing exists.");
            S1Property? property = null;
            foreach (var candidate in S1Property.OwnedProperties)
                if (candidate.CanDeliverToProperty() && candidate.LoadingDockCount > 0) { property = candidate; break; }
            Require(property != null, "No owned delivery destination is available.");
            receipt = new S1Receipt(Guid.NewGuid().ToString("N"), shop!.MatchingShopInterfaceName,
                property!.PropertyCode, 0, new[] { new S1ItemPair(listing!.MatchingListing.Item.ID, 1) });
            S1DeliveryManager.Instance!.RecordDeliveryReceipt_Server(receipt);
            startingBalance = S1MoneyManager.Instance!.sync___get_value_onlineBalance();
            price = S1DeliveryApp.Instance.GetDeliveryCost(receipt);
            Require(price > 0 && !float.IsNaN(price) && !float.IsInfinity(price) && price < 1_000_000_000f,
                $"Native quote invalid: price={price:R}, listing={listing.MatchingListing.Price:R}, balance={startingBalance:R}.");
            float fundedBalance = Math.Max(startingBalance, price + 1000f);
            if (fundedBalance > startingBalance)
                S1MoneyManager.Instance.CreateOnlineTransaction("Multiplayer smoke fixture", fundedBalance - startingBalance,
                    1f, "Disposable host test funds");
            yield return WaitUntil(() => Math.Abs(S1MoneyManager.Instance!.sync___get_value_onlineBalance() - fundedBalance) < 0.02f,
                30, $"Host fixture funding did not settle: target={fundedBalance:R}.");
            if (_finished) yield break;
            Write("funding", fundedBalance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "client-funded.txt")), 30,
                "Client did not observe the host's fixture funding.");
            if (_finished) yield break;
            float clientFundedBalance = float.Parse(File.ReadAllText(Path.Combine(_root, "client-funded.txt")),
                System.Globalization.CultureInfo.InvariantCulture);
            Require(Math.Abs(clientFundedBalance - fundedBalance) < 0.02f,
                $"Client fixture funding mismatch: host={fundedBalance:R}, client={clientFundedBalance:R}.");
            startingBalance = S1MoneyManager.Instance.sync___get_value_onlineBalance();
            Require(startingBalance > price,
                $"Native quote remains unaffordable after funding: price={price:R}, balance={startingBalance:R}.");
            Write("manifest", $"dealer={dealerId}\ncustomer={customerId}\nreceipt={receipt.DeliveryID}\nstore={receipt.StoreName}\ndestination={receipt.DestinationCode}\ndock={receipt.LoadingDockIndex}\nitem={listing.MatchingListing.Item.ID}\nprice={price:R}\nbalance={startingBalance:R}");
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "client-assigned.txt")), 60, "Client did not request dealer assignment.");
            yield return WaitUntil(() => CustomerAssigned(dealerId, customerId), 30, "Client dealer assignment did not replicate to host.");
            Write("host-assigned", "replicated=true");
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "client-removed.txt")), 60, "Client did not request dealer removal.");
            yield return WaitUntil(() => CustomerUnassigned(customerId), 30, "Client dealer removal did not replicate to host.");
            Write("host-removed", "replicated=true");
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "client-reordered.txt")), 60, "Client did not request delivery reorder.");
            yield return WaitUntil(() => HasActiveDelivery(shop), 45, "Native delivery reorder did not replicate to host.");
            float expected = startingBalance - price;
            yield return WaitUntil(() => Math.Abs(S1MoneyManager.Instance!.sync___get_value_onlineBalance() - expected) < 0.02f,
                30, "Host balance did not reflect the single native delivery charge.");
            Require(Math.Abs(S1MoneyManager.Instance!.sync___get_value_onlineBalance() - expected) < 0.02f,
                "Host balance did not charge the native quote exactly once.");
            Write("result", "PASS|two-peer host replication|dealer assign/remove|native delivery reorder|single host charge");
            Log("PASS host: two-peer membership, client dealer assignment/removal replication, native delivery replication, exact single charge");
            _finished = true;
    }

    private IEnumerator RunClientScenario()
    {
        yield return WaitUntil(() => File.Exists(Path.Combine(_root, "funding.txt")), 60,
            "Host fixture funding target did not arrive.");
        if (_finished) yield break;
        float fundedBalance = float.Parse(File.ReadAllText(Path.Combine(_root, "funding.txt")),
            System.Globalization.CultureInfo.InvariantCulture);
        yield return WaitUntil(() => S1MoneyManager.Instance != null &&
            Math.Abs(S1MoneyManager.Instance.sync___get_value_onlineBalance() - fundedBalance) < 0.02f,
            30, $"Client did not receive host fixture funding: target={fundedBalance:R}.");
        if (_finished) yield break;
        Write("client-funded", S1MoneyManager.Instance!.sync___get_value_onlineBalance()
            .ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        yield return WaitUntil(() => File.Exists(Path.Combine(_root, "manifest.txt")), 60, "Host feature manifest did not arrive.");
        if (_finished) yield break;
            Dictionary<string, string> manifest = ReadManifest();
            object dealers = CreateAdapter("UsableComputer.Native.DealersNativeAdapter");
            RefreshAdapter(dealers, 3, "Dealer");
            Require(InvokeBool(dealers, "TryChangeCustomer", manifest["dealer"], manifest["customer"], true), "Client adapter rejected dealer assignment.");
            Write("client-assigned", "requested=true");
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "host-assigned.txt")), 30, "Host did not observe dealer assignment.");
            yield return WaitUntil(() => CustomerAssigned(manifest["dealer"], manifest["customer"]), 30, "Client did not observe replicated dealer assignment.");
            if (_finished) yield break;
            RefreshAdapter(dealers, 3, "Dealer");
            Require(InvokeBool(dealers, "TryChangeCustomer", manifest["dealer"], manifest["customer"], false), "Client adapter rejected dealer removal.");
            Write("client-removed", "requested=true");
            yield return WaitUntil(() => File.Exists(Path.Combine(_root, "host-removed.txt")), 30, "Host did not observe dealer removal.");
            yield return WaitUntil(() => CustomerUnassigned(manifest["customer"]), 30, "Client did not observe replicated dealer removal.");
            if (_finished) yield break;
            RefreshAdapter(dealers, 3, "Dealer");

            object deliveries = CreateAdapter("UsableComputer.Native.DeliveriesNativeAdapter");
            RefreshAdapter(deliveries, 2, "Delivery");
            object?[] quoteArgs = { manifest["receipt"], 0m, string.Empty };
            bool quoteOk = (bool)(Method(deliveries, "TryQuote", 3).Invoke(deliveries, quoteArgs) ?? false);
            decimal quote = (decimal)quoteArgs[1]!;
            Require(quoteOk && Math.Abs((float)quote - float.Parse(manifest["price"], System.Globalization.CultureInfo.InvariantCulture)) < 0.02f,
                "Client did not obtain the host's native delivery quote.");
            float clientBalanceBefore = S1MoneyManager.Instance!.sync___get_value_onlineBalance();
            object?[] reorderArgs = { manifest["receipt"], quote, string.Empty };
            Require((bool)(Method(deliveries, "TryReorder", 3).Invoke(deliveries, reorderArgs) ?? false), "Client adapter rejected native reorder.");
            Write("client-reordered", "requested=true");
            yield return WaitUntil(() => HasReorderedDelivery(manifest), 45, "Client did not observe the native active delivery.");
            if (_finished) yield break;
            RefreshAdapter(deliveries, 2, "Delivery");
            float expected = float.Parse(manifest["balance"], System.Globalization.CultureInfo.InvariantCulture) - (float)quote;
            yield return WaitUntil(() => Math.Abs(S1MoneyManager.Instance!.sync___get_value_onlineBalance() - expected) < 0.02f,
                30, "Client did not observe the single delivery charge.");
            Require(Math.Abs(clientBalanceBefore - S1MoneyManager.Instance!.sync___get_value_onlineBalance() - (float)quote) < 0.02f,
                "Client balance did not change by the quoted amount exactly once.");
            object?[] duplicateArgs = { manifest["receipt"], quote, string.Empty };
            Require(!(bool)(Method(deliveries, "TryReorder", 3).Invoke(deliveries, duplicateArgs) ?? true),
                "Client adapter allowed a duplicate request before native history advanced.");
            Require(Math.Abs(S1MoneyManager.Instance!.sync___get_value_onlineBalance() - expected) < 0.02f,
                "Duplicate submission caused another client-side charge.");
            Write("result", "PASS|two-peer client authority path|dealer assign/remove|native delivery reorder|single charge");
            Log("PASS client: native app adapters invoked locally; dealer actions and delivery/balance replicated from client");
            _finished = true;
    }

    private static bool SelectDealerAndCustomer(out string dealerId, out string customerId)
    {
        dealerId = customerId = string.Empty;
        foreach (var dealer in S1Dealer.AllPlayerDealers)
        {
            if (dealer == null || !dealer.IsRecruited || dealer.AssignedCustomers.Count >= S1Dealer.MAX_CUSTOMERS) continue;
            foreach (var customer in S1Customer.UnlockedCustomers)
            {
                if (customer == null || customer.NPC == null || customer.AssignedDealer != null) continue;
                dealerId = dealer.ID;
                customerId = customer.NPC.ID;
                return true;
            }
        }
        return false;
    }

    private static bool CustomerAssigned(string dealerId, string customerId)
    {
        foreach (var customer in S1Customer.UnlockedCustomers)
            if (customer?.NPC?.ID == customerId) return customer.AssignedDealer?.ID == dealerId;
        return false;
    }

    private static bool CustomerUnassigned(string customerId)
    {
        foreach (var customer in S1Customer.UnlockedCustomers)
            if (customer?.NPC?.ID == customerId) return customer.AssignedDealer == null;
        return false;
    }

    private static bool HasActiveDelivery(S1DeliveryShop shop) => S1DeliveryManager.Instance!.GetActiveShopDelivery(shop) != null;
    private static bool HasReorderedDelivery(Dictionary<string, string> manifest)
    {
        S1DeliveryShop shop = S1DeliveryApp.Instance!.GetShop(manifest["store"]);
        if (shop == null) return false;
        var delivery = S1DeliveryManager.Instance!.GetActiveShopDelivery(shop);
        if (delivery == null || delivery.StoreName != manifest["store"] ||
            delivery.DestinationCode != manifest["destination"] ||
            delivery.LoadingDockIndex != int.Parse(manifest["dock"], System.Globalization.CultureInfo.InvariantCulture) ||
            delivery.Items.Length != 1) return false;
        return delivery.Items[0].String == manifest["item"] && delivery.Items[0].Int == 1;
    }

    private static S1SteamLobbyService? GetSteamLobbyService()
    {
        S1Lobby? lobby = S1Lobby.Instance;
        if (lobby == null) return null;
#if IL2CPP
        return lobby._lobbyService?.TryCast<S1SteamLobbyService>();
#else
        return typeof(S1Lobby).GetField("_lobbyService", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(lobby) as S1SteamLobbyService;
#endif
    }

    private static ulong GetNativeLobbyId()
    {
        S1SteamLobbyService? service = GetSteamLobbyService();
        if (service == null) return 0;
#if IL2CPP
        return service._lobbyID;
#else
        return (ulong?)typeof(S1SteamLobbyService).GetProperty("_lobbyID", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(service) ?? 0;
#endif
    }

    private static object CreateAdapter(string typeName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(typeName, false))
            .FirstOrDefault(candidate => candidate != null) ?? throw new InvalidOperationException($"Loaded mod type not found: {typeName}");
        return Activator.CreateInstance(type, nonPublic: true) ?? throw new InvalidOperationException($"Could not instantiate {typeName}");
    }

    private static bool InvokeBool(object target, string name, params object[] args)
    {
        object?[] invokeArgs = args.Concat(new object?[] { string.Empty }).ToArray();
        bool result = (bool)(Method(target, name, invokeArgs.Length).Invoke(target, invokeArgs) ?? false);
        if (!result) throw new InvalidOperationException($"{name} rejected: {invokeArgs[^1]}");
        return true;
    }

    private static void RefreshAdapter(object target, int parameterCount, string label)
    {
        object?[] args = parameterCount == 3 ? new object?[] { null, null, string.Empty }
            : new object?[] { null, string.Empty };
        bool read = (bool)(Method(target, "TryRead", parameterCount).Invoke(target, args) ?? false);
        Require(read, $"{label} adapter refresh failed: {args[^1]}");
    }

    private static MethodInfo Method(object target, string name, int parameterCount) => target.GetType()
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        .First(method => method.Name == name && method.GetParameters().Length == parameterCount);

    private static Dictionary<string, string> ReadManifest() => File.ReadAllLines(Path.Combine(Instance!._root, "manifest.txt"))
        .Where(line => line.Contains('='))
        .Select(line => line.Split(new[] { '=' }, 2))
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private IEnumerator WaitUntil(Func<bool> condition, int seconds, string failure)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        if (!condition()) Fail(failure, new TimeoutException(failure));
    }

    private IEnumerator Guard(IEnumerator scenario)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(scenario);
        while (stack.Count > 0 && !_finished)
        {
            IEnumerator active = stack.Peek();
            bool moved = false;
            object? current = null;
            Exception? error = null;
            try
            {
                moved = active.MoveNext();
                if (moved) current = active.Current;
            }
            catch (Exception exception)
            {
                error = exception;
            }

            if (error != null)
            {
                Fail("Multiplayer scenario failed", error);
                yield break;
            }
            if (!moved)
            {
                (active as IDisposable)?.Dispose();
                stack.Pop();
                continue;
            }
            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return current;
        }
    }

    private static Core? Instance { get; set; }

    private void Write(string name, string value)
    {
        if (name is "boot" or "game-ready" or "result") name = _role + "-" + name;
        string path = Path.Combine(_root, name + ".txt");
        string pending = path + ".tmp";
        File.WriteAllText(pending, value);
        if (File.Exists(path)) File.Delete(path);
        File.Move(pending, path);
        LoggerInstance.Msg($"[UsableComputerMultiplayerSmoke] {name}: {value}");
    }

    private void Fail(string message, Exception exception)
    {
        _finished = true;
        string detail = $"FAIL|{_role}|{message}|{exception.GetType().Name}: {exception.Message}";
        Write("result", detail);
        LoggerInstance.Error($"[UsableComputerMultiplayerSmoke] {detail}\n{exception}");
    }

    private void Log(string message) => LoggerInstance.Msg($"[UsableComputerMultiplayerSmoke] {message}");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static string RuntimeName =>
#if IL2CPP
        "IL2CPP";
#else
        "Mono";
#endif
    private static S1DateTime Now()
    {
#if IL2CPP
        return new S1DateTime(DateTime.Now.Ticks);
#else
        return DateTime.Now;
#endif
    }
    private static string Arg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
    }
}

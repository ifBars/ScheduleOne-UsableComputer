using UsableComputer.API;
using UsableComputer.Logic;

var tests = new (string Name, Action Test)[]
{
    ("deterministic ordering", TestDeterministicOrdering),
    ("duplicate ids fail clearly", TestDuplicateIds),
    ("unregister removes once", TestUnregister),
    ("change notification", TestChangeNotification),
    ("view model presentation", TestViewModelPresentation),
};

foreach (var test in tests)
{
    test.Test();
    Console.WriteLine($"PASS {test.Name}");
}

static void TestDeterministicOrdering()
{
    var store = new DesktopAppRegistryStore<FakeApp>();
    store.Register("zulu", new FakeApp("zulu"));
    store.Register("alpha", new FakeApp("alpha"));
    var apps = store.GetAll(app => app.Id);
    Assert(apps.Count == 2, "expected two registered apps");
    Assert(apps[0].Id == "alpha", "apps should sort by stable id");
    Assert(apps[1].Id == "zulu", "apps should retain deterministic ordering");
}

static void TestDuplicateIds()
{
    var store = new DesktopAppRegistryStore<FakeApp>();
    store.Register("calculator", new FakeApp("first"));
    InvalidOperationException exception = AssertThrows<InvalidOperationException>(
        () => store.Register("calculator", new FakeApp("second")));
    Assert(exception.Message.Contains("calculator", StringComparison.Ordinal), "duplicate error should name id");
}

static void TestUnregister()
{
    var store = new DesktopAppRegistryStore<FakeApp>();
    store.Register("notes", new FakeApp("notes"));
    Assert(store.Unregister("notes"), "first unregister should remove app");
    Assert(!store.Unregister("notes"), "second unregister should be a no-op");
    Assert(store.GetAll(app => app.Id).Count == 0, "unregistered app should not remain");
}

static void TestChangeNotification()
{
    var store = new DesktopAppRegistryStore<FakeApp>();
    int changes = 0;
    store.Changed += () => changes++;
    store.Register("one", new FakeApp("one"));
    store.Register("two", new FakeApp("two"));
    Assert(store.Unregister("one"), "registered app should unregister");
    Assert(!store.Unregister("missing"), "missing app should not notify");
    Assert(changes == 3, $"expected three changes, got {changes}");
}

static void TestViewModelPresentation()
{
    var entries = new[]
    {
        new JournalEntryViewModel("Deliver package", "Active"),
    };
    var quest = new JournalQuestViewModel(
        "quest-id",
        "The job",
        null!,
        null!,
        "Active",
        true,
        entries);
    Assert(quest.Title == "The job", "quest title should be preserved");
    Assert(quest.Subtitle == string.Empty, "missing subtitle should be safe");
    Assert(quest.Entries.Count == 1 && quest.Entries[0].State == "Active", "entry state should be preserved");

    var product = new ProductViewModel("product-id", "Green", "Weed", 12.5f, true, true);
    Assert(product.ValueLabel == "12.5", $"unexpected value label: {product.ValueLabel}");
    Assert(product.IsListed, "listed state should be preserved");
    Assert(product.IsFavourited, "favourite state should be preserved");
    Assert(product.ProductType == "Weed", "product type should be preserved");
}

static TException AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"expected {typeof(TException).Name}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

file sealed class FakeApp
{
    internal FakeApp(string id)
    {
        Id = id;
    }

    internal string Id { get; }
}

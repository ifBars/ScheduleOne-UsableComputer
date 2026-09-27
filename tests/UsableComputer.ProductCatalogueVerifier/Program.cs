using UsableComputer.Apps.Products;
using UsableComputer.Native;

var products = new[]
{
    new ProductViewModel("b", "Alpha", "Weed", 9, true, false),
    new ProductViewModel("a", "Alpha", "Meth", 10, false, true),
    new ProductViewModel("c", "Beta", "Weed", 100, true, true)
};
int assertions = 0;
void Check(string[] expected, string search = "", ProductFilter filter = ProductFilter.All, string? type = null, ProductSort sort = ProductSort.Name)
{
    string[] actual = ProductCatalogue.Select(products, search, filter, type, sort).Select(p => p.Id).ToArray();
    if (!expected.SequenceEqual(actual)) throw new Exception($"Expected {string.Join(',', expected)}; actual {string.Join(',', actual)}");
    assertions++;
}
Check(new[] { "a", "b", "c" });
Check(new[] { "a", "b" }, " ALPHA ");
Check(new[] { "b", "c" }, "weed");
Check(new[] { "b", "c" }, filter: ProductFilter.Listed);
Check(new[] { "a" }, filter: ProductFilter.Unlisted);
Check(new[] { "a", "c" }, filter: ProductFilter.Favourites);
Check(new[] { "c" }, filter: ProductFilter.Favourites, type: "WEED");
Check(new[] { "c", "a", "b" }, sort: ProductSort.ValueHigh);
Check(new[] { "b", "a", "c" }, sort: ProductSort.ValueLow);
Check(Array.Empty<string>(), "missing");
Check(Array.Empty<string>(), "Alpha", ProductFilter.Favourites, "Weed");
if (products[0].Id != "b") throw new Exception("Query mutated source ordering.");
assertions++;
var ties = new[]
{
    new ProductViewModel("z", "Zeta", "Weed", 5, false, false),
    new ProductViewModel("b", "Alpha", "Weed", 5, false, false),
    new ProductViewModel("a", "Alpha", "Weed", 5, false, false)
};
foreach (ProductSort sort in new[] { ProductSort.ValueHigh, ProductSort.ValueLow })
{
    if (!ProductCatalogue.Select(ties, "", ProductFilter.All, null, sort).Select(p => p.Id).SequenceEqual(new[] { "a", "b", "z" }))
        throw new Exception("Equal values did not use stable name/ID ordering.");
    assertions++;
}
var large = Enumerable.Range(0, 1000).Select(i => new ProductViewModel(i.ToString(), $"Product {i:D4}", "Weed", i, i % 2 == 0, false)).ToArray();
var filtered = ProductCatalogue.Select(large, "Product", ProductFilter.Listed, null, ProductSort.ValueHigh);
if (filtered.Count != 500 || filtered[0].Id != "998" || filtered[^1].Id != "0") throw new Exception("Large catalogue query failed.");
assertions++;
Console.WriteLine($"PASS | Product catalogue | {assertions} assertions");

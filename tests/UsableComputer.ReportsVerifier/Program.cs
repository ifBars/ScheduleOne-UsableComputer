using Newtonsoft.Json;
using UsableComputer.Apps.Reports;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new InvalidOperationException(message);
}
void Reject(BankReportSnapshot snapshot)
{
    bool rejected = false;
    try { _ = new BankReportBook(snapshot); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected, "Invalid snapshot was accepted.");
}

var book = new BankReportBook();
Check(book.Days.Count == 0, "Fresh report invented history.");
Check(book.Observe(4, 830), "First observation did not create a day.");
Check(!book.Observe(4, 830), "Unchanged observation reported a mutation.");
Check(book.Record(4, 831, "Deposit", 1000), "Deposit was not recorded.");
Check(book.Record(4, 832, "Hardware", -250.25f), "Purchase was not recorded.");
Check(book.Record(4, 833, "Hardware", -250.25f), "Identical legitimate purchase was deduplicated.");
Check(book.Days[0].MoneyIn == 1000m && book.Days[0].MoneyOut == 500.50m, "Money totals are incorrect.");
Check(book.Days[0].TransactionCount == 3, "Transaction count is incorrect.");
foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, 0f })
    Check(!book.Record(4, 900, "Bad amount", bad), "Invalid or zero amount was recorded.");
Check(!book.Record(4, 1260, "Bad time", 1), "Invalid time was recorded.");
Check(!book.Observe(-1, 900), "Negative day was accepted.");
Check(!book.Record(3, 900, "Past", 1), "Old day modified current history.");
Check(book.Record(4, 900, "\nLong\t" + new string('x', 200), 1.005f), "Rounding entry was rejected.");
Check(book.Days[0].Entries.Last().Amount == 1.01m, "Cent rounding is incorrect.");
Check(book.Days[0].Entries.Last().Name.Length <= 96 && !book.Days[0].Entries.Last().Name.Contains('\n'), "Unsafe label survived cleaning.");

string json = JsonConvert.SerializeObject(book.CreateSnapshot());
var restored = new BankReportBook(JsonConvert.DeserializeObject<BankReportSnapshot>(json));
Check(restored.Days[0].MoneyIn == 1001.01m && restored.Days[0].TransactionCount == 4, "JSON reload changed totals.");
restored.Record(4, 1000, "After reload", -1);
Check(restored.Days[0].TransactionCount == 5, "Reload did not resume existing day.");
Check(book.Days[0].TransactionCount == 4, "Restored data aliases the original.");
var detached = restored.CreateSnapshot();
detached.Days[0].Entries[0].Name = "Changed";
Check(restored.Days[0].Entries[0].Name == "Deposit", "Snapshot aliases live entries.");

var retained = new BankReportBook();
for (int i = 0; i < 200; i++) retained.Record(0, 900, "Repeat", -2);
Check(retained.Days[0].Entries.Count == BankReportBook.MaximumEntriesPerDay, "Receipt retention is unbounded.");
Check(retained.Days[0].MoneyOut == 400m && retained.Days[0].TransactionCount == 200, "Receipt pruning lost daily totals.");
string report = BankReportBook.Export(retained.Days[0]);
Check(report.Contains("not profit") && report.Contains("Transactions: 200; latest 128"), "Export omits scope or retention.");
for (int day = 2; day < 70; day += 2) retained.Observe(day, 700);
Check(retained.Days.Count == 28, "Daily history is unbounded.");
Check(retained.Days.All(day => day.Day % 2 == 0), "Unobserved days were invented.");
Check(new BankReportBook().Days.Count == 0, "A new save inherited another save's data.");

Reject(new BankReportSnapshot { SchemaVersion = 99 });
Reject(new BankReportSnapshot { Days = null! });
Reject(new BankReportSnapshot { Days = new() { new BankReportDay { Day = -1 } } });
Reject(new BankReportSnapshot { Days = new() { new BankReportDay { Day = 0, MoneyIn = -1 } } });
Reject(new BankReportSnapshot { Days = new() { new BankReportDay { Day = 0 }, new BankReportDay { Day = 0 } } });
Reject(new BankReportSnapshot { Days = new() { new BankReportDay { Entries = new() { new BankReportEntry() } } } });
var week = new BankReportBook();
week.Record(0, 900, "Outside range", 999);
week.Record(4, 900, "Deposit", 100);
week.Record(7, 900, "Purchase", -25);
week.Observe(8, 900);
BankReportRange range = week.GetRange(8);
Check(range.FirstDay == 2 && range.LastDay == 8 && range.CalendarDays == 7, "Range is not seven calendar days.");
Check(range.Days.Count == 3 && range.MoneyIn == 100 && range.MoneyOut == 25 && range.TransactionCount == 2,
    "Summary included activity outside the range or invented missing days.");
string summary = BankReportBook.Export(range);
Check(summary.Contains("Day 3: No observations") && summary.Contains("Day 9: In $0.00"),
    "Summary does not distinguish unobserved days from observed zero activity.");
Check(summary.Contains("3 of 7 days observed") && summary.Contains("not profit"), "Summary omits coverage or scope.");
range.Days[0].MoneyIn = 1234;
Check(week.GetRange(8).MoneyIn == 100, "Range aliases stored totals.");
Check(week.GetRange(0).CalendarDays == 1, "Early-game range included negative days.");
Check(new BankReportBook().GetRange(6).Days.Count == 0, "Empty summary invented observations.");
foreach (var input in new[] { (-1, 7), (int.MaxValue, 7), (8, 0), (8, 29) })
{
    bool rejected = false;
    try { week.GetRange(input.Item1, input.Item2); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "Invalid range accepted.");
}
Check(!week.Observe(int.MaxValue, 900), "Unrepresentable display day accepted.");
Reject(new BankReportSnapshot { Days = new() { new BankReportDay { Day = int.MaxValue } } });
Console.WriteLine($"PASS | Reports verifier | {assertions} assertions");

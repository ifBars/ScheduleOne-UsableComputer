using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UsableComputer.Reports;

internal sealed class BankReportSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public List<BankReportDay> Days { get; set; } = new();
}

internal sealed class BankReportDay
{
    public int Day { get; set; }
    public int FirstTime { get; set; }
    public int LastTime { get; set; }
    public decimal MoneyIn { get; set; }
    public decimal MoneyOut { get; set; }
    public int TransactionCount { get; set; }
    public List<BankReportEntry> Entries { get; set; } = new();

    internal BankReportDay Clone() => new()
    {
        Day = Day, FirstTime = FirstTime, LastTime = LastTime,
        MoneyIn = MoneyIn, MoneyOut = MoneyOut, TransactionCount = TransactionCount,
        Entries = Entries.Select(entry => entry.Clone()).ToList(),
    };
}

internal sealed class BankReportEntry
{
    public int Time { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    internal BankReportEntry Clone() => new() { Time = Time, Name = Name, Amount = Amount };
}

internal sealed class BankReportRange
{
    internal int FirstDay { get; set; }
    internal int LastDay { get; set; }
    internal IReadOnlyList<BankReportDay> Days { get; set; } = Array.Empty<BankReportDay>();
    internal decimal MoneyIn => Days.Sum(day => day.MoneyIn);
    internal decimal MoneyOut => Days.Sum(day => day.MoneyOut);
    internal long TransactionCount => Days.Sum(day => (long)day.TransactionCount);
    internal int CalendarDays => LastDay - FirstDay + 1;
}

/// <summary>Recorded bank activity only: transfers are deliberately not classified as sales or profit.</summary>
internal sealed class BankReportBook
{
    internal const int MaximumDays = 28;
    internal const int MaximumEntriesPerDay = 128;
    private const decimal MaximumAmount = 1_000_000_000_000m;
    private readonly List<BankReportDay> _days = new();

    internal IReadOnlyList<BankReportDay> Days => _days;

    internal BankReportBook(BankReportSnapshot? snapshot = null)
    {
        if (snapshot == null)
            return;
        if (snapshot.SchemaVersion != 1 || snapshot.Days == null || snapshot.Days.Count > MaximumDays)
            throw new ArgumentException("Unsupported bank report data.");

        int previousDay = -1;
        foreach (BankReportDay day in snapshot.Days)
        {
            if (day == null || day.Day <= previousDay || day.Day == int.MaxValue || !ValidTime(day.FirstTime) ||
                !ValidTime(day.LastTime) || day.Entries == null || day.Entries.Count > MaximumEntriesPerDay ||
                day.TransactionCount < day.Entries.Count || day.MoneyIn < 0 || day.MoneyOut < 0 ||
                day.MoneyIn > MaximumAmount || day.MoneyOut > MaximumAmount)
                throw new ArgumentException("Invalid bank report day.");
            foreach (BankReportEntry entry in day.Entries)
            {
                if (entry == null || !ValidTime(entry.Time) || entry.Amount < -MaximumAmount || entry.Amount > MaximumAmount ||
                    entry.Name == null || entry.Name.Length > 96)
                    throw new ArgumentException("Invalid bank report transaction.");
            }
            _days.Add(day.Clone());
            previousDay = day.Day;
        }
    }

    internal bool Observe(int day, int time)
    {
        if (day < 0 || day == int.MaxValue || !ValidTime(time))
            return false;
        if (_days.Count > 0 && day < _days[_days.Count - 1].Day)
            return false;
        if (_days.Count == 0 || _days[_days.Count - 1].Day != day)
        {
            _days.Add(new BankReportDay { Day = day, FirstTime = time, LastTime = time });
            while (_days.Count > MaximumDays)
                _days.RemoveAt(0);
            return true;
        }
        BankReportDay current = _days[_days.Count - 1];
        if (current.LastTime == time)
            return false;
        current.LastTime = time;
        return true;
    }

    internal bool Record(int day, int time, string? name, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || Math.Abs((double)amount) > (double)MaximumAmount ||
            day < 0 || day == int.MaxValue || !ValidTime(time) || (_days.Count > 0 && day < _days[_days.Count - 1].Day))
            return false;
        decimal rounded = Math.Round((decimal)amount, 2, MidpointRounding.AwayFromZero);
        if (rounded == 0)
            return false;
        Observe(day, time);
        BankReportDay current = _days[_days.Count - 1];
        if (current.TransactionCount == int.MaxValue ||
            current.MoneyIn + Math.Max(rounded, 0) > MaximumAmount ||
            current.MoneyOut + Math.Max(-rounded, 0) > MaximumAmount)
            return false;
        current.MoneyIn += Math.Max(rounded, 0);
        current.MoneyOut += Math.Max(-rounded, 0);
        current.TransactionCount++;
        current.Entries.Add(new BankReportEntry { Time = time, Name = CleanName(name), Amount = rounded });
        if (current.Entries.Count > MaximumEntriesPerDay)
            current.Entries.RemoveAt(0);
        return true;
    }

    internal BankReportSnapshot CreateSnapshot() => new() { Days = _days.Select(day => day.Clone()).ToList() };

    internal BankReportRange GetRange(int lastDay, int length = 7)
    {
        if (lastDay < 0 || lastDay == int.MaxValue || length < 1 || length > MaximumDays)
            throw new ArgumentOutOfRangeException(nameof(lastDay), "Choose a valid day and a range of 1 to 28 days.");
        int firstDay = Math.Max(0, lastDay - length + 1);
        return new BankReportRange
        {
            FirstDay = firstDay,
            LastDay = lastDay,
            Days = _days.Where(day => day.Day >= firstDay && day.Day <= lastDay).Select(day => day.Clone()).ToArray(),
        };
    }

    internal static string FormatMoney(decimal value) =>
        (value < 0 ? "-$" : "$") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);
    internal static string FormatTime(int time) => $"{time / 100:00}:{time % 100:00}";

    internal static string Export(BankReportDay day)
    {
        var text = new StringBuilder();
        text.AppendLine($"BANK REPORT - DAY {day.Day + 1}");
        text.AppendLine($"First observed {FormatTime(day.FirstTime)}; last observed {FormatTime(day.LastTime)}");
        text.AppendLine("Recorded activity only. Money in includes deposits and transfers; net movement is not profit.");
        text.AppendLine($"Money in: {FormatMoney(day.MoneyIn)}");
        text.AppendLine($"Money out: {FormatMoney(day.MoneyOut)}");
        text.AppendLine($"Net bank movement: {FormatMoney(day.MoneyIn - day.MoneyOut)}");
        text.AppendLine($"Transactions: {day.TransactionCount}; latest {day.Entries.Count} listed below.");
        text.AppendLine();
        foreach (BankReportEntry entry in day.Entries)
            text.AppendLine($"{FormatTime(entry.Time)}  {FormatMoney(entry.Amount)}  {entry.Name}");
        return text.ToString();
    }

    internal static string Export(BankReportRange range)
    {
        var text = new StringBuilder();
        text.AppendLine($"BANK SUMMARY - DAYS {range.FirstDay + 1} TO {range.LastDay + 1}");
        text.AppendLine($"{range.Days.Count} of {range.CalendarDays} days observed. Missing days are not counted as zero activity.");
        text.AppendLine("Recorded activity only. Deposits and transfers are included; net bank movement is not profit.");
        text.AppendLine($"Money in: {FormatMoney(range.MoneyIn)}");
        text.AppendLine($"Money out: {FormatMoney(range.MoneyOut)}");
        text.AppendLine($"Net bank movement: {FormatMoney(range.MoneyIn - range.MoneyOut)}");
        text.AppendLine($"Transactions: {range.TransactionCount}");
        text.AppendLine();
        for (int number = range.FirstDay; number <= range.LastDay; number++)
        {
            BankReportDay? day = range.Days.FirstOrDefault(item => item.Day == number);
            text.AppendLine(day == null ? $"Day {number + 1}: No observations"
                : $"Day {number + 1}: In {FormatMoney(day.MoneyIn)} | Out {FormatMoney(day.MoneyOut)} | Net {FormatMoney(day.MoneyIn - day.MoneyOut)}");
        }
        return text.ToString();
    }

    private static bool ValidTime(int time) => time >= 0 && time <= 2359 && time % 100 < 60;

    private static string CleanName(string? name)
    {
        string cleaned = new((name ?? string.Empty).Where(character => !char.IsControl(character)).Take(96).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Bank transaction" : cleaned;
    }
}

using System;
using System.Linq;
using UsableComputer.API;
using UsableComputer.Reports;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
using S1Alignment = Il2CppTMPro.TextAlignmentOptions;
using S1Wrapping = Il2CppTMPro.TextWrappingModes;
using S1Overflow = Il2CppTMPro.TextOverflowModes;
#else
using S1Text = TMPro.TextMeshProUGUI;
using S1Alignment = TMPro.TextAlignmentOptions;
using S1Wrapping = TMPro.TextWrappingModes;
using S1Overflow = TMPro.TextOverflowModes;
#endif

namespace UsableComputer.UI;

internal sealed class BankReportsApp : IDesktopAppSession
{
    private readonly DesktopAppContext _context;
    private readonly S1Text _balances;
    private readonly S1Text _dayTitle;
    private readonly S1Text _totals;
    private readonly S1Text _status;
    private readonly S1Text _coverage;
    private readonly RectTransform _rows;
    private readonly ScrollRect _scroll;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _export;
    private readonly Button _period;
    private readonly S1Text _columns;
    private readonly S1Text _amountHeader;
    private bool _weekly;
    private bool _renderedWeekly;
    private int? _selectedDay;
    private int _revision = -1;
    private bool _disposed;
    private int _renderedDay = -1;
    private long _renderedCount = -1;
    private float _messageUntil;

    internal BankReportsApp(DesktopAppContext context)
    {
        _context = context;
        Transform parent = context.Container;
        Label(parent, "ReportsHeading", "Bank reports", 23, 12, 8, 360, 30, true);
        Button(parent, "ReportDeliveries", "Deliveries", 526, 8, 128, () => context.OpenApp(Constants.DeliveriesAppId));
        _balances = Label(parent, "ReportBalances", "Waiting for finances...", 14, 12, 43, 650, 24);
        _previous = Button(parent, "ReportPrevious", "<", 12, 80, 34, Previous);
        _dayTitle = Label(parent, "ReportDay", "Today", 18, 56, 80, 210, 32, true);
        _next = Button(parent, "ReportNext", ">", 268, 80, 34, Next);
        Button(parent, "ReportToday", "Today", 312, 80, 76, () => { _selectedDay = null; Refresh(); });
        _period = Button(parent, "ReportPeriod", "7 days", 400, 80, 100, () => { _weekly = !_weekly; Refresh(); });
        _export = Button(parent, "ReportExport", "Save report", 0, 80, 116, Export);
        RectTransform exportRect = _export.GetComponent<RectTransform>();
        exportRect.anchorMin = exportRect.anchorMax = new Vector2(1, 1);
        exportRect.pivot = new Vector2(1, 1);
        exportRect.anchoredPosition = new Vector2(-12, -80);
        _totals = Label(parent, "ReportTotals", "", 17, 12, 121, 650, 30, true);
        _coverage = Label(parent, "ReportCoverage", "", 12, 12, 153, 650, 23);

        GameObject panel = UiFactory.CreatePanel(parent, "ReportLedger", UiFactory.SurfaceInset);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(12, 68);
        panelRect.offsetMax = new Vector2(-12, -184);
        _columns = Label(panel.transform, "LedgerColumns", "TIME          BANK TRANSACTION", 12, 8, 3, 440, 23, true);
        S1Text amountHeader = Label(panel.transform, "LedgerAmountHeader", "AMOUNT", 12, 0, 3, 124, 23, true);
        _amountHeader = amountHeader;
        Right(amountHeader.rectTransform, 8, 3, 124, 23);
        amountHeader.alignment = S1Alignment.MidlineRight;
        GameObject viewport = new("ReportViewport");
        viewport.transform.SetParent(panel.transform, false);
        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(4, 4);
        viewportRect.offsetMax = new Vector2(-4, -28);
        viewport.AddComponent<RectMask2D>();
        GameObject content = new("ReportRows");
        content.transform.SetParent(viewport.transform, false);
        _rows = content.AddComponent<RectTransform>();
        _rows.anchorMin = new Vector2(0, 1);
        _rows.anchorMax = new Vector2(1, 1);
        _rows.pivot = new Vector2(0.5f, 1);
        _rows.sizeDelta = Vector2.zero;
        _scroll = panel.AddComponent<ScrollRect>();
        _scroll.viewport = viewportRect;
        _scroll.content = _rows;
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.scrollSensitivity = 28;
        _scroll.movementType = ScrollRect.MovementType.Clamped;

        S1Text disclaimer = Label(parent, "ReportDisclaimer",
            "Bank activity includes transfers. Net movement is not profit; cash sales and wages are not itemized.",
            12, 12, 0, 650, 30);
        Bottom(disclaimer.rectTransform, 36, 30);
        _status = Label(parent, "ReportStatus", "", 12, 12, 0, 650, 26);
        Bottom(_status.rectTransform, 8, 26);
        UiFactory.SetLayerRecursively(parent.gameObject, Constants.UiLayer);
    }

    public void OnOpened() => Refresh();
    public void OnClosed() { }
    public void Dispose() => _disposed = true;
    public void OnTick()
    {
        if (!_disposed && _revision != BankReportsService.Revision)
            Refresh();
    }

    private void Refresh()
    {
        _revision = BankReportsService.Revision;
        bool available = BankReportsService.Available;
        _balances.text = available
            ? $"Your cash  {BankReportBook.FormatMoney(BankReportsService.Cash)}     Shared bank  {BankReportBook.FormatMoney(BankReportsService.Bank)}"
            : "Financial data is temporarily unavailable.";
        var days = BankReportsService.Book.Days;
        BankReportDay? day = available && BankReportsService.IsHost
            ? (_selectedDay.HasValue ? days.FirstOrDefault(item => item.Day == _selectedDay) : days.LastOrDefault())
            : null;
        _previous.interactable = day != null && days[0].Day < day.Day;
        _next.interactable = day != null && days[days.Count - 1].Day > day.Day;
        _export.interactable = day != null;
        _period.interactable = day != null;
        _period.GetComponentInChildren<S1Text>().text = _weekly ? "Daily" : "7 days";
        BankReportRange? range = _weekly && day != null ? BankReportsService.Book.GetRange(day.Day) : null;
        _dayTitle.text = day == null ? "No report yet" : $"Day {day.Day + 1}{(_selectedDay == null ? " - Latest" : "")}";
        _totals.text = day == null ? "" :
            $"In  {BankReportBook.FormatMoney(day.MoneyIn)}     Out  {BankReportBook.FormatMoney(day.MoneyOut)}     Net  {BankReportBook.FormatMoney(day.MoneyIn - day.MoneyOut)}";
        _coverage.text = day == null ? "" :
            $"{day.TransactionCount} transactions | First observed {BankReportBook.FormatTime(day.FirstTime)} | Last observed {BankReportBook.FormatTime(day.LastTime)}";
        if (range != null)
        {
            _dayTitle.text = $"Days {range.FirstDay + 1}-{range.LastDay + 1}";
            _totals.text = $"In  {BankReportBook.FormatMoney(range.MoneyIn)}     Out  {BankReportBook.FormatMoney(range.MoneyOut)}     Net  {BankReportBook.FormatMoney(range.MoneyIn - range.MoneyOut)}";
            _coverage.text = $"{range.TransactionCount} transactions | {range.Days.Count} of {range.CalendarDays} days observed; missing days are unknown.";
        }
        _columns.text = range == null ? "TIME          BANK TRANSACTION" : "DAY            RECORDED MONEY IN / OUT";
        _amountHeader.text = range == null ? "AMOUNT" : "NET";
        if (Time.unscaledTime >= _messageUntil)
            _status.text = !available ? "Reports will resume when the game is ready."
            : !BankReportsService.IsHost ? "Daily history is currently available to the session host."
            : "Records while you play. Keeps 28 observed days; written with the next game save.";

        int count = range?.CalendarDays ?? day?.Entries.Count ?? 0;
        if (range == null && day != null && day.TransactionCount > count)
            _coverage.text += $" | Latest {count} shown";
        long transactionCount = range?.TransactionCount ?? day?.TransactionCount ?? 0;
        if (_renderedDay == (day?.Day ?? -1) && _renderedCount == transactionCount && _renderedWeekly == _weekly)
            return;
        _renderedDay = day?.Day ?? -1;
        _renderedCount = transactionCount;
        _renderedWeekly = _weekly;
        float position = _scroll.verticalNormalizedPosition;
        for (int index = _rows.childCount - 1; index >= 0; index--)
        {
            Transform child = _rows.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
        _rows.sizeDelta = new Vector2(0, Math.Max(54, count * 30 + 24));
        if (count == 0)
        {
            Label(_rows, "EmptyReport", day == null ? "No recorded bank activity is available."
                : "No bank transactions recorded for this day yet.", 14, 10, 10, 570, 32);
        }
        else
        {
            for (int index = 0; index < count; index++)
            {
                BankReportDay? summaryDay = range?.Days.FirstOrDefault(item => item.Day == range.LastDay - index);
                BankReportEntry entry = range == null ? day!.Entries[count - index - 1] : new BankReportEntry
                {
                    Name = summaryDay == null ? "No observations" : $"In {BankReportBook.FormatMoney(summaryDay.MoneyIn)} / Out {BankReportBook.FormatMoney(summaryDay.MoneyOut)}",
                    Amount = summaryDay == null ? 0 : summaryDay.MoneyIn - summaryDay.MoneyOut,
                };
                GameObject row = UiFactory.CreatePanel(_rows, $"BankEntry_{index}",
                    index % 2 == 0 ? UiFactory.SurfaceRaised : UiFactory.SurfaceInset);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, 29);
                rect.anchoredPosition = new Vector2(0, -index * 30);
                Label(row.transform, "Time", range == null ? BankReportBook.FormatTime(entry.Time) : (range.LastDay - index + 1).ToString(), 13, 8, 0, 56, 29);
                S1Text name = Label(row.transform, "Transaction", entry.Name, 14, 76, 0, 420, 29);
                name.rectTransform.anchorMax = new Vector2(1, 1);
                name.rectTransform.offsetMin = new Vector2(76, -29);
                name.rectTransform.offsetMax = new Vector2(-142, 0);
                name.richText = false;
                name.textWrappingMode = S1Wrapping.NoWrap;
                name.overflowMode = S1Overflow.Ellipsis;
                S1Text amount = Label(row.transform, "Amount", range != null && summaryDay == null ? "--" : BankReportBook.FormatMoney(entry.Amount), 14, 0, 0, 128, 29);
                Right(amount.rectTransform, 8, 0, 128, 29);
                amount.alignment = S1Alignment.MidlineRight;
                amount.color = entry.Amount >= 0 ? UiFactory.Success : UiFactory.TextPrimary;
            }
        }
        UiFactory.SetLayerRecursively(_rows.gameObject, Constants.UiLayer);
        _scroll.verticalNormalizedPosition = position;
    }

    private void Previous() => Navigate(-1);
    private void Next() => Navigate(1);
    private void Navigate(int direction)
    {
        var days = BankReportsService.Book.Days;
        int index = _selectedDay == null ? days.Count - 1 : days.ToList().FindIndex(day => day.Day == _selectedDay);
        index += direction;
        if (index < 0 || index >= days.Count)
            return;
        _selectedDay = days[index].Day;
        Refresh();
        _scroll.verticalNormalizedPosition = 1;
    }

    private void Export()
    {
        var days = BankReportsService.Book.Days;
        BankReportDay? day = _selectedDay.HasValue ? days.FirstOrDefault(item => item.Day == _selectedDay) : days.LastOrDefault();
        if (!BankReportsService.Available || !BankReportsService.IsHost || day == null)
            return;
        try
        {
            var existing = DesktopFileSystem.GetChildren(DesktopFileSystem.DesktopId);
            BankReportRange? range = _weekly ? BankReportsService.Book.GetRange(day.Day) : null;
            string stem = range == null ? $"Bank report - Day {day.Day + 1}" : $"Bank summary - Days {range.FirstDay + 1}-{range.LastDay + 1}";
            string name = stem + ".txt";
            for (int suffix = 2; existing.Any(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)); suffix++)
                name = $"{stem} ({suffix}).txt";
            DesktopFileSystem.CreateTextFile(DesktopFileSystem.DesktopId, name,
                range == null ? BankReportBook.Export(day) : BankReportBook.Export(range));
            _status.text = $"Saved {name} to Desktop. Open it in Files or Notes.";
            _messageUntil = Time.unscaledTime + 8;
        }
        catch (Exception exception)
        {
            _status.text = $"Could not save report: {exception.Message}";
            _messageUntil = Time.unscaledTime + 8;
        }
    }

    private Button Button(Transform parent, string name, string title, float x, float y, float width, Action action)
    {
        Button button = UiFactory.CreateButton(parent, name, title, UiFactory.SurfaceRaised, out _);
        Top(button.GetComponent<RectTransform>(), x, y, width, 32);
        _context.Bind(button, action);
        return button;
    }

    private static S1Text Label(Transform parent, string name, string text, float size,
        float x, float y, float width, float height, bool bold = false)
    {
        S1Text label = UiFactory.CreateText(parent, name, text, size, UiFactory.TextPrimary, S1Alignment.MidlineLeft, bold);
        Top(label.rectTransform, x, y, width, height);
        return label;
    }

    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static void Right(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(-x, -y);
    }

    private static void Bottom(RectTransform rect, float y, float height)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = Vector2.zero;
        rect.offsetMin = new Vector2(12, y);
        rect.offsetMax = new Vector2(-12, y + height);
    }
}

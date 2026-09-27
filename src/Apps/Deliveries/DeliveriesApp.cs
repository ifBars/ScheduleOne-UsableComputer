using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.API;
using UsableComputer.Apps.Reports;
using UsableComputer.UI;
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

namespace UsableComputer.Apps.Deliveries;

internal sealed class DeliveriesApp : IDesktopAppSession
{
    private readonly DesktopAppContext _context;
    private readonly DeliveriesNativeAdapter _adapter = new();
    private readonly RectTransform _orders;
    private readonly RectTransform _items;
    private readonly S1Text _title;
    private readonly S1Text _destination;
    private readonly S1Text _state;
    private readonly S1Text _price;
    private readonly S1Text _status;
    private readonly S1Text _reorderLabel;
    private readonly Button _reorder;
    private readonly Button _cancel;
    private readonly Button _activeTab;
    private readonly Button _historyTab;
    private UiListenerRegistry _rowListeners = new();
    private List<DeliveryViewModel> _snapshot = new();
    private string _rowsKey = string.Empty;
    private string _itemsKey = string.Empty;
    private string? _selectedId;
    private decimal? _confirmedPrice;
    private bool _history;
    private bool _disposed;
    private float _nextRefresh;
    private float _messageUntil;

    internal DeliveriesApp(DesktopAppContext context)
    {
        _context = context;
        Text(context.Container, "DeliveriesHeading", "Deliveries", 22, 12, 8, 620, 30, true);
        _activeTab = MakeButton(context.Container, "DeliveryActiveTab", "Active orders", 12, 47, 132, () => SelectTab(false));
        _historyTab = MakeButton(context.Container, "DeliveryHistoryTab", "Previous orders", 156, 47, 148, () => SelectTab(true));
        MakeButton(context.Container, "DeliveryReports", "Bank reports", 526, 47, 140, () => context.OpenApp(Constants.ReportsAppId));
        _orders = Scroll(context.Container, "DeliveryOrders", new Vector2(0, 0), new Vector2(0.34f, 1),
            new Vector2(12, 44), new Vector2(-6, -94));
        GameObject detail = UiFactory.CreatePanel(context.Container, "DeliveryDetails", UiFactory.SurfaceRaised);
        RectTransform detailRect = detail.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.34f, 0);
        detailRect.anchorMax = Vector2.one;
        detailRect.offsetMin = new Vector2(6, 44);
        detailRect.offsetMax = new Vector2(-12, -94);
        _title = Text(detail.transform, "DeliveryStore", "Select an order", 19, 12, 8, 404, 29, true);
        _destination = Text(detail.transform, "DeliveryDestination", "", 14, 12, 42, 404, 25);
        _state = Text(detail.transform, "DeliveryState", "", 14, 12, 68, 404, 25);
        _items = Scroll(detail.transform, "DeliveryItems", Vector2.zero, Vector2.one,
            new Vector2(8, 112), new Vector2(-8, -104));
        _price = Text(detail.transform, "DeliveryPrice", "", 14, 12, 0, 404, 38);
        Bottom(_price.rectTransform, 12, 70, 404, 38);
        _reorder = MakeButton(detail.transform, "DeliveryReorder", "Reorder", 12, 0, 266, Reorder);
        _reorderLabel = _reorder.GetComponentInChildren<S1Text>();
        Bottom(_reorder.GetComponent<RectTransform>(), 12, 28, 266, 32);
        _cancel = MakeButton(detail.transform, "DeliveryCancel", "Cancel", 288, 0, 108, () => { _confirmedPrice = null; Refresh(); });
        Bottom(_cancel.GetComponent<RectTransform>(), 288, 28, 108, 32);
        _status = Text(context.Container, "DeliveryStatus", "", 12, 12, 0, 654, 34);
        Bottom(_status.rectTransform, 12, 6, 654, 34);
        UiFactory.SetLayerRecursively(context.Container.gameObject, Constants.UiLayer);
    }

    public void OnOpened() => Refresh();
    public void OnClosed() { }
    public void OnTick()
    {
        if (!_disposed && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 1;
            Refresh();
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rowListeners.Dispose();
    }

    private void SelectTab(bool history)
    {
        _history = history;
        _selectedId = null;
        _confirmedPrice = null;
        _rowsKey = string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        bool available = _adapter.TryRead(out _snapshot, out string message);
        if (!available) _snapshot.Clear();
        List<DeliveryViewModel> visible = _snapshot.Where(order => order.IsActive != _history).ToList();
        if (!visible.Any(order => order.Id == _selectedId))
        {
            _selectedId = visible.FirstOrDefault()?.Id;
            _confirmedPrice = null;
        }
        _activeTab.interactable = _history;
        _historyTab.interactable = !_history;
        string rowsKey = string.Join("|", visible.Select(order => order.Id + order.Status)) + _selectedId;
        if (rowsKey != _rowsKey)
        {
            _rowsKey = rowsKey;
            Clear(_orders);
            _rowListeners.Dispose();
            _rowListeners = new UiListenerRegistry();
            _orders.sizeDelta = new Vector2(0, Math.Max(80, visible.Count * 67));
            if (visible.Count == 0)
                Text(_orders, "NoDeliveries", _history ? "No previous orders.\nPlace your first order on the phone."
                    : "No active deliveries.\nCheck previous orders to restock.", 14, 8, 8, 190, 88);
            for (int index = 0; index < visible.Count; index++)
            {
                DeliveryViewModel order = visible[index];
                Button row = UiFactory.CreateButton(_orders, $"DeliveryRow_{index}", order.Store,
                    order.Id == _selectedId ? UiFactory.Accent : UiFactory.SurfaceRaised, out S1Text label);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(-4, 60);
                rect.anchoredPosition = new Vector2(0, -index * 67);
                label.fontSize = 14;
                label.richText = false;
                label.text = order.Store + "\n" + order.Destination;
                label.overflowMode = S1Overflow.Ellipsis;
                _rowListeners.Add(() => { _selectedId = order.Id; _confirmedPrice = null; Refresh(); }, row.onClick);
            }
            UiFactory.SetLayerRecursively(_orders.gameObject, Constants.UiLayer);
        }
        DeliveryViewModel? selected = visible.FirstOrDefault(order => order.Id == _selectedId);
        _title.text = selected?.Store ?? "Select an order";
        _destination.text = selected == null ? "" : $"{selected.Destination} | Loading dock {selected.Dock}";
        _state.text = selected?.Status ?? "";
        string itemsKey = selected == null ? "none" : selected.Id + string.Join("|", selected.Items.Select(item => item.Name + item.Quantity));
        if (_itemsKey != itemsKey)
        {
            _itemsKey = itemsKey;
            Clear(_items);
            int itemCount = selected?.Items.Count ?? 0;
            _items.sizeDelta = new Vector2(0, Math.Max(30, itemCount * 28));
            for (int index = 0; index < itemCount; index++)
            {
                DeliveryItemViewModel item = selected!.Items[index];
                S1Text itemText = Text(_items, $"DeliveryItem_{index}", $"{item.Quantity} x {item.Name}", 14, 6, index * 28, 382, 28);
                itemText.textWrappingMode = S1Wrapping.NoWrap;
                itemText.overflowMode = S1Overflow.Ellipsis;
            }
            UiFactory.SetLayerRecursively(_items.gameObject, Constants.UiLayer);
        }
        _cancel.gameObject.SetActive(_confirmedPrice.HasValue);
        _reorder.gameObject.SetActive(selected != null && !selected.IsActive);
        if (selected != null && !selected.IsActive)
        {
            bool canOrder = _adapter.TryQuote(selected.Id, out decimal cost, out string reason);
            _reorder.interactable = canOrder;
            if (_confirmedPrice.HasValue && (!canOrder || _confirmedPrice.Value != cost))
            {
                _confirmedPrice = null;
                _cancel.gameObject.SetActive(false);
            }
            _price.text = canOrder ? $"Current total: {BankReportBook.FormatMoney(cost)} including delivery"
                : string.IsNullOrEmpty(reason) ? "This order cannot be repeated right now." : reason;
            _reorderLabel.text = _confirmedPrice.HasValue ? $"Confirm {BankReportBook.FormatMoney(cost)}" : "Reorder...";
        }
        else
        {
            _price.text = selected == null ? "" : "Supplies stay in the delivery vehicle until unloaded.";
        }
        if (Time.unscaledTime >= _messageUntil)
            _status.text = !available ? message : _confirmedPrice.HasValue
                ? "Confirm to pay from the shared bank and send these items to the original loading dock."
                : "Live native orders. New orders and destination changes are available on your phone.";
    }

    private void Reorder()
    {
        if (_selectedId == null) return;
        if (!_confirmedPrice.HasValue)
        {
            if (_adapter.TryQuote(_selectedId, out decimal cost, out string reason))
                _confirmedPrice = cost;
            else
                ShowMessage(reason);
            Refresh();
            return;
        }
        _adapter.TryReorder(_selectedId, _confirmedPrice.Value, out string message);
        _confirmedPrice = null;
        ShowMessage(message);
        Refresh();
    }

    private void ShowMessage(string text)
    {
        _status.text = text;
        _messageUntil = Time.unscaledTime + 8;
    }

    private Button MakeButton(Transform parent, string name, string title, float x, float y, float width, Action action)
    {
        Button button = UiFactory.CreateButton(parent, name, title, UiFactory.SurfaceRaised, out _);
        Top(button.GetComponent<RectTransform>(), x, y, width, 32);
        _context.Bind(button, action);
        return button;
    }

    private static S1Text Text(Transform parent, string name, string text, float size, float x, float y, float width, float height, bool bold = false)
    {
        S1Text label = UiFactory.CreateText(parent, name, text, size, UiFactory.TextPrimary, S1Alignment.MidlineLeft, bold);
        label.richText = false;
        Top(label.rectTransform, x, y, width, height);
        return label;
    }

    private static RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max, Vector2 paddingMin, Vector2 paddingMax)
    {
        GameObject panel = UiFactory.CreatePanel(parent, name, UiFactory.SurfaceInset);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = paddingMin;
        rect.offsetMax = paddingMax;
        panel.AddComponent<RectMask2D>();
        GameObject content = new(name + "Content");
        content.transform.SetParent(panel.transform, false);
        RectTransform rows = content.AddComponent<RectTransform>();
        rows.anchorMin = new Vector2(0, 1);
        rows.anchorMax = Vector2.one;
        rows.pivot = new Vector2(0.5f, 1);
        rows.sizeDelta = Vector2.zero;
        ScrollRect scroll = panel.AddComponent<ScrollRect>();
        scroll.viewport = rect;
        scroll.content = rows;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 28;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return rows;
    }

    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }
    private static void Bottom(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }
    private static void Clear(RectTransform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}

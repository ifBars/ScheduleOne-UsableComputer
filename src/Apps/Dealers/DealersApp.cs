using System;
using System.Collections.Generic;
using System.Linq;
using UsableComputer.API;
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

namespace UsableComputer.Apps.Dealers;

internal sealed class DealersApp : IDesktopAppSession
{
    private readonly DesktopAppContext _context;
    private readonly DealersNativeAdapter _adapter = new();
    private readonly Image _portrait;
    private readonly S1Text _name, _overview, _home, _status;
    private readonly Button _previous, _next, _cancel;
    private readonly Button[] _tabs = new Button[3];
    private readonly RectTransform _rows;
    private readonly ScrollRect _scroll;
    private UiListenerRegistry _rowListeners = new();
    private List<DealerViewModel> _dealers = new();
    private string? _selectedId, _confirmCustomer;
    private int _tab;
    private string _rowsKey = "";
    private float _nextRefresh, _messageUntil;
    private bool _disposed;

    internal DealersApp(DesktopAppContext context)
    {
        _context = context;
        var parent = context.Container;
        _portrait = Picture(parent, "DealerPortrait", null, 12, 12, 64);
        _name = Text(parent, "DealerName", "Dealers", 22, 88, 12, 370, 30);
        _previous = Button(parent, "DealerPrevious", "<", 490, 12, 34, () => Navigate(-1));
        _next = Button(parent, "DealerNext", ">", 532, 12, 34, () => Navigate(1));
        Button(parent, "DealerReports", "Reports", 574, 12, 88, () => context.OpenApp(Constants.ReportsAppId));
        _overview = Text(parent, "DealerOverview", "", 15, 88, 48, 574, 28);
        _home = Text(parent, "DealerHome", "", 14, 12, 84, 650, 30);
        string[] titles = { "Stock", "Customers", "Assign customer" };
        for (int index = 0; index < titles.Length; index++)
        {
            int tab = index;
            _tabs[index] = Button(parent, "DealerTab_" + index, titles[index], 12 + index * 160, 126, 150,
                () => { _tab = tab; _confirmCustomer = null; _rowsKey = ""; Refresh(); _scroll!.verticalNormalizedPosition = 1; });
        }
        _cancel = Button(parent, "DealerCancel", "Cancel", 526, 126, 136, () => { _confirmCustomer = null; Refresh(); });
        GameObject viewport = UiFactory.CreatePanel(parent, "DealerViewport", UiFactory.SurfaceInset);
        RectTransform rect = viewport.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 80); rect.offsetMax = new Vector2(-12, -168);
        viewport.AddComponent<RectMask2D>();
        var content = new GameObject("DealerRows");
        content.transform.SetParent(viewport.transform, false);
        _rows = content.AddComponent<RectTransform>();
        _rows.anchorMin = new Vector2(0, 1); _rows.anchorMax = Vector2.one;
        _rows.pivot = new Vector2(0.5f, 1); _rows.sizeDelta = Vector2.zero;
        _scroll = viewport.AddComponent<ScrollRect>();
        _scroll.viewport = rect; _scroll.content = _rows; _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 28;
        _status = Text(parent, "DealerStatus", "", 13, 12, 0, 650, 58);
        _status.textWrappingMode = S1Wrapping.Normal;
        _status.rectTransform.anchorMin = Vector2.zero; _status.rectTransform.anchorMax = new Vector2(1, 0);
        _status.rectTransform.pivot = Vector2.zero;
        _status.rectTransform.offsetMin = new Vector2(12, 10); _status.rectTransform.offsetMax = new Vector2(-12, 68);
        UiFactory.SetLayerRecursively(parent.gameObject, Constants.UiLayer);
    }

    public void OnOpened() => Refresh();
    public void OnClosed() { }
    public void OnTick()
    {
        if (!_disposed && Time.unscaledTime >= _nextRefresh) { _nextRefresh = Time.unscaledTime + 1; Refresh(); }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _rowListeners.Dispose(); }

    private void Navigate(int direction)
    {
        int index = _dealers.FindIndex(dealer => dealer.Id == _selectedId) + direction;
        if (index < 0 || index >= _dealers.Count) return;
        _selectedId = _dealers[index].Id; _confirmCustomer = null; Refresh();
        _scroll.verticalNormalizedPosition = 1;
    }

    private void Refresh()
    {
        bool ready = _adapter.TryRead(out _dealers, out List<DealerCustomerViewModel> available, out string message);
        if (!_dealers.Any(dealer => dealer.Id == _selectedId)) { _selectedId = _dealers.FirstOrDefault()?.Id; _confirmCustomer = null; }
        DealerViewModel? selected = _dealers.FirstOrDefault(dealer => dealer.Id == _selectedId);
        int index = _dealers.FindIndex(dealer => dealer.Id == _selectedId);
        _previous.interactable = index > 0; _next.interactable = index >= 0 && index < _dealers.Count - 1;
        _portrait.sprite = selected?.Portrait; _portrait.enabled = _portrait.sprite != null;
        _name.text = selected == null ? "No recruited dealers" : $"{selected.Name} ({index + 1}/{_dealers.Count})";
        _overview.text = selected == null ? "" : $"Cash held  ${selected.Cash:N2}    Cut  {selected.Cut:P0}    Customers  {selected.Customers.Count}/{DealersNativeAdapter.MaximumCustomers}";
        _home.text = selected == null ? "Recruit a dealer in person to manage them here." : $"{selected.Region}  |  Home: {selected.Home}";
        for (int tab = 0; tab < _tabs.Length; tab++) _tabs[tab].interactable = selected != null && tab != _tab;
        _cancel.gameObject.SetActive(_confirmCustomer != null);
        if (!ready || Time.unscaledTime >= _messageUntil)
            _status.text = !ready ? message : _confirmCustomer != null
                ? (_tab == 2 ? "Confirm assignment? Any outstanding offer from this customer will expire." : "Confirm removal? The customer will no longer be assigned to this dealer.")
                : "Stock includes overflow storage. Product quantities are units. Collect cash and supply stock in person.";

        List<DealerCustomerViewModel> customers = _tab == 2 ? available : selected?.Customers ?? new();
        string key = _selectedId + ":" + _tab + ":" + _confirmCustomer + ":" + selected?.Customers.Count + ":" +
            (_tab == 0 ? string.Join("|", selected?.Stock.Select(item => item.Name + item.Quality + item.Quantity) ?? Array.Empty<string>())
                : string.Join("|", customers.Select(customer => customer.Id + customer.Name + customer.Standards)));
        if (key == _rowsKey) return;
        _rowsKey = key;
        _rowListeners.Dispose(); _rowListeners = new();
        for (int child = _rows.childCount - 1; child >= 0; child--)
        {
            Transform old = _rows.GetChild(child); old.SetParent(null, false); UnityEngine.Object.Destroy(old.gameObject);
        }
        int count = selected == null ? 0 : _tab == 0 ? selected.Stock.Count : customers.Count;
        _rows.sizeDelta = new Vector2(0, Math.Max(70, count * 56));
        if (count == 0)
            Text(_rows, "DealerEmpty", selected == null ? "Your recruited dealers will appear here."
                : _tab == 0 ? "No stock. Visit the dealer to supply packaged products."
                : _tab == 1 ? "No customers assigned. Choose Assign customer to get started."
                : "No unassigned customers. Meet more customers or remove an existing assignment.", 14, 10, 8, 610, 52).textWrappingMode = S1Wrapping.Normal;
        for (int rowIndex = 0; rowIndex < count; rowIndex++)
        {
            GameObject row = UiFactory.CreatePanel(_rows, "DealerRow_" + rowIndex, rowIndex % 2 == 0 ? UiFactory.SurfaceRaised : UiFactory.SurfaceInset);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 54); rect.anchoredPosition = new Vector2(0, -rowIndex * 56);
            if (_tab == 0)
            {
                DealerStockViewModel item = selected!.Stock[rowIndex];
                Picture(row.transform, "StockIcon", item.Icon, 6, 5, 44);
                Text(row.transform, "StockName", item.Name, 16, 60, 2, 425, 26);
                Text(row.transform, "StockQuality", item.Quality, 13, 60, 27, 425, 22);
                Text(row.transform, "StockQuantity", item.Quantity + " units", 15, 500, 12, 125, 28);
            }
            else
            {
                DealerCustomerViewModel customer = customers[rowIndex];
                Picture(row.transform, "CustomerPortrait", customer.Portrait, 6, 5, 44);
                Text(row.transform, "CustomerName", customer.Name, 16, 60, 2, 410, 26);
                Text(row.transform, "CustomerInfo", customer.Region + " | " + customer.Standards + " standards", 13, 60, 27, 410, 22);
                Button action = UiFactory.CreateButton(row.transform, "DealerCustomer_" + customer.Id,
                    _confirmCustomer == customer.Id ? "Confirm" : _tab == 2 ? "Assign" : "Remove", UiFactory.SurfaceRaised, out _);
                Top(action.GetComponent<RectTransform>(), 492, 10, 134, 34);
                action.interactable = _tab != 2 || selected!.Customers.Count < DealersNativeAdapter.MaximumCustomers;
                string dealerId = selected!.Id;
                bool assign = _tab == 2;
                _rowListeners.Add(() => ChangeCustomer(dealerId, customer.Id, assign), action.onClick);
            }
        }
        UiFactory.SetLayerRecursively(_rows.gameObject, Constants.UiLayer);
    }

    private void ChangeCustomer(string dealerId, string customerId, bool assign)
    {
        if (_confirmCustomer != customerId) { _confirmCustomer = customerId; _messageUntil = 0; Refresh(); return; }
        _adapter.TryChangeCustomer(dealerId, customerId, assign, out string message);
        _confirmCustomer = null; _status.text = message; _messageUntil = Time.unscaledTime + 8; Refresh();
    }

    private Button Button(Transform parent, string name, string title, float x, float y, float width, Action action)
    {
        Button button = UiFactory.CreateButton(parent, name, title, UiFactory.SurfaceRaised, out _);
        Top(button.GetComponent<RectTransform>(), x, y, width, 32); _context.Bind(button, action); return button;
    }
    private static S1Text Text(Transform parent, string name, string text, float size, float x, float y, float width, float height)
    {
        S1Text label = UiFactory.CreateText(parent, name, text, size, UiFactory.TextPrimary, S1Alignment.MidlineLeft);
        Top(label.rectTransform, x, y, width, height);
        label.richText = false; label.textWrappingMode = S1Wrapping.NoWrap; label.overflowMode = S1Overflow.Ellipsis; return label;
    }
    private static Image Picture(Transform parent, string name, Sprite? sprite, float x, float y, float size)
    {
        var imageObject = new GameObject(name); imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        Top(image.GetComponent<RectTransform>(), x, y, size, size); return image;
    }
    private static void Top(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y);
    }
}

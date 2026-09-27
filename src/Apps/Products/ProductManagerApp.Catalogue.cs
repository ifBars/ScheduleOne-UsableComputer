using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Native;
using UsableComputer.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#else
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.Apps.Products;

internal sealed partial class ProductManagerApp
{
    private readonly DesktopAppContext _context;
    private List<ProductViewModel> _visibleProducts = new();
    private string _search = string.Empty;
    private ProductFilter _filter;
    private ProductSort _sort;
    private string? _type;
    private bool _compact = true;
    private float _gridWidth;
    private string _feedback = string.Empty;
    private float _feedbackUntil;

    private void BuildCatalogueControls(DesktopAppContext context)
    {
        var search = UiFactory.CreateInputField(context.Container, "ProductSearch", "", "Search name or type", false, out S1Text text);
        ToolbarRect(search.GetComponent<RectTransform>(), 0f, 0.32f);
        search.characterLimit = 100;
        text.fontSize = 13f;
        text.richText = false;
        var trigger = search.gameObject.AddComponent<EventTrigger>();
        context.Listeners.AddTrigger(trigger, EventTriggerType.Select, _ => context.SetTyping(true));
        context.Listeners.AddTrigger(trigger, EventTriggerType.Deselect, _ => context.SetTyping(false));
        context.Listeners.Add<string>(_ => context.SetTyping(false), search.onEndEdit);
        context.Listeners.Add<string>(value =>
        {
            _search = value;
            if (search.isFocused) context.SetTyping(true);
            ApplyCatalogue(true);
        }, search.onValueChanged);
        AddControl("ProductFilter", "All products", 0.32f, 0.49f, label =>
        {
            _filter = (ProductFilter)(((int)_filter + 1) % 4);
            label.text = _filter == ProductFilter.All ? "All products" : _filter.ToString();
            ApplyCatalogue(true);
        });
        AddControl("ProductType", "All types", 0.49f, 0.65f, label =>
        {
            var types = new List<string>();
            foreach (ProductViewModel product in _products)
                if (!types.Contains(product.ProductType)) types.Add(product.ProductType);
            types.Sort(StringComparer.OrdinalIgnoreCase);
            int next = _type == null ? 0 : types.IndexOf(_type) + 1;
            _type = next < types.Count ? types[next] : null;
            label.text = _type ?? "All types";
            ApplyCatalogue(true);
        });
        AddControl("ProductSort", "Name A-Z", 0.65f, 0.84f, label =>
        {
            _sort = (ProductSort)(((int)_sort + 1) % 3);
            label.text = _sort == ProductSort.Name ? "Name A-Z" : _sort == ProductSort.ValueHigh ? "Value: high" : "Value: low";
            ApplyCatalogue(true);
        });
        AddControl("ProductDensity", "Compact", 0.84f, 1f, label =>
        {
            _compact = !_compact;
            label.text = _compact ? "Compact" : "Large tiles";
            RebuildProductRows();
        });
        if (_status != null) _status.richText = false;
    }

    private void AddControl(string name, string title, float left, float right, Action<S1Text> clicked)
    {
        Button button = UiFactory.CreateButton(_context.Container, name, title, UiFactory.SurfaceRaised, out S1Text label);
        label.fontSize = 12f;
        label.richText = false;
        ToolbarRect(button.GetComponent<RectTransform>(), left, right);
        _context.Listeners.Add(() => clicked(label), button.onClick);
    }

    private static void ToolbarRect(RectTransform rect, float left, float right)
    {
        rect.anchorMin = new Vector2(left, 1f);
        rect.anchorMax = new Vector2(right, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(8f, -104f);
        rect.offsetMax = new Vector2(-4f, -68f);
    }

    private static void SetActionRect(RectTransform rect, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-24f, 30f);
        rect.anchoredPosition = new Vector2(0f, bottom);
    }

    private void ApplyCatalogue(bool resetScroll)
    {
        _visibleProducts = ProductCatalogue.Select(_products, _search, _filter, _type, _sort);
        if (FindSelectedProduct() == null)
            _selectedProductId = _visibleProducts.Count > 0 ? _visibleProducts[0].Id : null;
        RebuildProductRows();
        if (resetScroll && _productScroll != null)
        {
            _productScroll.StopMovement();
            _productScroll.verticalNormalizedPosition = 1f;
        }
        UpdateDetail();
    }

    private void UpdateGridSize()
    {
        if (_productScroll != null && Math.Abs(_gridWidth - _productScroll.viewport.rect.width) > 1f)
            ApplyGridSize();
    }

    private void ApplyGridSize()
    {
        if (_productList == null || _productScroll == null) return;
        _gridWidth = _productScroll.viewport.rect.width;
        float width = Mathf.Max(80f, _gridWidth - 8f);
        float idealWidth = _compact ? 80f : 116f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((width + 6f) / (idealWidth + 6f)));
        float height = _compact ? 98f : 134f;
        GridLayoutGroup grid = _productList.GetComponent<GridLayoutGroup>();
        grid.constraintCount = columns;
        grid.cellSize = new Vector2((width - (columns - 1) * 6f) / columns, height);
        int rows = Mathf.CeilToInt(_visibleProducts.Count / (float)columns);
        _productList.sizeDelta = new Vector2(0f, Mathf.Max(1f, rows * (height + 6f) + 8f));
    }

    private void AddTileSummary(Transform parent, ProductViewModel product)
    {
        S1Text summary = UiFactory.CreateText(parent, "Summary", $"${product.ValueLabel} | {(product.IsListed ? "Listed" : "Unlisted")}",
            _compact ? 10f : 12f, product.Id == _selectedProductId ? UiFactory.TextOnAccent : UiFactory.TextPrimary, GetCenterAlignment());
        summary.rectTransform.anchorMin = Vector2.zero;
        summary.rectTransform.anchorMax = new Vector2(1f, 0f);
        summary.rectTransform.pivot = new Vector2(0.5f, 0f);
        summary.rectTransform.sizeDelta = new Vector2(-4f, 16f);
        summary.rectTransform.anchoredPosition = new Vector2(0f, 3f);
    }

    private void ShowFeedback(string message)
    {
        _feedback = message;
        _feedbackUntil = Time.unscaledTime + 4f;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status == null) return;
        _status.text = Time.unscaledTime < _feedbackUntil ? _feedback : !_dataAvailable
            ? "Product data is unavailable. Waiting for the game..." : _products.Count == 0
            ? "No discovered products yet."
            : _visibleProducts.Count == 0 ? "No matching products. Change the search or filters."
            : $"{_visibleProducts.Count} of {_products.Count} discovered products";
    }
}

using System;
using System.Collections.Generic;
using UsableComputer.API;
using UsableComputer.Logic;
using UsableComputer.Native;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPPMELON
using S1Text = Il2CppTMPro.TextMeshProUGUI;
#elif MONOMELON
using S1Text = TMPro.TextMeshProUGUI;
#endif

namespace UsableComputer.UI;

internal sealed class ProductManagerApp : IDesktopAppSession
{
    private UiListenerRegistry _rowListeners = new();
    private readonly List<ProductViewModel> _products = new();
    private RectTransform? _productList;
    private ScrollRect? _productScroll;
    private S1Text? _status;
    private S1Text? _detailTitle;
    private S1Text? _detailIdentity;
    private S1Text? _detailValue;
    private S1Text? _listedLabel;
    private Image? _detailIcon;
    private Button? _listedButton;
    private Button? _favouriteButton;
    private string? _selectedProductId;
    private float _nextRefresh;
    private bool _disposed;

    internal ProductManagerApp(DesktopAppContext context)
    {
        Build(context.Container, context.Listeners);
    }

    public void OnOpened()
    {
        RefreshFromNative(forceRows: true);
    }

    public void OnClosed()
    {
    }

    public void OnTick()
    {
        if (_disposed || Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + 1f;
        RefreshFromNative(forceRows: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _rowListeners.Dispose();
    }

    private void Build(Transform parent, UiListenerRegistry listeners)
    {
        S1Text heading = UiFactory.CreateText(
            parent,
            "Heading",
            "Product Manager",
            21f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(heading.rectTransform, 32f, -10f);

        _status = UiFactory.CreateText(
            parent,
            "Status",
            "Reading discovered products...",
            13f,
            UiFactory.TextMuted,
            GetHeadingAlignment());
        SetTopRect(_status.rectTransform, 22f, -42f);

        GameObject listPanel = UiFactory.CreatePanel(parent, "ProductList", UiFactory.SurfaceRaised);
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(0.46f, 1f);
        listRect.offsetMin = new Vector2(8f, 8f);
        listRect.offsetMax = new Vector2(-6f, -74f);

        S1Text listHeading = UiFactory.CreateText(
            listPanel.transform,
            "ListHeading",
            "My products",
            15f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(listHeading.rectTransform, 26f, -10f);

        _productScroll = listPanel.AddComponent<ScrollRect>();
        _productScroll.horizontal = false;
        _productScroll.vertical = true;
        _productScroll.inertia = true;
        _productScroll.scrollSensitivity = 28f;
        _productScroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = new GameObject("ProductViewport");
        viewportObject.transform.SetParent(listPanel.transform, false);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -42f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject listContent = new GameObject("ProductRows");
        listContent.transform.SetParent(viewportObject.transform, false);
        _productList = listContent.AddComponent<RectTransform>();
        _productList.anchorMin = new Vector2(0f, 1f);
        _productList.anchorMax = new Vector2(1f, 1f);
        _productList.pivot = new Vector2(0.5f, 1f);
        _productList.sizeDelta = new Vector2(0f, 1f);
        _productList.anchoredPosition = Vector2.zero;
        GridLayoutGroup grid = listContent.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.cellSize = new Vector2(80f, 96f);
        grid.spacing = new Vector2(6f, 6f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        _productScroll.viewport = viewportRect;
        _productScroll.content = _productList;

        GameObject detailPanel = UiFactory.CreatePanel(parent, "ProductDetails", UiFactory.SurfaceRaised);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.46f, 0f);
        detailRect.anchorMax = Vector2.one;
        detailRect.offsetMin = new Vector2(6f, 8f);
        detailRect.offsetMax = new Vector2(-8f, -74f);

        _detailTitle = UiFactory.CreateText(
            detailPanel.transform,
            "DetailTitle",
            "Select a product",
            19f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(_detailTitle.rectTransform, 30f, -12f);
        _detailTitle.rectTransform.sizeDelta = new Vector2(-150f, 30f);
        _detailTitle.rectTransform.anchoredPosition = new Vector2(58f, -12f);

        GameObject detailIconObject = new("ProductIcon");
        detailIconObject.transform.SetParent(detailPanel.transform, false);
        RectTransform detailIconRect = detailIconObject.AddComponent<RectTransform>();
        detailIconRect.anchorMin = new Vector2(0f, 1f);
        detailIconRect.anchorMax = new Vector2(0f, 1f);
        detailIconRect.pivot = new Vector2(0f, 1f);
        detailIconRect.sizeDelta = new Vector2(96f, 96f);
        detailIconRect.anchoredPosition = new Vector2(14f, -14f);
        _detailIcon = detailIconObject.AddComponent<Image>();
        _detailIcon.color = Color.white;
        _detailIcon.preserveAspect = true;
        _detailIcon.raycastTarget = false;

        _detailIdentity = UiFactory.CreateText(
            detailPanel.transform,
            "DetailIdentity",
            "",
            14f,
            UiFactory.TextMuted,
            GetHeadingAlignment());
        SetTopRect(_detailIdentity.rectTransform, 24f, -50f);
        _detailIdentity.rectTransform.sizeDelta = new Vector2(-150f, 24f);
        _detailIdentity.rectTransform.anchoredPosition = new Vector2(58f, -50f);

        _detailValue = UiFactory.CreateText(
            detailPanel.transform,
            "DetailValue",
            "",
            17f,
            UiFactory.TextPrimary,
            GetHeadingAlignment(),
            bold: true);
        SetTopRect(_detailValue.rectTransform, 28f, -82f);
        _detailValue.rectTransform.sizeDelta = new Vector2(-150f, 28f);
        _detailValue.rectTransform.anchoredPosition = new Vector2(58f, -82f);

        _listedLabel = UiFactory.CreateText(
            detailPanel.transform,
            "ListedState",
            "",
            15f,
            UiFactory.TextMuted,
            GetHeadingAlignment());
        SetTopRect(_listedLabel.rectTransform, 28f, -128f);

        _listedButton = UiFactory.CreateButton(
            detailPanel.transform,
            "ToggleListed",
            "List",
            UiFactory.Accent,
            out S1Text listedLabel);
        _listedButton.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
        _listedButton.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
        _listedButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        _listedButton.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 34f);
        _listedButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(14f, 24f);
        listedLabel.text = "Toggle listing";
        listeners.Add(ToggleListed, _listedButton.onClick);

        _favouriteButton = UiFactory.CreateButton(
            detailPanel.transform,
            "ToggleFavourite",
            "Favourite",
            UiFactory.Surface,
            out S1Text favouriteLabel);
        RectTransform favouriteRect = _favouriteButton.GetComponent<RectTransform>();
        favouriteRect.anchorMin = new Vector2(0f, 0f);
        favouriteRect.anchorMax = new Vector2(0f, 0f);
        favouriteRect.pivot = new Vector2(0f, 0.5f);
        favouriteRect.sizeDelta = new Vector2(150f, 34f);
        favouriteRect.anchoredPosition = new Vector2(174f, 24f);
        favouriteLabel.text = "Toggle favourite";
        listeners.Add(ToggleFavourited, _favouriteButton.onClick);

        S1Text note = UiFactory.CreateText(
            detailPanel.transform,
            "Note",
            "Listing changes use the native ProductManager service. If its network singleton is not ready, this view remains read-only.",
            13f,
            UiFactory.TextMuted,
            GetBodyAlignment());
        note.rectTransform.anchorMin = Vector2.zero;
        note.rectTransform.anchorMax = Vector2.one;
        note.rectTransform.offsetMin = new Vector2(14f, 68f);
        note.rectTransform.offsetMax = new Vector2(-14f, -174f);

        UiFactory.SetLayerRecursively(listPanel, Constants.UiLayer);
        UiFactory.SetLayerRecursively(detailPanel, Constants.UiLayer);
    }

    private void RefreshFromNative(bool forceRows)
    {
        List<ProductViewModel> fresh = ProductManagerNativeAdapter.ReadDiscoveredProducts();
        bool rowsChanged = forceRows || !SameProductTiles(_products, fresh);
        _products.Clear();
        _products.AddRange(fresh);

        if (_selectedProductId == null || FindSelectedProduct() == null)
            _selectedProductId = _products.Count > 0 ? _products[0].Id : null;

        if (rowsChanged)
            RebuildProductRows();

        UpdateDetail();
    }

    private void RebuildProductRows()
    {
        if (_productList == null)
            return;

        _rowListeners.Dispose();
        _rowListeners = new UiListenerRegistry();
        int rowCount = Mathf.CeilToInt(_products.Count / 3f);
        _productList.sizeDelta = new Vector2(0f, Mathf.Max(1f, (rowCount * 102f) + 8f));
        _productList.anchoredPosition = Vector2.zero;
        if (_productScroll != null)
        {
            _productScroll.StopMovement();
            _productScroll.verticalNormalizedPosition = 1f;
        }

        for (int index = _productList.childCount - 1; index >= 0; index--)
        {
            Transform child = _productList.GetChild(index);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }

        for (int index = 0; index < _products.Count; index++)
        {
            ProductViewModel product = _products[index];
            Sprite? productIcon = ProductManagerNativeAdapter.GetIcon(product.Id);
            Button button = UiFactory.CreateButton(
                _productList,
                $"Product_{index}",
                string.Empty,
                product.IsListed ? new Color(0.78f, 0.9f, 0.76f, 1f) : UiFactory.SurfaceInset,
                out S1Text buttonLabel);
            buttonLabel.gameObject.SetActive(false);

            GameObject iconObject = new("Icon");
            iconObject.transform.SetParent(button.transform, false);
            RectTransform iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(58f, 58f);
            iconRect.anchoredPosition = new Vector2(0f, -5f);
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = productIcon;
            icon.color = productIcon != null ? Color.white : UiFactory.TextMuted;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            S1Text name = UiFactory.CreateText(
                button.transform,
                "Name",
                product.Name,
                10.5f,
                UiFactory.TextPrimary,
                GetCenterAlignment());
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.sizeDelta = new Vector2(-4f, 29f);
            name.rectTransform.anchoredPosition = new Vector2(0f, 3f);

            if (product.IsFavourited)
            {
                S1Text favourite = UiFactory.CreateText(
                    button.transform,
                    "Favourite",
                    "★",
                    16f,
                    new Color(0.95f, 0.65f, 0.08f, 1f),
                    GetCenterAlignment(),
                    bold: true);
                favourite.rectTransform.anchorMin = new Vector2(1f, 1f);
                favourite.rectTransform.anchorMax = new Vector2(1f, 1f);
                favourite.rectTransform.pivot = new Vector2(1f, 1f);
                favourite.rectTransform.sizeDelta = new Vector2(22f, 22f);
                favourite.rectTransform.anchoredPosition = new Vector2(-3f, -2f);
            }
            string productId = product.Id;
            _rowListeners.Add(() => SelectProduct(productId), button.onClick);
            UiFactory.SetLayerRecursively(button.gameObject, Constants.UiLayer);
        }

        if (_status != null)
        {
            _status.text = _products.Count == 0
                ? "No discovered products yet, or Product Manager is not ready."
                : $"{_products.Count} discovered product{(_products.Count == 1 ? string.Empty : "s")}";
        }
    }

    private void SelectProduct(string productId)
    {
        _selectedProductId = productId;
        UpdateDetail();
    }

    private void ToggleListed()
    {
        ProductViewModel? selected = FindSelectedProduct();
        if (selected == null)
            return;

        bool listed = !selected.IsListed;
        if (ProductManagerNativeAdapter.TrySetListed(selected.Id, listed, out string message))
        {
            if (_status != null)
                _status.text = message;
        }
        else if (_status != null)
        {
            _status.text = message;
        }

        RefreshFromNative(forceRows: false);
    }

    private void ToggleFavourited()
    {
        ProductViewModel? selected = FindSelectedProduct();
        if (selected == null)
            return;

        ProductManagerNativeAdapter.TrySetFavourited(
            selected.Id,
            !selected.IsFavourited,
            out string message);
        if (_status != null)
            _status.text = message;

        RefreshFromNative(forceRows: false);
    }

    private void UpdateDetail()
    {
        ProductViewModel? selected = FindSelectedProduct();
        if (_detailTitle == null ||
            _detailIdentity == null ||
            _detailValue == null ||
            _listedLabel == null ||
            _detailIcon == null ||
            _listedButton == null ||
            _favouriteButton == null)
        {
            return;
        }

        if (selected == null)
        {
            _detailTitle.text = "Select a product";
            _detailIdentity.text = string.Empty;
            _detailValue.text = "";
            _listedLabel.text = "No product selected.";
            _detailIcon.sprite = null;
            _detailIcon.enabled = false;
            _listedButton.gameObject.SetActive(false);
            _favouriteButton.gameObject.SetActive(false);
            return;
        }

        _detailTitle.text = selected.Name;
        _detailIdentity.text = selected.ProductType;
        _detailValue.text = $"Market value: ${selected.ValueLabel}";
        _listedLabel.text = selected.IsListed
            ? "Listed for customer orders"
            : "Not listed for customer orders";
        Sprite? selectedIcon = ProductManagerNativeAdapter.GetIcon(selected.Id);
        _detailIcon.sprite = selectedIcon;
        _detailIcon.enabled = selectedIcon != null;
        _listedButton.gameObject.SetActive(true);
        _favouriteButton.gameObject.SetActive(true);
    }

    private ProductViewModel? FindSelectedProduct()
    {
        if (_selectedProductId == null)
            return null;

        foreach (ProductViewModel product in _products)
        {
            if (string.Equals(product.Id, _selectedProductId, StringComparison.Ordinal))
                return product;
        }

        return null;
    }

    private static bool SameProductTiles(
        IReadOnlyList<ProductViewModel> left,
        IReadOnlyList<ProductViewModel> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Id, right[index].Id, StringComparison.Ordinal) ||
                left[index].IsListed != right[index].IsListed ||
                left[index].IsFavourited != right[index].IsFavourited)
                return false;
        }

        return true;
    }

    private static void SetTopRect(RectTransform rectTransform, float height, float y)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(-24f, height);
        rectTransform.anchoredPosition = new Vector2(0f, y);
    }

#if IL2CPPMELON
    private static Il2CppTMPro.TextAlignmentOptions GetHeadingAlignment() => Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetBodyAlignment() => Il2CppTMPro.TextAlignmentOptions.TopLeft;
    private static Il2CppTMPro.TextAlignmentOptions GetCenterAlignment() => Il2CppTMPro.TextAlignmentOptions.Center;
#else
    private static TMPro.TextAlignmentOptions GetHeadingAlignment() => TMPro.TextAlignmentOptions.MidlineLeft;
    private static TMPro.TextAlignmentOptions GetBodyAlignment() => TMPro.TextAlignmentOptions.TopLeft;
    private static TMPro.TextAlignmentOptions GetCenterAlignment() => TMPro.TextAlignmentOptions.Center;
#endif
}

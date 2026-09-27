using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

#if IL2CPP
using ProductInput = Il2CppTMPro.TMP_InputField;
#else
using ProductInput = TMPro.TMP_InputField;
#endif

namespace UsableComputer.VfsSmoke;

public sealed partial class Core
{
    private IEnumerator RunProductsScenario()
    {
        object session;
        string selectedId = "";
        bool wasFavourite = false;
        try
        {
            CloseAllWindows();
            Type preferences = GetUsableComputerAssembly().GetType("UsableComputer.PreferencesStore", true)!;
            MethodInfo setTheme = preferences.GetMethod("SetTheme", BindingFlags.Static | BindingFlags.NonPublic)!;
            setTheme.Invoke(null, new[] { Enum.Parse(setTheme.GetParameters()[0].ParameterType, _phase == "seed" ? "Light" : "Dark") });
            _desktop!.GetType().GetMethod("OpenApp", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_desktop, new object[] { "product-manager" });
            session = GetAppSession("product-manager");
        }
        catch (Exception exception) { Fail("Product setup failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.6f);
        try
        {
            object[] products = VisibleProducts(session);
            Require(products.Length > 0, "Fixture has no native discovered products.");
            var search = GameObject.Find("ProductSearch").GetComponent<ProductInput>();
            search.text = "no matching product 938283";
            Require(VisibleProducts(session).Length == 0, "Search did not show empty state.");
            search.text = ReadProduct<string>(products[0], "Name");
            Require(VisibleProducts(session).Length > 0, "Native name search failed.");
            search.text = "";
            GameObject.Find("ProductFilter").GetComponent<Button>().onClick.Invoke();
            Require(VisibleProducts(session).All(p => ReadProduct<bool>(p, "IsListed")), "Listed filter contains unlisted products.");
            for (int index = 0; index < 3; index++) GameObject.Find("ProductFilter").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("ProductSort").GetComponent<Button>().onClick.Invoke();
            float[] values = VisibleProducts(session).Select(p => ReadProduct<float>(p, "MarketValue")).ToArray();
            Require(values.SequenceEqual(values.OrderByDescending(value => value)), "Value sorting is not numeric.");
            GameObject.Find("ProductType").GetComponent<Button>().onClick.Invoke();
            Require(VisibleProducts(session).Select(p => ReadProduct<string>(p, "ProductType")).Distinct().Count() <= 1, "Type filter mixed product types.");
            // Restore all types without depending on the fixture's number of native types.
            int typeSteps = 0;
            while (session.GetType().GetField("_type", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session) != null && typeSteps++ < 32)
                GameObject.Find("ProductType").GetComponent<Button>().onClick.Invoke();
            Require(typeSteps < 32, "Type filter did not return to all types.");
            selectedId = (string)session.GetType().GetField("_selectedProductId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            wasFavourite = ReadProduct<bool>(VisibleProducts(session).Single(p => ReadProduct<string>(p, "Id") == selectedId), "IsFavourited");
            GameObject.Find("ToggleFavourite").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Product query failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(1.2f);
        try
        {
            Require(ReadProduct<bool>(VisibleProducts(session).Single(p => ReadProduct<string>(p, "Id") == selectedId), "IsFavourited") != wasFavourite,
                "Favourite action did not reach native product state.");
            GameObject.Find("ToggleFavourite").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Product native action failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(1.2f);
        try
        {
            Require(ReadProduct<bool>(VisibleProducts(session).Single(p => ReadProduct<string>(p, "Id") == selectedId), "IsFavourited") == wasFavourite,
                "Favourite action did not restore native state.");
        }
        catch (Exception exception) { Fail("Product action restore failed", Unwrap(exception)); yield break; }
        string screenshot = Path.Combine(_outputDirectory, "products-compact.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            ValidateProductGrid();
            GameObject.Find("ProductDensity").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Window_product-manager").transform.Find("TitleBar/Maximize").GetComponent<Button>().onClick.Invoke();
        }
        catch (Exception exception) { Fail("Product layout failed", Unwrap(exception)); yield break; }
        yield return new WaitForSecondsRealtime(0.4f);
        screenshot = Path.Combine(_outputDirectory, "products-large.png");
        ScreenCapture.CaptureScreenshot(screenshot); yield return WaitForCapture(screenshot);
        if (_completed) yield break;
        try
        {
            ValidateProductGrid();
            var scroll = GameObject.Find("ProductList").GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            RectTransform last = scroll.content.GetChild(scroll.content.childCount - 1).GetComponent<RectTransform>();
            Vector3[] corners = GetWorldCorners(last);
            float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
            Require(bottom >= scroll.viewport.rect.yMin - 1f, "Last product row is unreachable.");
            LoggerInstance.Msg($"[UsableComputerProductsSmoke] PASS Runtime={ConstantsRuntime()} Phase={_phase} Search=True Filter=True Sort=True Density=True GridFits=True NativeFavourite=True");
        }
        catch (Exception exception) { Fail("Product expanded layout failed", Unwrap(exception)); }
    }

    private static T ReadProduct<T>(object product, string property) => (T)product.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(product)!;
    private static object[] VisibleProducts(object session) => ((IEnumerable)session.GetType().GetField("_visibleProducts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!).Cast<object>().ToArray();
    private static void ValidateProductGrid()
    {
        var grid = GameObject.Find("ProductRows").GetComponent<GridLayoutGroup>();
        var rect = grid.GetComponent<RectTransform>();
        float required = grid.padding.horizontal + grid.constraintCount * grid.cellSize.x + (grid.constraintCount - 1) * grid.spacing.x;
        Require(required <= rect.rect.width + 1f, "Product grid overflows horizontally.");
        Require(GameObject.Find("ProductIcon").GetComponent<Image>().sprite != null, "Native preview image is missing.");
    }
}

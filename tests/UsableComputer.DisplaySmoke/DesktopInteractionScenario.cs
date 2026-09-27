using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Reflection;
using UsableComputer.Tests.Shared;

namespace UsableComputer.DisplaySmoke;

internal static class DesktopInteractionScenario
{
    internal static IEnumerator Run(GameObject desktop, Camera camera, string outputDirectory,
        bool verifyIconPosition = false, bool expectSavedIconPosition = false)
    {
        RectTransform window = desktop.transform.Find("WindowLayer/Window_settings").GetComponent<RectTransform>();
        RectTransform parent = window.parent.GetComponent<RectTransform>();
        EventTrigger title = window.Find("TitleBar").GetComponent<EventTrigger>();
        Vector3 original = window.localPosition;
        Vector2 ScreenPoint(Vector2 local) => RectTransformUtility.WorldToScreenPoint(camera, parent.TransformPoint(local));
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            pointerId = -1,
            position = ScreenPoint(original)
        };
        title.OnPointerDown(pointer);
        pointer.position = ScreenPoint((Vector2)original + new Vector2(20f, -10f));
        title.OnDrag(pointer);
        Require(Vector2.Distance(window.localPosition, (Vector2)original + new Vector2(20f, -10f)) < 0.1f,
            "Title-bar drag did not preserve the grab offset.");
        Require(window.GetSiblingIndex() == parent.childCount - 1, "Dragging did not focus the window.");

        pointer.position = ScreenPoint(new Vector2(2000f, -2000f));
        title.OnDrag(pointer);
        Require(window.localPosition.x + window.rect.width / 2f <= parent.rect.xMax + 0.1f,
            "Window escaped the right edge.");
        Require(window.localPosition.y - window.rect.height / 2f >= parent.rect.yMin + 42f - 0.1f,
            "Window overlapped the taskbar.");
        title.OnEndDrag(pointer);
        Vector3 released = window.localPosition;
        pointer.position = ScreenPoint(Vector2.zero);
        title.OnDrag(pointer);
        Require(Vector3.Distance(released, window.localPosition) < 0.1f, "Window kept dragging after release.");

        pointer.button = PointerEventData.InputButton.Right;
        title.OnPointerDown(pointer);
        pointer.position = ScreenPoint(new Vector2(-50f, 50f));
        title.OnDrag(pointer);
        Require(Vector3.Distance(released, window.localPosition) < 0.1f, "Right-click started a drag.");
        window.localPosition = original;

        Button maximize = window.Find("TitleBar/Maximize").GetComponent<Button>();
        maximize.onClick.Invoke();
        Vector3 maximized = window.localPosition;
        pointer.button = PointerEventData.InputButton.Left;
        title.OnPointerDown(pointer);
        pointer.position = ScreenPoint(Vector2.zero);
        title.OnDrag(pointer);
        Require(Vector3.Distance(maximized, window.localPosition) < 0.1f, "Maximized window moved.");
        maximize.onClick.Invoke();
        Require(Vector3.Distance(original, window.localPosition) < 0.1f, "Restore lost the window position.");

        Button icon = desktop.transform.Find("DesktopIconViewport/DesktopIcons").GetChild(0).GetComponent<Button>();
        Image background = icon.GetComponent<Image>();
        Require(background.color.a > 0.99f, "Icon base color makes its hover invisible.");
        EventSystem.current.SetSelectedGameObject(null);
        icon.OnPointerExit(pointer);
        yield return new WaitForSecondsRealtime(0.2f);
        Require(background.canvasRenderer.GetColor().a < 0.01f, "Idle icon highlight is not transparent.");
        icon.OnPointerEnter(pointer);
        yield return new WaitForSecondsRealtime(0.2f);
        Require(background.canvasRenderer.GetColor().a > 0.4f, "Icon hover did not fade in.");
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "icon-hover.png"));
        yield return new WaitForSecondsRealtime(0.1f);
        icon.OnPointerExit(pointer);
        yield return new WaitForSecondsRealtime(0.2f);
        Require(background.canvasRenderer.GetColor().a < 0.01f, "Icon hover did not fade out.");
        if (verifyIconPosition)
        {
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            RectTransform iconParent = iconRect.parent.GetComponent<RectTransform>();
            Vector2 before = iconRect.anchoredPosition;
            Type preferences = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(ModTypeNames.PreferencesStore))
                .First(type => type != null)!;
            string id = icon.name.Substring("DesktopIcon_".Length);
            MethodInfo getPosition = preferences.GetMethod("GetIconPosition", BindingFlags.Static | BindingFlags.NonPublic)!;
            if (expectSavedIconPosition)
            {
                Vector2 persisted = (Vector2)getPosition.Invoke(null, new object[] { id, new Vector2(-9999f, -9999f) })!;
                Require(Vector2.Distance(persisted, before) < 0.1f, "Saved icon position did not survive process reload.");
            }
            pointer.button = PointerEventData.InputButton.Left;
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera, iconRect.position);
            EventTrigger drag = icon.GetComponent<EventTrigger>();
            drag.OnBeginDrag(pointer);
            pointer.position = RectTransformUtility.WorldToScreenPoint(camera,
                iconRect.position + iconParent.TransformVector(new Vector3(18f, -8f, 0f)));
            drag.OnDrag(pointer);
            drag.OnEndDrag(pointer);
            Require(Vector2.Distance(before, iconRect.anchoredPosition) > 1f, "Desktop icon did not move.");
            Vector2 saved = (Vector2)getPosition.Invoke(null, new object[] { id, Vector2.zero })!;
            Require(Vector2.Distance(saved, iconRect.anchoredPosition) < 0.1f, "Desktop icon position was not recorded.");
        }
        VerifyExplorer(desktop, pointer);
        yield return new WaitForSecondsRealtime(0.3f);
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "explorer-ux.png"));
        yield return new WaitForSecondsRealtime(0.2f);
        MelonLoader.MelonLogger.Msg("[UsableComputerDisplaySmoke] Drag bounds, release, right-click, maximize/restore and icon hover verified.");
    }

    private static void VerifyExplorer(GameObject desktop, PointerEventData pointer)
    {
        Type service = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(ModTypeNames.VirtualFileSystemService))
            .First(type => type != null)!;
        object Call(string method, params object[] args) => service.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
        string Id(object node) => (string)node.GetType().GetProperty("Id")!.GetValue(node)!;
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        string folder = Id(Call("CreateDirectory", "desktop", "UX workspace " + suffix));
        string target = Id(Call("CreateDirectory", folder, "Archive"));
        string file = Id(Call("CreateTextFile", folder, "Memo.txt", "A note for the drag and clipboard checks."));
        Transform Find(string name) => desktop.GetComponentsInChildren<Transform>(true)
            .First(item => item.name == name && item.gameObject.activeInHierarchy);
        void Click(Transform item, int count, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            pointer.button = button; pointer.clickCount = count; pointer.dragging = false;
            item.GetComponent<EventTrigger>().OnPointerClick(pointer);
        }
        Click(Find("DesktopIcon_" + folder), 1);
        Transform original = Find("DesktopIcon_" + folder);
        Click(original, 2);
        Require(Find("Window_files").gameObject.activeInHierarchy, "Desktop double-click did not open Files.");
        Transform fileItem = Find("Item_" + file);
        Click(fileItem, 1);
        Require(Find("Item_" + file) == fileItem && fileItem.gameObject.activeInHierarchy,
            "Selecting a file rebuilt its click target.");
        Click(fileItem, 2);
        Require(Find("Window_notes").gameObject.activeInHierarchy, "File double-click did not open Notes.");
        Find("Window_notes").Find("TitleBar/Minimize").GetComponent<Button>().onClick.Invoke();
        Click(fileItem, 1, PointerEventData.InputButton.Right);
        Require(Find("ContextMenu").gameObject.activeInHierarchy, "File context menu did not open.");
        Find("ContextMenu").Find("Menu_2").GetComponent<Button>().onClick.Invoke(); // Copy
        Click(Find("Item_" + target), 2);
        Click(Find("FileList"), 1, PointerEventData.InputButton.Right);
        Find("ContextMenu").Find("Menu_2").GetComponent<Button>().onClick.Invoke(); // Paste
        object copied = Call("ResolvePath", "/Desktop/UX workspace " + suffix + "/Archive/Memo.txt");
        Require(Id(copied) != file, "Copy/paste reused the source identity.");
        Require((string)Call("ReadText", Id(copied)) == (string)Call("ReadText", file), "Copy/paste lost text.");
        Find("Window_files").Find("Content/NavigationToolbar/Back").GetComponent<Button>().onClick.Invoke();
        Require(Find("Item_" + file).gameObject.activeInHierarchy, "Back did not restore the prior directory.");
        Find("Window_files").Find("Content/NavigationToolbar/Forward").GetComponent<Button>().onClick.Invoke();
        Require(Find("Item_" + Id(copied)).gameObject.activeInHierarchy, "Forward did not restore navigation history.");
        Find("Window_files").Find("Content/NavigationToolbar/Up").GetComponent<Button>().onClick.Invoke();
        string moved = Id(Call("CreateTextFile", folder, "Drag me.txt", "move"));
        EventTrigger source = Find("Item_" + moved).GetComponent<EventTrigger>();
        pointer.button = PointerEventData.InputButton.Left;
        source.OnBeginDrag(pointer);
        source.OnDrag(pointer);
        Find("Item_" + target).GetComponent<EventTrigger>().OnDrop(pointer);
        source.OnEndDrag(pointer);
        Require(Id(Call("ResolvePath", "/Desktop/UX workspace " + suffix + "/Archive/Drag me.txt")) == moved,
            "Drop into a folder did not move the original item.");
        Click(Find("DesktopIconViewport"), 1, PointerEventData.InputButton.Right);
        Require(Find("ContextMenu").gameObject.activeInHierarchy, "Desktop context menu did not open.");
        Find("ContextMenu").Find("Menu_0").GetComponent<Button>().onClick.Invoke();
        Require(Find("FileDialog").gameObject.activeInHierarchy, "New Folder did not open a naming dialog.");
        Find("FileDialog").Find("Cancel").GetComponent<Button>().onClick.Invoke();
        Click(Find("Item_" + target), 2);
        MelonLoader.MelonLogger.Msg("[UsableComputerDisplaySmoke] Explorer selection, double-click, menus, copy/paste, Back/Forward/Up and folder drop verified.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

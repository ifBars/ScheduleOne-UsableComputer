# S1API phone and TV apps on the computer

The computer automatically lists S1API phone and TV apps that implement S1API's `IExternalAppHost` contract. App authors do not register with Usable Computer or reference its assembly. The contract asks the app to create a separate display session under a new container; the original phone or TV UI keeps its normal ownership and navigation.

The bundled S1API hosting-preview runtime is required. The public `3.2.1-beta.5` release does not contain `S1API.ExternalHosting`. Use the matching Mono or IL2CPP preview DLL from the same Usable Computer package. Do not mix the two runtimes or replace that DLL with the stock release until this contract is published upstream.

## Eligibility and lifecycle

- `IExternalAppHost.AllowExternalHosting` is the explicit opt-in. Return `false` when the app relies on its original device or already has a native/manual desktop adapter.
- `CreateExternalSession` receives a computer-owned UI container and a callback that closes only this window. Create fresh UI and state for each call; never move an existing phone/TV canvas or call device navigation methods from the session.
- `Open`, `Tick`, `Close`, and `Dispose` map to the visible desktop window. Dispose must release event listeners, input state, coroutines, textures, and generated objects owned by that session. The original device's state remains the app's responsibility.
- Identity is `s1api.<family>.<assembly>.<type>.<AppName>`. S1API reports collisions in its catalog; Usable Computer reports collisions with its own app registry and keeps the preexisting desktop app.
- The S1API catalog reports late registration, replacement, removal, and scene reset. Removing an entry unregisters the desktop app and closes any open window. An app without the independent host contract stays on its original device; its type and the missing contract are logged once as a diagnostic.

The [bridge smoke scenario](../tests/UsableComputer.VfsSmoke/BridgeScenario.cs) is a compiled example of one phone app and one TV app implementing the contract. It exercises late registration, opt-out, collision, desktop session lifetime, and original device state in a disposable save. Run it through `tests/Run-VfsSmoke.ps1 -Runtime Mono -GamePath <path> -SourceSavePath <path> -Bridge` (or `-Runtime Il2cpp` for the other backend). A successful compile or contract test alone does not establish in-game compatibility of another mod's session implementation.

The following minimal phone implementation shows the ownership boundary. A TV app uses the same `IExternalAppHost` members and can place its own UI beneath `container`.

```csharp
using System;
using S1API.ExternalHosting;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExamplePhoneApp : PhoneApp, IExternalAppHost
{
    protected override string AppName => "example-phone";
    protected override string AppTitle => "Example phone";
    protected override string IconLabel => "Example";
    protected override string IconFileName => "example.png";

    public bool AllowExternalHosting => true;

    protected override void OnCreatedUI(GameObject container) => AddPanel(container, "Phone content");

    public IExternalAppSession CreateExternalSession(GameObject container, Action requestClose) =>
        new ExampleSession(container);

    private static GameObject AddPanel(GameObject container, string name)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(container.transform, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0.12f, 0.25f, 0.38f);
        return panel;
    }

    private sealed class ExampleSession : IExternalAppSession
    {
        private readonly GameObject _root;
        public ExampleSession(GameObject container)
        {
            _root = AddPanel(container, "Computer content");
            _root.SetActive(false);
        }
        public void Open() => _root.SetActive(true);
        public void Tick() { }
        public void Close() => _root.SetActive(false);
        public void Dispose() => UnityEngine.Object.Destroy(_root);
    }
}
```

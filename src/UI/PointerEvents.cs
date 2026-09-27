using UnityEngine.EventSystems;
#if IL2CPPMELON
using Il2CppInterop.Runtime;
#endif

namespace UsableComputer.UI;

internal static class PointerEvents
{
    internal static PointerEventData? Get(BaseEventData data)
    {
#if IL2CPPMELON
        return data.TryCast<PointerEventData>();
#else
        return data as PointerEventData;
#endif
    }
}

namespace UsableComputer.ChildHost;

internal static class NestedChildProtocol
{
    internal const string MarkerArgument = "--usable-computer-nested-child";
    internal const string MappingArgument = "--usable-computer-nested-map";
    internal const string ProfileArgument = "--usable-computer-nested-profile";
    internal const string ParentArgument = "--usable-computer-nested-parent";
    internal const int Magic = 0x55434E53;
    internal const int HeaderSize = 64;
    internal const int MaximumWidth = 1280;
    internal const int MaximumHeight = 720;
    internal const long MappingSize = HeaderSize + (MaximumWidth * MaximumHeight * 4L);
    internal const int StateStarting = 1;
    internal const int StateReady = 2;
    internal const int StateError = 3;
}

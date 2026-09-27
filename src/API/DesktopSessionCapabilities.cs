namespace UsableComputer.API;

// Optional capabilities a desktop window looks for on its session. They stay internal until the
// contract is ready to publish for other mods.

internal interface IDesktopAppVisibilitySession
{
    void OnVisibilityChanged(bool visible);
}

internal interface IDesktopDirectorySession
{
    void OpenDirectory(string directoryId);
}

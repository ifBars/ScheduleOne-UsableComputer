using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;

namespace UsableComputer.Doom;

internal static class DoomWadLocator
{
    private static readonly string[] PreferredNames =
    {
        "doom2.wad",
        "doom.wad",
        "doom1.wad",
        "freedoom2.wad",
        "freedoom1.wad",
    };

    internal static string WadDirectory => Path.Combine(
        MelonEnvironment.UserDataDirectory,
        "UsableComputer",
        "Doom");

    internal static string? FindIwad()
    {
        Directory.CreateDirectory(WadDirectory);
        foreach (string directory in CandidateDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (string name in PreferredNames)
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        yield return WadDirectory;

        string? programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            string steam = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
            yield return Path.Combine(steam, "Ultimate Doom", "base");
            yield return Path.Combine(steam, "Doom 2", "base");
            yield return Path.Combine(steam, "DOOM 2", "base");
        }
    }
}

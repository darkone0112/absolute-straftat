using Microsoft.Win32;
using System.Runtime.Versioning;

namespace AbsoluteStraftat.Installer;

internal static class SteamLocator
{
    public static string? FindGameDirectory(string gameName)
    {
        foreach (var steamRoot in FindSteamRoots())
        {
            var steamAppsDirectory = Path.Combine(steamRoot, "steamapps");
            if (!Directory.Exists(steamAppsDirectory))
            {
                continue;
            }

            foreach (var gameDirectory in FindGameDirectoriesInSteamApps(steamAppsDirectory, gameName))
            {
                return gameDirectory;
            }

            var libraryFile = Path.Combine(steamAppsDirectory, "libraryfolders.vdf");
            foreach (var libraryPath in VdfReader.ReadValues(libraryFile, "path"))
            {
                var librarySteamApps = Path.Combine(libraryPath, "steamapps");
                foreach (var gameDirectory in FindGameDirectoriesInSteamApps(librarySteamApps, gameName))
                {
                    return gameDirectory;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            AddWindowsRegistrySteamPath(candidates, @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath");
            AddWindowsRegistrySteamPath(candidates, @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                candidates.Add(Path.Combine(programFilesX86, "Steam"));
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                candidates.Add(Path.Combine(home, ".steam", "steam"));
                candidates.Add(Path.Combine(home, ".local", "share", "Steam"));
            }
        }

        return candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsRegistrySteamPath(List<string> candidates, string keyName, string valueName)
    {
        var value = Registry.GetValue(keyName, valueName, null) as string;
        if (!string.IsNullOrWhiteSpace(value))
        {
            candidates.Add(value);
        }
    }

    private static IEnumerable<string> FindGameDirectoriesInSteamApps(string steamAppsDirectory, string gameName)
    {
        if (!Directory.Exists(steamAppsDirectory))
        {
            yield break;
        }

        foreach (var manifest in Directory.EnumerateFiles(steamAppsDirectory, "appmanifest_*.acf"))
        {
            var name = VdfReader.ReadFirstValue(manifest, "name");
            if (!string.Equals(name, gameName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installDirectory = VdfReader.ReadFirstValue(manifest, "installdir");
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                continue;
            }

            yield return Path.Combine(steamAppsDirectory, "common", installDirectory);
        }
    }
}

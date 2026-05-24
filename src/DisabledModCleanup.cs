namespace AbsoluteStraftat.Installer;

internal static class DisabledModCleanup
{
    public static void RemoveGunGame(InstallTarget target)
    {
        RemovePluginFiles(target, "GunGameMod.dll", "Straftat GunGame");
    }

    private static void RemovePluginFiles(InstallTarget target, string fileName, string modName)
    {
        if (!Directory.Exists(target.PluginsDirectory))
        {
            return;
        }

        var removedFiles = Directory
            .EnumerateFiles(target.PluginsDirectory, fileName, SearchOption.AllDirectories)
            .ToArray();

        foreach (var file in removedFiles)
        {
            File.Delete(file);
            Program.PrintRemoved($"{modName}: {Path.GetRelativePath(target.PluginsDirectory, file)}");
        }

        if (removedFiles.Length > 0)
        {
            RemoveEmptyDirectories(target.PluginsDirectory);
        }
    }

    private static void RemoveEmptyDirectories(string rootDirectory)
    {
        foreach (var directory in Directory
            .EnumerateDirectories(rootDirectory, "*", SearchOption.AllDirectories)
            .OrderByDescending(directory => directory.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }
}

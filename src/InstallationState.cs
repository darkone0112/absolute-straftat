namespace AbsoluteStraftat.Installer;

internal static class InstallationState
{
    public static bool HasBepInEx(InstallTarget target)
    {
        return Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "core"))
            && Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "plugins"));
    }

    public static bool HasModMenu(InstallTarget target)
    {
        return HasPluginFile(target, "ModMenu.dll");
    }

    public static bool HasMoreStrafts(InstallTarget target)
    {
        return HasPluginFile(target, "moreStrafts.dll");
    }

    public static bool HasGunGame(InstallTarget target)
    {
        return HasPluginFile(target, "GunGameMod.dll");
    }

    public static bool HasMoreStraftsUiSpawnAddon(InstallTarget target)
    {
        return HasPluginFile(target, "MoreStrafts_UISpawnAddon.dll");
    }

    public static bool HasFancy(InstallTarget target)
    {
        return HasPluginFile(target, "Fancy.dll");
    }

    public static bool HasStraftatCosmeticsBundleIc(InstallTarget target)
    {
        return HasPluginFile(target, "*.cbundle")
            || HasPluginDirectory(target, "Straftat_Cosmetics_Bundle_IC")
            || HasPluginDirectory(target, "CosmeticBundles");
    }

    private static bool HasPluginFile(InstallTarget target, string fileName)
    {
        if (!Directory.Exists(target.PluginsDirectory))
        {
            return false;
        }

        return Directory
            .EnumerateFiles(target.PluginsDirectory, fileName, SearchOption.AllDirectories)
            .Any();
    }

    private static bool HasPluginDirectory(InstallTarget target, string directoryName)
    {
        if (!Directory.Exists(target.PluginsDirectory))
        {
            return false;
        }

        return Directory
            .EnumerateDirectories(target.PluginsDirectory, directoryName, SearchOption.AllDirectories)
            .Any();
    }
}

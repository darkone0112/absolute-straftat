namespace AbsoluteStraftat.Installer;

internal static class InstallationState
{
    public static bool HasBepInEx(InstallTarget target)
    {
        if (!Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "core"))
            || !Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "plugins")))
        {
            return false;
        }

        return target.OperatingSystem switch
        {
            OperatingSystemKind.Windows => File.Exists(Path.Combine(target.GameDirectory, "winhttp.dll")),
            OperatingSystemKind.Linux => File.Exists(Path.Combine(target.GameDirectory, "run_bepinex.sh"))
                && File.Exists(Path.Combine(target.GameDirectory, "libdoorstop.so")),
            _ => false
        };
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

    public static bool HasAbsoluteStraftatController(InstallTarget target)
    {
        return HasPluginFile(target, "AbsoluteStraftatController.dll");
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

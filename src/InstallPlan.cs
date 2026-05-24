namespace AbsoluteStraftat.Installer;

internal sealed record InstallPlanItem(
    string Name,
    Func<InstallTarget, bool> IsInstalled,
    Func<HttpClient, Task<ResolvedPackage>> ResolvePackage,
    Action<InstallTarget, ResolvedPackage, string> Install,
    bool IsEnabled = true,
    Action<InstallTarget>? Cleanup = null);

internal static class InstallPlan
{
    public static IReadOnlyList<InstallPlanItem> Create(PlatformInfo platform, System.Runtime.InteropServices.Architecture architecture)
    {
        return new InstallPlanItem[]
        {
            new(
                "BepInEx",
                InstallationState.HasBepInEx,
                httpClient => BepInExSource.ResolveAsync(httpClient, platform, architecture),
                InstallBepInExStep.Run),
            new(
                "Mod Menu",
                InstallationState.HasModMenu,
                httpClient => ThunderstorePackageSource.ResolveAsync(
                    httpClient,
                    "Mod Menu",
                    "kestrel",
                    "Mod_Menu",
                    PackageKind.ThunderstoreZip,
                    preferredDllName: "ModMenu.dll",
                    preferredFolderName: "ModMenu"),
                InstallThunderstoreFolderToPluginsStep.Run),
            new(
                "moreStrafts",
                InstallationState.HasMoreStrafts,
                httpClient => GitHubDllModSource.ResolveAsync(
                    httpClient,
                    "moreStrafts",
                    "ALBINALSHAIKH/moreStrafts",
                    "moreStrafts.dll"),
                InstallDllToPluginsStep.Run),
            new(
                "Straftat GunGame",
                InstallationState.HasGunGame,
                _ => throw new InstallerException("Straftat GunGame is WIP and is disabled in this installer release."),
                (_, _, _) => throw new InstallerException("Straftat GunGame is WIP and is disabled in this installer release."),
                IsEnabled: false,
                Cleanup: DisabledModCleanup.RemoveGunGame),
            new(
                "MoreStrafts UISpawnAddon",
                InstallationState.HasMoreStraftsUiSpawnAddon,
                httpClient => ThunderstorePackageSource.ResolveAsync(
                    httpClient,
                    "MoreStrafts UISpawnAddon",
                    "Yeastmans",
                    "MoreStrafts_UISpawnAddon",
                    PackageKind.ThunderstoreDll,
                    preferredDllName: "MoreStrafts_UISpawnAddon.dll",
                    preferredFolderName: "plugin"),
                InstallThunderstoreDllToPluginsStep.Run),
            new(
                "Fancy",
                InstallationState.HasFancy,
                httpClient => ThunderstorePackageSource.ResolveAsync(
                    httpClient,
                    "Fancy",
                    "kestrel",
                    "Fancy",
                    PackageKind.ThunderstoreDll,
                    preferredDllName: "Fancy.dll"),
                InstallThunderstoreDllToPluginsStep.Run),
            new(
                "Straftat Cosmetics Bundle IC",
                InstallationState.HasStraftatCosmeticsBundleIc,
                httpClient => ThunderstorePackageSource.ResolveAsync(
                    httpClient,
                    "Straftat Cosmetics Bundle IC",
                    "ImmortalChickens",
                    "Straftat_Cosmetics_Bundle_IC",
                    PackageKind.ThunderstoreFolder),
                InstallThunderstoreFolderPackageToPluginsStep.Run),
            new(
                "Absolute STRAFTAT Controller",
                InstallationState.HasAbsoluteStraftatController,
                _ => EmbeddedPluginSource.ResolveAsync(
                    "Absolute STRAFTAT Controller",
                    "0.1.0",
                    "AbsoluteStraftatController.dll"),
                InstallDllToPluginsStep.Run)
        };
    }
}

using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal static class BepInExSource
{
    private const string Repository = "BepInEx/BepInEx";

    public static async Task<ResolvedPackage> ResolveAsync(HttpClient httpClient, PlatformInfo platform, Architecture architecture)
    {
        var assetPattern = $"BepInEx_{platform.GitHubAssetName}_{ArchitectureDetector.ToAssetName(architecture)}_";
        var (version, asset) = await GitHubReleaseClient.FindAssetAsync(
            httpClient,
            Repository,
            assets => assets.FirstOrDefault(asset =>
                asset.Name.StartsWith(assetPattern, StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));

        return new ResolvedPackage(
            "BepInEx",
            version,
            new Uri(asset.DownloadUrl),
            PackageKind.BepInEx,
            asset.Name);
    }
}

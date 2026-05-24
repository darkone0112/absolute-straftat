namespace AbsoluteStraftat.Installer;

internal static class GitHubDllModSource
{
    public static async Task<ResolvedPackage> ResolveAsync(
        HttpClient httpClient,
        string name,
        string repository,
        string preferredDllName)
    {
        var (version, asset) = await GitHubReleaseClient.FindAssetAsync(
            httpClient,
            repository,
            assets => assets.FirstOrDefault(asset => asset.Name.Equals(preferredDllName, StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(asset => asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));

        return new ResolvedPackage(
            name,
            version,
            new Uri(asset.DownloadUrl),
            PackageKind.GitHubDll,
            preferredDllName);
    }
}

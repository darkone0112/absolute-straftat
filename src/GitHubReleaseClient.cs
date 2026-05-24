using System.Text.Json;

namespace AbsoluteStraftat.Installer;

internal static class GitHubReleaseClient
{
    public static async Task<(string Version, GitHubAsset Asset)> FindAssetAsync(
        HttpClient httpClient,
        string repository,
        Func<IReadOnlyList<GitHubAsset>, GitHubAsset?> selectAsset)
    {
        var url = $"https://api.github.com/repos/{repository}/releases/latest";
        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new InstallerException($"Could not fetch latest release for {repository}: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var version = document.RootElement.TryGetProperty("tag_name", out var tagName)
            ? tagName.GetString() ?? "latest"
            : "latest";

        if (!document.RootElement.TryGetProperty("assets", out var assets))
        {
            throw new InstallerException($"Latest release for {repository} does not contain assets.");
        }

        var parsedAssets = new List<GitHubAsset>();
        foreach (var assetJson in assets.EnumerateArray())
        {
            var name = assetJson.GetProperty("name").GetString();
            var downloadUrl = assetJson.GetProperty("browser_download_url").GetString();
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
            {
                parsedAssets.Add(new GitHubAsset(name, downloadUrl));
            }
        }

        var asset = selectAsset(parsedAssets);
        if (asset is null)
        {
            throw new InstallerException($"No matching asset was found in the latest {repository} release.");
        }

        return (version, asset);
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace AbsoluteStraftat.Installer;

internal static partial class ThunderstorePackageSource
{
    public static async Task<ResolvedPackage> ResolveAsync(
        HttpClient httpClient,
        string name,
        string owner,
        string packageName,
        PackageKind kind,
        string? preferredDllName = null,
        string? preferredFolderName = null)
    {
        var version = await TryResolveVersionFromApiAsync(httpClient, owner, packageName)
            ?? await TryResolveVersionFromPageAsync(httpClient, owner, packageName, new Uri($"https://new.thunderstore.io/c/straftat/p/{owner}/{packageName}/"))
            ?? await TryResolveVersionFromPageAsync(httpClient, owner, packageName, new Uri($"https://thunderstore.io/c/straftat/p/{owner}/{packageName}/"));

        if (version is null)
        {
            throw new InstallerException($"Could not resolve latest {name} version from Thunderstore.");
        }

        return new ResolvedPackage(
            name,
            version,
            new Uri($"https://thunderstore.io/package/download/{owner}/{packageName}/{version}/"),
            kind,
            $"{packageName}-{version}.zip",
            preferredDllName,
            preferredFolderName);
    }

    private static async Task<string?> TryResolveVersionFromApiAsync(HttpClient httpClient, string owner, string packageName)
    {
        try
        {
            var apiUri = new Uri($"https://thunderstore.io/api/experimental/package/{owner}/{packageName}/");
            using var response = await httpClient.GetAsync(apiUri);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("versions", out var versions))
            {
                return null;
            }

            return versions
                .EnumerateArray()
                .Select(version => version.TryGetProperty("version_number", out var versionNumber)
                    ? versionNumber.GetString()
                    : null)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .OrderByDescending(ParseVersion)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryResolveVersionFromPageAsync(HttpClient httpClient, string owner, string packageName, Uri pageUri)
    {
        try
        {
            var page = await httpClient.GetStringAsync(pageUri);
            var dependencyStringPattern = $@"\b{Regex.Escape(owner)}-{Regex.Escape(packageName)}-(?<version>\d+\.\d+\.\d+)\b";
            var dependencyStringMatch = Regex.Match(page, dependencyStringPattern, RegexOptions.IgnoreCase);
            if (dependencyStringMatch.Success)
            {
                return dependencyStringMatch.Groups["version"].Value;
            }

            return SemanticVersionRegex()
                .Matches(page)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(ParseVersion)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static Version ParseVersion(string? value)
    {
        return Version.TryParse(value, out var version) ? version : new Version(0, 0, 0);
    }

    [GeneratedRegex(@"\b\d+\.\d+\.\d+\b")]
    private static partial Regex SemanticVersionRegex();
}

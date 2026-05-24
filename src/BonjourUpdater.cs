using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AbsoluteStraftat.Installer;

internal static class BonjourUpdater
{
    private const string Repository = "darkone0112/absolute-straftat";

    public static async Task<bool> TryUpdateAndRelaunchAsync(
        HttpClient httpClient,
        PlatformInfo platform,
        string[] originalArgs,
        bool skipUpdate)
    {
        if (skipUpdate)
        {
            Program.PrintUpdate("bonjour says the update department is closed for this launch.");
            return false;
        }

        Program.PrintSection("bonjour");
        Program.PrintUpdate("bonjour is checking whether management changed the installer.");

        var currentExecutable = Environment.ProcessPath;
        if (!IsSelfContainedInstaller(currentExecutable))
        {
            Program.PrintUpdate("bonjour found a dev run. No self-replacement paperwork today.");
            return false;
        }

        var latestRelease = await TryGetLatestReleaseAsync(httpClient);
        if (latestRelease is null)
        {
            Program.PrintUpdate("bonjour found no release desk. Continuing with the local copy.");
            return false;
        }

        var currentVersion = CurrentVersion();
        var expectedAssetName = platform.Kind == OperatingSystemKind.Windows
            ? "absolute-straftat-installer.exe"
            : "absolute-straftat-installer";
        var asset = latestRelease.Assets.FirstOrDefault(asset =>
            asset.Name.Equals(expectedAssetName, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            Program.PrintUpdate($"bonjour requested {expectedAssetName}, but the release table declined to provide it.");
            return false;
        }

        var manifest = await TryGetManifestAsync(httpClient, latestRelease);
        if (manifest is not null && manifest.TryGetAssetHash(expectedAssetName, out var expectedHash))
        {
            var currentHash = await ComputeSha256Async(currentExecutable!);
            if (expectedHash.Equals(currentHash, StringComparison.OrdinalIgnoreCase))
            {
                Program.PrintUpdate("bonjour compared the hash and found no actionable difference.");
                return false;
            }
        }
        else if (!IsNewer(latestRelease.TagName, currentVersion))
        {
            Program.PrintUpdate($"bonjour says this installer is fresh enough: {currentVersion}.");
            return false;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"bonjour-{Guid.NewGuid():N}-{expectedAssetName}");
        Program.PrintUpdate($"new installer detected: {currentVersion} -> {latestRelease.TagName}.");
        Program.PrintUpdate("bonjour is downloading the replacement form.");
        await DownloadAsync(httpClient, asset.DownloadUrl, tempPath);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        RelaunchThroughHelper(currentExecutable!, tempPath, originalArgs);
        Program.PrintUpdate("bonjour started the newer installer. This one is leaving the meeting.");
        return true;
    }

    private static bool IsSelfContainedInstaller(string? currentExecutable)
    {
        if (string.IsNullOrWhiteSpace(currentExecutable))
        {
            return false;
        }

        var fileName = Path.GetFileName(currentExecutable);
        return fileName.Equals("absolute-straftat-installer.exe", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("absolute-straftat-installer", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<GitHubRelease?> TryGetLatestReleaseAsync(HttpClient httpClient)
    {
        try
        {
            using var response = await httpClient.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (!response.IsSuccessStatusCode)
            {
                Program.PrintUpdate($"bonjour got a GitHub refusal: {(int)response.StatusCode} {response.ReasonPhrase}.");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var tagName = document.RootElement.GetProperty("tag_name").GetString();
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            var assets = new List<GitHubReleaseAsset>();
            if (document.RootElement.TryGetProperty("assets", out var assetElements))
            {
                foreach (var assetElement in assetElements.EnumerateArray())
                {
                    var name = assetElement.GetProperty("name").GetString();
                    var downloadUrl = assetElement.GetProperty("browser_download_url").GetString();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        assets.Add(new GitHubReleaseAsset(name, new Uri(downloadUrl)));
                    }
                }
            }

            return new GitHubRelease(tagName, assets);
        }
        catch (Exception ex)
        {
            Program.PrintUpdate($"bonjour could not complete the update check: {ex.Message}");
            return null;
        }
    }

    private static async Task<BonjourManifest?> TryGetManifestAsync(HttpClient httpClient, GitHubRelease release)
    {
        var manifestAsset = release.Assets.FirstOrDefault(asset =>
            asset.Name.Equals("bonjour-manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestAsset is null)
        {
            return null;
        }

        try
        {
            await using var stream = await httpClient.GetStreamAsync(manifestAsset.DownloadUrl);
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("assets", out var assetsElement))
            {
                return null;
            }

            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assetProperty in assetsElement.EnumerateObject())
            {
                if (assetProperty.Value.TryGetProperty("sha256", out var hashElement))
                {
                    var hash = hashElement.GetString();
                    if (!string.IsNullOrWhiteSpace(hash))
                    {
                        hashes[assetProperty.Name] = hash;
                    }
                }
            }

            return new BonjourManifest(hashes);
        }
        catch (Exception ex)
        {
            Program.PrintUpdate($"bonjour could not read the release manifest: {ex.Message}");
            return null;
        }
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CurrentVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0]
            ?? "0.0.0-local";
    }

    private static bool IsNewer(string latestTag, string currentVersion)
    {
        var latest = NormalizeVersion(latestTag);
        var current = NormalizeVersion(currentVersion);

        if (Version.TryParse(latest, out var latestVersion) && Version.TryParse(current, out var currentParsed))
        {
            return latestVersion > currentParsed;
        }

        return !latest.Equals(current, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string value)
    {
        return value.Trim().TrimStart('v', 'V').Split('-')[0];
    }

    private static async Task DownloadAsync(HttpClient httpClient, Uri downloadUrl, string destinationPath)
    {
        await using var source = await httpClient.GetStreamAsync(downloadUrl);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
    }

    private static void RelaunchThroughHelper(string currentExecutable, string downloadedExecutable, string[] originalArgs)
    {
        var args = originalArgs
            .Where(arg => !arg.Equals("--skip-update", StringComparison.OrdinalIgnoreCase))
            .Append("--skip-update")
            .ToArray();

        if (OperatingSystem.IsWindows())
        {
            var argumentList = string.Join(' ', args.Select(WindowsArgumentQuote));
            var script = string.Join("; ", new[]
            {
                $"Wait-Process -Id {Environment.ProcessId}",
                $"Copy-Item -LiteralPath {PowerShellSingleQuote(downloadedExecutable)} -Destination {PowerShellSingleQuote(currentExecutable)} -Force",
                $"Start-Process -FilePath {PowerShellSingleQuote(currentExecutable)} -ArgumentList {PowerShellSingleQuote(argumentList)}"
            });

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                ArgumentList = { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-Command", script },
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return;
        }

        var shellArgs = string.Join(' ', args.Select(ShellSingleQuote));
        var shellScript = $"while kill -0 {Environment.ProcessId} 2>/dev/null; do sleep 0.2; done; cp {ShellSingleQuote(downloadedExecutable)} {ShellSingleQuote(currentExecutable)}; chmod +x {ShellSingleQuote(currentExecutable)}; exec {ShellSingleQuote(currentExecutable)} {shellArgs}";
        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList = { "-c", shellScript },
            UseShellExecute = false
        });
    }

    private static string PowerShellSingleQuote(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static string WindowsArgumentQuote(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return value.Any(char.IsWhiteSpace) ? $"\"{escaped}\"" : escaped;
    }

    private static string ShellSingleQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }

    private sealed record GitHubRelease(string TagName, IReadOnlyList<GitHubReleaseAsset> Assets);

    private sealed record GitHubReleaseAsset(string Name, Uri DownloadUrl);

    private sealed record BonjourManifest(IReadOnlyDictionary<string, string> AssetHashes)
    {
        public bool TryGetAssetHash(string assetName, out string hash)
        {
            return AssetHashes.TryGetValue(assetName, out hash!);
        }
    }
}

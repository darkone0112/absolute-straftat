using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AbsoluteStraftat.Installer;

internal static class BonjourUpdater
{
    private const string Repository = "darkone0112/absolute-straftat";
    private static readonly TimeSpan UpdateCheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UpdateDownloadTimeout = TimeSpan.FromSeconds(20);

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

        var latestRelease = await TryWithTimeoutAsync(
            TryGetLatestReleaseAsync(httpClient, CancellationToken.None),
            UpdateCheckTimeout,
            "bonjour update check timed out. Continuing with the local copy.");

        var currentVersion = CurrentVersion();
        var expectedAssetName = platform.Kind == OperatingSystemKind.Windows
            ? "absolute-straftat-installer.exe"
            : "absolute-straftat-installer";
        var directAssetUrl = new Uri($"https://github.com/{Repository}/releases/latest/download/{expectedAssetName}");
        var directManifestUrl = new Uri($"https://github.com/{Repository}/releases/latest/download/bonjour-manifest.json");

        var manifest = await TryWithTimeoutAsync(
            TryGetManifestAsync(httpClient, directManifestUrl, CancellationToken.None),
            UpdateCheckTimeout,
            "bonjour manifest check timed out. Continuing with version comparison.");
        if (manifest is not null && manifest.TryGetAssetHash(expectedAssetName, out var expectedHash))
        {
            var currentHash = await ComputeSha256Async(currentExecutable!);
            if (expectedHash.Equals(currentHash, StringComparison.OrdinalIgnoreCase))
            {
                Program.PrintUpdate("bonjour compared the hash and found no actionable difference.");
                return false;
            }
        }
        else if (latestRelease is null)
        {
            Program.PrintUpdate("bonjour found no release desk. Continuing with the local copy.");
            return false;
        }
        else if (!IsNewer(latestRelease.TagName, currentVersion))
        {
            Program.PrintUpdate($"bonjour says this installer is fresh enough: {currentVersion}.");
            return false;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"bonjour-{Guid.NewGuid():N}-{expectedAssetName}");
        Program.PrintUpdate(latestRelease is null
            ? $"replacement installer detected by manifest hash: {currentVersion} -> latest."
            : $"new installer detected: {currentVersion} -> {latestRelease.TagName}.");
        Program.PrintUpdate("bonjour is downloading the replacement form.");
        var downloaded = await TryWithTimeoutAsync(
            DownloadAsync(httpClient, directAssetUrl, tempPath, CancellationToken.None),
            UpdateDownloadTimeout,
            "bonjour download timed out. Continuing with the local copy.");
        if (!downloaded)
        {
            TryDelete(tempPath);
            return false;
        }

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

    private static async Task<GitHubRelease?> TryGetLatestReleaseAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"https://api.github.com/repos/{Repository}/releases/latest",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Program.PrintUpdate($"bonjour got a GitHub refusal: {(int)response.StatusCode} {response.ReasonPhrase}.");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
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

    private static async Task<BonjourManifest?> TryGetManifestAsync(HttpClient httpClient, Uri manifestUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                manifestUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Program.PrintUpdate($"bonjour could not read the release manifest: {(int)response.StatusCode} {response.ReasonPhrase}");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
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

    private static async Task<bool> DownloadAsync(HttpClient httpClient, Uri downloadUrl, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Program.PrintUpdate($"bonjour could not download the replacement form: {ex.Message}");
            return false;
        }
    }

    private static async Task<T?> TryWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            Program.PrintUpdate(timeoutMessage);
            return default;
        }

        return await task;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Failed update cleanup should not block the installer.
        }
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

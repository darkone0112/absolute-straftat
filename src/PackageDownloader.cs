using System.Reflection;

namespace AbsoluteStraftat.Installer;

internal static class PackageDownloader
{
    public static async Task DownloadAsync(HttpClient httpClient, ResolvedPackage package, string tempDirectory)
    {
        var destinationPath = GetDownloadedPath(tempDirectory, package);

        if (package.Kind == PackageKind.EmbeddedDll)
        {
            InstallerLog.Info($"Preparing bundled {package.Name} {package.Version}");
            Console.WriteLine($"Preparing bundled {package.Name} {package.Version}...");
            await using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(package.FileName)
                ?? throw new InstallerException($"Missing embedded package resource: {package.FileName}");
            await using var embeddedDestination = File.Create(destinationPath);
            await embedded.CopyToAsync(embeddedDestination);
            return;
        }

        InstallerLog.Info($"Downloading {package.Name} {package.Version} from {package.DownloadUrl}");
        Console.WriteLine($"Downloading {package.Name} {package.Version}...");
        await using var source = await httpClient.GetStreamAsync(package.DownloadUrl);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
    }

    public static string GetDownloadedPath(string tempDirectory, ResolvedPackage package)
    {
        return Path.Combine(tempDirectory, package.FileName);
    }
}

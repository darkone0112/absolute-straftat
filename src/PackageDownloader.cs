namespace AbsoluteStraftat.Installer;

internal static class PackageDownloader
{
    public static async Task DownloadAsync(HttpClient httpClient, ResolvedPackage package, string tempDirectory)
    {
        var destinationPath = GetDownloadedPath(tempDirectory, package);
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

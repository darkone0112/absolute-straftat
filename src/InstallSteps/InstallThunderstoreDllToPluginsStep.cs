using System.IO.Compression;

namespace AbsoluteStraftat.Installer;

internal static class InstallThunderstoreDllToPluginsStep
{
    public static void Run(InstallTarget target, ResolvedPackage package, string tempDirectory)
    {
        Directory.CreateDirectory(target.PluginsDirectory);

        var zipPath = PackageDownloader.GetDownloadedPath(tempDirectory, package);
        var extractDirectory = Path.Combine(tempDirectory, package.Name.Replace(' ', '-'));
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var dllName = package.PreferredDllName ?? "*.dll";
        var dllPath = FindDllPath(extractDirectory, dllName, package.PreferredFolderName);

        if (dllPath is null)
        {
            var folderHint = string.IsNullOrWhiteSpace(package.PreferredFolderName)
                ? string.Empty
                : $" inside a '{package.PreferredFolderName}' folder or";
            throw new InstallerException($"Could not find expected {dllName}{folderHint} in extracted package at {extractDirectory}.");
        }

        CopyFile(dllPath, target.PluginsDirectory, package.Name);
    }

    private static string? FindDllPath(string extractDirectory, string dllName, string? preferredFolderName)
    {
        if (!string.IsNullOrWhiteSpace(preferredFolderName))
        {
            foreach (var pluginDirectory in Directory.EnumerateDirectories(extractDirectory, preferredFolderName, SearchOption.AllDirectories))
            {
                var dllPath = Directory
                    .EnumerateFiles(pluginDirectory, dllName, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (dllPath is not null)
                {
                    return dllPath;
                }
            }
        }

        return Directory
            .EnumerateFiles(extractDirectory, dllName, SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static void CopyFile(string sourcePath, string destinationDirectory, string packageName)
    {
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, overwrite: true);
        Console.WriteLine($"  Copied {packageName}: {Path.GetRelativePath(destinationDirectory, destinationPath)}");
    }
}

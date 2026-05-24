using System.IO.Compression;

namespace AbsoluteStraftat.Installer;

internal static class InstallThunderstoreFolderPackageToPluginsStep
{
    private static readonly HashSet<string> PackageMetadataFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.md",
        "CHANGELOG.md",
        "icon.png",
        "manifest.json"
    };

    public static void Run(InstallTarget target, ResolvedPackage package, string tempDirectory)
    {
        Directory.CreateDirectory(target.PluginsDirectory);

        var zipPath = PackageDownloader.GetDownloadedPath(tempDirectory, package);
        var extractDirectory = Path.Combine(tempDirectory, package.Name.Replace(' ', '-'));
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var bundleDirectory = FindBundleDirectory(extractDirectory);
        if (bundleDirectory is not null)
        {
            CopyDirectory(bundleDirectory, Path.Combine(target.PluginsDirectory, Path.GetFileName(bundleDirectory)), package.Name);
            return;
        }

        var bundleFiles = Directory
            .EnumerateFiles(extractDirectory, "*.cbundle", SearchOption.AllDirectories)
            .ToArray();

        if (bundleFiles.Length == 0)
        {
            throw new InstallerException($"Could not find a cosmetic bundle folder or .cbundle files in extracted package at {extractDirectory}.");
        }

        var destinationDirectory = Path.Combine(target.PluginsDirectory, package.FileName[..^4]);
        Directory.CreateDirectory(destinationDirectory);
        foreach (var bundleFile in bundleFiles)
        {
            CopyFile(bundleFile, destinationDirectory, package.Name);
        }
    }

    private static string? FindBundleDirectory(string extractDirectory)
    {
        return Directory
            .EnumerateDirectories(extractDirectory, "*", SearchOption.AllDirectories)
            .Where(directory => Directory.EnumerateFiles(directory, "*.cbundle", SearchOption.AllDirectories).Any())
            .OrderBy(directory => Path.GetRelativePath(extractDirectory, directory).Count(separator => separator == Path.DirectorySeparatorChar))
            .FirstOrDefault();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, string packageName)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            if (PackageMetadataFiles.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
            InstallerLog.Info($"Copied {packageName}: {destinationPath}");
            Console.WriteLine($"  Copied {packageName}: {Path.GetRelativePath(Path.GetDirectoryName(destinationDirectory)!, destinationPath)}");
        }
    }

    private static void CopyFile(string sourcePath, string destinationDirectory, string packageName)
    {
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, overwrite: true);
        InstallerLog.Info($"Copied {packageName}: {destinationPath}");
        Console.WriteLine($"  Copied {packageName}: {Path.GetRelativePath(Path.GetDirectoryName(destinationDirectory)!, destinationPath)}");
    }
}

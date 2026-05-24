using System.IO.Compression;

namespace AbsoluteStraftat.Installer;

internal static class InstallThunderstoreFolderToPluginsStep
{
    public static void Run(InstallTarget target, ResolvedPackage package, string tempDirectory)
    {
        Directory.CreateDirectory(target.PluginsDirectory);

        var zipPath = PackageDownloader.GetDownloadedPath(tempDirectory, package);
        var extractDirectory = Path.Combine(tempDirectory, package.Name.Replace(' ', '-'));
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        var expectedFolderName = package.PreferredFolderName;
        var sourceDirectory = string.IsNullOrWhiteSpace(expectedFolderName)
            ? null
            : Directory
                .EnumerateDirectories(extractDirectory, expectedFolderName, SearchOption.AllDirectories)
                .FirstOrDefault();

        if (sourceDirectory is not null)
        {
            CopyDirectoryContents(sourceDirectory, target.PluginsDirectory, package.Name);
            return;
        }

        var preferredDllName = package.PreferredDllName ?? "*.dll";
        var modMenuDll = Directory
            .EnumerateFiles(extractDirectory, preferredDllName, SearchOption.AllDirectories)
            .FirstOrDefault();

        if (modMenuDll is null)
        {
            throw new InstallerException($"Could not find expected '{expectedFolderName}' directory or {preferredDllName} in extracted package at {extractDirectory}.");
        }

        CopyFile(modMenuDll, target.PluginsDirectory, package.Name);

        var modMenuXml = Path.ChangeExtension(modMenuDll, ".xml");
        if (File.Exists(modMenuXml))
        {
            CopyFile(modMenuXml, target.PluginsDirectory, package.Name);
        }
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory, string packageName)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
            Console.WriteLine($"  Copied {packageName}: {Path.GetRelativePath(destinationDirectory, destinationPath)}");
        }
    }

    private static void CopyFile(string sourcePath, string destinationDirectory, string packageName)
    {
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destinationPath, overwrite: true);
        Console.WriteLine($"  Copied {packageName}: {Path.GetRelativePath(destinationDirectory, destinationPath)}");
    }
}

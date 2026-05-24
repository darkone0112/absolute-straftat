namespace AbsoluteStraftat.Installer;

internal static class InstallDllToPluginsStep
{
    public static void Run(InstallTarget target, ResolvedPackage package, string tempDirectory)
    {
        Directory.CreateDirectory(target.PluginsDirectory);

        var sourcePath = PackageDownloader.GetDownloadedPath(tempDirectory, package);
        var destinationPath = Path.Combine(target.PluginsDirectory, package.FileName);
        File.Copy(sourcePath, destinationPath, overwrite: true);
        InstallerLog.Info($"Copied {package.Name}: {destinationPath}");
        Console.WriteLine($"  Copied {package.Name}: {Path.GetRelativePath(target.PluginsDirectory, destinationPath)}");
    }
}

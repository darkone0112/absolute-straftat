using System.IO.Compression;

namespace AbsoluteStraftat.Installer;

internal static class InstallBepInExStep
{
    public static void Run(InstallTarget target, ResolvedPackage package, string tempDirectory)
    {
        var zipPath = PackageDownloader.GetDownloadedPath(tempDirectory, package);
        var extractDirectory = Path.Combine(tempDirectory, "BepInEx");
        InstallerLog.Info($"Extracting BepInEx from {zipPath}");
        ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);

        InstallerLog.Info($"Copying BepInEx files into {target.GameDirectory}");
        CopyDirectory(extractDirectory, target.GameDirectory);
        MakeUnixShellScriptsExecutable(target);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
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
        }
    }

    private static void MakeUnixShellScriptsExecutable(InstallTarget target)
    {
        if (target.OperatingSystem != OperatingSystemKind.Linux || OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (var script in Directory.EnumerateFiles(target.GameDirectory, "*.sh", SearchOption.TopDirectoryOnly))
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            InstallerLog.Info($"Made executable: {script}");
        }
    }
}

using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal enum OperatingSystemKind
{
    Windows,
    Linux
}

internal enum PackageKind
{
    BepInEx,
    GitHubDll,
    ThunderstoreZip,
    ThunderstoreDll,
    ThunderstoreFolder
}

internal sealed record InstallTarget(
    string GameDirectory,
    string PluginsDirectory,
    OperatingSystemKind OperatingSystem,
    Architecture Architecture);

internal sealed record ResolvedPackage(
    string Name,
    string Version,
    Uri DownloadUrl,
    PackageKind Kind,
    string FileName,
    string? PreferredDllName = null,
    string? PreferredFolderName = null);

internal sealed record GitHubAsset(string Name, string DownloadUrl);

internal sealed record PlatformInfo(OperatingSystemKind Kind, string GitHubAssetName)
{
    public static PlatformInfo Current()
    {
        if (OperatingSystem.IsWindows())
        {
            return new PlatformInfo(OperatingSystemKind.Windows, "win");
        }

        if (OperatingSystem.IsLinux())
        {
            return new PlatformInfo(OperatingSystemKind.Linux, "linux");
        }

        throw new InstallerException("Only Windows and Linux are supported.");
    }
}

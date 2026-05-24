namespace AbsoluteStraftat.Installer;

internal static class EmbeddedPluginSource
{
    public static Task<ResolvedPackage> ResolveAsync(string name, string version, string fileName)
    {
        return Task.FromResult(new ResolvedPackage(
            name,
            version,
            new Uri($"embedded://absolute-straftat/{fileName}"),
            PackageKind.EmbeddedDll,
            fileName));
    }
}

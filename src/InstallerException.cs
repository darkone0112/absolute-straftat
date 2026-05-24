namespace AbsoluteStraftat.Installer;

internal sealed class InstallerException : Exception
{
    public InstallerException(string message)
        : base(message)
    {
    }
}

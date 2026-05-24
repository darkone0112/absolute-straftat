namespace AbsoluteStraftat.Installer;

internal static class InstallerLog
{
    private static readonly object Sync = new();
    private static readonly List<string> LogFiles = new();

    static InstallerLog()
    {
        AddLogFile(Path.Combine(AppContext.BaseDirectory, "absolute-straftat-installer.log"));
    }

    public static IReadOnlyList<string> Files
    {
        get
        {
            lock (Sync)
            {
                return LogFiles.ToArray();
            }
        }
    }

    public static void AddGameDirectory(string gameDirectory)
    {
        AddLogFile(Path.Combine(gameDirectory, "absolute-straftat-installer.log"));
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message)
    {
        Write("ERR", message);
    }

    private static void AddLogFile(string path)
    {
        lock (Sync)
        {
            if (LogFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{Environment.NewLine}=== Absolute STRAFTAT installer {DateTimeOffset.Now:O} ==={Environment.NewLine}");
            LogFiles.Add(path);
        }
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        lock (Sync)
        {
            foreach (var file in LogFiles)
            {
                File.AppendAllText(file, line);
            }
        }
    }
}

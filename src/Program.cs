using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var options = InstallerOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var platform = PlatformInfo.Current();
            PrintBanner();
            PrintInfo("Platform", platform.GitHubAssetName);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("absolute-straftat-installer");

            if (await BonjourUpdater.TryUpdateAndRelaunchAsync(httpClient, platform, args, options.SkipUpdate))
            {
                return 0;
            }

            InstallerUi.Start(options.NoPopup);
            var gameDirectory = options.GameDirectory ?? SteamLocator.FindGameDirectory("STRAFTAT");
            if (gameDirectory is null)
            {
                gameDirectory = PromptForGameDirectory();
            }

            if (!Directory.Exists(gameDirectory))
            {
                throw new InstallerException($"Game directory does not exist: {gameDirectory}");
            }

            gameDirectory = Path.GetFullPath(gameDirectory);
            PrintInfo("Game folder", gameDirectory);

            var architecture = options.GameArchitecture ?? ArchitectureDetector.Detect(gameDirectory) ?? RuntimeInformation.ProcessArchitecture;
            if (architecture is not Architecture.X64 and not Architecture.X86)
            {
                throw new InstallerException($"Unsupported architecture: {architecture}. Use --arch x64 or --arch x86.");
            }

            PrintInfo("Architecture", ArchitectureDetector.ToAssetName(architecture));

            var target = new InstallTarget(
                gameDirectory,
                Path.Combine(gameDirectory, "BepInEx", "plugins"),
                platform.Kind,
                architecture);

            var plan = InstallPlan.Create(platform, architecture);
            var missingItems = plan.Where(item => !item.IsInstalled(target)).ToArray();

            PrintSection("Install Check");
            foreach (var item in plan)
            {
                PrintStatus(item.Name, item.IsInstalled(target));
            }

            if (missingItems.Length == 0)
            {
                PrintSection("Nothing To Do");
                PrintSuccess("Everything is already installed.");
                InstallerUi.ShowResult(options.NoPopup, "NO LABOR DETECTED", "All required files are already present. The situation has been reviewed and found suspiciously acceptable.");
                return 0;
            }

            using var tempDirectory = TempDirectory.Create();
            PrintSection("Resolve");
            PrintInfo("Missing packages", string.Join(", ", missingItems.Select(item => item.Name)));

            var resolvedItems = new List<(InstallPlanItem Item, ResolvedPackage Package)>();
            foreach (var item in missingItems)
            {
                resolvedItems.Add((item, await item.ResolvePackage(httpClient)));
            }

            PrintSection("Selected Packages");
            foreach (var (_, package) in resolvedItems)
            {
                PrintInfo($"{package.Name} {package.Version}", package.DownloadUrl.ToString());
            }

            PrintSection("Download");
            foreach (var (_, package) in resolvedItems)
            {
                await PackageDownloader.DownloadAsync(httpClient, package, tempDirectory.Path);
            }

            Directory.CreateDirectory(target.PluginsDirectory);

            foreach (var (item, package) in resolvedItems)
            {
                PrintStep($"Installing {item.Name}");
                item.Install(target, package, tempDirectory.Path);
            }

            PrintSection("Complete");
            PrintSuccess("Installation complete. The DLL paperwork has been accepted.");
            PrintInfo("Next step", "Launch STRAFTAT once so BepInEx can finish generating its config files.");

            InstallerUi.ShowResult(options.NoPopup, "INSTALLATION OPINION: YES", "The files are arranged in a way the committee currently accepts. Launch STRAFTAT and observe.");

            return 0;
        }
        catch (InstallerException ex)
        {
            PrintError(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            PrintError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static string PromptForGameDirectory()
    {
        Console.WriteLine("Could not find STRAFTAT automatically.");
        Console.WriteLine("In Steam, right click STRAFTAT, select Manage, then Browse local files.");
        Console.Write("Paste the game folder path here: ");

        var input = Console.ReadLine()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InstallerException("No game directory was provided.");
        }

        return input;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Absolute STRAFTAT installer");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  absolute-straftat-installer [--game-dir <path>] [--arch x64|x86] [--no-popup]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --game-dir <path>  STRAFTAT install directory.");
        Console.WriteLine("  --arch <arch>      Override game architecture detection.");
        Console.WriteLine("  --no-popup         Skip the completion window.");
        Console.WriteLine("  --skip-update      Skip bonjour self-update check.");
        Console.WriteLine("  --help             Show this help.");
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("        ABSOLUTE STRAFTAT INSTALLER     ");
        Console.WriteLine("========================================");
        Console.ResetColor();
    }

    internal static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"-- {title} --------------------------------");
        Console.ResetColor();
    }

    private static void PrintStatus(string name, bool installed)
    {
        Console.ForegroundColor = installed ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.Write(installed ? "[OK]   " : "[MISS] ");
        Console.ResetColor();
        Console.WriteLine($"{name,-27} {(installed ? "installed" : "missing")}");
    }

    internal static void PrintInfo(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("[INFO] ");
        Console.ResetColor();
        Console.WriteLine($"{label}: {value}");
    }

    internal static void PrintStep(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[RUN]  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    internal static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[DONE] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("[ERR]  ");
        Console.ResetColor();
        Console.Error.WriteLine(message);
    }

    internal static void PrintUpdate(string message)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("[BONJ] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}

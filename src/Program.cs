using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            InstallerLog.Info($"Started with args: {string.Join(' ', args)}");
            var options = InstallerOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var installerPlatform = PlatformInfo.Current();
            PrintBanner();
            PrintInfo("Installer platform", installerPlatform.GitHubAssetName);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("absolute-straftat-installer");

            if (await BonjourUpdater.TryUpdateAndRelaunchAsync(httpClient, installerPlatform, args, options.SkipUpdate))
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
            InstallerLog.AddGameDirectory(gameDirectory);
            PrintInfo("Game folder", gameDirectory);

            var detectedGameBinary = ArchitectureDetector.DetectGameBinary(gameDirectory);
            var targetPlatform = detectedGameBinary?.Platform ?? installerPlatform;
            var architecture = options.GameArchitecture ?? detectedGameBinary?.Architecture ?? RuntimeInformation.ProcessArchitecture;
            if (architecture is not Architecture.X64 and not Architecture.X86)
            {
                throw new InstallerException($"Unsupported architecture: {architecture}. Use --arch x64 or --arch x86.");
            }

            PrintInfo("Game build", targetPlatform.GitHubAssetName);
            PrintInfo("Architecture", ArchitectureDetector.ToAssetName(architecture));

            var target = new InstallTarget(
                gameDirectory,
                Path.Combine(gameDirectory, "BepInEx", "plugins"),
                targetPlatform.Kind,
                architecture);

            PrintBepInExLoaderDiagnostics(target);
            WriteLinuxLaunchHelp(target);

            var plan = InstallPlan.Create(targetPlatform, architecture);

            PrintSection("Disabled Mod Cleanup");
            foreach (var item in plan.Where(item => !item.IsEnabled))
            {
                item.Cleanup?.Invoke(target);
            }

            var missingItems = plan.Where(item => item.IsEnabled && !item.IsInstalled(target)).ToArray();

            PrintSection("Install Check");
            foreach (var item in plan)
            {
                if (item.IsEnabled)
                {
                    PrintStatus(item.Name, item.IsInstalled(target));
                }
                else
                {
                    PrintWipStatus(item.Name);
                }
            }

            if (missingItems.Length == 0)
            {
                PrintSection("Nothing To Do");
                PrintSuccess("Everything is already installed.");
                PrintLinuxLaunchNotice(target);
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
            PrintLinuxLaunchNotice(target);

            InstallerUi.ShowResult(options.NoPopup, "INSTALLATION OPINION: YES", "The files are arranged in a way the committee currently accepts. Launch STRAFTAT and observe.");

            return 0;
        }
        catch (InstallerException ex)
        {
            PrintError(ex.Message);
            InstallerUi.ShowResult(false, "INSTALLER FELL DOWN", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            PrintError($"Unexpected error: {ex.Message}");
            InstallerLog.Error(ex.ToString());
            InstallerUi.ShowResult(false, "UNEXPECTED FLOOR EVENT", ex.Message);
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

    private static void WriteLinuxLaunchHelp(InstallTarget target)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var launchOption = GetLinuxLaunchOption(target);
        if (launchOption is null)
        {
            return;
        }

        var helpPath = Path.Combine(target.GameDirectory, "ABSOLUTE_STRAFTAT_LINUX_LAUNCH_OPTIONS.txt");
        File.WriteAllText(
            helpPath,
            "Steam > STRAFTAT > Properties > Launch Options\n"
            + "Paste this line:\n\n"
            + launchOption
            + "\n\nIf Mod Menu does not appear, Steam probably launched STRAFTAT without BepInEx.\n");
    }

    private static void PrintBepInExLoaderDiagnostics(InstallTarget target)
    {
        var windowsLoader = File.Exists(Path.Combine(target.GameDirectory, "winhttp.dll"));
        var windowsConfig = File.Exists(Path.Combine(target.GameDirectory, "doorstop_config.ini"));
        var linuxLauncher = File.Exists(Path.Combine(target.GameDirectory, "run_bepinex.sh"));
        var linuxLoader = File.Exists(Path.Combine(target.GameDirectory, "libdoorstop.so"));
        var generatedConfig = Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "config"));
        var generatedCache = Directory.Exists(Path.Combine(target.GameDirectory, "BepInEx", "cache"));

        PrintSection("BepInEx Loader Check");
        PrintInfo("Expected loader", target.OperatingSystem == OperatingSystemKind.Windows ? "Windows/Proton" : "Native Linux");
        PrintInfo("winhttp.dll", windowsLoader ? "present" : "missing");
        PrintInfo("doorstop_config.ini", windowsConfig ? "present" : "missing");
        PrintInfo("run_bepinex.sh", linuxLauncher ? "present" : "missing");
        PrintInfo("libdoorstop.so", linuxLoader ? "present" : "missing");
        PrintInfo("BepInEx/config", generatedConfig ? "present" : "not generated yet");
        PrintInfo("BepInEx/cache", generatedCache ? "present" : "not generated yet");

        if (target.OperatingSystem == OperatingSystemKind.Windows && !windowsLoader && linuxLauncher)
        {
            PrintInfo("Loader diagnosis", "Native Linux BepInEx appears to be in a Windows/Proton game folder. Reinstalling BepInEx should repair it.");
        }
        else if (target.OperatingSystem == OperatingSystemKind.Linux && !linuxLauncher && windowsLoader)
        {
            PrintInfo("Loader diagnosis", "Windows BepInEx appears to be in a native Linux game folder. Reinstalling BepInEx should repair it.");
        }
        else if (!generatedConfig || !generatedCache)
        {
            PrintInfo("Loader diagnosis", "BepInEx has not generated runtime folders yet. If files are installed, check Steam launch options.");
        }
    }

    private static void PrintLinuxLaunchNotice(InstallTarget target)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var launchOption = GetLinuxLaunchOption(target);
        if (launchOption is null)
        {
            return;
        }

        PrintSection("Linux Launch Option");
        PrintInfo("Steam launch options", launchOption);
        PrintInfo("Saved note", Path.Combine(target.GameDirectory, "ABSOLUTE_STRAFTAT_LINUX_LAUNCH_OPTIONS.txt"));
    }

    private static string? GetLinuxLaunchOption(InstallTarget target)
    {
        return target.OperatingSystem switch
        {
            OperatingSystemKind.Linux => "./run_bepinex.sh %command%",
            OperatingSystemKind.Windows => "WINEDLLOVERRIDES=\"winhttp.dll=n,b\" %command%",
            _ => null
        };
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
        InstallerLog.Info($"-- {title} --");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"-- {title} --------------------------------");
        Console.ResetColor();
    }

    private static void PrintStatus(string name, bool installed)
    {
        InstallerLog.Info($"{name}: {(installed ? "installed" : "missing")}");
        Console.ForegroundColor = installed ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.Write(installed ? "[OK]   " : "[MISS] ");
        Console.ResetColor();
        Console.WriteLine($"{name,-27} {(installed ? "installed" : "missing")}");
    }

    internal static void PrintInfo(string label, string value)
    {
        InstallerLog.Info($"{label}: {value}");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("[INFO] ");
        Console.ResetColor();
        Console.WriteLine($"{label}: {value}");
    }

    internal static void PrintStep(string message)
    {
        InstallerLog.Info(message);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[RUN]  ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    internal static void PrintSuccess(string message)
    {
        InstallerLog.Info(message);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[DONE] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    internal static void PrintRemoved(string message)
    {
        InstallerLog.Info($"Removed {message}");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("[FIX]  ");
        Console.ResetColor();
        Console.WriteLine($"Removed {message}");
    }

    private static void PrintWipStatus(string name)
    {
        InstallerLog.Info($"{name}: WIP disabled");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("[WIP]  ");
        Console.ResetColor();
        Console.WriteLine($"{name,-27} disabled for this release");
    }

    private static void PrintError(string message)
    {
        InstallerLog.Error(message);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write("[ERR]  ");
        Console.ResetColor();
        Console.Error.WriteLine(message);
    }

    internal static void PrintUpdate(string message)
    {
        InstallerLog.Info($"bonjour: {message}");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("[BONJ] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}

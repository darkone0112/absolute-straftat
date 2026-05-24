using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal sealed record InstallerOptions(string? GameDirectory, Architecture? GameArchitecture, bool ShowHelp, bool NoPopup, bool SkipUpdate)
{
    public static InstallerOptions Parse(string[] args)
    {
        string? gameDirectory = null;
        Architecture? architecture = null;
        var showHelp = false;
        var noPopup = false;
        var skipUpdate = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--game-dir":
                    gameDirectory = RequireValue(args, ref i, "--game-dir");
                    break;
                case "--arch":
                    architecture = ParseArchitecture(RequireValue(args, ref i, "--arch"));
                    break;
                case "--no-popup":
                    noPopup = true;
                    break;
                case "--skip-update":
                    skipUpdate = true;
                    break;
                default:
                    throw new InstallerException($"Unknown option: {args[i]}");
            }
        }

        return new InstallerOptions(gameDirectory, architecture, showHelp, noPopup, skipUpdate);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InstallerException($"{optionName} requires a value.");
        }

        index++;
        return args[index].Trim().Trim('"');
    }

    private static Architecture ParseArchitecture(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "x64" => Architecture.X64,
            "x86" => Architecture.X86,
            _ => throw new InstallerException("Architecture must be x64 or x86.")
        };
    }
}

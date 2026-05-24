using System.Runtime.InteropServices;

namespace AbsoluteStraftat.Installer;

internal static class ArchitectureDetector
{
    public static Architecture? Detect(string gameDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var executable in Directory.EnumerateFiles(gameDirectory, "*.exe", SearchOption.TopDirectoryOnly))
            {
                var architecture = ReadWindowsExecutableArchitecture(executable);
                if (architecture is not null)
                {
                    return architecture;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            foreach (var file in Directory.EnumerateFiles(gameDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var architecture = ReadElfArchitecture(file);
                if (architecture is not null)
                {
                    return architecture;
                }
            }
        }

        return null;
    }

    public static string ToAssetName(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => throw new InstallerException($"Unsupported architecture: {architecture}")
        };
    }

    private static Architecture? ReadWindowsExecutableArchitecture(string path)
    {
        Span<byte> header = stackalloc byte[4096];
        using var file = File.OpenRead(path);
        var bytesRead = file.Read(header);
        if (bytesRead < 0x40 || header[0] != 'M' || header[1] != 'Z')
        {
            return null;
        }

        var peHeaderOffset = BitConverter.ToInt32(header.Slice(0x3c, 4));
        if (peHeaderOffset <= 0 || peHeaderOffset + 6 >= bytesRead)
        {
            return null;
        }

        if (header[peHeaderOffset] != 'P' || header[peHeaderOffset + 1] != 'E')
        {
            return null;
        }

        var machine = BitConverter.ToUInt16(header.Slice(peHeaderOffset + 4, 2));
        return machine switch
        {
            0x014c => Architecture.X86,
            0x8664 => Architecture.X64,
            _ => null
        };
    }

    private static Architecture? ReadElfArchitecture(string path)
    {
        Span<byte> header = stackalloc byte[20];
        using var file = File.OpenRead(path);
        if (file.Read(header) < header.Length)
        {
            return null;
        }

        if (header[0] != 0x7f || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
        {
            return null;
        }

        return header[4] switch
        {
            1 => Architecture.X86,
            2 => Architecture.X64,
            _ => null
        };
    }
}

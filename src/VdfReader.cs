namespace AbsoluteStraftat.Installer;

internal static class VdfReader
{
    public static string? ReadFirstValue(string path, string key)
    {
        return ReadValues(path, key).FirstOrDefault();
    }

    public static IEnumerable<string> ReadValues(string path, string key)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
            {
                yield return parts[1].Replace(@"\\", @"\");
            }
        }
    }
}

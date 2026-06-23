namespace Rag.Core.Configuration;

public static class EnvFile
{
    public static IReadOnlyDictionary<string, string?> LoadFromWorkingDirectory(string fileName = ".env")
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, fileName);
            if (File.Exists(path))
            {
                return Parse(path);
            }

            directory = directory.Parent;
        }

        return new Dictionary<string, string?>();
    }

    private static IReadOnlyDictionary<string, string?> Parse(string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        return values;
    }
}

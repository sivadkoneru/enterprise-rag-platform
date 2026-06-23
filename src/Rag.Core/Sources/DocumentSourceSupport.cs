namespace Rag.Core.Sources;

internal static class DocumentSourceSupport
{
    public static bool IsSupported(string pathOrName)
    {
        var extension = Path.GetExtension(pathOrName);
        return string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeExtension(string pathOrName)
    {
        return Path.GetExtension(pathOrName).TrimStart('.').ToLowerInvariant();
    }

    public static string TempPathFor(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var tempDirectory = Path.Combine(Path.GetTempPath(), "rag-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return Path.Combine(tempDirectory, $"source{extension}");
    }

    public static ValueTask CleanupTempFileAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }
}

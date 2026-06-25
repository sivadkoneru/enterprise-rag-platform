namespace Rag.Core.Sources;

internal static class DocumentSourceSupport
{
    public static bool IsSupported(string pathOrName)
    {
        var extension = NormalizeExtension(pathOrName);
        return !IsSchemaFile(pathOrName) &&
            (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".jsonl", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".ndjson", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".jsonl.gz", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".ndjson.gz", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeExtension(string pathOrName)
    {
        if (pathOrName.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".jsonl.gz";
        }

        if (pathOrName.EndsWith(".ndjson.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".ndjson.gz";
        }

        return Path.GetExtension(pathOrName).ToLowerInvariant();
    }

    public static bool IsSchemaFile(string pathOrName)
    {
        var name = Path.GetFileName(pathOrName);
        return string.Equals(name, "rag-ingestion.schema.json", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase);
    }

    public static string TempPathFor(string fileName)
    {
        var extension = NormalizeExtension(fileName);
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

namespace Rag.Evals;

/// <summary>
/// Locates repository-relative paths by walking up from the build output until the solution file
/// appears. Depending on the working directory would make results differ between "run from the repo
/// root" and "run from the project directory", and a fixed "../../../.." hop breaks whenever the
/// output layout changes.
/// </summary>
internal static class RepoPaths
{
    private const string SolutionFileName = "enterprise-rag-platform.sln";

    public static string Root { get; } = FindRoot();

    public static string Samples => Path.Combine(Root, "samples");

    public static string HandbookPdf => Path.Combine(Samples, "handbook.pdf");

    public static string Distractors => Path.Combine(Root, "evals", "corpus", "distractors");

    public static string Results => Path.Combine(Root, "evals", "results");

    public static string Readme => Path.Combine(Root, "README.md");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' above '{AppContext.BaseDirectory}'. " +
            "Run the eval tools from inside a checkout of the repository.");
    }
}

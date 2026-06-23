namespace Rag.Core.Configuration;

public sealed class S3Options
{
    public string? Region { get; set; } = "us-east-1";

    public string? ServiceUrl { get; set; }

    public bool ForcePathStyle { get; set; } = true;
}

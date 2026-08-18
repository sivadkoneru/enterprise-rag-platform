using System.Globalization;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Rag.Evals.Corpus;

/// <summary>
/// Renders <see cref="HandbookContent"/> to a PDF.
///
/// The layout constants are functionally load-bearing, not cosmetic. PdfPig's
/// <c>ContentOrderTextExtractor</c> reconstructs structure from baseline gaps: a gap wider than
/// 0.9x the font size becomes a line break, and one wider than 1.7x becomes a paragraph break. The
/// leading and gap values below sit on the correct side of both thresholds so that the extracted
/// text carries real blank lines, which is what the markdown-aware and semantic chunking
/// strategies split on. Shrinking the paragraph gap or growing the font would silently flatten the
/// corpus into one paragraph and collapse two of the four strategies to a single chunk, so the
/// relationships are asserted at generation time rather than trusted.
///
/// Output is deterministic: fixed content, fixed layout, fixed document metadata, no clock and no
/// randomness. Byte-for-byte stability across PdfPig versions is not promised, which is why the
/// eval guards corpus <em>content</em> through anchor resolution rather than diffing the binary.
/// </summary>
internal static class HandbookPdfWriter
{
    private const double PageWidth = 612;
    private const double PageHeight = 792;
    private const double MarginLeft = 72;
    private const double MarginRight = 72;
    private const double MarginTop = 72;
    private const double MarginBottom = 72;

    private const double BodyFontSize = 11;
    private const double HeadingFontSize = 13;
    private const double TitleFontSize = 18;

    /// <summary>Baseline-to-baseline distance inside a paragraph: a line break, not a paragraph break.</summary>
    private const double LineLeading = 14;

    /// <summary>Baseline-to-baseline distance between paragraphs: wide enough to read as a paragraph break.</summary>
    private const double ParagraphGap = 26;

    /// <summary>Space above a heading, and below it. Both must clear the paragraph threshold.</summary>
    private const double HeadingGapBefore = 34;
    private const double HeadingGapAfter = 22;

    private static double TextWidth => PageWidth - MarginLeft - MarginRight;

    public static void Write(string outputPath)
    {
        AssertLayoutSeparatesParagraphs();

        using var builder = new PdfDocumentBuilder();
        var body = builder.AddStandard14Font(Standard14Font.Helvetica);
        var bold = builder.AddStandard14Font(Standard14Font.HelveticaBold);

        // Fixed metadata: a generated timestamp would make every regeneration a diff.
        builder.DocumentInformation.Title = HandbookContent.Title;
        builder.DocumentInformation.Author = "Northwind Systems People Operations";
        builder.DocumentInformation.Producer = "Rag.Evals handbook generator";
        builder.DocumentInformation.Creator = "Rag.Evals";
        builder.DocumentInformation.CreationDate = "D:20260101000000Z";

        var page = builder.AddPage(PageSize.Letter);
        var cursor = PageHeight - MarginTop;
        var requiredSentenceLines = 0;

        cursor -= TitleFontSize;
        page.AddText(HandbookContent.Title, TitleFontSize, new PdfPoint(MarginLeft, cursor), bold);
        cursor -= HeadingGapAfter;

        foreach (var section in HandbookContent.Sections)
        {
            var headingLines = WrapLines(page, section.Heading, HeadingFontSize, bold);
            if (!Fits(cursor, HeadingGapBefore, headingLines.Count, HeadingGapAfter))
            {
                (page, cursor) = NewPage(builder);
            }
            else
            {
                cursor -= HeadingGapBefore;
            }

            cursor = DrawLines(page, headingLines, HeadingFontSize, bold, cursor);
            cursor -= HeadingGapAfter;

            foreach (var paragraph in section.Paragraphs)
            {
                var lines = WrapLines(page, paragraph, BodyFontSize, body);
                if (lines.Count * LineLeading > PageHeight - MarginTop - MarginBottom)
                {
                    throw new InvalidOperationException(
                        $"Paragraph is taller than a page and would be split across a page break, " +
                        $"which the paragraph heuristic cannot see across: '{Truncate(paragraph)}'.");
                }

                // Page breaks land only between paragraphs. The extractor compares baselines within
                // a single page, so a paragraph split across pages would lose its internal breaks.
                if (!Fits(cursor, 0, lines.Count, ParagraphGap))
                {
                    (page, cursor) = NewPage(builder);
                }

                requiredSentenceLines += lines.Count(line =>
                    line.Contains(HandbookContent.RequiredSentence, StringComparison.Ordinal));

                cursor = DrawLines(page, lines, BodyFontSize, body, cursor);
                cursor -= ParagraphGap;
            }
        }

        if (requiredSentenceLines != 1)
        {
            throw new InvalidOperationException(
                $"'{HandbookContent.RequiredSentence}' must render unwrapped on exactly one line so " +
                $"tests/Rag.Core.Tests/PdfParserTests.cs keeps passing, but it landed on " +
                $"{requiredSentenceLines.ToString(CultureInfo.InvariantCulture)} line(s).");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(outputPath, builder.Build());
    }

    /// <summary>
    /// Pins the inequalities the extractor's heuristic depends on, so a future font-size or spacing
    /// tweak fails here with an explanation instead of silently flattening the corpus.
    /// </summary>
    private static void AssertLayoutSeparatesParagraphs()
    {
        Require(LineLeading > BodyFontSize * 0.9, "line leading must exceed 0.9x the body font size or lines run together");
        Require(LineLeading < BodyFontSize * 1.7, "line leading must stay below 1.7x the body font size or every line becomes a paragraph");
        Require(ParagraphGap > BodyFontSize * 1.7, "paragraph gap must exceed 1.7x the body font size or paragraphs merge");
        Require(HeadingGapBefore > BodyFontSize * 1.7, "the gap above a heading must read as a paragraph break");
        Require(HeadingGapAfter > BodyFontSize * 1.7, "the gap below a heading must read as a paragraph break");
    }

    private static void Require(bool condition, string because)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Handbook layout is invalid: {because}.");
        }
    }

    private static bool Fits(double cursor, double gapBefore, int lineCount, double gapAfter)
    {
        return cursor - gapBefore - (lineCount * LineLeading) - gapAfter >= MarginBottom;
    }

    private static (PdfPageBuilder Page, double Cursor) NewPage(PdfDocumentBuilder builder)
    {
        return (builder.AddPage(PageSize.Letter), PageHeight - MarginTop);
    }

    private static double DrawLines(
        PdfPageBuilder page,
        IReadOnlyList<string> lines,
        double fontSize,
        PdfDocumentBuilder.AddedFont font,
        double cursor)
    {
        foreach (var line in lines)
        {
            cursor -= LineLeading;
            page.AddText(line, fontSize, new PdfPoint(MarginLeft, cursor), font);
        }

        return cursor;
    }

    /// <summary>Greedy word wrap measured against the real font metrics, so it is layout-exact.</summary>
    private static List<string> WrapLines(
        PdfPageBuilder page,
        string text,
        double fontSize,
        PdfDocumentBuilder.AddedFont font)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length > 0 && Measure(page, candidate, fontSize, font) > TextWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    private static double Measure(
        PdfPageBuilder page,
        string text,
        double fontSize,
        PdfDocumentBuilder.AddedFont font)
    {
        var letters = page.MeasureText(text, fontSize, new PdfPoint(0, 0), font);
        return letters.Count == 0 ? 0 : letters.Max(letter => letter.BoundingBox.Right);
    }

    private static string Truncate(string value)
    {
        return value.Length <= 60 ? value : string.Concat(value.AsSpan(0, 60), "...");
    }
}

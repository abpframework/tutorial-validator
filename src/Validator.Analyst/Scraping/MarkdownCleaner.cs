using System.Text.RegularExpressions;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Cleans converted markdown content by removing noise lines, variant links, and excessive whitespace.
/// </summary>
internal static partial class MarkdownCleaner
{
    /// <summary>
    /// Removes noise lines from markdown content (contributor counts, feedback prompts, etc.).
    /// </summary>
    internal static string CleanMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        // Remove lines that are just noise
        var noisePatterns = new[]
        {
            @"^\s*\d+Contributors\s*$",
            @"^\s*Translate\s*$",
            @"^\s*Share on X\s*$",
            @"^\s*Share on Linkedin\s*$",
            @"^\s*Share via e-mail\s*$",
            @"^\s*Feedback\s*$",
            @"^\s*Print\s*$",
            @"^\s*Undo Translation\s*$",
            @"^\s*Document Options\s*$",
            @"^\s*UI:\s*$",
            @"^\s*Database:\s*$",
            @"^\s*Angular\s*$",
            @"^\s*Blazor Server\s*$",
            @"^\s*Blazor WebAssembly\s*$",
            @"^\s*MAUI Blazor \(Hybrid\)\s*$",
            @"^\s*MVC / Razor Pages\s*$",
            @"^\s*Entity Framework Core\s*$",
            @"^\s*MongoDB\s*$",
            @"^\s*Please make a selection\.\s*$",
            @"^\s*Please enter a note\.\s*$",
            @"^\s*Submit Your Feedback\s*$",
            @"^\s*Yes, Very Helpful!\s*$",
            @"^\s*No, Needs Improvement\s*$",
            @"^\s*Some parts of this topic may be machine translated\.\s*$",
            @"^\s*There are multiple versions of this document.*$",
            @"^\s*Pick the options that suit you best\.\s*$",
            @"^\s*Last updated:.*$",
            @"^\s*To help us improve.*$",
            @"^\s*Please note that although we cannot respond.*$",
        };

        var lines = markdown.Split('\n').ToList();
        var cleanedLines = new List<string>();
        var blankCount = 0;

        foreach (var line in lines)
        {
            // Check if line matches any noise pattern
            var isNoise = noisePatterns.Any(pattern => 
                Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase));

            if (isNoise)
            {
                continue;
            }

            // Handle blank lines
            if (string.IsNullOrWhiteSpace(line))
            {
                blankCount++;
                if (blankCount <= 2)
                {
                    cleanedLines.Add(line);
                }
            }
            else
            {
                blankCount = 0;
                cleanedLines.Add(line);
            }
        }

        return string.Join('\n', cleanedLines).Trim();
    }

    /// <summary>
    /// Removes UI/DB variant links from markdown content.
    /// These are links like [UI=MVC&amp;DB=EF](/docs/...) that just switch variants.
    /// </summary>
    internal static string RemoveVariantLinks(string markdown)
    {
        // Remove lines that are just variant links
        // Pattern: [UI=...&DB=...](...) or similar
        var variantLinkPattern = VariantLinkRegex();
        
        var lines = markdown.Split('\n');
        var cleanedLines = lines
            .Where(line => !variantLinkPattern.IsMatch(line))
            .ToList();

        // Also remove inline variant links
        var result = string.Join('\n', cleanedLines);
        
        // Remove avatar/contributor image links
        result = AvatarLinkRegex().Replace(result, "");
        
        return result.Trim();
    }

    [GeneratedRegex(@"^\s*\[UI=\w+&(?:amp;)?DB=\w+\]", RegexOptions.IgnoreCase)]
    private static partial Regex VariantLinkRegex();

    [GeneratedRegex(@"!\[Avatar\]\([^)]+\)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex AvatarLinkRegex();
}

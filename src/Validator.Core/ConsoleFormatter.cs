using System.Diagnostics;

namespace Validator.Core;

/// <summary>
/// Shared console formatting utility for unified visual output across all validator projects.
/// Uses only plain ASCII characters for maximum compatibility.
/// </summary>
public static class ConsoleFormatter
{
    /// <summary>
    /// The fixed width of separator lines.
    /// </summary>
    public const int SeparatorWidth = 70;

    /// <summary>
    /// Writes a separator line of exactly <see cref="SeparatorWidth"/> characters using '=' characters.
    /// If a label is provided, it is centered within the separator.
    /// If elapsed is provided, it is appended after the separator.
    /// </summary>
    /// <param name="label">Optional label to center within the separator line.</param>
    /// <param name="elapsed">Optional elapsed time string to append after the separator.</param>
    public static void WriteSeparator(string? label = null, string? elapsed = null)
    {
        Console.WriteLine(BuildSeparator(label, elapsed));
    }

    /// <summary>
    /// Writes a phase header with blank lines above and below a labeled separator.
    /// </summary>
    /// <param name="phaseName">The name of the phase to display (e.g. "Phase 1: Analyst").</param>
    /// <param name="stopwatch">Optional stopwatch whose elapsed time is included in the header.</param>
    public static void WritePhaseHeader(string phaseName, Stopwatch? stopwatch = null)
    {
        var elapsed = stopwatch is not null ? FormatElapsed(stopwatch) : null;
        Console.WriteLine();
        WriteSeparator(phaseName, elapsed);
        Console.WriteLine();
    }

    /// <summary>
    /// Formats the elapsed time of a <see cref="Stopwatch"/> as a compact human-readable string.
    /// </summary>
    /// <param name="stopwatch">The stopwatch to read elapsed time from.</param>
    /// <returns>A string like <c>[+0s]</c>, <c>[+45s]</c>, <c>[+1m 23s]</c>, or <c>[+1h 5m 12s]</c>.</returns>
    public static string FormatElapsed(Stopwatch stopwatch)
    {
        return FormatElapsed(stopwatch.Elapsed);
    }

    /// <summary>
    /// Formats a <see cref="TimeSpan"/> as a compact human-readable elapsed time string.
    /// </summary>
    /// <param name="timeSpan">The time span to format.</param>
    /// <returns>A string like <c>[+0s]</c>, <c>[+45s]</c>, <c>[+1m 23s]</c>, or <c>[+1h 5m 12s]</c>.</returns>
    public static string FormatElapsed(TimeSpan timeSpan)
    {
        var totalSeconds = (int)Math.Floor(timeSpan.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"[+{hours}h {minutes}m {seconds}s]";
        }

        if (minutes > 0)
        {
            return $"[+{minutes}m {seconds}s]";
        }

        return $"[+{seconds}s]";
    }

    /// <summary>
    /// Writes a formatted banner block with label-value pairs, bordered by separator lines.
    /// Labels are padded so that ':' characters are vertically aligned.
    /// </summary>
    /// <param name="items">The label-value pairs to display.</param>
    public static void WriteBanner(IEnumerable<(string Label, string Value)> items)
    {
        WriteItemBlock(items);
    }

    /// <summary>
    /// Writes a compact summary of the output directory structure.
    /// Shows each top-level subdirectory with file count and each top-level file with its size.
    /// </summary>
    /// <param name="outputPath">The path to the output directory to summarize.</param>
    public static void WriteDirectorySummary(string outputPath)
    {
        if (!Directory.Exists(outputPath))
        {
            Console.WriteLine($"  {outputPath} (not created)");
            return;
        }

        var entries = Directory.GetFileSystemEntries(outputPath);
        if (entries.Length == 0)
        {
            Console.WriteLine($"  {Path.GetFileName(outputPath)}/ (empty)");
            return;
        }

        // Find max name length for alignment
        var names = new List<(string Name, bool IsDirectory)>();
        foreach (var entry in entries.OrderBy(e => e))
        {
            var isDir = Directory.Exists(entry);
            var name = Path.GetFileName(entry) + (isDir ? "/" : "");
            names.Add((name, isDir));
        }

        var maxNameLength = names.Max(n => n.Name.Length);

        foreach (var (name, isDirectory) in names)
        {
            var paddedName = name.PadRight(maxNameLength + 1);
            if (isDirectory)
            {
                var fullPath = Path.Combine(outputPath, name.TrimEnd('/'));
                var fileCount = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories).Length;
                var suffix = fileCount == 0 ? "(empty)" : $"({fileCount} file{(fileCount == 1 ? "" : "s")})";
                Console.WriteLine($"  {paddedName} {suffix}");
            }
            else
            {
                var fileInfo = new FileInfo(Path.Combine(outputPath, name));
                var size = FormatFileSize(fileInfo.Length);
                Console.WriteLine($"  {paddedName} ({size})");
            }
        }
    }

    /// <summary>
    /// Writes an end summary block with label-value pairs, visually identical to <see cref="WriteBanner"/>.
    /// </summary>
    /// <param name="items">The label-value pairs to display.</param>
    public static void WriteEndSummary(IEnumerable<(string Label, string Value)> items)
    {
        WriteItemBlock(items);
    }

    private static void WriteItemBlock(IEnumerable<(string Label, string Value)> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            WriteSeparator();
            WriteSeparator();
            return;
        }

        var maxLabelLength = itemList.Max(i => i.Label.Length);

        WriteSeparator();
        foreach (var (label, value) in itemList)
        {
            var paddedLabel = label.PadRight(maxLabelLength + 1);
            Console.WriteLine($"  {paddedLabel}: {value}");
        }
        WriteSeparator();
    }

    private static string BuildSeparator(string? label, string? elapsed)
    {
        if (label is null)
        {
            var line = new string('=', SeparatorWidth);
            return elapsed is not null ? $"{line} {elapsed}" : line;
        }

        var content = $" {label} ";
        var elapsedSuffix = elapsed is not null ? $" {elapsed}" : "";
        var availableWidth = SeparatorWidth - (int)elapsedSuffix.Length;

        if (content.Length >= availableWidth - 2)
        {
            // Label too long, just pad minimally
            var line = $"== {label} ==" + elapsedSuffix;
            return line;
        }

        var remaining = availableWidth - content.Length;
        var leftPad = remaining / 2;
        var rightPad = remaining - leftPad;

        return new string('=', leftPad) + content + new string('=', rightPad) + elapsedSuffix;
    }

    private static string FormatFileSize(long bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        return bytes switch
        {
            >= (long)gb => $"{bytes / gb:F1} GB",
            >= (long)mb => $"{bytes / mb:F1} MB",
            >= (long)kb => $"{bytes / kb:F1} KB",
            _ => $"{bytes} B"
        };
    }
}

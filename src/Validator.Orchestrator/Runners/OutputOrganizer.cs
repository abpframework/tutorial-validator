using Validator.Core;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Organizes and validates the output directory structure.
/// </summary>
public class OutputOrganizer
{
    /// <summary>
    /// Organize the output directory after orchestration.
    /// </summary>
    public async Task OrganizeAsync(string outputPath, OrchestrationSummary summary)
    {
        var fullPath = Path.GetFullPath(outputPath);

        // Ensure all directories exist
        EnsureDirectoryStructure(fullPath);

        // Update summary with log paths
        var analystLogPath = Path.Combine(fullPath, "logs", "analyst.log");
        if (File.Exists(analystLogPath))
        {
            summary.Files.AnalystLog = "logs/analyst.log";
        }

        var executorLogPath = Path.Combine(fullPath, "logs", "executor.log");
        if (File.Exists(executorLogPath))
        {
            summary.Files.ExecutorLog = "logs/executor.log";
        }

        var dockerLogPath = Path.Combine(fullPath, "logs", "docker.log");
        if (File.Exists(dockerLogPath))
        {
            summary.Files.DockerLog = "logs/docker.log";
        }

        // Create a manifest file listing all outputs
        await CreateManifestAsync(fullPath, summary);

        Console.WriteLine("Output directory contents:");
        ConsoleFormatter.WriteDirectorySummary(fullPath);
    }

    private void EnsureDirectoryStructure(string outputPath)
    {
        var directories = new[]
        {
            outputPath,
            Path.Combine(outputPath, "scraped"),
            Path.Combine(outputPath, "results"),
            Path.Combine(outputPath, "logs")
        };

        foreach (var dir in directories)
        {
            Directory.CreateDirectory(dir);
        }
    }

    private async Task CreateManifestAsync(string outputPath, OrchestrationSummary summary)
    {
        var manifestPath = Path.Combine(outputPath, "manifest.txt");

        var lines = new List<string>
        {
            "Tutorial Validator - Output Manifest",
            "=========================================",
            "",
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            $"Tutorial: {summary.TutorialName ?? summary.TutorialUrl ?? "Unknown"}",
            $"Status: {summary.OverallStatus}",
            "",
            "Files:",
            "------"
        };

        // List all files in the output directory
        var files = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(outputPath, f))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            var fullFilePath = Path.Combine(outputPath, file);
            var fileInfo = new FileInfo(fullFilePath);
            lines.Add($"  {file} ({FormatFileSize(fileInfo.Length)})");
        }

        await File.WriteAllLinesAsync(manifestPath, lines);
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}

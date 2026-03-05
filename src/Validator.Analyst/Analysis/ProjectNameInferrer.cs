using Validator.Core.Models;
using Validator.Core.Models.Steps;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Infers project names from tutorial steps for project creation step generation.
/// </summary>
public static class ProjectNameInferrer
{
    /// <summary>
    /// Checks if the step list needs a project creation step prepended.
    /// Returns true if no "abp new" or "dotnet new" command exists in the steps.
    /// </summary>
    public static bool NeedsProjectCreationStep(List<TutorialStep> steps)
    {
        return !steps.OfType<CommandStep>().Any(cmd => CommandDetector.IsProjectCreationCommand(cmd.Command));
    }

    /// <summary>
    /// Infers the project name from file paths in the steps.
    /// Extracts the common project prefix from paths like "src/Acme.BookStore.Domain/...".
    /// </summary>
    /// <param name="steps">List of tutorial steps</param>
    /// <returns>Inferred project name or null if cannot determine</returns>
    public static string? InferProjectNameFromSteps(List<TutorialStep> steps)
    {
        // Collect file paths from FileOperation and CodeChange steps
        var filePaths = new List<string>();

        foreach (var step in steps)
        {
            switch (step)
            {
                case FileOperationStep fileOp when !string.IsNullOrWhiteSpace(fileOp.Path):
                    filePaths.Add(fileOp.Path);
                    break;
                case CodeChangeStep codeChange when codeChange.Modifications != null:
                    filePaths.AddRange(codeChange.Modifications.Select(m => m.FilePath));
                    break;
            }
        }

        if (filePaths.Count == 0)
            return null;

        // Extract project names from paths like "src/Acme.BookStore.Domain/..."
        var projectNames = new List<string>();
        foreach (var path in filePaths)
        {
            var projectName = ExtractProjectNameFromPath(path);
            if (!string.IsNullOrEmpty(projectName))
            {
                projectNames.Add(projectName);
            }
        }

        if (projectNames.Count == 0)
            return null;

        // Find the common prefix (most common project name)
        var grouped = projectNames.GroupBy(p => GetProjectRoot(p))
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return grouped?.Key;
    }

    /// <summary>
    /// Extracts the project name from a file path.
    /// Examples:
    /// - "src/Acme.BookStore.Domain/Books/Book.cs" -> "Acme.BookStore"
    /// - "Acme.BookStore.Application/Services/BookService.cs" -> "Acme.BookStore"
    /// </summary>
    private static string? ExtractProjectNameFromPath(string path)
    {
        // Normalize path separators
        var normalizedPath = path.Replace('\\', '/');
        var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Look for parts that look like project names (contain dots and standard ABP suffixes)
        var abpSuffixes = new[] { ".Domain", ".Application", ".EntityFrameworkCore", ".Web", 
            ".HttpApi", ".DbMigrator", ".Domain.Shared", ".Application.Contracts" };

        foreach (var part in parts)
        {
            // Check if this part ends with an ABP suffix
            foreach (var suffix in abpSuffixes)
            {
                if (part.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    // Return the part without the suffix
                    return part[..^suffix.Length];
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the root project name from a potentially compound name.
    /// Example: "Acme.BookStore" from "Acme.BookStore.Domain"
    /// </summary>
    private static string GetProjectRoot(string projectName)
    {
        // Already extracted by ExtractProjectNameFromPath, so just return as-is
        return projectName;
    }
}

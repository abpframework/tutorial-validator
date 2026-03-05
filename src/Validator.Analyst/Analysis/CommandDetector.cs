namespace Validator.Analyst.Analysis;

/// <summary>
/// Detects command types (build, project creation, migration) from CLI command strings.
/// </summary>
public static class CommandDetector
{
    /// <summary>
    /// Detects if a command is a build command.
    /// </summary>
    public static bool IsBuildCommand(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();
        return cmd.StartsWith("dotnet build") || 
               cmd.StartsWith("dotnet restore") ||
               cmd == "msbuild";
    }

    /// <summary>
    /// Detects if a command creates a new project.
    /// </summary>
    public static bool IsProjectCreationCommand(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();
        return cmd.StartsWith("abp new") || 
               cmd.StartsWith("dotnet new");
    }

    /// <summary>
    /// Detects if a command is a migration command.
    /// </summary>
    public static bool IsMigrationCommand(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();
        return cmd.Contains("migrations add") || 
               cmd.Contains("database update") ||
               cmd.Contains("dbmigrator");
    }

    /// <summary>
    /// Extracts the project name from a project creation command.
    /// </summary>
    /// <param name="command">The CLI command string (e.g., "abp new BookStore -u mvc").</param>
    /// <returns>The project name, or null if it cannot be determined.</returns>
    internal static string? ExtractProjectName(string command)
    {
        // Extract project name from "abp new ProjectName ..." or "dotnet new ... -n ProjectName"
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // abp new ProjectName
        if (parts.Length >= 3 && parts[0].Equals("abp", StringComparison.OrdinalIgnoreCase) && 
            parts[1].Equals("new", StringComparison.OrdinalIgnoreCase))
        {
            return parts[2];
        }
        
        // dotnet new ... -n ProjectName or --name ProjectName
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "-n" || parts[i] == "--name")
            {
                return parts[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures an "abp new" command has an explicit output directory (-o) flag.
    /// ABP Studio CLI (v9+) outputs directly to the current directory without creating
    /// a subdirectory. Adding -o ensures the project is created in a predictable
    /// subdirectory that matches subsequent steps' working directories.
    /// </summary>
    internal static string EnsureOutputDirectory(string command, string projectName)
    {
        // Check if -o or --output-folder is already present
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] is "-o" or "--output-folder")
            {
                return command; // Already has output directory
            }
        }

        return $"{command} -o {projectName}";
    }
}

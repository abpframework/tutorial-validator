namespace Validator.Executor.Agent;

/// <summary>
/// Helper to locate the project root directory within a workspace.
/// Used by the build gate to determine where to run `dotnet build`.
/// </summary>
internal static class ProjectDirectoryResolver
{
    /// <summary>
    /// Finds the project root directory by searching for .slnx or .sln files.
    /// Returns null if no project root is found.
    /// </summary>
    /// <param name="workspaceDirectory">The workspace root directory.</param>
    /// <returns>The project root directory path, or null if not found.</returns>
    internal static string? FindProjectRoot(string workspaceDirectory)
    {
        if (!Directory.Exists(workspaceDirectory))
            return null;

        // Search for .slnx or .sln files in immediate subdirectories
        foreach (var dir in Directory.GetDirectories(workspaceDirectory))
        {
            if (Directory.GetFiles(dir, "*.slnx").Length > 0 ||
                Directory.GetFiles(dir, "*.sln").Length > 0)
            {
                return dir;
            }
        }

        // Check workspace root itself
        if (Directory.GetFiles(workspaceDirectory, "*.slnx").Length > 0 ||
            Directory.GetFiles(workspaceDirectory, "*.sln").Length > 0)
        {
            return workspaceDirectory;
        }

        return null;
    }
}

using Microsoft.Extensions.Configuration;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Resolves paths to sibling projects and Docker compose files.
/// Consolidates the duplicated "navigate up from BaseDirectory" pattern.
/// </summary>
internal static class ProjectPathResolver
{
    /// <summary>
    /// Finds a sibling project's .csproj file by navigating up to the <c>src</c> directory.
    /// </summary>
    /// <param name="projectName">The project folder and .csproj name (e.g., "Validator.Analyst").</param>
    /// <returns>The full path to the .csproj file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the project cannot be found.</exception>
    internal static string FindProjectFile(string projectName)
    {
        // Navigate up from BaseDirectory to find src directory
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.Name != "src")
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            var projectPath = Path.Combine(dir.FullName, projectName, $"{projectName}.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }
        }

        // Fallback: try relative path from working directory
        var relativePath = Path.Combine(Directory.GetCurrentDirectory(), "..", projectName, $"{projectName}.csproj");
        if (File.Exists(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        throw new FileNotFoundException($"Could not find {projectName} project");
    }

    /// <summary>
    /// Finds the docker-compose.yml file, checking configuration first, then navigating up from BaseDirectory.
    /// </summary>
    /// <param name="configuration">Optional configuration to check for <c>Docker:ComposeFile</c> setting.</param>
    /// <returns>The full path to the docker-compose.yml file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the compose file cannot be found.</exception>
    internal static string FindComposeFile(IConfiguration? configuration = null)
    {
        // Check configuration first
        var configPath = configuration?.GetValue<string>("Docker:ComposeFile");
        if (!string.IsNullOrEmpty(configPath))
        {
            var fullPath = Path.GetFullPath(configPath);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Navigate up from BaseDirectory to find docker/docker-compose.yml
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var dockerDir = Path.Combine(dir.FullName, "docker");
            var composePath = Path.Combine(dockerDir, "docker-compose.yml");
            if (File.Exists(composePath))
            {
                return composePath;
            }
            dir = dir.Parent;
        }

        // Fallback: try relative path from working directory
        var relativePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "docker", "docker-compose.yml");
        if (File.Exists(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        throw new FileNotFoundException("Could not find docker-compose.yml");
    }
}

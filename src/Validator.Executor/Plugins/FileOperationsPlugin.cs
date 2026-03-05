using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;

namespace Validator.Executor.Plugins;

/// <summary>
/// Plugin for file system operations.
/// Used by the AI agent to create, read, modify, and delete files exactly as instructed.
/// </summary>
public class FileOperationsPlugin
{
    private readonly string _rootDirectory;

    /// <summary>
    /// Creates a new FileOperationsPlugin with the specified root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory for file operations.</param>
    public FileOperationsPlugin(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    /// <summary>
    /// Resolves a path relative to the root directory.
    /// </summary>
    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }
        return Path.GetFullPath(Path.Combine(_rootDirectory, path));
    }

    /// <summary>
    /// Reads the contents of a file.
    /// </summary>
    [KernelFunction]
    [Description("Read the contents of a file. Returns the file content as text.")]
    public async Task<string> ReadFileAsync(
        [Description("Path to the file to read (relative to project root or absolute)")] string path)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Reading: {fullPath}");

        try
        {
            if (!File.Exists(fullPath))
            {
                return $"ERROR: File not found: {fullPath}";
            }

            var content = await File.ReadAllTextAsync(fullPath);
            return $"File content ({content.Length} characters):\n\n{content}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to read file: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes content to a file, creating parent directories if needed.
    /// </summary>
    [KernelFunction]
    [Description("Write content to a file. Creates the file and parent directories if they don't exist. Overwrites existing content.")]
    public async Task<string> WriteFileAsync(
        [Description("Path to the file to write (relative to project root or absolute)")] string path,
        [Description("The content to write to the file")] string content)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Writing: {fullPath}");

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Console.WriteLine($"           Created directory: {directory}");
            }

            await File.WriteAllTextAsync(fullPath, content);
            return $"SUCCESS: File written to {fullPath} ({content.Length} characters)";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to write file: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes a file or directory.
    /// </summary>
    [KernelFunction]
    [Description("Delete a file or directory. For directories, deletes all contents recursively.")]
    public Task<string> DeleteAsync(
        [Description("Path to the file or directory to delete")] string path)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Deleting: {fullPath}");

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult($"SUCCESS: File deleted: {fullPath}");
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                return Task.FromResult($"SUCCESS: Directory deleted: {fullPath}");
            }
            else
            {
                return Task.FromResult($"ERROR: Path not found: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult($"ERROR: Failed to delete: {ex.Message}");
        }
    }

    /// <summary>
    /// Lists the contents of a directory.
    /// </summary>
    [KernelFunction]
    [Description("List the contents of a directory. Shows files and subdirectories.")]
    public Task<string> ListDirectoryAsync(
        [Description("Path to the directory to list (relative to project root or absolute)")] string path)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Listing: {fullPath}");

        try
        {
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult($"NOT FOUND: Directory does not exist: {fullPath}");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Contents of {fullPath}:");
            sb.AppendLine();

            var directories = Directory.GetDirectories(fullPath);
            var files = Directory.GetFiles(fullPath);

            sb.AppendLine($"Directories ({directories.Length}):");
            foreach (var dir in directories.OrderBy(d => d))
            {
                sb.AppendLine($"  [DIR]  {Path.GetFileName(dir)}/");
            }

            sb.AppendLine();
            sb.AppendLine($"Files ({files.Length}):");
            foreach (var file in files.OrderBy(f => f))
            {
                var info = new FileInfo(file);
                sb.AppendLine($"  [FILE] {Path.GetFileName(file)} ({info.Length} bytes)");
            }

            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex)
        {
            return Task.FromResult($"ERROR: Failed to list directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a file or directory exists.
    /// </summary>
    [KernelFunction]
    [Description("Check if a file or directory exists at the given path.")]
    public Task<string> ExistsAsync(
        [Description("Path to check (relative to project root or absolute)")] string path)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Checking existence: {fullPath}");

        if (File.Exists(fullPath))
        {
            return Task.FromResult($"EXISTS: File found at {fullPath}");
        }
        else if (Directory.Exists(fullPath))
        {
            return Task.FromResult($"EXISTS: Directory found at {fullPath}");
        }
        else
        {
            return Task.FromResult($"NOT FOUND: No file or directory at {fullPath}");
        }
    }

    /// <summary>
    /// Creates a directory.
    /// </summary>
    [KernelFunction]
    [Description("Create a directory, including any parent directories that don't exist.")]
    public Task<string> CreateDirectoryAsync(
        [Description("Path of the directory to create")] string path)
    {
        var fullPath = ResolvePath(path);
        Console.WriteLine($"    [FILE] Creating directory: {fullPath}");

        try
        {
            if (Directory.Exists(fullPath))
            {
                return Task.FromResult($"Directory already exists: {fullPath}");
            }

            Directory.CreateDirectory(fullPath);
            return Task.FromResult($"SUCCESS: Directory created: {fullPath}");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"ERROR: Failed to create directory: {ex.Message}");
        }
    }
}

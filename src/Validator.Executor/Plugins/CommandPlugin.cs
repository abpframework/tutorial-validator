using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace Validator.Executor.Plugins;

/// <summary>
/// Plugin for executing shell commands.
/// Used by the AI agent to run CLI commands exactly as instructed.
/// Also supports long-running background processes (e.g., web servers).
/// </summary>
public class CommandPlugin
{
    private const int MaxOutputChars = 6000;
    private readonly string _defaultWorkingDirectory;
    private readonly TimeSpan _defaultTimeout;
    /// <summary>
    /// The currently running background process, if any.
    /// </summary>
    private Process? _backgroundProcess;

    /// <summary>
    /// The base URL captured from the background process readiness output.
    /// For example, "https://localhost:44312" captured from "Now listening on https://localhost:44312".
    /// </summary>
    public string? BackgroundProcessBaseUrl { get; private set; }

    /// <summary>
    /// Whether a background process is currently running.
    /// </summary>
    public bool HasBackgroundProcess => _backgroundProcess is { HasExited: false };

    /// <summary>
    /// Creates a new CommandPlugin with the specified default working directory.
    /// </summary>
    /// <param name="defaultWorkingDirectory">The default working directory for commands.</param>
    /// <param name="defaultTimeout">Default timeout for commands. Defaults to 5 minutes.</param>
    public CommandPlugin(string defaultWorkingDirectory, TimeSpan? defaultTimeout = null)
    {
        _defaultWorkingDirectory = defaultWorkingDirectory;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Executes a shell command exactly as provided and returns the result.
    /// </summary>
    /// <param name="command">The exact command to execute.</param>
    /// <param name="workingDirectory">Optional working directory. Uses default if not specified.</param>
    /// <returns>A formatted string containing exit code, stdout, and stderr.</returns>
    [KernelFunction]
    [Description("Execute a shell command exactly as provided. Returns the exit code, stdout, and stderr. Use this for running CLI commands like 'dotnet build', 'abp new', etc.")]
    public async Task<string> ExecuteCommandAsync(
        [Description("The exact command to execute (e.g., 'dotnet build', 'abp new BookStore -u mvc')")] string command,
        [Description("Working directory for the command. Leave empty to use the project root.")] string? workingDirectory = null)
    {
        var workDir = ResolveWorkingDirectory(workingDirectory);

        // Validate working directory exists before starting the process
        if (!Directory.Exists(workDir))
        {
            Console.WriteLine($"    [COMMAND] Working directory not found: {workDir}");
            return FormatResult(-1, "", $"Working directory does not exist: {workDir}");
        }

        Console.WriteLine($"    [COMMAND] Executing: {command}");
        Console.WriteLine($"              Working directory: {workDir}");

        try
        {
            // Determine shell based on OS
            var isWindows = OperatingSystem.IsWindows();
            var shellName = isWindows ? "cmd.exe" : "/bin/bash";
            var shellArgs = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = shellName,
                Arguments = shellArgs,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit((int)_defaultTimeout.TotalMilliseconds));

            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return FormatResult(-1, stdout.ToString(), $"Command timed out after {_defaultTimeout.TotalMinutes} minutes.\n{stderr}");
            }

            var exitCode = process.ExitCode;
            var result = FormatResult(exitCode, stdout.ToString(), stderr.ToString());

            Console.WriteLine($"              Exit code: {exitCode}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"              [ERROR] {ex.Message}");
            return FormatResult(-1, "", $"Failed to execute command: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts a long-running process in the background (e.g., a web server) and waits for a
    /// readiness signal in stdout. The process stays alive after this method returns so that
    /// subsequent HTTP assertions can execute against it.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="readinessPattern">Regex pattern to match in stdout that indicates readiness (e.g., "Now listening on").</param>
    /// <param name="timeoutSeconds">How long to wait for the readiness signal before giving up.</param>
    /// <returns>A formatted result string indicating success (with captured URL) or failure.</returns>
    [KernelFunction]
    [Description("Start a long-running process (e.g., web server) in the background. Waits for a readiness signal in stdout before returning. Use this instead of ExecuteCommandAsync for commands that start servers or watchers.")]
    public async Task<string> StartBackgroundProcessAsync(
        [Description("The command to execute (e.g., 'dotnet run --project src/MyApp.Web/MyApp.Web.csproj')")] string command,
        [Description("Working directory for the command. Leave empty to use the project root.")] string? workingDirectory,
        [Description("Regex pattern to match in stdout that indicates the process is ready (e.g., 'Now listening on')")] string readinessPattern,
        [Description("Timeout in seconds to wait for the readiness signal")] int timeoutSeconds = 60)
    {
        // Kill any existing background process first
        StopBackgroundProcess();

        var workDir = ResolveWorkingDirectory(workingDirectory);

        if (!Directory.Exists(workDir))
        {
            Console.WriteLine($"    [BACKGROUND] Working directory not found: {workDir}");
            return FormatResult(-1, "", $"Working directory does not exist: {workDir}");
        }

        Console.WriteLine($"    [BACKGROUND] Starting: {command}");
        Console.WriteLine($"                 Working directory: {workDir}");
        Console.WriteLine($"                 Readiness pattern: {readinessPattern}");
        Console.WriteLine($"                 Timeout: {timeoutSeconds}s");

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var shellName = isWindows ? "cmd.exe" : "/bin/bash";
            var shellArgs = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = shellName,
                Arguments = shellArgs,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var readyTcs = new TaskCompletionSource<string>();
            var regex = new Regex(readinessPattern, RegexOptions.IgnoreCase);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                stdout.AppendLine(e.Data);

                // Check if this line matches the readiness pattern
                if (!readyTcs.Task.IsCompleted)
                {
                    var match = regex.Match(e.Data);
                    if (match.Success)
                    {
                        // Try to extract a URL from the line (common for ASP.NET Core apps)
                        var urlMatch = Regex.Match(e.Data, @"https?://[^\s]+");
                        readyTcs.TrySetResult(urlMatch.Success ? urlMatch.Value : e.Data);
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for readiness or timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                cts.Token.Register(() => readyTcs.TrySetCanceled());
                var readinessValue = await readyTcs.Task;

                // Process is ready -- store it and the base URL
                _backgroundProcess = process;
                BackgroundProcessBaseUrl = readinessValue.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? readinessValue.TrimEnd('/')
                    : null;

                Console.WriteLine($"                 Process is READY (PID: {process.Id})");
                if (BackgroundProcessBaseUrl != null)
                {
                    Console.WriteLine($"                 Base URL: {BackgroundProcessBaseUrl}");
                }

                var result = new StringBuilder();
                result.AppendLine("Exit Code: 0");
                result.AppendLine();
                result.AppendLine("=== STDOUT ===");
                result.AppendLine($"Background process started successfully (PID: {process.Id}).");
                if (BackgroundProcessBaseUrl != null)
                {
                    result.AppendLine($"Listening on: {BackgroundProcessBaseUrl}");
                }
                result.AppendLine($"Readiness signal matched: {readinessPattern}");

                return result.ToString();
            }
            catch (TaskCanceledException)
            {
                // Timeout -- check if the process died early
                if (process.HasExited)
                {
                    Console.WriteLine($"                 Process exited prematurely with code {process.ExitCode}");
                    var exitResult = FormatResult(process.ExitCode, stdout.ToString(), stderr.ToString());
                    process.Dispose();
                    return exitResult;
                }

                // Process is still alive but never became ready -- kill it
                Console.WriteLine($"                 TIMEOUT waiting for readiness after {timeoutSeconds}s");
                try { process.Kill(entireProcessTree: true); } catch { }
                process.Dispose();

                return FormatResult(-1, stdout.ToString(),
                    $"Background process did not become ready within {timeoutSeconds} seconds.\n{stderr}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"                 [ERROR] {ex.Message}");
            return FormatResult(-1, "", $"Failed to start background process: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the currently running background process, if any.
    /// </summary>
    public void StopBackgroundProcess()
    {
        if (_backgroundProcess == null) return;

        try
        {
            if (!_backgroundProcess.HasExited)
            {
                Console.WriteLine($"    [BACKGROUND] Stopping background process (PID: {_backgroundProcess.Id})");
                _backgroundProcess.Kill(entireProcessTree: true);
                _backgroundProcess.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [BACKGROUND] Error stopping process: {ex.Message}");
        }
        finally
        {
            _backgroundProcess.Dispose();
            _backgroundProcess = null;
            BackgroundProcessBaseUrl = null;
        }
    }

    /// <summary>
    /// Resolves the working directory from the provided value.
    /// Handles absolute paths, relative paths, and empty/null values.
    /// </summary>
    private string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return _defaultWorkingDirectory;
        }

        // If the path is already absolute, use it directly
        if (Path.IsPathRooted(workingDirectory))
        {
            return Path.GetFullPath(workingDirectory);
        }

        // Relative path - combine with default working directory
        return Path.GetFullPath(Path.Combine(_defaultWorkingDirectory, workingDirectory));
    }

    private static string FormatResult(int exitCode, string stdout, string stderr)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exit Code: {exitCode}");
        
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            sb.AppendLine();
            sb.AppendLine("=== STDOUT ===");
            sb.AppendLine(TrimForAgent(stdout));
        }
        
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            sb.AppendLine();
            sb.AppendLine("=== STDERR ===");
            sb.AppendLine(TrimForAgent(stderr));
        }

        return sb.ToString();
    }

    private static string TrimForAgent(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= MaxOutputChars)
        {
            return trimmed;
        }

        var tail = trimmed[^MaxOutputChars..];
        return $"[output truncated to last {MaxOutputChars} chars of {trimmed.Length} total]{Environment.NewLine}{tail}";
    }
}

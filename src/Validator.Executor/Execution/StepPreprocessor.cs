using Validator.Core.Models;
using Validator.Core.Models.Steps;

namespace Validator.Executor.Execution;

/// <summary>
/// Pre-processes tutorial steps before execution to handle known CLI compatibility issues.
/// </summary>
internal static class StepPreprocessor
{
    /// <summary>
    /// Normalizes steps before execution to handle known CLI compatibility issues
    /// and ensure long-running commands are properly flagged.
    /// </summary>
    internal static void NormalizeStepsForExecution(IList<TutorialStep> steps)
    {
        foreach (var step in steps)
        {
            if (step is not CommandStep cmd)
                continue;

            NormalizeProjectCreationCommand(cmd);
            NormalizeLongRunningCommand(cmd);
        }
    }

    /// <summary>
    /// Ensures "abp new" / "dotnet new" commands have an explicit -o flag.
    /// ABP Studio CLI (v9+) creates projects directly in the current directory unless
    /// the -o flag is specified. This ensures commands always produce output
    /// in a predictable subdirectory.
    /// </summary>
    private static void NormalizeProjectCreationCommand(CommandStep cmd)
    {
        var command = cmd.Command.Trim();
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (command.StartsWith("abp new", StringComparison.OrdinalIgnoreCase))
        {
            NormalizeAbpNewCommand(cmd, command, parts);
        }
        else if (command.StartsWith("dotnet new", StringComparison.OrdinalIgnoreCase))
        {
            NormalizeDotnetNewCommand(cmd, command, parts);
        }
    }

    /// <summary>
    /// Normalizes "abp new ProjectName ..." commands.
    /// For ABP CLI, the 3rd token is always the project name (e.g., "abp new BookStore -u mvc").
    /// </summary>
    private static void NormalizeAbpNewCommand(CommandStep cmd, string command, string[] parts)
    {
        var hasOutputFlag = parts.Any(p => p is "-o" or "--output-folder");
        if (hasOutputFlag)
            return;

        // "abp new ProjectName ..." - 3rd token is the project name
        if (parts.Length < 3)
            return;

        var projectName = parts[2];
        if (projectName.StartsWith('-'))
            return;

        cmd.Command = $"{command} -o {projectName}";
        Console.WriteLine($"  [NORMALIZE] Added -o {projectName} to abp new command");
    }

    /// <summary>
    /// Normalizes "dotnet new TemplateName ..." commands.
    /// For dotnet CLI, the project name is specified via -n/--name, not as a positional argument.
    /// Only adds -o if -n/--name is present and -o/--output is not.
    /// </summary>
    private static void NormalizeDotnetNewCommand(CommandStep cmd, string command, string[] parts)
    {
        var hasOutputFlag = parts.Any(p => p is "-o" or "--output");
        if (hasOutputFlag)
            return;

        // Find project name from -n or --name flag
        string? projectName = null;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] is "-n" or "--name")
            {
                projectName = parts[i + 1];
                break;
            }
        }

        if (string.IsNullOrEmpty(projectName) || projectName.StartsWith('-'))
            return;

        cmd.Command = $"{command} -o {projectName}";
        Console.WriteLine($"  [NORMALIZE] Added -o {projectName} to dotnet new command");
    }

    /// <summary>
    /// Auto-detects long-running commands (web servers, watchers) and sets the appropriate flags
    /// if the Analyst didn't mark them. This is a safety net for test plans created without
    /// the Analyst's long-running command awareness.
    /// </summary>
    private static void NormalizeLongRunningCommand(CommandStep cmd)
    {
        if (cmd.IsLongRunning)
            return; // Already flagged

        var command = cmd.Command.Trim().ToLowerInvariant();

        var isLongRunning =
            // dotnet run for web projects
            (command.StartsWith("dotnet run") &&
             (command.Contains(".web") || command.Contains(".blazor") || command.Contains(".httpapi.host")))
            // dotnet watch is always long-running
            || command.StartsWith("dotnet watch")
            // npm/yarn start/dev
            || command is "npm start" or "npm run dev" or "yarn start" or "yarn dev";

        if (!isLongRunning)
            return;

        cmd.IsLongRunning = true;
        cmd.ReadinessPattern ??= command.StartsWith("dotnet") ? "Now listening on" : "localhost";
        if (cmd.ReadinessTimeoutSeconds <= 0)
            cmd.ReadinessTimeoutSeconds = 120;

        // Long-running commands should not have an exit code expectation
        cmd.Expects = null;

        Console.WriteLine($"  [NORMALIZE] Marked command as long-running: {cmd.Command}");
        Console.WriteLine($"              Readiness pattern: {cmd.ReadinessPattern}");
    }
}

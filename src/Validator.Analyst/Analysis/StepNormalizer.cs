using System.Text.RegularExpressions;
using Validator.Core.Models;
using Validator.Core.Models.Assertions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Steps;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Validates and post-processes extracted steps.
/// Ensures steps conform to Validator.Core schemas and have correct metadata.
/// </summary>
public static class StepNormalizer
{
    /// <summary>
    /// Validates and normalizes a list of extracted steps.
    /// </summary>
    /// <param name="steps">Steps to validate</param>
    /// <returns>Validated and normalized steps with any issues</returns>
    public static StepValidationResult ValidateAndNormalize(List<TutorialStep> steps)
    {
        var result = new StepValidationResult();
        var normalizedSteps = new List<TutorialStep>();
        var stepId = 1;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var nextStep = i + 1 < steps.Count ? steps[i + 1] : null;
            var (normalizedStep, issues) = NormalizeStep(step, stepId, nextStep);
            
            if (normalizedStep != null)
            {
                normalizedStep.StepId = stepId++;
                normalizedSteps.Add(normalizedStep);
            }
            
            result.Issues.AddRange(issues);
        }

        result.Steps = normalizedSteps;
        return result;
    }

    /// <summary>
    /// Infers the scope for a code change based on file path.
    /// </summary>
    public static string InferScope(string filePath)
    {
        var path = filePath.ToLowerInvariant();
        
        if (path.Contains(".domain.shared") || path.Contains("domain.shared"))
            return "shared";
        if (path.Contains(".domain") || path.Contains("/domain/"))
            return "domain";
        if (path.Contains(".application.contracts") || path.Contains("application.contracts"))
            return "contracts";
        if (path.Contains(".application") || path.Contains("/application/"))
            return "application";
        if (path.Contains(".entityframeworkcore") || path.Contains("entityframeworkcore") || 
            path.Contains(".mongodb") || path.Contains("mongodb"))
            return "infrastructure";
        if (path.Contains(".web") || path.Contains("/web/") || path.Contains(".httpapi"))
            return "web";
        if (path.Contains(".dbmigrator") || path.Contains("dbmigrator"))
            return "migrator";
        if (path.Contains("test"))
            return "test";
        
        return "domain"; // Default
    }

    private static (TutorialStep? Step, List<string> Issues) NormalizeStep(TutorialStep step, int expectedId, TutorialStep? nextStep)
    {
        var issues = new List<string>();

        switch (step)
        {
            case CommandStep cmd:
                return NormalizeCommandStep(cmd, expectedId, issues);
            case FileOperationStep fileOp:
                return NormalizeFileOperationStep(fileOp, expectedId, issues, nextStep);
            case CodeChangeStep codeChange:
                return NormalizeCodeChangeStep(codeChange, expectedId, issues);
            case ExpectationStep expectation:
                return NormalizeExpectationStep(expectation, expectedId, issues);
            default:
                issues.Add($"Step {expectedId}: Unknown step type");
                return (null, issues);
        }
    }

    private static (TutorialStep?, List<string>) NormalizeCommandStep(CommandStep cmd, int stepId, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(cmd.Command))
        {
            issues.Add($"Step {stepId}: CommandStep has empty command");
            return (null, issues);
        }

        // Infer expected outputs for project creation commands
        if (CommandDetector.IsProjectCreationCommand(cmd.Command))
        {
            var projectName = CommandDetector.ExtractProjectName(cmd.Command);
            if (!string.IsNullOrEmpty(projectName))
            {
                if (cmd.Expects?.Creates == null)
                {
                    cmd.Expects ??= new CommandExpectation();
                    cmd.Expects.Creates = [projectName];
                }

                // Ensure the command has an explicit output directory (-o) flag.
                // ABP Studio CLI creates the project directly in the current directory
                // without -o, but subsequent steps expect a subdirectory named after
                // the project to exist.
                cmd.Command = CommandDetector.EnsureOutputDirectory(cmd.Command, projectName);
            }
        }

        // Auto-detect long-running commands if not already marked.
        // These are commands that start a process which never exits on its own (web servers, watchers, etc.).
        if (!cmd.IsLongRunning && IsLongRunningCommand(cmd.Command))
        {
            cmd.IsLongRunning = true;
            cmd.ReadinessPattern ??= InferReadinessPattern(cmd.Command);
            cmd.ReadinessTimeoutSeconds = cmd.ReadinessTimeoutSeconds <= 0 ? 120 : cmd.ReadinessTimeoutSeconds;

            // Long-running commands should not have an exit code expectation
            cmd.Expects = null;
        }

        return (cmd, issues);
    }

    /// <summary>
    /// Detects commands that start long-running processes (web servers, watchers, etc.).
    /// </summary>
    private static bool IsLongRunningCommand(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();

        // dotnet run for web projects (*.Web.*, *.HttpApi.Host.*, *.Blazor.*, etc.)
        if (cmd.StartsWith("dotnet run") &&
            (cmd.Contains(".web") || cmd.Contains(".blazor") || cmd.Contains(".httpapi.host")))
        {
            return true;
        }

        // dotnet watch is always long-running
        if (cmd.StartsWith("dotnet watch"))
            return true;

        // npm/yarn start or dev commands
        if (cmd is "npm start" or "npm run dev" or "yarn start" or "yarn dev")
            return true;

        return false;
    }

    /// <summary>
    /// Infers the readiness pattern for a long-running command based on the technology.
    /// </summary>
    private static string InferReadinessPattern(string command)
    {
        var cmd = command.Trim().ToLowerInvariant();

        // ASP.NET Core apps print "Now listening on" when ready
        if (cmd.StartsWith("dotnet run") || cmd.StartsWith("dotnet watch"))
            return "Now listening on";

        // Node.js/Vite apps typically print a localhost URL
        if (cmd.StartsWith("npm") || cmd.StartsWith("yarn"))
            return "localhost";

        return "listening";
    }

    private static (TutorialStep?, List<string>) NormalizeFileOperationStep(FileOperationStep fileOp, int stepId, List<string> issues, TutorialStep? nextStep)
    {
        if (string.IsNullOrWhiteSpace(fileOp.Path))
        {
            issues.Add($"Step {stepId}: FileOperationStep has empty path");
            return (null, issues);
        }

        // Normalize entity type
        if (string.IsNullOrWhiteSpace(fileOp.EntityType))
        {
            // Infer from path - if it has an extension, it's a file
            fileOp.EntityType = System.IO.Path.HasExtension(fileOp.Path) ? "file" : "directory";
        }

        // Check for misclassified file_operation:create that should be code_change
        if (fileOp.Operation == FileOperationType.Create && fileOp.EntityType == "file" && !string.IsNullOrWhiteSpace(fileOp.Content))
        {
            var (reclassifiedStep, reason) = DetectAndReclassifyMisclassifiedCreate(fileOp);
            if (reason != null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [NORMALIZER WARNING] Step {stepId}: Reclassified file_operation:create → code_change for '{fileOp.Path}'");
                Console.WriteLine($"    Reason: {reason}");
                Console.ResetColor();
                issues.Add($"Step {stepId}: Reclassified file_operation:create to code_change for '{fileOp.Path}'. Reason: {reason}");
                // Run the reclassified step through code_change normalization
                return NormalizeCodeChangeStep((CodeChangeStep)reclassifiedStep, stepId, issues);
            }
        }

        // Validate operation - check for file creation without content
        if (fileOp.Operation == FileOperationType.Create && fileOp.EntityType == "file" && string.IsNullOrWhiteSpace(fileOp.Content))
        {
            // Look ahead: if next step is a code_change with fullContent for the same file, this is valid
            // (tutorial pattern: "create file X" followed by "here's the content for X")
            var hasContentInNextStep = nextStep is CodeChangeStep codeChange &&
                codeChange.Modifications?.Any(m => 
                    NormalizePath(m.FilePath) == NormalizePath(fileOp.Path) && 
                    !string.IsNullOrWhiteSpace(m.FullContent)) == true;

            if (!hasContentInNextStep)
            {
                issues.Add($"Step {stepId}: File creation has no content");
            }
        }

        return (fileOp, issues);
    }

    /// <summary>
    /// Normalizes a file path for comparison (lowercase, forward slashes).
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.ToLowerInvariant().Replace('\\', '/').TrimEnd('/');
    }

    private static (TutorialStep?, List<string>) NormalizeCodeChangeStep(CodeChangeStep codeChange, int stepId, List<string> issues)
    {
        // Validate modifications exist
        if (codeChange.Modifications == null || codeChange.Modifications.Count == 0)
        {
            issues.Add($"Step {stepId}: CodeChangeStep has no modifications");
            return (null, issues);
        }

        // Infer scope from first modification if not set
        if (string.IsNullOrWhiteSpace(codeChange.Scope) && codeChange.Modifications.Count > 0)
        {
            codeChange.Scope = InferScope(codeChange.Modifications[0].FilePath);
        }

        // Validate each modification
        foreach (var mod in codeChange.Modifications)
        {
            if (string.IsNullOrWhiteSpace(mod.FilePath))
            {
                issues.Add($"Step {stepId}: Modification has empty file path");
            }
            
            if (string.IsNullOrWhiteSpace(mod.FullContent) && 
                string.IsNullOrWhiteSpace(mod.SearchPattern))
            {
                issues.Add($"Step {stepId}: Modification has neither fullContent nor searchPattern");
            }
        }

        // Set expected files
        codeChange.ExpectedFiles ??= codeChange.Modifications
            .Select(m => m.FilePath)
            .Distinct()
            .ToList();

        return (codeChange, issues);
    }

    private static (TutorialStep?, List<string>) NormalizeExpectationStep(ExpectationStep expectation, int stepId, List<string> issues)
    {
        if (expectation.Assertions == null || expectation.Assertions.Count == 0)
        {
            issues.Add($"Step {stepId}: ExpectationStep has no assertions");
            return (null, issues);
        }

        // Normalize assertions
        foreach (var assertion in expectation.Assertions)
        {
            switch (assertion)
            {
                case BuildAssertion build:
                    if (string.IsNullOrWhiteSpace(build.Command))
                    {
                        build.Command = "dotnet build";
                    }
                    break;
                case HttpAssertion http:
                    if (string.IsNullOrWhiteSpace(http.Url))
                    {
                        issues.Add($"Step {stepId}: HttpAssertion has empty URL");
                    }
                    if (string.IsNullOrWhiteSpace(http.Method))
                    {
                        http.Method = "GET";
                    }
                    break;
            }
        }

        return (expectation, issues);
    }

    /// <summary>
    /// Detects misclassified file_operation:create steps that should actually be code_change steps.
    /// Uses four heuristics to identify partial snippets or modifications to existing files.
    /// </summary>
    private static (TutorialStep step, string? reclassificationReason) DetectAndReclassifyMisclassifiedCreate(FileOperationStep fileOp)
    {
        var content = fileOp.Content ?? string.Empty;
        var path = fileOp.Path;
        var description = fileOp.Description ?? string.Empty;
        var extension = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        var isCSharp = extension == ".cs";

        // Heuristic 1: No namespace in .cs content
        if (isCSharp && !Regex.IsMatch(content, @"\bnamespace\b"))
        {
            return BuildReclassifiedStep(fileOp,
                "C# file has no namespace declaration — likely a partial snippet meant for an existing file");
        }

        // Heuristic 2: Missing using statements in .cs content that references types
        if (isCSharp)
        {
            var hasUsings = content.Split('\n')
                .Any(line => line.TrimStart().StartsWith("using "));

            if (!hasUsings)
            {
                // Check if content references types (attributes, inheritance, or generics)
                var hasAttributes = Regex.IsMatch(content, @"\[\s*\w+");
                var hasInheritance = Regex.IsMatch(content, @":\s*\w+");
                var hasGenerics = Regex.IsMatch(content, @"<\s*\w+");

                if (hasAttributes || hasInheritance || hasGenerics)
                {
                    return BuildReclassifiedStep(fileOp,
                        "C# file references types but has no using statements — likely a partial snippet");
                }
            }
        }

        // Heuristic 3: File name referenced as existing in Description
        if (!string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(description))
        {
            var descLower = description.ToLowerInvariant();
            var fileNameLower = fileName.ToLowerInvariant();

            var referencePatterns = new[]
            {
                $"in the {fileNameLower}",
                $"in {fileNameLower}",
                $"modify {fileNameLower}",
                $"update {fileNameLower}",
                $"open {fileNameLower}",
                $"add to {fileNameLower}"
            };

            var existenceKeywords = new[]
            {
                "pre-configured",
                "already exists",
                "comes with"
            };

            var matchesReference = referencePatterns.Any(p => descLower.Contains(p));
            var matchesExistence = existenceKeywords.Any(k => descLower.Contains(k));

            if (matchesReference || matchesExistence)
            {
                return BuildReclassifiedStep(fileOp,
                    "Step description suggests modifying an existing file, not creating a new one");
            }
        }

        // Heuristic 4: Suspicious subfolder placement for project-level files
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var rootLevelSuffixes = new[]
            {
                "Mapper", "Module", "DbContext", "Context",
                "Startup", "Program", "Configuration"
            };

            var hasRootLevelSuffix = rootLevelSuffixes.Any(s =>
                fileName.Contains(s, StringComparison.OrdinalIgnoreCase));

            if (hasRootLevelSuffix)
            {
                // Check if there's an intermediate directory (not just project-root/file)
                var normalizedPath = path.Replace('\\', '/');
                var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Need at least 3 segments: project-dir / subfolder / file.ext
                // This means the file is placed inside a subfolder, not at the project root
                if (segments.Length >= 3)
                {
                    return BuildReclassifiedStep(fileOp,
                        "File name suggests a project-level file but path places it in a subfolder — likely should modify the existing root-level file");
                }
            }
        }

        // No heuristic triggered — return original step unchanged
        return (fileOp, null);
    }

    /// <summary>
    /// Builds a CodeChangeStep from a misclassified FileOperationStep.
    /// </summary>
    private static (TutorialStep step, string reclassificationReason) BuildReclassifiedStep(
        FileOperationStep fileOp, string reason)
    {
        var codeChangeStep = new CodeChangeStep
        {
            StepId = fileOp.StepId,
            Description = fileOp.Description,
            Scope = InferScope(fileOp.Path),
            Modifications = new List<CodeModification>
            {
                new CodeModification
                {
                    FilePath = fileOp.Path,
                    FullContent = fileOp.Content
                }
            }
        };

        return (codeChangeStep, reason);
    }
}

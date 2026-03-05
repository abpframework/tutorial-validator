using Validator.Core.Models;
using Validator.Core.Models.Assertions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Steps;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Compacts normalized steps while preserving order and step-type compatibility.
/// </summary>
public static class StepCompactor
{
    public static List<TutorialStep> Compact(IReadOnlyList<TutorialStep> steps, StepCompactionOptions? options = null)
    {
        options ??= new StepCompactionOptions();
        if (!options.Enabled || steps.Count == 0)
            return Renumber(CloneSteps(steps));

        var compacted = CloneSteps(steps);
        if (compacted.Count > options.TargetStepCount)
        {
            compacted = MergeAdjacentCodeChanges(compacted, options.MaxCodeModificationsPerStep);
            compacted = MergeAdjacentExpectations(compacted);
            compacted = MergeAdjacentCommands(compacted);
            compacted = RemoveRedundantBuildExpectations(compacted);
        }

        if (compacted.Count > options.MaxStepCount)
        {
            compacted = RemoveDirectoryCreateBeforeFileCreate(compacted);
            compacted = ConvertFileOperationsToCodeChanges(compacted);
            compacted = MergeAdjacentCodeChanges(compacted, options.MaxCodeModificationsPerStep + 4);
            compacted = MergeAdjacentExpectations(compacted);
            compacted = MergeAdjacentCodeChangesAnyScope(compacted, options.MaxCodeModificationsPerStep + 8);
        }

        return Renumber(compacted);
    }

    private static List<TutorialStep> MergeAdjacentCodeChanges(List<TutorialStep> steps, int maxModsPerStep)
    {
        var result = new List<TutorialStep>();
        foreach (var step in steps)
        {
            if (result.LastOrDefault() is CodeChangeStep previous &&
                step is CodeChangeStep current &&
                string.Equals(previous.Scope, current.Scope, StringComparison.OrdinalIgnoreCase))
            {
                var prevMods = previous.Modifications ?? [];
                var currMods = current.Modifications ?? [];

                if (prevMods.Count + currMods.Count <= maxModsPerStep)
                {
                    previous.Modifications = prevMods.Concat(currMods).ToList();
                    previous.ExpectedFiles = (previous.ExpectedFiles ?? [])
                        .Concat(current.ExpectedFiles ?? [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    previous.Description = MergeDescription(previous.Description, current.Description);
                    continue;
                }
            }

            result.Add(step);
        }

        return result;
    }

    private static List<TutorialStep> MergeAdjacentCodeChangesAnyScope(List<TutorialStep> steps, int maxModsPerStep)
    {
        var result = new List<TutorialStep>();
        foreach (var step in steps)
        {
            if (result.LastOrDefault() is CodeChangeStep previous &&
                step is CodeChangeStep current)
            {
                var prevMods = previous.Modifications ?? [];
                var currMods = current.Modifications ?? [];

                if (prevMods.Count + currMods.Count <= maxModsPerStep)
                {
                    previous.Modifications = prevMods.Concat(currMods).ToList();
                    previous.ExpectedFiles = (previous.ExpectedFiles ?? [])
                        .Concat(current.ExpectedFiles ?? [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    previous.Description = MergeDescription(previous.Description, current.Description);
                    continue;
                }
            }

            result.Add(step);
        }

        return result;
    }

    private static List<TutorialStep> MergeAdjacentExpectations(List<TutorialStep> steps)
    {
        var result = new List<TutorialStep>();
        foreach (var step in steps)
        {
            if (result.LastOrDefault() is ExpectationStep previous &&
                step is ExpectationStep current)
            {
                previous.Assertions = previous.Assertions
                    .Concat(current.Assertions)
                    .ToList();
                previous.Description = MergeDescription(previous.Description, current.Description);
                continue;
            }

            result.Add(step);
        }

        return result;
    }

    private static List<TutorialStep> MergeAdjacentCommands(List<TutorialStep> steps)
    {
        var result = new List<TutorialStep>();
        foreach (var step in steps)
        {
            if (result.LastOrDefault() is CommandStep previous &&
                step is CommandStep current &&
                CanMergeCommands(previous, current))
            {
                previous.Command = $"{previous.Command} && {current.Command}";
                previous.Description = MergeDescription(previous.Description, current.Description);
                previous.Expects = MergeCommandExpectations(previous.Expects, current.Expects);
                continue;
            }

            result.Add(step);
        }

        return result;
    }

    private static bool CanMergeCommands(CommandStep left, CommandStep right)
    {
        if (left.IsLongRunning || right.IsLongRunning)
            return false;

        if (CommandDetector.IsProjectCreationCommand(left.Command) ||
            CommandDetector.IsProjectCreationCommand(right.Command))
            return false;

        return true;
    }

    private static CommandExpectation? MergeCommandExpectations(CommandExpectation? left, CommandExpectation? right)
    {
        if (left == null && right == null)
            return null;

        left ??= new CommandExpectation();
        if (right == null)
            return left;

        left.ExitCode = Math.Max(left.ExitCode, right.ExitCode);
        left.Creates = (left.Creates ?? [])
            .Concat(right.Creates ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return left;
    }

    private static List<TutorialStep> RemoveRedundantBuildExpectations(List<TutorialStep> steps)
    {
        var result = new List<TutorialStep>();
        var pendingBuildExpectationIndex = -1;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];

            if (step is ExpectationStep expectation &&
                expectation.Assertions.All(a => a is BuildAssertion))
            {
                if (pendingBuildExpectationIndex >= 0)
                {
                    result[pendingBuildExpectationIndex] = expectation;
                }
                else
                {
                    pendingBuildExpectationIndex = result.Count;
                    result.Add(step);
                }

                continue;
            }

            pendingBuildExpectationIndex = -1;
            result.Add(step);
        }

        return result;
    }

    private static List<TutorialStep> RemoveDirectoryCreateBeforeFileCreate(List<TutorialStep> steps)
    {
        var result = new List<TutorialStep>();

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i] is FileOperationStep fileOp &&
                fileOp.Operation == FileOperationType.Create &&
                fileOp.EntityType.Equals("directory", StringComparison.OrdinalIgnoreCase))
            {
                var nextStep = i + 1 < steps.Count ? steps[i + 1] : null;
                if (nextStep is FileOperationStep nextFileOp &&
                    nextFileOp.Operation == FileOperationType.Create &&
                    nextFileOp.EntityType.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                    IsChildPath(nextFileOp.Path, fileOp.Path))
                {
                    continue;
                }
            }

            result.Add(steps[i]);
        }

        return result;
    }

    private static List<TutorialStep> ConvertFileOperationsToCodeChanges(List<TutorialStep> steps)
    {
        var result = new List<TutorialStep>(steps.Count);
        foreach (var step in steps)
        {
            if (step is FileOperationStep fileOp &&
                fileOp.EntityType.Equals("file", StringComparison.OrdinalIgnoreCase) &&
                fileOp.Operation is FileOperationType.Create or FileOperationType.Modify &&
                !string.IsNullOrWhiteSpace(fileOp.Content))
            {
                result.Add(new CodeChangeStep
                {
                    StepId = fileOp.StepId,
                    Description = fileOp.Description,
                    Scope = StepNormalizer.InferScope(fileOp.Path),
                    ExpectedFiles = [fileOp.Path],
                    Modifications =
                    [
                        new CodeModification
                        {
                            FilePath = fileOp.Path,
                            FullContent = fileOp.Content
                        }
                    ]
                });
                continue;
            }

            result.Add(step);
        }

        return result;
    }

    private static bool IsChildPath(string path, string potentialParent)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedParent = NormalizePath(potentialParent).TrimEnd('/');
        return normalizedPath.StartsWith($"{normalizedParent}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/');

    private static string? MergeDescription(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right;
        if (string.IsNullOrWhiteSpace(right))
            return left;
        return $"{left}; {right}";
    }

    private static List<TutorialStep> CloneSteps(IReadOnlyList<TutorialStep> steps)
    {
        var cloned = new List<TutorialStep>(steps.Count);
        foreach (var step in steps)
        {
            switch (step)
            {
                case CommandStep command:
                    cloned.Add(new CommandStep
                    {
                        StepId = command.StepId,
                        Description = command.Description,
                        Command = command.Command,
                        IsLongRunning = command.IsLongRunning,
                        ReadinessPattern = command.ReadinessPattern,
                        ReadinessTimeoutSeconds = command.ReadinessTimeoutSeconds,
                        Expects = command.Expects == null
                            ? null
                            : new CommandExpectation
                            {
                                ExitCode = command.Expects.ExitCode,
                                Creates = command.Expects.Creates?.ToList()
                            }
                    });
                    break;
                case FileOperationStep fileOperation:
                    cloned.Add(new FileOperationStep
                    {
                        StepId = fileOperation.StepId,
                        Description = fileOperation.Description,
                        Operation = fileOperation.Operation,
                        Path = fileOperation.Path,
                        EntityType = fileOperation.EntityType,
                        Content = fileOperation.Content
                    });
                    break;
                case CodeChangeStep codeChange:
                    cloned.Add(new CodeChangeStep
                    {
                        StepId = codeChange.StepId,
                        Description = codeChange.Description,
                        Scope = codeChange.Scope,
                        InputContext = codeChange.InputContext,
                        Constraints = codeChange.Constraints,
                        ExpectedFiles = codeChange.ExpectedFiles?.ToList(),
                        Modifications = codeChange.Modifications?.Select(m => new CodeModification
                        {
                            FilePath = m.FilePath,
                            FullContent = m.FullContent,
                            SearchPattern = m.SearchPattern,
                            ReplaceWith = m.ReplaceWith
                        }).ToList()
                    });
                    break;
                case ExpectationStep expectation:
                    cloned.Add(new ExpectationStep
                    {
                        StepId = expectation.StepId,
                        Description = expectation.Description,
                        Assertions = expectation.Assertions.ToList()
                    });
                    break;
            }
        }

        return cloned;
    }

    private static List<TutorialStep> Renumber(List<TutorialStep> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            steps[i].StepId = i + 1;
        }

        return steps;
    }
}


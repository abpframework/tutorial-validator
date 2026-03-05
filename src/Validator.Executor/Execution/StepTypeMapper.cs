using Validator.Core.Models;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Steps;

namespace Validator.Executor.Execution;

/// <summary>
/// Shared utility for mapping tutorial step instances to their type metadata.
/// </summary>
internal static class StepTypeMapper
{
    /// <summary>
    /// Gets the <see cref="StepType"/> enum value from a step instance.
    /// </summary>
    internal static StepType GetStepType(TutorialStep step) => step switch
    {
        CommandStep => StepType.Command,
        FileOperationStep => StepType.FileOperation,
        CodeChangeStep => StepType.CodeChange,
        ExpectationStep => StepType.Expectation,
        _ => throw new ArgumentException($"Unknown step type: {step.GetType().Name}")
    };

    /// <summary>
    /// Gets the human-readable name for a step type.
    /// </summary>
    internal static string GetStepTypeName(TutorialStep step) => step switch
    {
        CommandStep => "Command",
        FileOperationStep => "FileOperation",
        CodeChangeStep => "CodeChange",
        ExpectationStep => "Expectation",
        _ => step.GetType().Name
    };
}

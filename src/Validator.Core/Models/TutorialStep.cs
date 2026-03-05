using System.Text.Json.Serialization;
using Validator.Core.Models.Steps;

namespace Validator.Core.Models;

/// <summary>
/// Base class for all tutorial steps.
/// Uses polymorphic JSON serialization with a type discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CommandStep), "command")]
[JsonDerivedType(typeof(FileOperationStep), "file_operation")]
[JsonDerivedType(typeof(CodeChangeStep), "code_change")]
[JsonDerivedType(typeof(ExpectationStep), "expectation")]
public abstract class TutorialStep
{
    /// <summary>
    /// Unique identifier for this step within the test plan.
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// Human-readable description of what this step does.
    /// </summary>
    public string? Description { get; set; }
}

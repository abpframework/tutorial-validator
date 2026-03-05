namespace Validator.Executor.Agent;

/// <summary>
/// Defines the developer experience level that the executor agent simulates.
/// Each persona uses different system prompts and behavioral constraints.
/// </summary>
public enum DeveloperPersona
{
    /// <summary>
    /// Knows basic programming but is not proficient in C# or .NET.
    /// Follows tutorial instructions with zero code intelligence.
    /// Will not add using statements, fix syntax, or do smart merges.
    /// </summary>
    Junior,

    /// <summary>
    /// Familiar with C# and .NET fundamentals but new to ABP Framework.
    /// Can ensure syntactic validity (usings, braces) but will not use ABP knowledge.
    /// This is the default persona and the original executor behavior.
    /// </summary>
    Mid,

    /// <summary>
    /// Expert in C#, .NET, and ABP Framework.
    /// Can self-fix errors, diagnose build failures, and retry with corrections.
    /// Goal: execute every step successfully, noting any fixes applied.
    /// </summary>
    Senior
}

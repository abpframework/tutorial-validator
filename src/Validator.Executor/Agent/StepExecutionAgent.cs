using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Validator.Core.Models;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;
using Validator.Core.Models.Steps;
using Validator.Executor.Execution;

namespace Validator.Executor.Agent;

/// <summary>
/// AI agent that executes tutorial steps by simulating a developer with a specific persona.
/// Uses Semantic Kernel with function calling to interact with the system.
/// </summary>
public class StepExecutionAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly string _workingDirectory;
    private readonly FunctionCallTracker? _tracker;
    private readonly DeveloperPersona _persona;
    private readonly Plugins.CommandPlugin? _commandPlugin;

    /// <summary>
    /// Maximum number of retry attempts for the Senior persona when a step fails.
    /// </summary>
    private const int SeniorMaxRetries = 3;
    private readonly int _seniorBuildGateInterval;

    /// <summary>
    /// Creates a new StepExecutionAgent.
    /// </summary>
    /// <param name="kernel">The Semantic Kernel with plugins registered.</param>
    /// <param name="workingDirectory">The root working directory for execution.</param>
    /// <param name="tracker">Optional tracker that captures actual function call results for deterministic evaluation.</param>
    /// <param name="persona">The developer persona to simulate. Defaults to <see cref="DeveloperPersona.Mid"/>.</param>
    /// <param name="commandPlugin">Optional command plugin reference for background process management.</param>
    public StepExecutionAgent(Kernel kernel, string workingDirectory, FunctionCallTracker? tracker = null, DeveloperPersona persona = DeveloperPersona.Mid, Plugins.CommandPlugin? commandPlugin = null)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _workingDirectory = workingDirectory;
        _tracker = tracker;
        _persona = persona;
        _commandPlugin = commandPlugin;
        _seniorBuildGateInterval = ResolveBuildGateInterval();
    }

    /// <summary>
    /// The developer persona this agent is simulating.
    /// </summary>
    public DeveloperPersona Persona => _persona;

    /// <summary>
    /// The command plugin instance for direct background process management.
    /// </summary>
    internal Plugins.CommandPlugin? CommandPlugin => _commandPlugin;

    /// <summary>
    /// Executes a single tutorial step using the AI agent.
    /// For Senior persona, includes a post-step build gate for code-modifying steps
    /// and retries up to 3 times with error context if the step fails.
    /// </summary>
    /// <param name="step">The step to execute.</param>
    /// <returns>The result of executing the step.</returns>
    public async Task<StepResult> ExecuteStepAsync(TutorialStep step)
    {
        StepResult? result = null;
        string? retryContext = null;
        var maxAttempts = _persona == DeveloperPersona.Senior ? SeniorMaxRetries + 1 : 1;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                Console.WriteLine($"  [AGENT] Senior retry {attempt}/{SeniorMaxRetries} — attempting to fix the issue");
            }

            // Execute the step (with retry context if this is a retry)
            result = await ExecuteStepCoreAsync(step, retryContext);

            // If execution succeeded, check if we need to run the build gate
            if (result.Status == StepExecutionStatus.Success && _persona == DeveloperPersona.Senior)
            {
                // Run build gate only at configured intervals to reduce runtime/token cost.
                if (IsCodeModifyingStep(step) && ShouldRunBuildGate(step.StepId))
                {
                    var buildResult = await VerifyBuildAsync();
                    
                    if (buildResult.Failed)
                    {
                        Console.WriteLine($"  [BUILD GATE] Build failed after step {step.StepId}");
                        
                        // Override the step result to failed with build error context
                        result.Status = StepExecutionStatus.Failed;
                        result.ErrorMessage = "Build failed after step completion";
                        retryContext = BuildRetryContext(buildResult.Output, isBuildFailure: true);
                        
                        // Continue to retry if we have attempts left
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"  [BUILD GATE] Build passed after step {step.StepId}");
                    }
                }
            }

            // If step succeeded, we're done
            if (result.Status == StepExecutionStatus.Success)
            {
                return result;
            }

            // Step failed - prepare retry context for next attempt
            if (attempt < maxAttempts - 1)
            {
                retryContext = BuildRetryContext(result.ErrorMessage, result.Output, isBuildFailure: false);
            }
        }

        // All attempts exhausted - return the last result
        return result!;
    }

    /// <summary>
    /// Core step execution logic shared by initial attempt and retries.
    /// </summary>
    /// <param name="step">The step to execute.</param>
    /// <param name="retryContext">Optional retry context from a previous failed attempt.</param>
    private async Task<StepResult> ExecuteStepCoreAsync(TutorialStep step, string? retryContext = null)
    {
        var result = new StepResult
        {
            StepId = step.StepId,
            StepType = StepTypeMapper.GetStepType(step),
            StartedAt = DateTime.UtcNow,
            Status = StepExecutionStatus.Running
        };

        try
        {
            // Reset the tracker so we only capture calls for this step
            _tracker?.Reset();

            // Build the user prompt based on step type
            var userPrompt = BuildUserPrompt(step);
            
            // Append retry context if this is a retry
            if (retryContext != null)
            {
                userPrompt += retryContext;
            }

            Console.WriteLine($"  [AGENT] Processing step {step.StepId}...");

            // Create chat history with persona-specific system prompt
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(ExecutorPrompts.GetSystemPrompt(_persona));
            chatHistory.AddUserMessage(userPrompt);

            // Configure auto function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            // Execute with function calling
            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: _kernel);

            var agentResponse = response.Content ?? "";

            // Parse the response to determine success/failure.
            // Uses tracker for deterministic evaluation when available,
            // falls back to improved text analysis otherwise.
            var (status, details, errorMessage) = AgentResponseParser.ParseResponse(agentResponse, step, _tracker, _persona);

            result.Status = status;
            result.Details = details;
            result.Output = agentResponse;
            result.ErrorMessage = errorMessage;
            result.CompletedAt = DateTime.UtcNow;

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [AGENT] Error: {ex.Message}");

            result.Status = StepExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
            result.ErrorOutput = ex.ToString();
            result.CompletedAt = DateTime.UtcNow;

            return result;
        }
    }

    /// <summary>
    /// Determines if a step modifies code.
    /// </summary>
    private static bool IsCodeModifyingStep(TutorialStep step)
    {
        return step is FileOperationStep or CodeChangeStep;
    }

    private static int ResolveBuildGateInterval()
    {
        var value = Environment.GetEnvironmentVariable("EXECUTOR_BUILD_GATE_INTERVAL");
        return int.TryParse(value, out var interval) && interval >= 0 ? interval : 0;
    }

    private bool ShouldRunBuildGate(int stepId)
    {
        return _seniorBuildGateInterval > 0 && stepId > 1 && stepId % _seniorBuildGateInterval == 0;
    }

    /// <summary>
    /// Builds retry context message based on the failure type.
    /// </summary>
    private static string BuildRetryContext(string? errorMessage = null, string? output = null, bool isBuildFailure = false)
    {
        if (isBuildFailure)
        {
            return $"""
                
                PREVIOUS ATTEMPT: The step actions completed but the project BUILD FAILED afterwards.
                Build errors:
                {output ?? errorMessage ?? "(no output)"}
                
                Fix the build errors using your C#/.NET/ABP expertise. Focus on:
                - Do NOT create stub/placeholder files for types from future tutorial steps.
                - Do NOT re-run migration commands that already created migration files.
                - Apply minimal fixes: prefer adding using statements, fixing namespaces, etc.
                Then verify the build passes before reporting success.
                """;
        }
        else
        {
            return $"""
                
                PREVIOUS ATTEMPT FAILED. Here is what happened:
                - Error: {errorMessage ?? "(no error message)"}
                - Output: {output ?? "(no output)"}
                
                Analyze the error above and fix the issue using your C#/.NET/ABP expertise.
                Then re-execute the step. Your goal is to make this step succeed.
                """;
        }
    }

    /// <summary>
    /// Runs `dotnet build` in the project directory to verify the project compiles.
    /// Returns a result indicating success or failure with build output.
    /// </summary>
    private async Task<BuildVerificationResult> VerifyBuildAsync()
    {
        try
        {
            // Find the project root directory
            var projectRoot = ProjectDirectoryResolver.FindProjectRoot(_workingDirectory);
            
            if (projectRoot == null)
            {
                // Project not yet created or can't be found - skip build verification
                return new BuildVerificationResult { Failed = false, Output = "Project root not found - skipping build verification" };
            }

            // Run dotnet build via CommandPlugin
            if (_commandPlugin == null)
            {
                return new BuildVerificationResult { Failed = false, Output = "CommandPlugin not available - skipping build verification" };
            }

            Console.WriteLine($"  [BUILD GATE] Running dotnet build in {projectRoot}");
            var buildOutput = await _commandPlugin.ExecuteCommandAsync("dotnet build", projectRoot);

            // Parse exit code from output
            var exitCode = ParseExitCode(buildOutput);

            return new BuildVerificationResult
            {
                Failed = exitCode != 0,
                Output = buildOutput
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [BUILD GATE] Error during build verification: {ex.Message}");
            return new BuildVerificationResult
            {
                Failed = true,
                Output = $"Build verification error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses the exit code from CommandPlugin output.
    /// </summary>
    private static int ParseExitCode(string output)
    {
        // CommandPlugin returns: "Exit Code: {N}\n=== STDOUT ===\n..."
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("Exit Code:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out var exitCode))
                {
                    return exitCode;
                }
            }
        }
        return -1; // Unknown exit code
    }

    /// <summary>
    /// Builds the user prompt based on the step type and active persona.
    /// </summary>
    private string BuildUserPrompt(TutorialStep step)
    {
        return step switch
        {
            CommandStep cmd => ExecutorPrompts.ForCommandStep(
                step.StepId,
                step.Description,
                cmd.Command,
                _persona,
                cmd.IsLongRunning,
                cmd.ReadinessPattern,
                cmd.ReadinessTimeoutSeconds),

            FileOperationStep fileOp => ExecutorPrompts.ForFileOperationStep(
                step.StepId,
                step.Description,
                fileOp.Operation.ToString(),
                fileOp.Path,
                fileOp.EntityType,
                fileOp.Content,
                _persona),

            CodeChangeStep codeChange => ExecutorPrompts.ForCodeChangeStep(
                step.StepId,
                step.Description,
                codeChange.Scope,
                codeChange.Modifications?.Select(m => (
                    m.FilePath,
                    m.FullContent,
                    m.SearchPattern,
                    m.ReplaceWith
                )) ?? [],
                _persona),

            ExpectationStep expectation => ExecutorPrompts.ForExpectationStep(
                step.StepId,
                step.Description,
                expectation.Assertions.Select(a => (
                    ExecutorPrompts.GetAssertionKind(a),
                    ExecutorPrompts.GetAssertionDetails(a)
                )),
                _persona),

            _ => $"Execute Step {step.StepId}: {step.Description ?? "(unknown step type)"}"
        };
    }

}

/// <summary>
/// Result of build verification.
/// </summary>
internal class BuildVerificationResult
{
    /// <summary>
    /// Whether the build failed (exit code != 0).
    /// </summary>
    public required bool Failed { get; init; }

    /// <summary>
    /// The build output (stdout/stderr).
    /// </summary>
    public required string Output { get; init; }
}

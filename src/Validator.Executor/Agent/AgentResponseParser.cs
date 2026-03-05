using System.Text.RegularExpressions;
using Validator.Core.Models;
using Validator.Core.Models.Assertions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Steps;

namespace Validator.Executor.Agent;

/// <summary>
/// Parses AI agent responses to determine step execution success or failure.
/// 
/// Uses a hybrid approach:
///   1. PRIMARY: Check actual function call results captured by FunctionCallTracker
///      (exit codes, file operation outcomes) for deterministic evaluation.
///   2. FALLBACK: Improved text-based analysis of the AI's response with markdown
///      stripping and code-block awareness to avoid false positives.
/// 
/// For <see cref="DeveloperPersona.Senior"/>, evaluation uses "last command wins" logic:
/// earlier failed commands that were subsequently retried and succeeded are not treated
/// as failures, since the senior persona is expected to self-fix.
/// </summary>
internal static partial class AgentResponseParser
{
    /// <summary>
    /// Parses the agent's response to determine execution status.
    /// When a tracker is provided and has data, uses deterministic evaluation
    /// based on actual tool call results. Falls back to text analysis otherwise.
    /// </summary>
    internal static (StepExecutionStatus status, string details, string? errorMessage) ParseResponse(
        string response,
        TutorialStep step,
        FunctionCallTracker? tracker = null,
        DeveloperPersona persona = DeveloperPersona.Mid)
    {
        // PRIMARY PATH: Use actual function call results when available
        if (tracker is { HasTrackedCalls: true })
        {
            var trackerResult = EvaluateFromTracker(step, tracker, persona);
            if (trackerResult.HasValue)
            {
                return trackerResult.Value;
            }
        }

        // FALLBACK PATH: Improved text-based analysis
        return EvaluateFromText(response, step);
    }

    /// <summary>
    /// Evaluates step outcome from actual function call results captured by the tracker.
    /// Returns null if the tracker data is insufficient to determine outcome.
    /// </summary>
    private static (StepExecutionStatus status, string details, string? errorMessage)? EvaluateFromTracker(
        TutorialStep step,
        FunctionCallTracker tracker,
        DeveloperPersona persona)
    {
        return step switch
        {
            CommandStep => EvaluateCommandStep(step, tracker, persona),
            FileOperationStep => EvaluateFileOperationStep(step, tracker, persona),
            CodeChangeStep => EvaluateCodeChangeStep(step, tracker, persona),
            ExpectationStep => EvaluateExpectationStep(step, tracker, persona),
            _ => null
        };
    }

    private static (StepExecutionStatus status, string details, string? errorMessage)? EvaluateCommandStep(
        TutorialStep step, FunctionCallTracker tracker, DeveloperPersona persona)
    {
        var commandCalls = tracker.CommandCalls.ToList();
        if (commandCalls.Count == 0)
            return null;

        // Senior persona: use "last command wins" logic.
        // The senior may run a command, see it fail, fix the issue, and re-run.
        // We care about the final outcome, not intermediate failures.
        if (persona == DeveloperPersona.Senior)
        {
            var lastCommand = commandCalls[^1];
            if (lastCommand.ExitCode == 0)
            {
                return (
                    StepExecutionStatus.Success,
                    $"Step {step.StepId} completed successfully (exit code 0, senior self-fixed)",
                    null
                );
            }

            // Last command still failed — report the final failure
            var errorSnippet = ExtractStderrFromCommandResult(lastCommand.RawResult);
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} failed (exit code {lastCommand.ExitCode})",
                errorSnippet ?? $"Command failed with exit code {lastCommand.ExitCode}. See output for details."
            );
        }

        // Junior and Mid: all commands must succeed
        if (tracker.AllCommandsSucceeded)
        {
            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} completed successfully (exit code 0)",
                null
            );
        }

        // Find the first failed command for diagnostics
        var failedCall = commandCalls.FirstOrDefault(c => c.ExitCode is not 0);
        var exitCode = failedCall?.ExitCode ?? -1;
        var stderr = failedCall != null ? ExtractStderrFromCommandResult(failedCall.RawResult) : null;

        return (
            StepExecutionStatus.Failed,
            $"Step {step.StepId} failed (exit code {exitCode})",
            stderr ?? $"Command failed with exit code {exitCode}. See output for details."
        );
    }

    private static (StepExecutionStatus status, string details, string? errorMessage)? EvaluateFileOperationStep(
        TutorialStep step, FunctionCallTracker tracker, DeveloperPersona persona)
    {
        var fileOps = tracker.FileOperationCalls.ToList();
        if (fileOps.Count == 0)
            return null;

        // Senior persona: if there are mutating failures followed by successes
        // (the agent fixed the issue and re-wrote), use the final state.
        if (persona == DeveloperPersona.Senior)
        {
            var mutatingOps = tracker.MutatingFileOperationCalls.ToList();
            if (mutatingOps.Count > 0)
            {
                // Check the last mutating operation — if it succeeded, the senior fixed it
                var lastMutating = mutatingOps[^1];
                if (lastMutating.WasSuccessful)
                {
                    return (
                        StepExecutionStatus.Success,
                        $"Step {step.StepId} completed successfully",
                        null
                    );
                }

                return (
                    StepExecutionStatus.Failed,
                    $"Step {step.StepId} file operation failed",
                    lastMutating.RawResult
                );
            }

            return null;
        }

        // Junior and Mid: check mutating operations first.
        // Query operations (ListDirectory, ReadFile, Exists) may legitimately "fail"
        // during exploration (e.g., listing a directory that doesn't exist yet before
        // WriteFile creates it), so they should not determine step outcome.
        if (tracker.HasMutatingFileOperationFailure)
        {
            var failedOp = tracker.MutatingFileOperationCalls.FirstOrDefault(c => !c.WasSuccessful);
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} file operation failed",
                failedOp?.RawResult ?? "File operation failed. See output for details."
            );
        }

        if (tracker.AllMutatingFileOperationsSucceeded && tracker.MutatingFileOperationCalls.Any())
        {
            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} completed successfully",
                null
            );
        }

        // No mutating operations were tracked — fall through to text-based evaluation.
        return null;
    }

    private static (StepExecutionStatus status, string details, string? errorMessage)? EvaluateCodeChangeStep(
        TutorialStep step, FunctionCallTracker tracker, DeveloperPersona persona)
    {
        // Code changes use file read/write operations
        var fileOps = tracker.FileOperationCalls.ToList();
        if (fileOps.Count == 0)
            return null;

        // Senior persona: if the agent fixed issues and re-wrote files,
        // we care about the final write succeeding, plus an optional build check.
        if (persona == DeveloperPersona.Senior)
        {
            var mutatingOps = tracker.MutatingFileOperationCalls.ToList();
            var hasSuccessfulWrite = mutatingOps.Any(c =>
                c.WasSuccessful && c.RawResult.StartsWith("SUCCESS: File written", StringComparison.OrdinalIgnoreCase));

            if (!hasSuccessfulWrite)
            {
                return null; // Fall through to text analysis
            }

            // If the senior also ran a build command, check the last build result
            var commandCalls = tracker.CommandCalls.ToList();
            if (commandCalls.Count > 0)
            {
                var lastCommand = commandCalls[^1];
                if (lastCommand.ExitCode != 0)
                {
                    var errorSnippet = ExtractStderrFromCommandResult(lastCommand.RawResult);
                    return (
                        StepExecutionStatus.Failed,
                        $"Step {step.StepId} code change applied but build failed",
                        errorSnippet ?? $"Build failed with exit code {lastCommand.ExitCode}."
                    );
                }
            }

            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} completed successfully",
                null
            );
        }

        // Junior and Mid: check for mutating file operation failures.
        // Query operations (ReadFile, ListDirectory, Exists) may fail during exploration
        // without indicating a code change failure.
        if (tracker.HasMutatingFileOperationFailure)
        {
            var failedOp = tracker.MutatingFileOperationCalls.FirstOrDefault(c => !c.WasSuccessful);
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} code change failed",
                failedOp?.RawResult ?? "Code change failed. See output for details."
            );
        }

        // For code changes, mutating operations "succeeding" only means the write
        // calls didn't error. We also need to verify that at least one write actually
        // occurred — if the agent only read files (e.g. pattern wasn't found, so no
        // replacement was written back), we should fall through to text analysis.
        var hasWriteOperation = fileOps.Any(c =>
            c.RawResult.StartsWith("SUCCESS: File written", StringComparison.OrdinalIgnoreCase));

        if (hasWriteOperation)
        {
            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} completed successfully",
                null
            );
        }

        // No writes occurred — the code change likely wasn't applied.
        // Fall through to text-based analysis which can detect "pattern not found" etc.
        return null;
    }

    private static (StepExecutionStatus status, string details, string? errorMessage)? EvaluateExpectationStep(
        TutorialStep step, FunctionCallTracker tracker, DeveloperPersona persona)
    {
        if (step is not ExpectationStep expectationStep)
        {
            return null;
        }

        var deterministicResult = TryEvaluateExpectationAssertions(step.StepId, expectationStep, tracker);
        if (deterministicResult.HasValue)
        {
            return deterministicResult.Value;
        }

        // Senior persona: use "last result wins" for expectations too.
        // The senior may run a build, see it fail, fix, and re-build.
        if (persona == DeveloperPersona.Senior)
        {
            var commandCalls = tracker.CommandCalls.ToList();
            var httpCalls = tracker.HttpCalls.ToList();

            // Check the last command result (if any)
            if (commandCalls.Count > 0)
            {
                var lastCmd = commandCalls[^1];
                if (lastCmd.ExitCode is not 0)
                {
                    return (
                        StepExecutionStatus.Failed,
                        $"Step {step.StepId} expectation failed",
                        ExtractStderrFromCommandResult(lastCmd.RawResult) ?? $"Command failed with exit code {lastCmd.ExitCode}"
                    );
                }
            }

            // Check the last HTTP result (if any)
            if (httpCalls.Count > 0)
            {
                var lastHttp = httpCalls[^1];
                if (!lastHttp.WasSuccessful)
                {
                    return (
                        StepExecutionStatus.Failed,
                        $"Step {step.StepId} expectation failed",
                        lastHttp.RawResult
                    );
                }
            }

            // All final results passed
            if (tracker.HasTrackedCalls)
            {
                return (
                    StepExecutionStatus.Success,
                    $"Step {step.StepId} expectations met",
                    null
                );
            }

            return null;
        }

        // Junior and Mid: any failure means the step failed
        if (tracker.HasCommandFailure)
        {
            var failedCmd = tracker.CommandCalls.FirstOrDefault(c => c.ExitCode is not 0);
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} expectation failed",
                failedCmd != null
                    ? ExtractStderrFromCommandResult(failedCmd.RawResult) ?? $"Command failed with exit code {failedCmd.ExitCode}"
                    : "Expectation check failed. See output for details."
            );
        }

        if (!tracker.AllHttpCallsSucceeded)
        {
            var failedHttp = tracker.HttpCalls.FirstOrDefault(c => !c.WasSuccessful);
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} expectation failed",
                failedHttp?.RawResult ?? "HTTP check failed. See output for details."
            );
        }

        // If we have tracked calls and none failed, it's a success
        if (tracker.HasTrackedCalls)
        {
            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} expectations met",
                null
            );
        }

        return null;
    }

    /// <summary>
    /// Tries to evaluate expectation assertions deterministically using tracked tool-call data.
    /// Returns null when assertions cannot be mapped to tracked calls (fallback to legacy behavior).
    /// </summary>
    private static (StepExecutionStatus status, string details, string? errorMessage)? TryEvaluateExpectationAssertions(
        int stepId,
        ExpectationStep expectationStep,
        FunctionCallTracker tracker)
    {
        var failures = new List<string>();

        for (var i = 0; i < expectationStep.Assertions.Count; i++)
        {
            var assertion = expectationStep.Assertions[i];

            switch (assertion)
            {
                case BuildAssertion buildAssertion:
                {
                    if (!TryGetRelevantBuildCall(buildAssertion, tracker, out var buildCall))
                    {
                        return null;
                    }

                    var actualExitCode = buildCall!.ExitCode;
                    if (actualExitCode != buildAssertion.ExpectsExitCode)
                    {
                        failures.Add(
                            $"Assertion {i + 1} [build] expected exit code {buildAssertion.ExpectsExitCode} for '{buildAssertion.Command}', but got {actualExitCode}.");
                    }

                    break;
                }

                case HttpAssertion httpAssertion:
                {
                    if (!TryGetRelevantHttpCall(httpAssertion, tracker, out var httpCall))
                    {
                        return null;
                    }

                    var actualStatus = httpCall!.HttpStatusCode;
                    if (actualStatus != httpAssertion.ExpectsStatus)
                    {
                        failures.Add(
                            $"Assertion {i + 1} [http] expected status {httpAssertion.ExpectsStatus} for '{httpAssertion.Url}', but got {(actualStatus?.ToString() ?? "no status")}.");
                    }

                    if (!string.IsNullOrWhiteSpace(httpAssertion.ExpectsContent) &&
                        !ResponseContainsExpectedContent(httpCall.RawResult, httpAssertion.ExpectsContent))
                    {
                        failures.Add(
                            $"Assertion {i + 1} [http] expected response content containing '{httpAssertion.ExpectsContent}', but it was not found.");
                    }

                    break;
                }

                default:
                    return null;
            }
        }

        if (failures.Count > 0)
        {
            return (
                StepExecutionStatus.Failed,
                $"Step {stepId} expectation failed",
                string.Join(Environment.NewLine, failures)
            );
        }

        return (
            StepExecutionStatus.Success,
            $"Step {stepId} expectations met",
            null
        );
    }

    private static bool TryGetRelevantBuildCall(
        BuildAssertion assertion,
        FunctionCallTracker tracker,
        out TrackedFunctionCall? call)
    {
        var commandCalls = tracker.CommandCalls.ToList();
        if (commandCalls.Count == 0)
        {
            call = null;
            return false;
        }

        var normalizedExpected = NormalizeCommand(assertion.Command);
        call = commandCalls
            .Where(c => !string.IsNullOrWhiteSpace(c.CommandText))
            .LastOrDefault(c => NormalizeCommand(c.CommandText!).Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase))
            ?? commandCalls.Last();

        return true;
    }

    private static bool TryGetRelevantHttpCall(
        HttpAssertion assertion,
        FunctionCallTracker tracker,
        out TrackedFunctionCall? call)
    {
        var httpCalls = tracker.HttpCalls.ToList();
        if (httpCalls.Count == 0)
        {
            call = null;
            return false;
        }

        var normalizedExpectedUrl = NormalizeUrl(assertion.Url);
        call = httpCalls
            .Where(c => !string.IsNullOrWhiteSpace(c.Url))
            .LastOrDefault(c => string.Equals(NormalizeUrl(c.Url!), normalizedExpectedUrl, StringComparison.OrdinalIgnoreCase))
            ?? httpCalls.Last();

        return true;
    }

    private static bool ResponseContainsExpectedContent(string response, string expectedContent)
    {
        return response.Contains(expectedContent, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCommand(string command)
    {
        return command.Trim().Replace('\t', ' ');
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim().TrimEnd('/');
    }

    /// <summary>
    /// Extracts the STDERR section from a CommandPlugin formatted result.
    /// </summary>
    private static string? ExtractStderrFromCommandResult(string result)
    {
        const string stderrHeader = "=== STDERR ===";
        var idx = result.IndexOf(stderrHeader, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var stderr = result[(idx + stderrHeader.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(stderr)) return null;

        // Return first meaningful line
        var firstLine = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }

    // ──────────────────────────────────────────────────────────────
    // FALLBACK: Improved text-based evaluation
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates step outcome by analyzing the AI agent's text response.
    /// Strips markdown formatting and ignores content inside fenced code blocks
    /// to avoid false positives from quoted CLI output.
    /// </summary>
    private static (StepExecutionStatus status, string details, string? errorMessage) EvaluateFromText(
        string response,
        TutorialStep step)
    {
        // Strip fenced code blocks (content between ``` markers) to avoid
        // matching CLI output like "extension not found in NuGet cache"
        var cleanedResponse = StripFencedCodeBlocks(response);

        // Strip markdown formatting so "**Exit code:** `0`" becomes "Exit code: 0"
        cleanedResponse = StripMarkdownFormatting(cleanedResponse);

        // Normalize whitespace
        cleanedResponse = NormalizeWhitespace(cleanedResponse);

        var responseLower = cleanedResponse.ToLowerInvariant();

        // Check for negated error phrases (these indicate success)
        var negatedErrorPatterns = new[]
        {
            "without any errors",
            "without errors",
            "no errors",
            "0 errors",
            "zero errors"
        };

        var hasNegatedError = negatedErrorPatterns.Any(p => responseLower.Contains(p));

        // Check for clear failure indicators
        var failureIndicators = new[]
        {
            "failed", "failure", "error:", "error!",
            "does not exist",
            "exit code: 1", "exit code: 2", "exit code: -1",
            "cannot find", "could not", "unable to", "exception",
            "http error", "not reachable", "timed out",
            "result: failed", "not found:", "cannot apply", "blocked from"
        };

        var successIndicators = new[]
        {
            "success", "succeeded", "completed successfully", "exit code: 0",
            "file written", "directory created", "passed", "reachable"
        };

        var hasFailure = failureIndicators.Any(f => responseLower.Contains(f));
        var hasSuccess = successIndicators.Any(s => responseLower.Contains(s));

        // If we have negated error phrases, treat as success
        if (hasNegatedError)
        {
            hasFailure = false;
            hasSuccess = true;
        }

        if (hasFailure && !hasSuccess)
        {
            return (
                StepExecutionStatus.Failed,
                $"Step {step.StepId} failed",
                ExtractErrorMessage(response)
            );
        }

        if (hasSuccess && !hasFailure)
        {
            return (
                StepExecutionStatus.Success,
                $"Step {step.StepId} completed successfully",
                null
            );
        }

        // Ambiguous: both indicators present
        if (hasFailure && hasSuccess)
        {
            var successIdx = responseLower.LastIndexOf("success");
            var errorIdx = responseLower.LastIndexOf("error");

            if (errorIdx > successIdx)
            {
                return (
                    StepExecutionStatus.Failed,
                    $"Step {step.StepId} encountered errors",
                    ExtractErrorMessage(response)
                );
            }
        }

        // Default to failed — a step with no clear indicators should not silently pass.
        // This ensures we don't mask real failures when the AI's response is ambiguous.
        return (
            StepExecutionStatus.Failed,
            $"Step {step.StepId} outcome uncertain",
            "Could not determine step outcome from agent response. No clear success or failure indicators found."
        );
    }

    /// <summary>
    /// Removes fenced code blocks (``` ... ```) from the response text.
    /// This prevents CLI output quoted by the AI from triggering false-positive
    /// failure indicators like "not found" from NuGet diagnostic messages.
    /// </summary>
    private static string StripFencedCodeBlocks(string text)
    {
        return FencedCodeBlockPattern().Replace(text, " ");
    }

    /// <summary>
    /// Strips markdown formatting so patterns like "**Exit code:** `0`"
    /// become "Exit code: 0" and can match success indicators.
    /// </summary>
    private static string StripMarkdownFormatting(string text)
    {
        // Remove bold markers
        text = text.Replace("**", "");
        // Remove inline code backticks
        text = text.Replace("`", "");
        // Remove heading markers
        text = HeadingPattern().Replace(text, "");
        return text;
    }

    /// <summary>
    /// Normalizes whitespace: collapses multiple spaces/tabs into single space.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        return MultipleWhitespacePattern().Replace(text, " ");
    }

    /// <summary>
    /// Attempts to extract a meaningful error message from the response.
    /// </summary>
    private static string ExtractErrorMessage(string response)
    {
        var lines = response.Split('\n');

        foreach (var line in lines)
        {
            var lineLower = line.ToLowerInvariant();
            if (lineLower.Contains("error:") || lineLower.Contains("error!") ||
                lineLower.Contains("failed:") || lineLower.Contains("exception:"))
            {
                return line.Trim();
            }
        }

        foreach (var line in lines)
        {
            if (line.ToLowerInvariant().Contains("error") ||
                line.ToLowerInvariant().Contains("fail"))
            {
                return line.Trim();
            }
        }

        return "Step execution failed. See output for details.";
    }

    /// <summary>
    /// Matches fenced code blocks: ``` optionally followed by a language tag, then content, then ```.
    /// Uses singleline mode so . matches newlines.
    /// </summary>
    [GeneratedRegex(@"```[\w]*\s*.*?```", RegexOptions.Singleline)]
    private static partial Regex FencedCodeBlockPattern();

    /// <summary>
    /// Matches markdown heading markers (e.g., "### ").
    /// </summary>
    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// Matches multiple consecutive whitespace characters.
    /// </summary>
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleWhitespacePattern();
}

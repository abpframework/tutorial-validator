using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace Validator.Executor.Agent;

/// <summary>
/// Tracks actual function call results from the AI agent's tool invocations.
/// Implements IAutoFunctionInvocationFilter to intercept every function call
/// and capture structured results (exit codes, success/failure) for deterministic
/// step outcome evaluation -- eliminating reliance on parsing the AI's natural language response.
/// </summary>
public sealed partial class FunctionCallTracker : IAutoFunctionInvocationFilter
{
    private readonly List<TrackedFunctionCall> _calls = [];

    /// <summary>
    /// All tracked function calls for the current step.
    /// </summary>
    public IReadOnlyList<TrackedFunctionCall> Calls => _calls;

    /// <summary>
    /// All command execution calls tracked.
    /// Uses plugin name instead of function name because Semantic Kernel may strip
    /// the "Async" suffix from method names, making function name matching unreliable.
    /// </summary>
    public IEnumerable<TrackedFunctionCall> CommandCalls =>
        _calls.Where(c => c.PluginName == "Command");

    /// <summary>
    /// All file operation calls tracked (plugin name "FileOps").
    /// </summary>
    public IEnumerable<TrackedFunctionCall> FileOperationCalls =>
        _calls.Where(c => c.PluginName == "FileOps");

    /// <summary>
    /// All HTTP calls tracked (plugin name "Http").
    /// </summary>
    public IEnumerable<TrackedFunctionCall> HttpCalls =>
        _calls.Where(c => c.PluginName == "Http");

    /// <summary>
    /// Whether all executed commands returned exit code 0.
    /// Returns true if there are no command calls (vacuously true).
    /// </summary>
    public bool AllCommandsSucceeded =>
        !CommandCalls.Any() || CommandCalls.All(c => c.ExitCode == 0);

    /// <summary>
    /// Whether any command returned a non-zero exit code.
    /// </summary>
    public bool HasCommandFailure =>
        CommandCalls.Any(c => c.ExitCode is not null and not 0);

    /// <summary>
    /// Whether all file operations succeeded.
    /// Returns true if there are no file operation calls (vacuously true).
    /// </summary>
    public bool AllFileOperationsSucceeded =>
        !FileOperationCalls.Any() || FileOperationCalls.All(c => c.WasSuccessful);

    /// <summary>
    /// Whether any file operation failed.
    /// </summary>
    public bool HasFileOperationFailure =>
        FileOperationCalls.Any(c => !c.WasSuccessful);

    /// <summary>
    /// Mutating file operation calls: WriteFile, CreateDirectory, Delete.
    /// These are the operations that actually change the filesystem and should
    /// determine step success/failure. Semantic Kernel may strip the "Async"
    /// suffix from method names, so we match on prefix.
    /// </summary>
    public IEnumerable<TrackedFunctionCall> MutatingFileOperationCalls =>
        FileOperationCalls.Where(c => IsMutatingFileOperation(c.FunctionName));

    /// <summary>
    /// Query (read-only) file operation calls: ReadFile, ListDirectory, Exists.
    /// These are exploratory operations whose failure should not prevent a step
    /// from succeeding when the actual mutating operations complete successfully.
    /// </summary>
    public IEnumerable<TrackedFunctionCall> QueryFileOperationCalls =>
        FileOperationCalls.Where(c => !IsMutatingFileOperation(c.FunctionName));

    /// <summary>
    /// Whether all mutating file operations succeeded.
    /// Returns true if there are no mutating calls (vacuously true).
    /// Query operations (ListDirectory, ReadFile, Exists) are excluded because
    /// they may legitimately "fail" during exploration (e.g., listing a directory
    /// that will be created by a subsequent WriteFile call).
    /// </summary>
    public bool AllMutatingFileOperationsSucceeded =>
        !MutatingFileOperationCalls.Any() || MutatingFileOperationCalls.All(c => c.WasSuccessful);

    /// <summary>
    /// Whether any mutating file operation failed.
    /// </summary>
    public bool HasMutatingFileOperationFailure =>
        MutatingFileOperationCalls.Any(c => !c.WasSuccessful);

    /// <summary>
    /// Whether all HTTP calls succeeded.
    /// </summary>
    public bool AllHttpCallsSucceeded =>
        !HttpCalls.Any() || HttpCalls.All(c => c.WasSuccessful);

    /// <summary>
    /// Whether we have any tracked calls at all.
    /// </summary>
    public bool HasTrackedCalls => _calls.Count > 0;

    /// <summary>
    /// Clears all tracked calls. Must be called between steps.
    /// </summary>
    public void Reset()
    {
        _calls.Clear();
    }

    /// <summary>
    /// Adds a tracked call directly. Intended for unit tests.
    /// </summary>
    internal void AddTrackedCall(TrackedFunctionCall call)
    {
        if (call.PluginName == "Http" && call.HttpStatusCode is null)
        {
            call.HttpStatusCode = ParseHttpStatusCode(call.RawResult);
        }

        _calls.Add(call);
    }

    /// <summary>
    /// Intercepts function invocations to capture results after execution.
    /// </summary>
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        // Let the function execute first
        await next(context);

        // Capture the result
        var result = context.Result;
        var resultString = result?.ToString() ?? "";
        var pluginName = context.Function.PluginName ?? "";
        var functionName = context.Function.Name;

        var tracked = new TrackedFunctionCall
        {
            PluginName = pluginName,
            FunctionName = functionName,
            RawResult = resultString,
            CommandText = TryGetArgument(context, "command"),
            Url = TryGetArgument(context, "url")
        };

        // Parse command results for exit codes (match by plugin name to avoid
        // sensitivity to SK's function naming conventions)
        if (pluginName == "Command")
        {
            tracked.ExitCode = ParseExitCode(resultString);
            tracked.WasSuccessful = tracked.ExitCode == 0;
        }
        // Parse file operation results
        else if (pluginName == "FileOps")
        {
            tracked.WasSuccessful = IsFileOperationSuccess(resultString);
        }
        // HTTP plugin results — match actual prefixes returned by HttpPlugin:
        //   "HTTP ERROR: ..."  from GetAsync/PostAsync/RequestAsync on failure
        //   "NOT REACHABLE: ..." from IsReachableAsync on connection failure
        else if (pluginName == "Http")
        {
            tracked.HttpStatusCode = ParseHttpStatusCode(resultString);
            tracked.WasSuccessful = !resultString.StartsWith("HTTP ERROR:", StringComparison.OrdinalIgnoreCase)
                                 && !resultString.StartsWith("NOT REACHABLE:", StringComparison.OrdinalIgnoreCase);
        }

        _calls.Add(tracked);
    }

    /// <summary>
    /// Parses the exit code from a CommandPlugin result string.
    /// The CommandPlugin.FormatResult produces: "Exit Code: {N}\n..."
    /// </summary>
    private static int? ParseExitCode(string result)
    {
        var match = ExitCodePattern().Match(result);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var exitCode))
        {
            return exitCode;
        }
        return null;
    }

    /// <summary>
    /// Parses HTTP status code from HttpPlugin output.
    /// Supports outputs like:
    /// - "Status: 404 NotFound"
    /// - "responded with status 404"
    /// </summary>
    private static int? ParseHttpStatusCode(string result)
    {
        var match = HttpStatusPattern().Match(result);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var statusCode))
        {
            return statusCode;
        }

        return null;
    }

    private static string? TryGetArgument(AutoFunctionInvocationContext context, string argumentName)
    {
        var arguments = context.Arguments;
        if (arguments != null && arguments.TryGetValue(argumentName, out var value))
        {
            return value?.ToString();
        }

        return null;
    }

    /// <summary>
    /// Determines if a file operation function is mutating (changes the filesystem).
    /// Semantic Kernel may strip the "Async" suffix, so we match on prefix.
    /// Mutating: WriteFile, CreateDirectory, Delete
    /// Query: ReadFile, ListDirectory, Exists (and anything else not explicitly mutating)
    /// </summary>
    private static bool IsMutatingFileOperation(string functionName)
    {
        return functionName.StartsWith("Write", StringComparison.OrdinalIgnoreCase)
            || functionName.StartsWith("Create", StringComparison.OrdinalIgnoreCase)
            || functionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a file operation result indicates success.
    /// Matches the known success prefixes from FileOperationsPlugin methods.
    /// 
    /// Note: "NOT FOUND:" is returned by ExistsAsync and ListDirectoryAsync
    /// when a path doesn't exist. This is a valid informational result
    /// (like File.Exists returning false), not an error.
    /// </summary>
    private static bool IsFileOperationSuccess(string result)
    {
        return result.StartsWith("SUCCESS:", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("EXISTS:", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("NOT FOUND:", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("Contents of", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("File content", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("Directory already exists", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"Exit Code:\s*(-?\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExitCodePattern();

    [GeneratedRegex(@"(?:Status:\s*|status\s+)(\d{3})", RegexOptions.IgnoreCase)]
    private static partial Regex HttpStatusPattern();
}

/// <summary>
/// Represents a single tracked function call with its parsed result.
/// </summary>
public class TrackedFunctionCall
{
    /// <summary>
    /// The plugin that owns the function (e.g., "Command", "FileOps", "Http").
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// The function name (e.g., "ExecuteCommandAsync", "WriteFileAsync").
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// The raw result string returned by the function.
    /// </summary>
    public required string RawResult { get; init; }

    /// <summary>
    /// Command text argument for command plugin calls.
    /// </summary>
    public string? CommandText { get; init; }

    /// <summary>
    /// URL argument for HTTP plugin calls.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// The exit code for command executions. Null for non-command functions.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Parsed HTTP status code for HTTP plugin calls.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    /// Whether the function call was successful.
    /// </summary>
    public bool WasSuccessful { get; set; }
}

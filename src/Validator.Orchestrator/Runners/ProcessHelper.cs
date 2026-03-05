using System.Diagnostics;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Helper class for running external processes.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Run an external process and capture output.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        bool suppressConsoleEcho = false)
    {
        var result = new ProcessResult();
        var stopwatch = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add environment variables
        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        using var outputWaitHandle = new AutoResetEvent(false);
        using var errorWaitHandle = new AutoResetEvent(false);

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data == null)
            {
                outputWaitHandle.Set();
            }
            else
            {
                outputBuilder.AppendLine(e.Data);
                if (!suppressConsoleEcho)
                {
                    Console.WriteLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null)
            {
                errorWaitHandle.Set();
            }
            else
            {
                errorBuilder.AppendLine(e.Data);
                if (!suppressConsoleEcho)
                {
                    Console.Error.WriteLine(e.Data);
                }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() =>
            {
                return process.WaitForExit((int)timeout.TotalMilliseconds) &&
                       outputWaitHandle.WaitOne((int)TimeSpan.FromSeconds(30).TotalMilliseconds) &&
                       errorWaitHandle.WaitOne((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            });

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore kill errors
                }

                result.ExitCode = -1;
                var stderr = errorBuilder.ToString();
                result.Error = string.IsNullOrWhiteSpace(stderr)
                    ? $"Process timed out after {timeout.TotalMinutes} minutes"
                    : $"Process timed out after {timeout.TotalMinutes} minutes. Stderr: {stderr}";
            }
            else
            {
                result.ExitCode = process.ExitCode;
                result.Error = errorBuilder.ToString();
            }

            result.Output = outputBuilder.ToString();
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        return result;
    }
}

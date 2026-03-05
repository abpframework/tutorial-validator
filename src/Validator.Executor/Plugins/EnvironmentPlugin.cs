using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.SemanticKernel;

namespace Validator.Executor.Plugins;

/// <summary>
/// Plugin for getting environment information.
/// Used by the AI agent to understand the execution environment.
/// </summary>
public class EnvironmentPlugin
{
    /// <summary>
    /// Gets comprehensive environment information.
    /// </summary>
    [KernelFunction]
    [Description("Get information about the current environment including OS, .NET version, and installed tools.")]
    public async Task<string> GetEnvironmentInfoAsync()
    {
        Console.WriteLine("    [ENV] Collecting environment information...");

        var sb = new StringBuilder();
        sb.AppendLine("=== Environment Information ===");
        sb.AppendLine();

        // OS Information
        sb.AppendLine("Operating System:");
        sb.AppendLine($"  Description: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"  Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"  Platform: {Environment.OSVersion.Platform}");
        sb.AppendLine();

        // .NET Information
        sb.AppendLine(".NET Runtime:");
        sb.AppendLine($"  Version: {Environment.Version}");
        sb.AppendLine($"  Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine();

        // Tool versions (async)
        sb.AppendLine("Installed Tools:");
        
        var dotnetVersion = await GetToolVersionAsync("dotnet", "--version");
        sb.AppendLine($"  dotnet: {dotnetVersion}");
        
        var nodeVersion = await GetToolVersionAsync("node", "--version");
        sb.AppendLine($"  node: {nodeVersion}");
        
        var npmVersion = await GetToolVersionAsync("npm", "--version");
        sb.AppendLine($"  npm: {npmVersion}");
        
        var abpVersion = await GetToolVersionAsync("abp", "--version");
        sb.AppendLine($"  abp-cli: {abpVersion}");

        sb.AppendLine();
        sb.AppendLine("Machine:");
        sb.AppendLine($"  Name: {Environment.MachineName}");
        sb.AppendLine($"  User: {Environment.UserName}");
        sb.AppendLine($"  Current Directory: {Environment.CurrentDirectory}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets the version of a specific tool.
    /// </summary>
    [KernelFunction]
    [Description("Get the version of a specific command-line tool.")]
    public async Task<string> GetToolVersionAsync(
        [Description("The tool name (e.g., 'dotnet', 'node', 'abp')")] string toolName,
        [Description("The version argument (usually '--version' or '-v')")] string versionArg = "--version")
    {
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var shellName = isWindows ? "cmd.exe" : "/bin/bash";
            var command = $"{toolName} {versionArg}";
            var shellArgs = isWindows ? $"/c {command}" : $"-c \"{command}\"";

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = shellName,
                Arguments = shellArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await Task.Run(() => process.WaitForExit(5000));

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim().Split('\n')[0].Trim();
            }
            
            if (!string.IsNullOrWhiteSpace(error))
            {
                return $"Error: {error.Trim().Split('\n')[0]}";
            }
            
            return "Not installed or not found";
        }
        catch
        {
            return "Not installed or not found";
        }
    }

    /// <summary>
    /// Gets the current working directory.
    /// </summary>
    [KernelFunction]
    [Description("Get the current working directory.")]
    public Task<string> GetCurrentDirectoryAsync()
    {
        return Task.FromResult(Environment.CurrentDirectory);
    }

    /// <summary>
    /// Gets the value of an environment variable.
    /// </summary>
    [KernelFunction]
    [Description("Get the value of an environment variable.")]
    public Task<string> GetEnvironmentVariableAsync(
        [Description("The name of the environment variable")] string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        
        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult($"Environment variable '{name}' is not set or is empty.");
        }
        
        return Task.FromResult($"{name}={value}");
    }
}

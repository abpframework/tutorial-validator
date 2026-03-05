using System.Text;
using Microsoft.Extensions.Configuration;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Manages Docker containers for the executor environment.
/// </summary>
public class DockerRunner
{
    private readonly IConfiguration _configuration;
    private readonly string _composeFilePath;
    private readonly StringBuilder _dockerLog = new();
    private string? _outputPath;

    /// <summary>
    /// Timeout for Docker operations. Defaults to configuration value or 60 minutes.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 60;

    public DockerRunner(IConfiguration configuration)
    {
        _configuration = configuration;
        _composeFilePath = FindComposeFile();
    }

    /// <summary>
    /// Start the Docker environment (SQL Server + build executor image).
    /// </summary>
    public async Task StartEnvironmentAsync(string outputPath)
    {
        _outputPath = Path.GetFullPath(outputPath);

        Console.WriteLine("  Building Docker images...");

        // Build the executor image with --pull to get latest base images
        // This avoids issues with cached RC/preview SDK layers
        var buildResult = await RunDockerComposeAsync("build", "--pull", "executor");
        AppendToDockerLog("docker compose build --pull executor", buildResult);
        if (!buildResult.Success)
        {
            throw new Exception($"Failed to build Docker images: {buildResult.Error}");
        }

        Console.WriteLine("  Starting SQL Server...");

        // Start only SQL Server first and wait for it to be healthy
        var sqlResult = await RunDockerComposeAsync("up", "-d", "sqlserver");
        AppendToDockerLog("docker compose up -d sqlserver", sqlResult);
        if (!sqlResult.Success)
        {
            throw new Exception($"Failed to start SQL Server: {sqlResult.Error}");
        }

        // Wait for SQL Server to be healthy
        await WaitForSqlServerAsync();

        Console.WriteLine("  Docker environment ready.");
    }

    /// <summary>
    /// Run the executor in Docker.
    /// </summary>
    public async Task<ProcessResult> RunExecutorAsync(string testPlanPath, string outputPath, string persona = "mid")
    {
        _outputPath = Path.GetFullPath(outputPath);

        // Ensure testplan is in the output directory
        var testPlanInOutput = Path.Combine(_outputPath, "testplan.json");
        if (!Path.GetFullPath(testPlanPath).Equals(testPlanInOutput, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(testPlanPath, testPlanInOutput, overwrite: true);
        }

        // Clean the workspace volume before running
        // This ensures "abp new" has an empty directory
        Console.WriteLine("  Cleaning workspace...");
        await CleanWorkspaceVolumeAsync();

        Console.WriteLine("  Running executor container...");

        // Run the executor container (don't suppress output - show step progress)
        var result = await RunDockerComposeAsync(
            suppressConsoleEcho: false,
            "run",
            "--rm",
            "executor",
            "run",
            "--input", "/output/testplan.json",
            "--workdir", "/workspace",
            "--output", "/output/results",
            "--persona", persona
        );
        AppendToDockerLog("docker compose run executor", result);

        // Save executor log
        var logPath = Path.Combine(_outputPath, "logs", "executor.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.WriteAllTextAsync(logPath, $"=== Executor Output ===\n{result.Output}\n\n=== Executor Errors ===\n{result.Error}");

        // Copy workspace to output if executor actually ran (not timeout/crash)
        if (result.ExitCode >= 0)
        {
            try
            {
                await CopyWorkspaceToOutputAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to copy workspace to output: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Stop and remove Docker containers.
    /// </summary>
    public async Task StopEnvironmentAsync()
    {
        Console.WriteLine("  Stopping containers...");

        var result = await RunDockerComposeAsync("down", "-v");
        AppendToDockerLog("docker compose down -v", result);
    }

    /// <summary>
    /// Cleans the workspace volume by removing and recreating it.
    /// This ensures "abp new" commands have a completely empty directory.
    /// </summary>
    private async Task CleanWorkspaceVolumeAsync()
    {
        try
        {
            // Remove the volume (this will fail if it's in use, which is fine)
            var removeResult = await ProcessHelper.RunAsync(
                "docker",
                "volume rm tutorial-validator-workspace",
                Directory.GetCurrentDirectory(),
                TimeSpan.FromSeconds(10),
                suppressConsoleEcho: true
            );
            AppendToDockerLog("docker volume rm tutorial-validator-workspace", removeResult);

            if (!removeResult.Success)
            {
                // Volume might be in use or doesn't exist - try to clean it instead
                var cleanResult = await ProcessHelper.RunAsync(
                    "docker",
                    "run --rm -v tutorial-validator-workspace:/workspace alpine sh -c \"rm -rf /workspace/* /workspace/.* 2>/dev/null; exit 0\"",
                    Directory.GetCurrentDirectory(),
                    TimeSpan.FromSeconds(60),
                    suppressConsoleEcho: true
                );
                AppendToDockerLog("docker run cleanup workspace contents", cleanResult);

                if (!cleanResult.Success)
                {
                    Console.WriteLine($"  Warning: Cleanup failed: {cleanResult.Error}");
                }
            }

            // Create the volume explicitly (will be created automatically if it doesn't exist)
            var createResult = await ProcessHelper.RunAsync(
                "docker",
                "volume create tutorial-validator-workspace",
                Directory.GetCurrentDirectory(),
                TimeSpan.FromSeconds(10),
                suppressConsoleEcho: true
            );
            AppendToDockerLog("docker volume create tutorial-validator-workspace", createResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Workspace cleanup error: {ex.Message}");
        }
    }

    /// <summary>
    /// Copies the workspace contents from the Docker volume to the host output directory.
    /// </summary>
    private async Task CopyWorkspaceToOutputAsync()
    {
        // Check if workspace has any content
        var checkResult = await ProcessHelper.RunAsync(
            "docker",
            "run --rm -v tutorial-validator-workspace:/workspace alpine sh -c \"ls -A /workspace\"",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(30),
            suppressConsoleEcho: true
        );
        AppendToDockerLog("docker run check workspace contents", checkResult);

        if (string.IsNullOrWhiteSpace(checkResult.Output))
        {
            return;
        }

        // Copy workspace contents to output/generated-project/
        var copyResult = await ProcessHelper.RunAsync(
            "docker",
            $"run --rm -v tutorial-validator-workspace:/workspace -v \"{_outputPath}\":/output alpine sh -c \"cp -r /workspace/. /output/generated-project/\"",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromMinutes(5),
            suppressConsoleEcho: true
        );
        AppendToDockerLog("docker run copy workspace to output", copyResult);

        if (copyResult.Success)
        {
            Console.WriteLine("  Generated project copied.");
        }
        else
        {
            Console.WriteLine($"  Warning: Failed to copy workspace contents: {copyResult.Error}");
        }
    }

    private Task<ProcessResult> RunDockerComposeAsync(params string[] args)
    {
        return RunDockerComposeAsync(suppressConsoleEcho: true, args);
    }

    private async Task<ProcessResult> RunDockerComposeAsync(bool suppressConsoleEcho, params string[] args)
    {
        var composeDir = Path.GetDirectoryName(_composeFilePath)!;
        var composeFile = Path.GetFileName(_composeFilePath);

        // Build environment variables
        var envVars = new Dictionary<string, string>
        {
            ["OUTPUT_PATH"] = _outputPath ?? "./output"
        };

        // Pass through AI configuration
        AddAiEnvironmentVariables(envVars);

        var arguments = $"compose -f \"{composeFile}\" {string.Join(" ", args)}";

        return await ProcessHelper.RunAsync(
            "docker",
            arguments,
            composeDir,
            TimeSpan.FromMinutes(TimeoutMinutes),
            envVars,
            suppressConsoleEcho: suppressConsoleEcho
        );
    }

    private void AddAiEnvironmentVariables(Dictionary<string, string> envVars)
    {
        // Azure OpenAI
        var azureEndpoint = _configuration.GetValue<string>("AI:Endpoint")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureKey = _configuration.GetValue<string>("AI:ApiKey")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = _configuration.GetValue<string>("AI:DeploymentName")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
            ?? "gpt-4o";

        if (!string.IsNullOrEmpty(azureEndpoint))
        {
            envVars["AZURE_OPENAI_ENDPOINT"] = azureEndpoint;
        }
        if (!string.IsNullOrEmpty(azureKey))
        {
            envVars["AZURE_OPENAI_API_KEY"] = azureKey;
        }
        envVars["AZURE_OPENAI_DEPLOYMENT"] = azureDeployment;

        // OpenAI
        var openAiKey = _configuration.GetValue<string>("AI:ApiKey")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openAiModel = _configuration.GetValue<string>("AI:Model")
            ?? _configuration.GetValue<string>("AI:DeploymentName")
            ?? Environment.GetEnvironmentVariable("OPENAI_MODEL")
            ?? "gpt-4o";

        if (!string.IsNullOrEmpty(openAiKey))
        {
            envVars["OPENAI_API_KEY"] = openAiKey;
            envVars["OPENAI_MODEL"] = openAiModel;
        }

        // Provider override
        var provider = _configuration.GetValue<string>("AI:Provider")
            ?? Environment.GetEnvironmentVariable("AI_PROVIDER");
        if (!string.IsNullOrEmpty(provider))
        {
            envVars["AI_PROVIDER"] = provider;
        }
    }

    private async Task WaitForSqlServerAsync()
    {
        var maxAttempts = 30;
        var delaySeconds = 5;

        for (int i = 0; i < maxAttempts; i++)
        {
            var result = await ProcessHelper.RunAsync(
                "docker",
                "exec tutorial-validator-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"YourStrong!Password123\" -C -Q \"SELECT 1\"",
                Directory.GetCurrentDirectory(),
                TimeSpan.FromSeconds(10),
                suppressConsoleEcho: true
            );
            AppendToDockerLog($"SQL Server health check (attempt {i + 1}/{maxAttempts})", result);

            if (result.Success)
            {
                Console.WriteLine("  SQL Server ready.");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        throw new Exception("SQL Server did not become ready in time");
    }

    private void AppendToDockerLog(string section, ProcessResult result)
    {
        _dockerLog.AppendLine($"--- {section} ---");
        _dockerLog.AppendLine($"Exit code: {result.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            _dockerLog.AppendLine("STDOUT:");
            _dockerLog.AppendLine(result.Output);
        }
        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            _dockerLog.AppendLine("STDERR:");
            _dockerLog.AppendLine(result.Error);
        }
        _dockerLog.AppendLine();
    }

    public async Task WriteDockerLogAsync(string outputPath)
    {
        if (_dockerLog.Length == 0) return;
        var logDir = Path.Combine(outputPath, "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "docker.log");
        await File.WriteAllTextAsync(logPath, _dockerLog.ToString());
    }

    private string FindComposeFile()
    {
        return ProjectPathResolver.FindComposeFile(_configuration);
    }
}


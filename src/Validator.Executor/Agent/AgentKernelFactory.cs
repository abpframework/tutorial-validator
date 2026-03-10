using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Validator.Executor.Plugins;

namespace Validator.Executor.Agent;

/// <summary>
/// Factory for creating Semantic Kernel instances configured for the Executor agent.
/// Includes all plugins needed for system interaction.
/// </summary>
public static class AgentKernelFactory
{
    private const string ProviderAzureOpenAI = "AzureOpenAI";
    private const string ProviderOpenAI = "OpenAI";
    private const string ProviderOpenAICompatible = "OpenAICompatible";

    /// <summary>
    /// Loads AI configuration from appsettings.json and environment variables.
    /// Environment variables take precedence over JSON config.
    /// </summary>
    /// <param name="configPath">Optional path to a config file.</param>
    /// <returns>The AI configuration.</returns>
    public static ExecutorAIConfiguration LoadConfiguration(string? configPath = null)
    {
        var config = new ExecutorAIConfiguration();

        // Try to load from JSON config file
        var configBuilder = new ConfigurationBuilder();

        // Add default config file locations
        var possiblePaths = new[]
        {
            configPath,
            "appsettings.json",
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(Environment.CurrentDirectory, "appsettings.json")
        };

        foreach (var path in possiblePaths.Where(p => !string.IsNullOrEmpty(p) && File.Exists(p)))
        {
            configBuilder.AddJsonFile(path!, optional: true);
            break;
        }

        // Add environment variables (override JSON)
        configBuilder.AddEnvironmentVariables();

        var configuration = configBuilder.Build();

        // Bind from "AI" section if present
        var aiSection = configuration.GetSection("AI");
        if (aiSection.Exists())
        {
            config.Provider = aiSection["Provider"] ?? config.Provider;
            config.Endpoint = aiSection["Endpoint"] ?? config.Endpoint;
            config.DeploymentName = aiSection["DeploymentName"] ?? config.DeploymentName;
            config.ApiKey = aiSection["ApiKey"] ?? config.ApiKey;
            config.ModelId = aiSection["ModelId"];
            if (string.IsNullOrEmpty(config.ModelId))
            {
                config.ModelId = aiSection["Model"];
            }
            config.BaseUrl = aiSection["BaseUrl"];
            config.Organization = aiSection["Organization"];
            config.Project = aiSection["Project"];
        }

        // Override with environment variables
        var explicitProvider = false;
        var aiProvider = Environment.GetEnvironmentVariable("AI_PROVIDER");
        if (!string.IsNullOrEmpty(aiProvider))
        {
            config.Provider = aiProvider;
            explicitProvider = true;
        }

        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");

        if (!string.IsNullOrEmpty(azureEndpoint) && !explicitProvider)
        {
            config.Provider = ProviderAzureOpenAI;
            config.Endpoint = azureEndpoint;
        }

        if (!string.IsNullOrEmpty(azureApiKey))
        {
            config.ApiKey = azureApiKey;
        }

        if (!string.IsNullOrEmpty(azureDeployment))
        {
            config.DeploymentName = azureDeployment;
        }

        // OpenAI-compatible environment variables
        var openAiCompatBaseUrl = Environment.GetEnvironmentVariable("OPENAI_COMPAT_BASE_URL");
        var openAiCompatApiKey = Environment.GetEnvironmentVariable("OPENAI_COMPAT_API_KEY");
        var openAiCompatModel = Environment.GetEnvironmentVariable("OPENAI_COMPAT_MODEL");
        var openAiCompatOrg = Environment.GetEnvironmentVariable("OPENAI_COMPAT_ORG");
        var openAiCompatProject = Environment.GetEnvironmentVariable("OPENAI_COMPAT_PROJECT");

        if (!string.IsNullOrEmpty(openAiCompatBaseUrl))
        {
            config.BaseUrl = openAiCompatBaseUrl;
        }

        if (!string.IsNullOrEmpty(openAiCompatOrg))
        {
            config.Organization = openAiCompatOrg;
        }

        if (!string.IsNullOrEmpty(openAiCompatProject))
        {
            config.Project = openAiCompatProject;
        }

        if (!string.IsNullOrEmpty(openAiCompatApiKey))
        {
            config.ApiKey = openAiCompatApiKey;
        }

        if (!string.IsNullOrEmpty(openAiCompatModel))
        {
            config.ModelId = openAiCompatModel;
        }

        if (!explicitProvider && !string.IsNullOrEmpty(openAiCompatBaseUrl) && !string.IsNullOrEmpty(openAiCompatApiKey))
        {
            config.Provider = ProviderOpenAICompatible;
        }

        // OpenAI environment variables
        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openaiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");

        if (!string.IsNullOrEmpty(openaiApiKey) && string.IsNullOrEmpty(azureApiKey))
        {
            if (!explicitProvider)
            {
                config.Provider = ProviderOpenAI;
            }
            config.ApiKey = openaiApiKey;
        }

        if (!string.IsNullOrEmpty(openaiModel))
        {
            config.DeploymentName = openaiModel;
        }

        if (config.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(config.ModelId))
        {
            config.ModelId = config.DeploymentName;
        }

        return config;
    }

    /// <summary>
    /// Validates that the configuration has all required fields.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid.</exception>
    public static void ValidateConfiguration(ExecutorAIConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException(
                "API key is required. Set AZURE_OPENAI_API_KEY, OPENAI_API_KEY, or OPENAI_COMPAT_API_KEY environment variable, " +
                "or configure in appsettings.json under AI:ApiKey.");
        }

        if (config.Provider.Equals(ProviderAzureOpenAI, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrEmpty(config.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is required. Set AZURE_OPENAI_ENDPOINT environment variable, " +
                "or configure in appsettings.json under AI:Endpoint.");
        }

        if (config.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                throw new InvalidOperationException(
                    "OpenAI-compatible base URL is required. Set OPENAI_COMPAT_BASE_URL environment variable, " +
                    "or configure in appsettings.json under AI:BaseUrl.");
            }

            if (string.IsNullOrWhiteSpace(config.ModelId) && string.IsNullOrWhiteSpace(config.DeploymentName))
            {
                throw new InvalidOperationException(
                    "OpenAI-compatible model is required. Set OPENAI_COMPAT_MODEL environment variable, " +
                    "or configure in appsettings.json under AI:ModelId or AI:DeploymentName.");
            }
        }
    }

    /// <summary>
    /// Creates a Semantic Kernel configured for the Executor agent with all plugins,
    /// and a FunctionCallTracker that captures actual tool call results for deterministic evaluation.
    /// </summary>
    /// <param name="config">The AI configuration.</param>
    /// <param name="workingDirectory">The root working directory for file and command operations.</param>
    /// <returns>A <see cref="KernelContext"/> containing the configured Kernel, tracker, and plugin references.</returns>
    public static KernelContext CreateExecutorKernel(ExecutorAIConfiguration config, string workingDirectory)
    {
        var builder = Kernel.CreateBuilder();

        // Add AI service based on provider
        if (config.Provider.Equals(ProviderAzureOpenAI, StringComparison.OrdinalIgnoreCase))
        {
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: config.DeploymentName,
                endpoint: config.Endpoint,
                apiKey: config.ApiKey,
                modelId: config.ModelId);
        }
        else if (config.Provider.Equals(ProviderOpenAI, StringComparison.OrdinalIgnoreCase))
        {
            builder.AddOpenAIChatCompletion(
                modelId: config.ModelId ?? config.DeploymentName,
                apiKey: config.ApiKey);
        }
        else if (config.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = new Uri(config.BaseUrl!, UriKind.Absolute);

            builder.AddOpenAIChatCompletion(
                modelId: config.ModelId ?? config.DeploymentName,
                apiKey: config.ApiKey,
                endpoint: endpoint);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported AI provider: {config.Provider}. Supported providers: {ProviderAzureOpenAI}, {ProviderOpenAI}, {ProviderOpenAICompatible}.");
        }

        // Build the kernel first
        var kernel = builder.Build();

        // Register plugins with working directory context
        var commandPlugin = new CommandPlugin(workingDirectory);
        kernel.Plugins.AddFromObject(commandPlugin, "Command");
        kernel.Plugins.AddFromObject(new FileOperationsPlugin(workingDirectory), "FileOps");
        kernel.Plugins.AddFromObject(new HttpPlugin(), "Http");
        kernel.Plugins.AddFromObject(new EnvironmentPlugin(), "Environment");

        // Register function call tracker as an auto-invocation filter.
        // This intercepts every tool call the AI makes, capturing exit codes
        // and success/failure status for deterministic step evaluation.
        var tracker = new FunctionCallTracker();
        kernel.AutoFunctionInvocationFilters.Add(tracker);

        return new KernelContext(kernel, tracker, commandPlugin);
    }
}

/// <summary>
/// Holds the configured Kernel and associated components created by <see cref="AgentKernelFactory"/>.
/// </summary>
/// <param name="Kernel">The Semantic Kernel with AI service and plugins registered.</param>
/// <param name="Tracker">The function call tracker for deterministic evaluation.</param>
/// <param name="CommandPlugin">The command plugin instance for background process control.</param>
public record KernelContext(
    Kernel Kernel,
    FunctionCallTracker Tracker,
    CommandPlugin CommandPlugin);

using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Factory for creating Semantic Kernel instances with configured AI services.
/// </summary>
public static class KernelFactory
{
    private const string ProviderAzureOpenAI = "AzureOpenAI";
    private const string ProviderOpenAI = "OpenAI";
    private const string ProviderOpenAICompatible = "OpenAICompatible";

    /// <summary>
    /// Creates a Kernel instance from configuration.
    /// </summary>
    public static Kernel CreateFromConfiguration(AIConfiguration config)
    {
        var builder = Kernel.CreateBuilder();

        if (config.Provider.Equals(ProviderAzureOpenAI, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.Endpoint))
                throw new InvalidOperationException("Azure OpenAI endpoint is required");
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("Azure OpenAI API key is required");

            builder.AddAzureOpenAIChatCompletion(
                deploymentName: config.DeploymentName,
                endpoint: config.Endpoint,
                apiKey: config.ApiKey,
                modelId: config.ModelId);
        }
        else if (config.Provider.Equals(ProviderOpenAI, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is required");

            builder.AddOpenAIChatCompletion(
                modelId: config.ModelId ?? config.DeploymentName,
                apiKey: config.ApiKey);
        }
        else if (config.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI-compatible API key is required");
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                throw new InvalidOperationException("OpenAI-compatible base URL is required");

            var endpoint = new Uri(config.BaseUrl, UriKind.Absolute);

            builder.AddOpenAIChatCompletion(
                modelId: config.ModelId ?? config.DeploymentName,
                apiKey: config.ApiKey,
                endpoint: endpoint);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown AI provider: {config.Provider}. Supported providers: {ProviderAzureOpenAI}, {ProviderOpenAI}, {ProviderOpenAICompatible}.");
        }

        return builder.Build();
    }

    /// <summary>
    /// Loads AI configuration from environment variables and optional config file.
    /// </summary>
    public static AIConfiguration LoadConfiguration(string? configPath = null)
    {
        var configBuilder = new ConfigurationBuilder();

        // Add optional JSON config file
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            configBuilder.AddJsonFile(configPath, optional: true);
        }
        else
        {
            // Try current directory first
            if (File.Exists("appsettings.json"))
            {
                configBuilder.AddJsonFile("appsettings.json", optional: true);
            }
            // Then try application base directory
            else
            {
                var baseDir = AppContext.BaseDirectory;
                var appSettingsPath = Path.Combine(baseDir, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    configBuilder.AddJsonFile(appSettingsPath, optional: true);
                }
                // Also try the source directory (for dotnet run scenarios)
                else
                {
                    var sourceDir = Path.GetDirectoryName(typeof(KernelFactory).Assembly.Location);
                    if (sourceDir != null)
                    {
                        appSettingsPath = Path.Combine(sourceDir, "..", "..", "..", "appsettings.json");
                        if (File.Exists(appSettingsPath))
                        {
                            configBuilder.AddJsonFile(Path.GetFullPath(appSettingsPath), optional: true);
                        }
                    }
                }
            }
        }

        // Environment variables take precedence
        configBuilder.AddEnvironmentVariables();

        var configuration = configBuilder.Build();
        var aiConfig = new AIConfiguration();

        // Try to bind from "AI" section first
        configuration.GetSection("AI").Bind(aiConfig);

        // Support existing AI:Model field by mapping it to ModelId when present.
        var configuredModel = configuration["AI:Model"];
        if (!string.IsNullOrEmpty(configuredModel) && string.IsNullOrEmpty(aiConfig.ModelId))
        {
            aiConfig.ModelId = configuredModel;
        }

        // Override with environment variables if present
        var explicitProvider = false;
        var provider = Environment.GetEnvironmentVariable("AI_PROVIDER");
        if (!string.IsNullOrEmpty(provider))
        {
            aiConfig.Provider = provider;
            explicitProvider = true;
        }

        // Azure OpenAI environment variables
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        if (!string.IsNullOrEmpty(azureEndpoint) && !explicitProvider)
        {
            aiConfig.Provider = ProviderAzureOpenAI;
            aiConfig.Endpoint = azureEndpoint;
        }

        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(azureApiKey))
            aiConfig.ApiKey = azureApiKey;

        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        if (!string.IsNullOrEmpty(azureDeployment))
            aiConfig.DeploymentName = azureDeployment;

        // OpenAI-compatible environment variables
        var openAiCompatBaseUrl = Environment.GetEnvironmentVariable("OPENAI_COMPAT_BASE_URL");
        var openAiCompatApiKey = Environment.GetEnvironmentVariable("OPENAI_COMPAT_API_KEY");
        var openAiCompatModel = Environment.GetEnvironmentVariable("OPENAI_COMPAT_MODEL");
        var openAiCompatOrg = Environment.GetEnvironmentVariable("OPENAI_COMPAT_ORG");
        var openAiCompatProject = Environment.GetEnvironmentVariable("OPENAI_COMPAT_PROJECT");

        if (!string.IsNullOrEmpty(openAiCompatBaseUrl))
            aiConfig.BaseUrl = openAiCompatBaseUrl;

        if (!string.IsNullOrEmpty(openAiCompatOrg))
            aiConfig.Organization = openAiCompatOrg;

        if (!string.IsNullOrEmpty(openAiCompatProject))
            aiConfig.Project = openAiCompatProject;

        if (!string.IsNullOrEmpty(openAiCompatApiKey))
            aiConfig.ApiKey = openAiCompatApiKey;

        if (!string.IsNullOrEmpty(openAiCompatModel))
            aiConfig.ModelId = openAiCompatModel;

        if (!explicitProvider && !string.IsNullOrEmpty(openAiCompatBaseUrl) && !string.IsNullOrEmpty(openAiCompatApiKey))
        {
            aiConfig.Provider = ProviderOpenAICompatible;
        }

        // OpenAI environment variables
        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openaiApiKey) && string.IsNullOrEmpty(aiConfig.ApiKey))
        {
            if (!explicitProvider)
            {
                aiConfig.Provider = ProviderOpenAI;
            }
            aiConfig.ApiKey = openaiApiKey;
        }

        var openaiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (!string.IsNullOrEmpty(openaiModel))
            aiConfig.ModelId = openaiModel;

        if (aiConfig.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(aiConfig.ModelId))
        {
            aiConfig.ModelId = aiConfig.DeploymentName;
        }

        return aiConfig;
    }

    /// <summary>
    /// Validates that the configuration has all required settings.
    /// </summary>
    public static void ValidateConfiguration(AIConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "AI API key is required. Set AZURE_OPENAI_API_KEY, OPENAI_API_KEY, or OPENAI_COMPAT_API_KEY environment variable, " +
                "or configure in appsettings.json under AI:ApiKey");
        }

        if (config.Provider.Equals(ProviderAzureOpenAI, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is required. Set AZURE_OPENAI_ENDPOINT environment variable, " +
                "or configure in appsettings.json under AI:Endpoint");
        }

        if (config.Provider.Equals(ProviderOpenAICompatible, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
            {
                throw new InvalidOperationException(
                    "OpenAI-compatible base URL is required. Set OPENAI_COMPAT_BASE_URL environment variable, " +
                    "or configure in appsettings.json under AI:BaseUrl");
            }

            if (string.IsNullOrWhiteSpace(config.ModelId) && string.IsNullOrWhiteSpace(config.DeploymentName))
            {
                throw new InvalidOperationException(
                    "OpenAI-compatible model is required. Set OPENAI_COMPAT_MODEL environment variable, " +
                    "or configure in appsettings.json under AI:ModelId or AI:DeploymentName");
            }
        }
    }
}

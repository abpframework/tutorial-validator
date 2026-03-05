using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Factory for creating Semantic Kernel instances with configured AI services.
/// </summary>
public static class KernelFactory
{
    /// <summary>
    /// Creates a Kernel instance from configuration.
    /// </summary>
    public static Kernel CreateFromConfiguration(AIConfiguration config)
    {
        var builder = Kernel.CreateBuilder();

        if (config.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
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
        else if (config.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new InvalidOperationException("OpenAI API key is required");

            builder.AddOpenAIChatCompletion(
                modelId: config.ModelId ?? config.DeploymentName,
                apiKey: config.ApiKey);
        }
        else
        {
            throw new InvalidOperationException($"Unknown AI provider: {config.Provider}");
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

        // Override with environment variables if present
        var provider = Environment.GetEnvironmentVariable("AI_PROVIDER");
        if (!string.IsNullOrEmpty(provider))
            aiConfig.Provider = provider;

        // Azure OpenAI environment variables
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        if (!string.IsNullOrEmpty(azureEndpoint))
        {
            aiConfig.Provider = "AzureOpenAI";
            aiConfig.Endpoint = azureEndpoint;
        }

        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(azureApiKey))
            aiConfig.ApiKey = azureApiKey;

        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
        if (!string.IsNullOrEmpty(azureDeployment))
            aiConfig.DeploymentName = azureDeployment;

        // OpenAI environment variables
        var openaiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openaiApiKey) && string.IsNullOrEmpty(aiConfig.ApiKey))
        {
            aiConfig.Provider = "OpenAI";
            aiConfig.ApiKey = openaiApiKey;
        }

        var openaiModel = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        if (!string.IsNullOrEmpty(openaiModel))
            aiConfig.ModelId = openaiModel;

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
                "AI API key is required. Set AZURE_OPENAI_API_KEY or OPENAI_API_KEY environment variable, " +
                "or configure in appsettings.json under AI:ApiKey");
        }

        if (config.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase) && 
            string.IsNullOrWhiteSpace(config.Endpoint))
        {
            throw new InvalidOperationException(
                "Azure OpenAI endpoint is required. Set AZURE_OPENAI_ENDPOINT environment variable, " +
                "or configure in appsettings.json under AI:Endpoint");
        }
    }
}

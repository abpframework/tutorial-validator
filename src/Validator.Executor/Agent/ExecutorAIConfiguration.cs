namespace Validator.Executor.Agent;

/// <summary>
/// AI provider configuration for the Executor agent.
/// </summary>
public class ExecutorAIConfiguration
{
    /// <summary>
    /// The AI provider to use: "AzureOpenAI" or "OpenAI".
    /// </summary>
    public string Provider { get; set; } = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL (required for AzureOpenAI provider).
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Deployment name (Azure) or model name (OpenAI).
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// API key for the AI service.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Optional model ID override.
    /// </summary>
    public string? ModelId { get; set; }
}

namespace Validator.Analyst.Analysis;

/// <summary>
/// Configuration for AI providers used in tutorial analysis.
/// </summary>
public class AIConfiguration
{
    /// <summary>
    /// AI provider type: "AzureOpenAI" or "OpenAI".
    /// </summary>
    public string Provider { get; set; } = "AzureOpenAI";

    /// <summary>
    /// Azure OpenAI endpoint URL (for AzureOpenAI provider).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Deployment name (for AzureOpenAI) or model name (for OpenAI).
    /// </summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// API key for the AI service.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model ID to use (defaults to DeploymentName if not specified).
    /// </summary>
    public string? ModelId { get; set; }
}

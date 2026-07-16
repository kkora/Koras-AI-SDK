namespace Koras.AI.AzureOpenAI;

/// <summary>
/// Configures the Azure OpenAI provider. Bind from configuration (section
/// <c>Koras:AI:AzureOpenAI</c> by convention) or configure in code. The API key must come
/// from a secret source (Key Vault, environment variables, user secrets) — never source code.
/// </summary>
public sealed class AzureOpenAIOptions
{
    /// <summary>The Azure OpenAI resource endpoint, e.g. <c>https://my-resource.openai.azure.com</c>. Required.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>The chat model deployment name. Required for chat.</summary>
    public string? Deployment { get; set; }

    /// <summary>The embedding model deployment name. Required for embeddings.</summary>
    public string? EmbeddingDeployment { get; set; }

    /// <summary>The Azure OpenAI API key, sent as the <c>api-key</c> header. Required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The data-plane API version (default <c>2024-10-21</c>).</summary>
    public string ApiVersion { get; set; } = "2024-10-21";
}

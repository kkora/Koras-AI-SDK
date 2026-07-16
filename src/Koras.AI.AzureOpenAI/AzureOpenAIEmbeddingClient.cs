using System.Text;
using System.Text.Json.Nodes;
using Koras.AI.OpenAI;

namespace Koras.AI.AzureOpenAI;

/// <summary>
/// <see cref="IEmbeddingClient"/> for Azure OpenAI embedding deployments, reusing the OpenAI
/// wire protocol with Azure URLs and authentication.
/// </summary>
public sealed class AzureOpenAIEmbeddingClient : OpenAIEmbeddingClient
{
    private readonly AzureOpenAIOptions _azureOptions;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The Azure provider configuration.</param>
    public AzureOpenAIEmbeddingClient(HttpClient httpClient, AzureOpenAIOptions options)
        : base(httpClient, new OpenAIOptions { ApiKey = options?.ApiKey, DefaultEmbeddingModel = options?.EmbeddingDeployment }, providerName: "azure_openai")
    {
        ArgumentNullException.ThrowIfNull(options);
        _azureOptions = options;
    }

    /// <inheritdoc />
    protected override HttpRequestMessage CreateEmbeddingHttpRequest(EmbeddingRequest request)
    {
        string deployment = request.Model
            ?? _azureOptions.EmbeddingDeployment
            ?? throw new AiException(
                $"No embedding deployment specified: set {nameof(AzureOpenAIOptions)}.{nameof(AzureOpenAIOptions.EmbeddingDeployment)} or pass EmbeddingRequest.Model.",
                AiErrorCode.Configuration)
            { Provider = ProviderName };

        var input = new JsonArray();
        foreach (string value in request.Values)
        {
            input.Add((JsonNode)value);
        }

        var body = new JsonObject { ["input"] = input };
        if (request.Dimensions is { } dimensions)
        {
            body["dimensions"] = dimensions;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, AzureOpenAIUris.Deployment(_azureOptions, deployment, "embeddings"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("api-key", _azureOptions.ApiKey);
        return httpRequest;
    }
}

using System.Text;
using Koras.AI.OpenAI;

namespace Koras.AI.AzureOpenAI;

/// <summary>
/// <see cref="IChatClient"/> for Azure OpenAI deployments. Reuses the OpenAI wire protocol
/// with Azure's per-deployment URLs and <c>api-key</c> authentication.
/// </summary>
public sealed class AzureOpenAIChatClient : OpenAIChatClient
{
    private readonly AzureOpenAIOptions _azureOptions;

    /// <summary>Initializes the client.</summary>
    /// <param name="httpClient">The HTTP client used for API calls.</param>
    /// <param name="options">The Azure provider configuration.</param>
    public AzureOpenAIChatClient(HttpClient httpClient, AzureOpenAIOptions options)
        : base(httpClient, ToOpenAIOptions(options), providerName: "azure_openai")
    {
        _azureOptions = options;
    }

    /// <inheritdoc />
    protected override HttpRequestMessage CreateChatHttpRequest(ChatRequest request, bool stream)
    {
        string model = ResolveModel(request.Model);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, AzureOpenAIUris.Deployment(_azureOptions, _azureOptions.Deployment!, "chat/completions"))
        {
            Content = new StringContent(
                OpenAIWire.BuildChatBody(request, model, stream).ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
        ApplyAuthentication(httpRequest);
        return httpRequest;
    }

    /// <inheritdoc />
    protected override void ApplyAuthentication(HttpRequestMessage httpRequest)
        => httpRequest.Headers.TryAddWithoutValidation("api-key", _azureOptions.ApiKey);

    /// <inheritdoc />
    protected override string ResolveModel(string? requestModel)
        => requestModel ?? _azureOptions.Deployment!;

    private static OpenAIOptions ToOpenAIOptions(AzureOpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new OpenAIOptions
        {
            ApiKey = options.ApiKey,
            Endpoint = options.Endpoint ?? new Uri("https://invalid.local/"),
            DefaultModel = options.Deployment,
        };
    }
}

/// <summary>URI helpers for Azure OpenAI data-plane routes.</summary>
internal static class AzureOpenAIUris
{
    public static Uri Deployment(AzureOpenAIOptions options, string deployment, string operation)
    {
        string endpoint = options.Endpoint!.AbsoluteUri.TrimEnd('/');
        return new Uri($"{endpoint}/openai/deployments/{Uri.EscapeDataString(deployment)}/{operation}?api-version={Uri.EscapeDataString(options.ApiVersion)}");
    }
}

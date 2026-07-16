using System.Diagnostics;
using System.Text.Json;

namespace Koras.AI;

/// <summary>
/// A decorator that automatically executes tool calls: when the model requests tools created
/// with <see cref="AiTool.Create"/>, the handlers run and their results are sent back to the
/// model, up to <see cref="ToolInvocationOptions.MaxIterations"/> round-trips. Responses whose
/// tool calls reference declaration-only tools are returned to the caller untouched. Applies
/// to <see cref="CompleteAsync"/> only; streaming passes through so callers can observe
/// tool-call deltas directly.
/// </summary>
public sealed class ToolInvokingChatClient : DelegatingChatClient
{
    private readonly ToolInvocationOptions _options;

    /// <summary>Initializes the decorator.</summary>
    /// <param name="innerClient">The client to wrap.</param>
    /// <param name="options">Loop behavior; defaults apply when omitted.</param>
    public ToolInvokingChatClient(IChatClient innerClient, ToolInvocationOptions? options = null)
        : base(innerClient)
    {
        _options = options ?? new ToolInvocationOptions();
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        IReadOnlyList<AiTool>? tools = request.Options?.Tools;
        if (tools is not { Count: > 0 })
        {
            return await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var toolsByName = new Dictionary<string, AiTool>(StringComparer.Ordinal);
        foreach (AiTool tool in tools)
        {
            toolsByName[tool.Name] = tool;
        }

        List<ChatMessage> messages = [.. request.Messages];
        TokenUsage totalUsage = default;

        for (var iteration = 1; iteration <= _options.MaxIterations; iteration++)
        {
            var iterationRequest = new ChatRequest
            {
                Messages = messages,
                Model = request.Model,
                Options = request.Options,
            };

            ChatResponse response = await base.CompleteAsync(iterationRequest, cancellationToken).ConfigureAwait(false);
            totalUsage += response.Usage;

            IReadOnlyList<ToolCall> toolCalls = response.Message.ToolCalls;
            if (toolCalls.Count == 0 || !toolCalls.All(call => toolsByName.TryGetValue(call.Name, out AiTool? tool) && tool.CanInvoke))
            {
                return WithUsage(response, totalUsage);
            }

            messages.Add(response.Message);
            foreach (ToolCall call in toolCalls)
            {
                string result = await ExecuteToolAsync(toolsByName[call.Name], call, cancellationToken).ConfigureAwait(false);
                messages.Add(ChatMessage.ToolResult(call.Id, result));
            }
        }

        throw new AiException(
            $"The tool-invocation loop did not converge within {_options.MaxIterations} iterations. " +
            "Increase ToolInvocationOptions.MaxIterations or review the tool design.",
            AiErrorCode.ToolExecutionFailed)
        {
            Provider = ProviderName,
        };
    }

    private async Task<string> ExecuteToolAsync(AiTool tool, ToolCall call, CancellationToken cancellationToken)
    {
        using Activity? activity = KorasAiDiagnostics.ActivitySource.StartActivity($"execute_tool {tool.Name}");
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", tool.Name);
        activity?.SetTag("gen_ai.tool.call.id", call.Id);

        try
        {
            return await tool.InvokeAsync(call.ArgumentsJson, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (_options.ErrorBehavior == ToolErrorBehavior.Throw)
            {
                throw new AiException(
                    $"Tool '{tool.Name}' failed: {ex.Message}",
                    AiErrorCode.ToolExecutionFailed,
                    ex)
                {
                    Provider = ProviderName,
                };
            }

            return JsonSerializer.Serialize(
                new { error = $"Tool '{tool.Name}' failed: {ex.Message}" },
                KorasJson.DefaultOptions);
        }
    }

    private static ChatResponse WithUsage(ChatResponse response, TokenUsage totalUsage)
        => totalUsage == response.Usage
            ? response
            : new ChatResponse
            {
                Message = response.Message,
                Provider = response.Provider,
                Model = response.Model,
                FinishReason = response.FinishReason,
                Usage = totalUsage,
                ResponseId = response.ResponseId,
                RawRepresentation = response.RawRepresentation,
            };
}

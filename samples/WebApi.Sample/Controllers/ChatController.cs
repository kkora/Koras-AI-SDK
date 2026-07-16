using Koras.AI;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Sample.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController(IChatClient chat, IChatClientFactory clientFactory) : ControllerBase
{
    public sealed record ChatRequestDto(string Prompt, string? Client);

    public sealed record ChatResponseDto(string? Text, string Provider, string? Model, int InputTokens, int OutputTokens);

    /// <summary>Completes a prompt with the default (fallback) client or a named one.</summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponseDto>> Complete(ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        IChatClient client = request.Client is { Length: > 0 } name ? clientFactory.GetChatClient(name) : chat;

        try
        {
            ChatResponse response = await client.CompleteAsync(request.Prompt, cancellationToken);
            return Ok(new ChatResponseDto(
                response.Text,
                response.Provider,
                response.Model,
                response.Usage.InputTokens,
                response.Usage.OutputTokens));
        }
        catch (AiException ex) when (ex.Code == AiErrorCode.RateLimited)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "AI provider rate limited", retryAfterSeconds = ex.RetryAfter?.TotalSeconds });
        }
        catch (AiException ex) when (ex.Code == AiErrorCode.ContentFiltered)
        {
            return UnprocessableEntity(new { error = "The AI provider's safety system blocked this request." });
        }
        catch (AiException ex) when (ex.IsTransient)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "AI providers are temporarily unavailable.", code = ex.Code.ToString() });
        }
    }

    /// <summary>Lists the configured client names (diagnostics).</summary>
    [HttpGet("clients")]
    public ActionResult<IReadOnlyList<string>> Clients() => Ok(clientFactory.ClientNames);
}

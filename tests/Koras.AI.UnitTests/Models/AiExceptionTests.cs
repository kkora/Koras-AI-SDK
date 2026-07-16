namespace Koras.AI.UnitTests.Models;

public class AiExceptionTests
{
    [Theory]
    [InlineData(AiErrorCode.RateLimited, true)]
    [InlineData(AiErrorCode.ProviderUnavailable, true)]
    [InlineData(AiErrorCode.Network, true)]
    [InlineData(AiErrorCode.Timeout, true)]
    [InlineData(AiErrorCode.Authentication, false)]
    [InlineData(AiErrorCode.PermissionDenied, false)]
    [InlineData(AiErrorCode.ModelNotFound, false)]
    [InlineData(AiErrorCode.InvalidRequest, false)]
    [InlineData(AiErrorCode.ContentFiltered, false)]
    [InlineData(AiErrorCode.InvalidResponse, false)]
    [InlineData(AiErrorCode.NotSupported, false)]
    [InlineData(AiErrorCode.Configuration, false)]
    [InlineData(AiErrorCode.Unknown, false)]
    public void IsTransient_defaults_follow_the_documented_taxonomy(AiErrorCode code, bool expected)
        => Assert.Equal(expected, new AiException("m", code).IsTransient);

    [Fact]
    public void IsTransient_can_be_overridden_per_instance()
    {
        var quotaExhausted = new AiException("quota", AiErrorCode.RateLimited) { IsTransient = false };
        Assert.False(quotaExhausted.IsTransient);

        var forcedTransient = new AiException("odd", AiErrorCode.Unknown) { IsTransient = true };
        Assert.True(forcedTransient.IsTransient);
    }

    [Fact]
    public void Diagnostic_properties_round_trip()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new AiException("failed", AiErrorCode.RateLimited, inner)
        {
            Provider = "openai",
            StatusCode = 429,
            RetryAfter = TimeSpan.FromSeconds(7),
            ProviderErrorBody = "{\"error\":{}}",
            RequestId = "req_123",
        };

        Assert.Equal(AiErrorCode.RateLimited, ex.Code);
        Assert.Equal("openai", ex.Provider);
        Assert.Equal(429, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
        Assert.Equal("req_123", ex.RequestId);
        Assert.Same(inner, ex.InnerException);
    }
}

# Configuration: Startup Validation

Every provider registration attaches option validators with `ValidateOnStart()`. Invalid or
missing configuration fails when the host builds/starts — at deploy time, in one obvious
place — instead of surfacing as a confusing 401 on the first request at 3 a.m.

## What is validated, per provider

| Provider | Rule |
|---|---|
| OpenAI | `ApiKey` non-blank; `Endpoint` not null; `Endpoint` HTTPS unless loopback |
| Azure OpenAI | `Endpoint` required; HTTPS unless loopback; `ApiKey` non-blank; `Deployment` and/or `EmbeddingDeployment` set; `ApiVersion` non-empty |
| Anthropic | `ApiKey` non-blank; `Endpoint` not null; HTTPS unless loopback; `DefaultMaxOutputTokens > 0` |
| Gemini | `ApiKey` non-blank; `Endpoint` not null; HTTPS unless loopback |
| Ollama | `Endpoint` not null (HTTP allowed — local daemon) |

Two related checks happen outside the options system:

- **Registration time:** duplicate client names throw `InvalidOperationException`
  immediately inside `AddKorasAI` (`A chat client named 'openai' is already registered...`).
- **Call time:** rules that depend on the request are enforced then — e.g. a chat call on an
  Azure client with no `Deployment`, or a request with neither `ChatRequest.Model` nor
  `DefaultModel`, throws `AiException` with `AiErrorCode.Configuration`.

## What a failure looks like

Each rule carries an actionable message naming the client and the option. A boot with a
missing OpenAI key ends with:

```text
Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException:
  Koras.AI OpenAI client 'openai': ApiKey is required. Provide it via configuration or
  user secrets — never source code.
```

A non-loopback HTTP endpoint:

```text
Microsoft.Extensions.Options.OptionsValidationException:
  Koras.AI OpenAI client 'openai': Endpoint must use HTTPS (HTTP is allowed only for
  loopback addresses).
```

Multiple violations on one options instance are reported together in the exception's
failure list. The message prefix always identifies the named client (`client 'openai-batch'`),
which matters when the same provider is registered more than once.

## The HTTPS rule

Remote AI endpoints carry credentials and user content, so `http://` is rejected for any
non-loopback host. Loopback (`localhost`, `127.0.0.1`, `[::1]`) is exempt — that permits
local Ollama, in-process fake servers in integration tests, and local gateways. There is no
override switch; front a plain-HTTP internal gateway with TLS or expose it on loopback.

## Verifying configuration in CI

Because validation runs when the host starts, a smoke boot is a cheap configuration test:

```csharp
[Fact]
public void Production_configuration_is_valid()
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Production.json")
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Koras:AI:OpenAI:ApiKey"] = "sk-placeholder-for-validation",
        })
        .Build();

    var services = new ServiceCollection();
    services.AddKorasAI(ai => ai.AddOpenAI(config.GetSection("Koras:AI:OpenAI")));

    using ServiceProvider provider = services.BuildServiceProvider(
        new ServiceProviderOptions { ValidateOnBuild = true });

    // Options ValidateOnStart executes via the hosted startup filter in a real host;
    // resolving the options here forces the same validators to run.
    provider.GetRequiredService<IOptionsMonitor<Koras.AI.OpenAI.OpenAIOptions>>().Get("openai");
}
```

## Validating your own options

Custom providers (registered with `ai.AddClient(...)`) should follow the same pattern using
the standard options APIs:

```csharp
ai.Services.AddOptions<MyProviderOptions>("mine")
    .Configure(configure)
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
        "Koras.AI custom client 'mine': ApiKey is required.")
    .Validate(o => o.Endpoint is null || o.Endpoint.Scheme == Uri.UriSchemeHttps || o.Endpoint.IsLoopback,
        "Koras.AI custom client 'mine': Endpoint must use HTTPS.")
    .ValidateOnStart();

ai.AddClient("mine", sp => new MyChatClient(
    sp.GetRequiredService<IOptionsMonitor<MyProviderOptions>>().Get("mine")));
```

Keep the message format — `Koras.AI <provider> client '<name>': <problem>. <fix>` — so
operators see uniform errors regardless of provider. See
[custom providers](../features/custom-providers.md).

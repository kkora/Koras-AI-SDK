# Koras.AI.AspNetCore

ASP.NET Core integration for the [Koras AI SDK](https://github.com/korastechnologies/koras-ai-sdk):
health checks that probe your configured AI providers through lightweight endpoints (never a
paid completion).

```csharp
builder.Services.AddHealthChecks().AddKorasAI();
app.MapHealthChecks("/health");
```

Docs: https://github.com/korastechnologies/koras-ai-sdk/tree/main/docs/features/health-checks.md

# Security Checklist

Two audiences, two lists. Consumers: run the first list before every production deployment
that uses Koras.AI. Maintainers: run the second before every release.

## For consumers — deploying to production

### Credentials

- [ ] API keys come from environment variables or a vault via `IConfiguration` — not
      `appsettings*.json`, not source code, not container images.
- [ ] Keys are least-privilege (per-app, model-scoped, spend-limited where the provider
      supports it) and a rotation procedure exists.
- [ ] Development keys (user secrets) differ from production keys.
- [ ] Secret scanning is enabled on your repository.

### Transport and endpoints

- [ ] Every remote provider endpoint is `https://` (startup validation enforces this — verify
      your configuration values so the app actually starts).
- [ ] No endpoint option is ever populated from user input — endpoints are deploy-time
      configuration only (SSRF; see the [threat model](threat-model.md)).
- [ ] Outbound egress rules allow only the configured provider hosts, if your network policy
      supports allowlisting.

### Logging and data

- [ ] `KorasAiTelemetryOptions.EnableSensitiveData` is `false` (or unset) in production.
- [ ] Production log level for `Koras.AI.*` categories is `Information` or higher.
- [ ] Exception sinks are access-controlled — `AiException.ProviderErrorBody` carries up to
      4 KB of provider error JSON.
- [ ] Prompts are treated as PII in your data map; the relevant provider DPA is on file
      (see [data-protection.md](data-protection.md)).

### Tools and resilience

- [ ] Every registered `AiTool` handler validates and authorizes its arguments — model input
      is untrusted (see [threat-model.md](threat-model.md)).
- [ ] `ToolInvocationOptions.MaxIterations` is left at 8 or set deliberately.
- [ ] Retry settings reviewed for your traffic (defaults: 3 attempts, 100 s per-attempt
      timeout, Retry-After honored).
- [ ] Multi-tenant apps use per-tenant named clients and never share conversation state
      ([secure-configuration.md](secure-configuration.md)).

### Hygiene

- [ ] You are on a [supported version](../../SECURITY.md) and subscribed to the repo's
      security advisories.
- [ ] `dotnet list package --vulnerable --include-transitive` is clean in your own CI.

## For maintainers — per release

### Before tagging

- [ ] `main` is green: build, test (ubuntu + windows, all TFMs), package, CodeQL,
      dependency-review workflows all passing.
- [ ] `dotnet list package --vulnerable --include-transitive` clean; no suppressed
      NuGetAudit warnings.
- [ ] All Dependabot security PRs merged or consciously deferred with an issue.
- [ ] Public API snapshot diffs (`tests/Koras.AI.ArchitectureTests/PublicApi/`) reviewed —
      no accidental surface, no third-party types leaked.
- [ ] Grep-audit of new code for secrets in diagnostics: no options values in log messages,
      exception messages, or telemetry tags (definition-of-done rule).
- [ ] New/changed provider registrations keep HTTPS validation and header-only key transmission.
- [ ] Any new dependency followed the [dependency policy](../architecture/dependency-rules.md)
      review; `Directory.Packages.props` diff is intentional.
- [ ] Threat model reviewed if the release adds an asset, boundary, or capability
      (new provider, new transport, tool changes) — update
      [threat-model.md](threat-model.md) in the same release.

### Release mechanics

- [ ] Publish happens **only** via the tag-triggered `release.yml` in the protected `nuget`
      environment — no manual `dotnet nuget push`
      ([nuget-publishing.md](../release/nuget-publishing.md)).
- [ ] NuGet Trusted Publishing policy (owner `kora.kanchan`) still pins exactly this
      repository, `release.yml`, and the `nuget` environment; no long-lived NuGet API
      keys exist for these packages.
- [ ] Packed artifacts spot-checked: no unexpected files, snupkg present, SourceLink resolves.

### After release

- [ ] SECURITY.md supported-versions table still accurate.
- [ ] Any fixed vulnerability gets a GitHub Security Advisory with credited reporter and
      affected-version range.

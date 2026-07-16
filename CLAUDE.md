# CLAUDE.md — Koras.AI SDK

## 1. Project mission

Provider-neutral generative-AI SDK for .NET: one abstraction for chat, streaming, structured
output, tool calling, and embeddings across OpenAI, Azure OpenAI, Anthropic, Gemini, and
Ollama — with DI, resilience, fallback, normalized errors, and OTel-convention telemetry
built in. Docs tree under `docs/` is the source of truth for scope and design.

## 2. Package scope

In: what `docs/features/mvp-scope.md` lists. Out: orchestration, RAG storage, vector DBs,
prompt IDEs, provider control-plane APIs. New feature ideas get a roadmap classification in
`docs/features/` before any code.

## 3. Architecture rules

- Dependency direction: providers → `Koras.AI` → `Koras.AI.Abstractions`. Nothing else.
  Providers never reference each other (exception: AzureOpenAI → OpenAI).
- Cross-cutting behavior = decorator over `IChatClient` (`DelegatingChatClient`), never a flag
  on a provider client.
- Errors cross the provider boundary only as `AiException` with an `AiErrorCode`.
- No vendor SDK dependencies (ADR-0003). No new dependencies without the review in
  `docs/architecture/dependency-rules.md`.
- Architecture tests (`tests/Koras.AI.ArchitectureTests`) enforce this — keep them green.

## 4. Coding conventions

Modern C#: file-scoped namespaces, nullable enabled, `var` when clear, expression bodies for
one-liners, primary constructors where they clarify. `ConfigureAwait(false)` in all `src/`
awaits (CA2007 is an error there). No `.Result`/`.Wait()`/`Thread.Sleep` anywhere in `src/`.
Time comes from `TimeProvider`; HTTP from `IHttpClientFactory`-managed clients.

## 5. Naming conventions

See `docs/api/naming-guidelines.md`. Key points: `Ai` (not `AI`) inside identifiers;
`*Options` for options; decorators end in `ChatClient`; async ends in `Async`;
`CancellationToken cancellationToken` last and defaulted.

## 6. Dependency restrictions

Allowed in src: `Microsoft.Extensions.*` abstractions/primitives, `System.Text.Json` (net8.0
TFM only), `OpenTelemetry.Api` (OTel package only). Everything else needs the documented
review + maintainer sign-off. Versions pinned centrally in `Directory.Packages.props`.

## 7. Public API rules

- Every public-surface change must update the affected `PublicApi/*.approved.txt` snapshot
  (API surface tests in ArchitectureTests fail otherwise) and pass
  `docs/api/public-api-review-checklist.md`.
- Interfaces are frozen once shipped; new capability = new interface or extension method.
- No third-party types in public signatures. XML docs required (build fails without).

## 8. Test requirements

Every feature: happy path, invalid input, boundary, failure paths, cancellation; provider
mappings tested against fixtures in `tests/Koras.AI.UnitTests/Fixtures/`. Providers must pass
the shared contract suite. Run `dotnet test` before claiming anything works.

## 9. Documentation requirements

Feature work updates its guide in `docs/features/`, the CHANGELOG `[Unreleased]` section, and
samples when the primary usage path changes. New public types need XML docs with examples for
non-obvious usage.

## 10. Security requirements

No secrets in code, logs, exceptions, telemetry, or fixtures (use `sk-test-...` fake keys).
Message content is never logged/traced unless `EnableSensitiveData=true`. Auth via headers,
HTTPS-only defaults (Ollama localhost exempt). See `docs/security/`.

## 11. Performance requirements

Streaming never buffers whole responses; hot paths avoid needless allocation (source-generated
logging, pooled HttpClient); no unbounded retries/collections. Benchmarks in `benchmarks/`.

## 12. Git workflow

Feature branches off `main`; conventional-style commit subjects (`feat:`, `fix:`, `docs:`,
`test:`, `chore:`); small commits per milestone/task. Never commit secrets or generated
artifacts (`bin/`, `obj/`, `artifacts/`).

## 13. Pull-request checklist

Build + tests green on all TFMs; `dotnet format --verify-no-changes` clean; public API
snapshots updated deliberately; CHANGELOG updated; docs updated; no new dependencies without
review; no placeholder code.

## 14. Release workflow

See `docs/release/release-process.md`: version bump in `Directory.Build.props` → tag `vX.Y.Z`
→ `release.yml` packs, validates, publishes to NuGet.org from the protected `nuget`
environment. Never publish manually.

## 15. Definition of done

`docs/planning/definition-of-done.md` — applies to every task.

## 16. Commands Claude should use

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet format --verify-no-changes
dotnet pack --configuration Release --no-build --output artifacts/packages
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

## 17. Files not to modify without justification

`LICENSE`, `Directory.Packages.props` (version pins), `global.json`, `.github/workflows/release.yml`,
`PublicApi/*.approved.txt` (only with an intentional API change), `assets/icon.png`.

## 18. Rules for adding dependencies

Document in the PR: necessity, alternatives considered, license, maintenance status, security
implications, size impact, public-API leakage (must be none). Pin in `Directory.Packages.props`.

## 19. Rules for breaking changes

Pre-1.0: allowed in minor versions with CHANGELOG + migration notes. Post-1.0: majors only,
after deprecation per `docs/api/backward-compatibility.md`. Package validation + API snapshots
are the gate — never silence them to make a build pass.

## 20. Required validation before completing a task

`dotnet build -c Release` (zero warnings) → `dotnet test -c Release` (all green) →
`dotnet format --verify-no-changes` → confirm no secrets/artifacts staged → CHANGELOG + docs
updated. Do not report success unless all of these actually ran and passed.

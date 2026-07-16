# Milestones

| Milestone | Name | Scope | Exit criteria |
|---|---|---|---|
| 0 | Repository foundation | Solution, build props, CPM, analyzers, community files, CI skeleton, CLAUDE.md | empty solution builds in CI, warnings-as-errors on |
| 1 | Abstractions & core models | Complete `Koras.AI.Abstractions` | model + serialization + tool-schema tests green |
| 2 | Core implementation | Decorators (retry, telemetry, logging, tool loop), structured output, templates, provider plumbing | core unit suite green incl. FakeTimeProvider retry tests |
| 3 | DI & configuration | Builder, factory, fallback, options validation, config binding | DI suite green; invalid config fails startup with clear message |
| 4 | Provider packages | OpenAI, AzureOpenAI, Anthropic, Ollama, Gemini | shared contract tests green ×5 against fixtures |
| 5 | ASP.NET Core integration | Health checks + provider probes | health-check tests green |
| 6 | Observability & diagnostics | OTel package, GenAI conventions verification | telemetry assertions green |
| 7 | Samples & documentation | 4 samples, docs tree, README | samples run; docs complete per checklist |
| 8 | Performance & security hardening | Integration + architecture tests, benchmarks, security checklist | full suite green; `dotnet format --verify-no-changes` clean; no vulnerable deps |
| 9 | NuGet packaging & release | Pack metadata, validation, consumer smoke, release workflows, release docs | `dotnet pack` artifacts pass validation; workflows lint clean |

Sequencing is strict for 0→3 (dependency direction); 4 can parallelize per provider; 5–6 after
3; 7–9 close the release.

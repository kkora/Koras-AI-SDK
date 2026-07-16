# Risk Register

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | **Provider wire-format drift** — providers change/version their REST APIs | High | High | Pin API versions per adapter (configurable); recorded-fixture contract tests catch mapping regressions; error normalization tables reviewed each release; endpoint override supports gateways |
| R-02 | **Microsoft.Extensions.AI ecosystem matures**, eroding differentiation | Medium | High | Compete on the productized whole (resilience, fallback, uniform providers, error taxonomy); ship the M.E.AI bridge (F-022) so adoption is reversible = low-risk |
| R-03 | **Premature public-API lock-in** | Medium | High | PublicAPI.txt gate + package validation baseline; 0.5.0 freeze window; interfaces frozen from 1.0; extensible-enum record structs for wire vocabularies |
| R-04 | **Streaming/tool-calling edge cases** (split SSE frames, partial tool-arg deltas, provider quirks) | High | Medium | Dense parser unit tests incl. adversarial chunking; fixture replays from real captures; aggregation helpers tested for equivalence with non-streaming |
| R-05 | **Scope creep** into orchestration/RAG/memory | Medium | Medium | Standing out-of-scope list; new features require roadmap classification before code |
| R-06 | **Secret leakage** via logs/exceptions/telemetry | Low | Critical | Default-off sensitive logging, scrub lists at the provider base, dedicated tests asserting absence, security checklist gate |
| R-07 | **Retry amplification** during provider incidents | Medium | Medium | Bounded attempts, honored Retry-After, full jitter, fallback exhaustion aggregates rather than loops |
| R-08 | **Dependency vulnerabilities** (even in the small closure) | Low | Medium | CPM pinning, Dependabot, `dotnet list package --vulnerable` in CI, dependency-review action |
| R-09 | **Trust deficit as a new publisher** | High | Medium | Deterministic builds, SourceLink, CodeQL, published threat model, high test visibility, reserved NuGet prefix |
| R-10 | **Multi-TFM matrix cost** (net8/9/10 × packages) | Medium | Low | Shared build props, CI matrix, TFM-conditional package refs only where needed (STJ on net8) |

Review cadence: every release; any Critical-impact risk change triggers an ADR.

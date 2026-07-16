# Performance Testing

Performance is tested as a **regression discipline**, not a leaderboard: the question is
always "did this change make the hot paths slower or more allocatey than the last release?",
never "what number can we publish?" (see [benchmarks.md](../performance/benchmarks.md) for
why no official numbers exist).

## The benchmark suite as regression tool

`benchmarks/Koras.AI.Benchmarks` (BenchmarkDotNet + `MemoryDiagnoser`) covers the SDK-owned
hot paths — wire serialization, the in-memory `CompleteAsync` round-trip, 50-chunk SSE
parsing, schema generation, and template rendering. Because the network is removed, results
are stable enough on one machine to compare run-over-run; regressions in these paths multiply
across every request in production.

Allocation numbers from `MemoryDiagnoser` are the most trustworthy regression signal: unlike
timings, `Allocated` per op is deterministic — a change from e.g. 3 allocations to 5 on the
SSE path is a real finding regardless of machine noise.

## Comparing before/after

BenchmarkDotNet writes artifacts to `BenchmarkDotNet.Artifacts/results/` (GitHub markdown,
CSV, HTML). The comparison workflow:

```bash
# 1. Baseline on the reference commit (last release tag or main)
git checkout v0.5.0
dotnet run -c Release --project benchmarks/Koras.AI.Benchmarks -- --filter '*' \
    --artifacts /tmp/bench/baseline

# 2. Candidate on your branch — same machine, same power profile, nothing else running
git checkout my-change
dotnet run -c Release --project benchmarks/Koras.AI.Benchmarks -- --filter '*' \
    --artifacts /tmp/bench/candidate

# 3. Diff the reports
diff /tmp/bench/{baseline,candidate}/results/*CoreBenchmarks-report-github.md
```

Rules for honest comparison:

- **Same machine, same session.** Never compare artifacts from different hardware.
- Trust the error/StdDev columns — a delta inside the confidence interval is noise.
- Treat any `Allocated` delta as real; treat time deltas under ~5 % as suspect.
- Paste the two BenchmarkDotNet tables into the PR when a change is performance-motivated,
  labeled with the commit each was measured on.

## What triggers a benchmark run

There is **no CI performance gate yet** — benchmarks do not run in any workflow, deliberately:
shared CI runners produce noise that would either mask real regressions or fail builds
randomly. Instead:

| When | What |
|---|---|
| PRs touching hot paths (wire mappers, `SseReader`, schema gen, templates, decorators) | Author runs before/after locally; tables in the PR description |
| Every release | Manual suite run and comparison against the previous release's artifacts — a required pre-flight step in the [release checklist](../release/release-checklist.md) |
| Adding a hot path | Add a benchmark to `CoreBenchmarks` in the same PR |

A CI-based tracking setup (dedicated runner or threshold-tolerant comparison action) is a
candidate improvement once numbers stabilize post-1.0; until then the release-time manual
comparison is the gate of record.

## Interpreting regressions

1. Allocation growth on per-request/per-chunk paths (`ParseSse`, `CompleteRoundTrip`,
   `BuildChatBody`) — investigate before merging; these compound under load.
2. Time regressions beyond noise on `ParseSse` — check for accidental buffering or
   per-chunk string churn; parsing must stay linear in chunk count.
3. `SchemaGeneration`/`TemplateRender` — verify the parse-once/cache-once design didn't
   silently become parse-per-call.

When in doubt, bisect with `--filter '*Sse*'`-style narrow runs rather than the full suite.

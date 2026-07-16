# Benchmarks

How the SDK's performance is measured, how to run the suite yourself, and what we watch for
regressions. **No official numbers are published yet** — results below are described, never
quoted, because they are environment-specific.

## Methodology

The benchmark project is
[`benchmarks/Koras.AI.Benchmarks`](../../benchmarks/Koras.AI.Benchmarks/CoreBenchmarks.cs),
built on [BenchmarkDotNet](https://benchmarkdotnet.org/) with `[MemoryDiagnoser]` enabled, so
every benchmark reports allocation per operation alongside timing.

`CoreBenchmarks` covers the SDK-owned critical paths — the work the SDK adds on top of the
network call:

| Benchmark | What it measures |
|---|---|
| Chat request build+serialize (OpenAI wire) | Building a `ChatRequest` body into provider wire JSON. |
| Full `CompleteAsync` round-trip (in-memory HTTP) | The whole client pipeline — request build, HTTP send via an in-memory handler, response parse into `ChatResponse` — with the network removed. |
| SSE parse 50-chunk stream | `SseReader` parsing a realistic 50-event server-sent-events stream (plus `[DONE]`). |
| JSON schema generation from a record type | `AiJsonSchema.FromType<T>()` for a representative record (structured output / tool schemas). |
| Prompt template render | `PromptTemplate.Render` on a pre-parsed template (parse cost is paid once, by design). |

Real provider latency is deliberately excluded: inference time is provider-side, variable, and
would drown the signal. These benchmarks isolate what the SDK itself costs.

## How to run

```bash
dotnet run -c Release --project benchmarks/Koras.AI.Benchmarks -- --filter '*'
```

Notes:

- Always `-c Release`; BenchmarkDotNet refuses meaningful runs in Debug.
- Narrow with `--filter '*Sse*'` etc. while iterating.
- Results (markdown/CSV/HTML) land in `BenchmarkDotNet.Artifacts/results/` under the working
  directory.
- Close background load; on laptops, pin power settings — thermal throttling ruins comparisons.

## On numbers

Results depend on CPU, RAM, OS, .NET runtime version, and ambient load. A number measured on
one machine is not a claim about yours, so this repository publishes **methodology, not
numbers**. Run the suite on hardware representative of your deployment if you need absolute
figures. If official baseline numbers are ever published, they will state the exact hardware,
OS, and runtime — anything else would be fabrication.

## What we watch for regressions

Before each release the suite is run and compared against the previous release's artifacts
(see [performance-testing.md](../testing/performance-testing.md)). Red flags, in order of
concern:

1. **Allocation growth** on the serialization, SSE-parse, and round-trip paths — these run
   per request (or per chunk) and allocation regressions compound under load.
2. **Superlinear time changes** in SSE parsing — it must stay linear in chunk count.
3. **Schema generation cost** — cached by callers in practice, but a large jump signals an
   accidental reflection hot spot.
4. **Template render** — must remain proportional to output size, with no re-parsing.

There is no automated CI performance gate yet; comparison is a manual pre-release step in the
[release checklist](../release/release-checklist.md).

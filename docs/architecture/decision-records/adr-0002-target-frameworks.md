# ADR-0002: Target net8.0;net9.0;net10.0 — no netstandard2.0

**Status:** Accepted · **Date:** 2026-07-16

## Context
The instruction set allows netstandard2.0 "only when broad legacy compatibility is genuinely
needed". The SDK's ergonomics rest on `IAsyncEnumerable` streaming, `required` members, generic
math-free `TimeProvider`, keyed DI-era Microsoft.Extensions versions, and
`System.Text.Json` `JsonSchemaExporter` (STJ 9+).

## Decision
Multi-target `net8.0;net9.0;net10.0` for all shipping libraries. net8.0 is the LTS floor;
net9.0/net10.0 targets use in-box System.Text.Json instead of a package reference. No
netstandard2.0 / .NET Framework support.

## Consequences
- .NET Framework consumers are not served — acceptable: the target users run modern hosts.
- net8.0 target carries `System.Text.Json` 9.x and matching Microsoft.Extensions 9.x packages.
- Framework support policy: a TFM is dropped in the next major release after Microsoft support
  ends (documented in versioning policy).

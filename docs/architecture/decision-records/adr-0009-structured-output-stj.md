# ADR-0009: Structured output via System.Text.Json `JsonSchemaExporter`

**Status:** Accepted · **Date:** 2026-07-16

## Context
Structured output requires generating JSON Schema from C# types. Options: Newtonsoft-based
generators, JsonSchema.Net, hand-rolled reflection, or `System.Text.Json.Schema.
JsonSchemaExporter` (STJ 9+, in-box on net9/net10, package on net8).

## Decision
Use `JsonSchemaExporter` with a post-processing step that enforces provider strict-mode
requirements (e.g. OpenAI: `additionalProperties: false`, all properties required). Respect
`[Description]` attributes. Deserialize responses with STJ using safe defaults (no polymorphic
type handling from payload data).

## Consequences
- No new dependency (net8 gets `System.Text.Json` 9.x, which the Microsoft.Extensions 9 stack
  pulls anyway).
- Schema fidelity limits follow STJ's exporter; documented per provider.
- AOT-friendly path (JsonTypeInfo overloads) is possible later without API breaks.

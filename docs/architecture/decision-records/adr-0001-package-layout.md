# ADR-0001: Multi-package layout (abstractions + core + provider packages)

**Status:** Accepted · **Date:** 2026-07-16

## Context
A single package would carry every provider's surface to every consumer; a metapackage-of-many
adds install friction. Provider adapters have no heavy dependencies (raw REST), so package
weight is about API surface and change isolation more than bytes.

## Decision
Ship `Koras.AI.Abstractions` (contracts), `Koras.AI` (core engine incl. DI), one package per
provider, and two integration packages (AspNetCore, OpenTelemetry). All version and release
together.

## Consequences
- Libraries can depend on Abstractions alone; provider choice stays an app-level decision.
- New providers are additive packages — no core release required conceptually, though we
  release the family together for predictability.
- Cost: 9 csproj/nuspecs to maintain; mitigated by shared build props.

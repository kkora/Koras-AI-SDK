# Versioning Policy

Koras.AI follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html). All
packages in the family version together and release together. The current version is
**0.1.0-preview.1**. This page summarizes the normative policy in
[backward compatibility](../api/backward-compatibility.md).

## The compatibility promise (from 1.0.0)

Within a major version:

- **No removals or renames** of public types/members; no signature changes.
- **No behavioral changes** that documented code depends on — defaults, the `AiErrorCode`
  produced for a given wire condition, decorator ordering.
- **Additions are allowed:** new types, new members on non-interface types, new overloads
  (never new optional parameters on existing signatures), new `AiErrorCode` values
  (documented: consumers must `default`-case their switches), new packages, new TFMs.
- **Interfaces are frozen.** New capabilities arrive as new interfaces (the
  `IProviderHealthProbe` pattern) or extension methods — never as members added to shipped
  interfaces, not even with default implementations.
- Public models (`ChatMessage`, `ChatResponse`, options types) guarantee System.Text.Json
  round-trip stability within a major version, for persisted-history scenarios.

## Pre-1.0 rules (current phase)

- **0.x minor bumps may include breaking changes**, always with migration notes in the
  [CHANGELOG](../../CHANGELOG.md) and an entry under `docs/migration/`.
- **0.x patch releases never break.**
- **0.5.0 is the API-freeze candidate:** breaking changes after 0.5.0 require explicit
  maintainer sign-off, and the surface at 0.5.0 is intended to ship as 1.0.
- Preview suffixes (`0.1.0-preview.1`) mark releases where anything may still move.

## What counts as breaking

Removals/renames, signature changes, behavior changes to documented semantics, narrowing
accepted inputs, and changing validation from lenient to strict. Not breaking: new
`AiErrorCode` values, new overloads/types/packages, performance improvements, message-text
changes in exceptions/logs (do not parse them), and bug fixes that align behavior with
documentation.

## Target framework (TFM) support

- Shipping targets: `net8.0`, `net9.0`, `net10.0`
  ([ADR-0002](../architecture/decision-records/adr-0002-target-frameworks.md)).
- No `netstandard2.0`, no .NET Framework.
- **Drop policy:** a TFM is removed only in the first *major* release after Microsoft's
  support for that runtime ends. Adding a TFM can happen in a minor.

## Deprecation process

Nothing is removed without a full major cycle of warnings:

1. A minor release marks the API
   `[Obsolete("Use X. Removed in <major+1>.0.0.", error: false)]`.
2. The CHANGELOG and a migration-guide entry document a mechanical rewrite recipe.
3. The last minor of the major escalates to `error: true` (compile error).
4. The next major removes the API.

## Enforcement

The policy is mechanically enforced, not aspirational:

- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project with
  `Microsoft.CodeAnalysis.PublicApiAnalyzers` — any public-surface change fails the build
  until explicitly declared (and reviewed in the PR diff).
- `EnablePackageValidation` with the previous release as baseline — `dotnet pack` fails on
  binary/source breaking changes.
- Public API changes require the
  [review checklist](../api/public-api-review-checklist.md); architectural changes require
  an ADR.

## Version numbers in practice

| Change | Pre-1.0 | Post-1.0 |
|---|---|---|
| Bug fix, no surface change | patch | patch |
| New feature, additive | minor | minor |
| Breaking change | minor (with migration notes) | major (with migration guide) |
| New provider package | minor | minor |

See [upgrading](upgrading.md) for the practical upgrade workflow and
[breaking changes](breaking-changes.md) for the running log.

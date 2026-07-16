# Dependency Rules

Enforced by architecture tests (`Koras.AI.ArchitectureTests`) and central package management.

## Direction rules

1. `Koras.AI.Abstractions` depends only on the BCL (plus `System.Text.Json` package on net8.0).
2. `Koras.AI` depends on Abstractions + Microsoft.Extensions.* abstractions/primitives only.
3. Provider packages depend on `Koras.AI` only. Providers must not reference each other
   (sanctioned exception: `Koras.AI.AzureOpenAI` → `Koras.AI.OpenAI`).
4. Integration packages depend on `Koras.AI` + their framework. Nothing depends on integration
   packages.
5. Tests may depend on anything. `src/` never depends on `tests/`.

## Third-party dependency policy

Adding any dependency to a `src/` project requires documenting in the PR (and, for public-API
impact, an ADR):

- why it is required and what was considered instead,
- license (must be MIT/Apache-2.0/BSD-compatible),
- maintenance status (active releases within 12 months),
- security posture (known CVEs, supply-chain reputation),
- size impact on the dependency closure,
- whether any of its types leak into our public API (**must be no** outside integration
  packages whose purpose is that framework).

Current allowed set (see `Directory.Packages.props`): `Microsoft.Extensions.*`,
`System.Text.Json`, `OpenTelemetry.Api` (OTel package only). Vendor AI SDKs are banned
(ADR-0003). Polly/Microsoft.Extensions.Resilience deliberately not used (ADR-0008).

## Framework targets

Libraries: `net8.0;net9.0;net10.0`. No `netstandard2.0` (ADR-0002).

## Enforcement

- NetArchTest rules assert the direction table above on every CI run.
- Central Package Management (`Directory.Packages.props`) pins every version; no floating
  versions; `dotnet list package --vulnerable --include-transitive` runs in CI.
- Public API surfaces tracked; a new public type from a third-party namespace fails review.

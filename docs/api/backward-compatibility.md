# Backward Compatibility Policy

## The promise (from 1.0.0)

Within a major version:

- No public type/member removal or rename; no signature changes.
- No behavioral change that documented code depends on (defaults, error codes for a given
  wire condition, decorator ordering).
- Adding is allowed: new types, new members on non-interface types, new optional parameters
  **only via new overloads**, new `AiErrorCode` values (documented: consumers must default-case),
  new packages, new TFMs.
- Interfaces are frozen. New capabilities arrive as new interfaces (`IProviderHealthProbe`
  pattern) or extension methods — never members added to shipped interfaces (even with DIM).

## Pre-1.0 rules

- 0.x minor bumps may break with migration notes in CHANGELOG + `docs/migration/`.
- 0.5.0 is the API-freeze candidate: breaking changes after it require maintainer sign-off.

## Enforcement mechanisms

1. **Public API tracking** — `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` per project
   with `Microsoft.CodeAnalysis.PublicApiAnalyzers`: any public-surface change fails the build
   until the diff is made explicit in the txt files (reviewable in PRs).
   - Pre-1.0: everything lives in `Unshipped`; at each release, entries move to `Shipped`.
2. **Package validation** — `EnablePackageValidation` with `PackageValidationBaselineVersion`
   set from the previous release: `dotnet pack` fails on binary/source breaking changes.
3. **Review gate** — public API changes require the checklist in
   [public-api-review-checklist](public-api-review-checklist.md) and an ADR when architectural.

## Deprecation process

1. Mark `[Obsolete("Use X. Removed in <major+1>.0.0.", error: false)]` in a minor release.
2. Document in CHANGELOG + migration guide with a mechanical rewrite recipe.
3. Escalate to `error: true` in the last minor of the major.
4. Remove in the next major.

## Serialization compatibility

Wire-facing DTOs are internal — providers own their JSON. Public models (`ChatMessage`,
`ChatResponse`, options) guarantee STJ round-trip stability within a major version for
persisted-history scenarios.

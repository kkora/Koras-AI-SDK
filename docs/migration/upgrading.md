# Upgrading

How to move between Koras.AI versions. Current version: **0.1.0-preview.1** — there are no
earlier releases, so no migrations exist yet. This page documents the process that applies
from the next release onward.

## The family moves together

All `Koras.AI.*` packages ship as one family with one version number. Upgrade them together —
mixed versions within the family are unsupported and package dependencies enforce matching
ranges. With central package management, one line change upgrades everything:

```xml
<!-- Directory.Packages.props -->
<ItemGroup>
  <PackageVersion Include="Koras.AI.OpenAI" Version="0.1.0-preview.1" />
  <PackageVersion Include="Koras.AI.AspNetCore" Version="0.1.0-preview.1" />
  <PackageVersion Include="Koras.AI.OpenTelemetry" Version="0.1.0-preview.1" />
</ItemGroup>
```

## Upgrade workflow

1. **Read the [CHANGELOG](../../CHANGELOG.md)** for every version between yours and the
   target. Entries follow Keep a Changelog: `Added` is safe, `Changed`/`Removed` may need
   action, and pre-1.0 breaking changes are called out with links to migration notes.
2. **Check [breaking changes](breaking-changes.md)** for entries in your range. Each entry
   contains a mechanical rewrite recipe.
3. **Bump all `Koras.AI.*` package references** to the same version.
4. **Build.** Obsoletion warnings tell you what to migrate ahead of the next major;
   pre-freeze (before 0.5.0) surface changes appear as ordinary compile errors covered by
   the migration notes.
5. **Boot in staging.** Startup validation (`ValidateOnStart`) catches any changed or newly
   required options at deploy time — see [validation](../configuration/validation.md).
6. **Run your tests.** Fakes that implement `IChatClient` keep compiling within a major
   (interfaces are frozen post-1.0; pre-1.0 interface changes are listed in migration notes).

## What to expect by version bump

| Bump | Expectation |
|---|---|
| Patch (`0.1.0` → `0.1.1`) | Drop-in. Bug fixes only, never breaking. |
| Minor, pre-1.0 (`0.1` → `0.2`) | May break — read the migration notes for that version below. |
| Minor, post-1.0 (`1.1` → `1.2`) | Drop-in. Additive only; new `AiErrorCode` values possible (default-case your switches). |
| Major (`1.x` → `2.0`) | Breaking. Previously `[Obsolete]` APIs removed; follow the major migration guide. |

## Migration notes by version

No migrations yet — `0.1.0-preview.1` is the first release. Future entries will follow this
structure:

### Template: 0.x → 0.y

> **Scope:** which packages/APIs changed.
>
> **Why:** one paragraph on the motivation.
>
> **Before / after:** compiling code for each change:
>
> ```csharp
> // 0.x
> // var old = ...;
> // 0.y
> // var updated = ...;
> ```
>
> **Mechanical recipe:** find/replace or fix steps, in order.
>
> **Behavioral changes:** defaults or semantics that changed without compile errors.

## Preview-to-preview upgrades

While the SDK is in preview (`0.1.0-preview.N`), previews of the same version may change the
surface without individual migration notes; the CHANGELOG `Unreleased` section is the
authoritative diff. Pin exact preview versions in production experiments.

## See also

- [Versioning policy](versioning-policy.md) — the rules behind these expectations.
- [Breaking changes](breaking-changes.md) — the running log and entry template.

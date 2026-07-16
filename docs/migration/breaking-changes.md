# Breaking Changes

The running log of breaking changes across Koras.AI releases, newest first. Each entry is
written so the upgrade is mechanical: what changed, why, and exactly how to rewrite affected
code.

## Current status

**None yet.** The current version, `0.1.0-preview.1`, is the first release — there is no
earlier surface to break.

## When breaking changes can happen

Per the [versioning policy](versioning-policy.md):

- **Pre-1.0:** a 0.x **minor** bump may break, always with an entry here and a note in the
  [CHANGELOG](../../CHANGELOG.md). Patches never break. After 0.5.0 (the API-freeze
  candidate), breaking changes additionally require maintainer sign-off.
- **Post-1.0:** breaking changes ship only in **major** versions, at the end of the
  deprecation process (obsolete warning → obsolete error → removal). Interfaces are frozen;
  they are never broken within a major.

How a change lands in this file:

1. The PR that introduces the break must update `PublicAPI.*.txt` (the analyzer forces the
   diff to be explicit) and add the entry here in the same commit.
2. The CHANGELOG entry for the release links to the entry.
3. Release notes surface the entry titles.

## Entry template

Future entries use exactly this structure:

---

### `<Version>`: `<Short title of the change>`

**Affected packages:** `Koras.AI.<...>`

**Affected APIs:**

```text
Koras.AI.SomeType.SomeMember(...)   // removed / renamed / signature changed / behavior changed
```

**Kind:** source-breaking · binary-breaking · behavioral

**Why:** One short paragraph. What problem the old shape caused and why a compatible fix was
not possible.

**Before:**

```csharp
// code that compiled against the previous version
```

**After:**

```csharp
// the equivalent code on the new version
```

**Mechanical recipe:**

1. Ordered steps a developer (or a script) can follow — find/replace patterns, overload
   substitutions, options renames.
2. Configuration changes, if any (e.g. a renamed `Koras:AI:*` key), including the old→new
   key mapping.

**Behavioral notes:** Anything that changes at runtime without a compile error — changed
defaults, changed error codes for a wire condition, changed decorator ordering. Omit the
section if empty.

---

## What does *not* appear here

Non-breaking changes, even when noticeable:

- New `AiErrorCode` values (documented policy: always `default`-case your switches).
- New overloads, types, packages, or TFMs.
- Exception/log message wording (never parse message text).
- Bug fixes that make behavior match the documentation.

Those live in the [CHANGELOG](../../CHANGELOG.md) under `Added`/`Fixed`.

## See also

- [Upgrading](upgrading.md) — the practical upgrade workflow.
- [Backward compatibility policy](../api/backward-compatibility.md) — the normative rules
  and their enforcement (API analyzers, package validation, review gates).

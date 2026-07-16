# Compatibility Testing

Three compatibility surfaces are tested: target frameworks, the public API, and the packages
themselves.

## Target framework matrix

Libraries multi-target `net8.0;net9.0;net10.0` (set in
[`src/Directory.Build.props`](../../src/Directory.Build.props); no `netstandard2.0` by
ADR-0002). The unit test project multi-targets the same three TFMs, so the 207 unit tests run
**three times per test run** — once on each runtime — catching TFM-conditional regressions
(e.g., the `System.Text.Json` package reference that only net8.0 needs).

CI runs the full matrix on **ubuntu-latest and windows-latest**
([`test.yml`](../../.github/workflows/test.yml)), with .NET 8/9 runtimes installed alongside
the SDK pinned by `global.json`.

| Axis | Values |
|---|---|
| TFM | net8.0, net9.0, net10.0 |
| OS | Ubuntu, Windows |
| Configuration | Release, `TreatWarningsAsErrors=true` |

## Public API snapshot gate

`tests/Koras.AI.ArchitectureTests/PublicApiSurfaceTests.cs` renders every shipped assembly's
public surface via reflection and compares it against checked-in snapshots in
[`tests/Koras.AI.ArchitectureTests/PublicApi/`](../../tests/Koras.AI.ArchitectureTests/PublicApi)
(`<Assembly>.approved.txt`, all 9 shipping assemblies).

Any public-surface change — added member, changed signature, removed type — fails the test.
The intended-change workflow:

```bash
UPDATE_PUBLIC_API=1 dotnet test tests/Koras.AI.ArchitectureTests
git diff tests/Koras.AI.ArchitectureTests/PublicApi/   # review like any code change
```

Review the diff against the
[public API review checklist](../api/public-api-review-checklist.md) before committing the
regenerated snapshots. The snapshot diff in a PR **is** the API review artifact: reviewers see
exactly what surface is being shipped. See also
[backward-compatibility.md](../api/backward-compatibility.md) and
[versioning.md](../release/versioning.md) for what each kind of diff means for the version
number.

## Package validation

`EnablePackageValidation=true` is set for all packable projects. Once a baseline release
exists, `PackageValidationBaselineVersion` (see the placeholder comment in
[`src/Directory.Build.props`](../../src/Directory.Build.props)) makes `dotnet pack` fail on
binary-breaking changes against the last released version — a compile-time compatibility gate
on top of the reflection snapshots. Bumping the baseline is a post-release step in the
[release process](../release/release-process.md).

The package workflow ([`package.yml`](../../.github/workflows/package.yml)) additionally
verifies every `.nupkg` contains its README, icon, license expression, and XML docs.

## Local consumer smoke test

Before a release, prove the packages install and run as a real consumer would — from a local
NuGet source, not project references:

```bash
# 1. Pack to a local folder
dotnet pack -c Release -o /tmp/koras-packages

# 2. New console app using the local source
dotnet new console -n SmokeTest && cd SmokeTest
dotnet add package Koras.AI.OpenAI --source /tmp/koras-packages --prerelease

# 3. Wire up a client against a fake/echo endpoint or a real key, then:
dotnet run -f net8.0 && dotnet run -f net10.0
```

What this catches that project references cannot: missing package dependencies (a
`ProjectReference` that never became a `PackageReference`), TFM asset selection, README/icon
packaging, and SourceLink/symbols resolution. This step is part of the
[release checklist](../release/release-checklist.md).

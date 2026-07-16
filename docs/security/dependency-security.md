# Dependency Security

How the SDK keeps its supply chain small, pinned, audited, and hard to tamper with.

## Policy

The dependency policy lives in
[architecture/dependency-rules.md](../architecture/dependency-rules.md) and is enforced by
architecture tests. The essentials:

- The runtime dependency set is deliberately tiny: `Microsoft.Extensions.*` abstractions,
  `System.Text.Json`, and `OpenTelemetry.Api` (in the OTel wiring package only). Vendor AI
  SDKs are banned (ADR-0003); Polly/Microsoft.Extensions.Resilience deliberately not used
  (ADR-0008).
- Adding any `src/` dependency requires PR documentation: necessity, license
  (MIT/Apache-2.0/BSD-compatible), maintenance status, security posture, closure size, and
  confirmation that no third-party type leaks into our public API.

## Pinned versions — Central Package Management

Every package version in the repository is pinned in
[`Directory.Packages.props`](../../Directory.Packages.props)
(`ManagePackageVersionsCentrally=true`). No floating versions, no per-project drift: a version
bump is a reviewable one-line diff in one file.

## `NuGetAudit` — builds fail on known advisories

[`Directory.Build.props`](../../Directory.Build.props) sets:

```xml
<NuGetAudit>true</NuGetAudit>
<NuGetAuditMode>all</NuGetAuditMode>
```

With `TreatWarningsAsErrors=true`, any direct **or transitive** package with a known
vulnerability advisory fails the build — locally and in CI.

**This has already paid for itself.** During development, NuGetAudit failed the build against
`OpenTelemetry.Api` 1.11.1 and again on 1.12.0 when advisories were published for those
versions, forcing the pin forward before anything shipped. The check is not theoretical.

## CI enforcement

| Gate | Where | What it does |
|---|---|---|
| `dotnet list package --vulnerable --include-transitive` | [`build.yml`](../../.github/workflows/build.yml) | Fails the job if any vulnerable package is reported — a second, explicit net under NuGetAudit. |
| Dependency review | [`dependency-review.yml`](../../.github/workflows/dependency-review.yml) | On every PR: fails on newly introduced advisories of moderate+ severity and enforces a license allowlist (MIT, Apache-2.0, BSD-2/3-Clause, MS-PL, 0BSD). |
| CodeQL | [`codeql.yml`](../../.github/workflows/codeql.yml) | `security-and-quality` queries on push, PR, and a weekly schedule. |
| Dependabot | [`.github/dependabot.yml`](../../.github/dependabot.yml) | Weekly NuGet and GitHub Actions updates, grouped (`Microsoft.Extensions.*`+STJ as one PR; test dependencies as another) to keep review load sane. |

Dependabot proposes; CPM review disposes — every bump still goes through the normal PR gates
(build, tests, audit, dependency review).

## SBOM

No SBOM is published with releases yet. Two supported ways to produce one from a checkout:

- .NET SDK-integrated generation: `dotnet build -c Release --property:GenerateSBOM=true`
  (SPDX output under `bin/<config>/<tfm>/_manifest/`), or
- Microsoft's standalone tool: `sbom-tool generate -b artifacts/packages -pn Koras.AI -pv <version>`
  (`dotnet tool install --global Microsoft.Sbom.DotNetTool`).

Attaching an SBOM to GitHub releases is a roadmap item alongside package signing.

## Package integrity for consumers

Author-signature signing of packages is a **roadmap item** (post-1.0). What ships today:

- **Deterministic builds** (`Deterministic=true`, `ContinuousIntegrationBuild` in CI) — the
  same source produces bit-identical assemblies, so binaries can be independently verified
  against the tagged source.
- **SourceLink** (`PublishRepositoryUrl`, `EmbedUntrackedSources`) — debuggers step into the
  exact committed source.
- **snupkg symbol packages** on NuGet.org for every release.
- Packages are only ever published from the tag-triggered
  [`release.yml`](../../.github/workflows/release.yml) workflow through a protected
  environment — see [nuget-publishing.md](../release/nuget-publishing.md).

## What consumers should do

- Restore with `NuGetAudit` enabled in your own build (it is on by default in current SDKs).
- Pin the `Koras.AI.*` versions you consume; upgrade deliberately via your own dependency PRs.
- Watch this repo's security advisories (GitHub → Security → Advisories) and see
  [SECURITY.md](../../SECURITY.md) for supported versions.

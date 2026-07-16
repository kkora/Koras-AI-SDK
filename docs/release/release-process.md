# Release process

Releases are cut from `main` with all CI workflows green. Publishing is fully automated
from a version tag — never publish manually (see
[nuget-publishing.md](nuget-publishing.md)).

## Steps

1. **Version bump** — set `VersionPrefix`/`VersionSuffix` in `Directory.Build.props` on
   `main` (e.g. `0.1.0` + `preview.1`).
2. **CHANGELOG** — move the `[Unreleased]` content into a dated `[X.Y.Z]` section.
3. **Verify** — CI green on the release commit; locally: `dotnet build -c Release`,
   `dotnet test -c Release`, `dotnet format --verify-no-changes`.
4. **Tag** — annotated tag matching the version, prefixed with `v`:

   ```bash
   git tag -a vX.Y.Z -m "Koras.AI SDK X.Y.Z"
   git push origin vX.Y.Z
   ```

5. **Automation** — the tag triggers
   [`release.yml`](../../.github/workflows/release.yml), which restores, builds, tests,
   packs with the tag-derived version, publishes all packages to NuGet.org via Trusted
   Publishing, and creates the GitHub release with generated notes. The job runs in the
   protected `nuget-release` environment, so it waits for any configured required
   reviewers.
6. **After release** — run the "After release" section of the
   [security checklist](../security/security-checklist.md); verify the packages render
   correctly on NuGet.org.

Pre-1.0, breaking changes are allowed in minor versions with CHANGELOG migration notes —
see [backward-compatibility.md](../api/backward-compatibility.md).

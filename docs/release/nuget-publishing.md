# NuGet publishing

Packages are published to NuGet.org **only** by the tag-triggered
[`release.yml`](../../.github/workflows/release.yml) workflow running in the protected
`nuget-release` GitHub environment. Manual `dotnet nuget push` is never used.

## Trusted Publishing (no stored API key)

Publishing authenticates via [NuGet Trusted
Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing): the
workflow exchanges a GitHub OIDC token for a short-lived (1-hour, single-use) API key at
publish time. No long-lived NuGet API key exists, so there is nothing to store, rotate, or
leak.

Flow inside `release.yml`:

1. The `release` job requests an OIDC token (`permissions: id-token: write`).
2. `NuGet/login@v1` (with `user: kora.kanchan`, the nuget.org profile that owns the
   policy) sends the token to nuget.org, which validates it against the trusted
   publishing policy and returns a temporary API key.
3. `dotnet nuget push` publishes with that key. The login step runs immediately before
   the push so the key cannot expire mid-release.

## nuget.org policy configuration

The trusted publishing policy lives under the `kora.kanchan` nuget.org profile →
**Trusted Publishing**, and must pin exactly:

| Field | Value |
|---|---|
| Repository owner | `kkora` |
| Repository | `Koras-AI-SDK` |
| Workflow file | `release.yml` (file name only) |
| Environment | `nuget-release` |

New policies may start as *temporarily active* for 7 days until the first successful
publish permanently binds them to the repository's GitHub IDs; re-arm the window from the
nuget.org UI if it lapses before the first release.

## GitHub environment

The `nuget-release` environment exists for approval gating: configure required reviewers
in the repository settings (Settings → Environments → `nuget-release`) so a tag push
cannot publish
without sign-off. The environment holds no secrets.

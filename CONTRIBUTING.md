# Contributing to Koras.AI

Thanks for helping build the provider-neutral AI SDK for .NET.

## Getting started

```bash
git clone https://github.com/korastechnologies/koras-ai-sdk.git
cd koras-ai-sdk
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

Requirements: .NET SDK 10.0.1xx (see `global.json`); runtimes for net8.0/net9.0 to run the
full test matrix locally (CI runs it regardless).

## Good first contributions

1. **Documentation** — fixes and clarifications in `docs/`.
2. **Provider fixtures** — real (sanitized!) wire captures in `tests/**/Fixtures/` that expose
   mapping gaps.
3. **Bug fixes** — start from a failing test.
4. **New providers** — the best large contribution. Read
   `docs/features/custom-providers.md` and mirror an existing provider package + its test
   suite; your provider must pass the shared contract tests.

## Ground rules

- Read `CLAUDE.md` (the repo working agreement) and `docs/planning/definition-of-done.md`.
- Public API changes: update the API snapshot (`tests/Koras.AI.ArchitectureTests/PublicApi/`),
  complete `docs/api/public-api-review-checklist.md`, and expect design discussion first —
  open an issue before large surface changes.
- New dependencies require the review in `docs/architecture/dependency-rules.md` (usually: no).
- Never include real API keys anywhere, including fixtures and test names. Use `sk-test-*`.

## PR checklist

- [ ] `dotnet build -c Release` warning-free, `dotnet test -c Release` green
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Tests cover happy path + failure + cancellation for the change
- [ ] Docs + CHANGELOG (`[Unreleased]`) updated
- [ ] No secrets, no placeholder code, no unrelated churn

## Commit style

Conventional prefixes: `feat:`, `fix:`, `docs:`, `test:`, `chore:`, `perf:`, `refactor:`.
One logical change per commit.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). Be excellent to each other.

## Questions

Open a [discussion or issue](https://github.com/korastechnologies/koras-ai-sdk/issues) — see
[SUPPORT.md](SUPPORT.md).

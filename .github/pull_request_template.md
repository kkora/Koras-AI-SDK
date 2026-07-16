# Pull Request

## What & why

<!-- Describe the change and the problem it solves. Link related issues: Fixes #123 -->

## Type

- [ ] Bug fix
- [ ] Feature
- [ ] Documentation
- [ ] Infrastructure / CI
- [ ] Breaking change (requires migration notes + maintainer sign-off)

## Checklist (see docs/planning/definition-of-done.md)

- [ ] `dotnet build -c Release` warning-free on all TFMs
- [ ] `dotnet test -c Release` green (unit, integration, architecture)
- [ ] `dotnet format --verify-no-changes` clean
- [ ] Tests cover happy path + failure paths + cancellation
- [ ] Public API snapshot updated deliberately (if surface changed) + review checklist applied
- [ ] Docs updated (feature guide, configuration reference as applicable)
- [ ] CHANGELOG updated under `[Unreleased]`
- [ ] No secrets, no placeholder code (`TODO`, `NotImplementedException`)
- [ ] New dependencies documented per docs/architecture/dependency-rules.md (usually: none)

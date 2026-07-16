# Definition of Done

## Per task/feature

- [ ] Code compiles warning-free (`TreatWarningsAsErrors=true`) on all TFMs.
- [ ] Unit tests: happy path, invalid input, boundary, failure paths, cancellation; thread-safety
      where the type is documented singleton-safe.
- [ ] Integration/contract tests where the feature touches a provider or the full pipeline.
- [ ] Public API: XML docs complete; `PublicAPI.Unshipped.txt` updated deliberately;
      review checklist applied.
- [ ] No `NotImplementedException`, `TODO`, placeholder returns, or swallowed exceptions.
- [ ] No blocking calls (`.Result`, `.Wait()`, `Thread.Sleep`) — analyzer + review enforced.
- [ ] Errors surface as `AiException` with correct `AiErrorCode`; secrets absent from all
      diagnostic output.
- [ ] Docs: feature guide updated; sample updated if the primary path changed.
- [ ] CHANGELOG entry under `[Unreleased]`.

## Per milestone

- [ ] `dotnet build -c Release` and `dotnet test -c Release` green locally and in CI.
- [ ] `dotnet format --verify-no-changes` clean.
- [ ] Architecture tests green (dependency rules).
- [ ] Committed with a descriptive message; pushed to the feature branch.

## Per release (adds)

- [ ] `dotnet pack` artifacts validated (contents, README, icon, symbols, SourceLink).
- [ ] `dotnet list package --vulnerable --include-transitive` clean.
- [ ] Package-validation baseline updated; PublicAPI Shipped/Unshipped rolled.
- [ ] Release notes + migration notes (if any) published; versioning policy honored.

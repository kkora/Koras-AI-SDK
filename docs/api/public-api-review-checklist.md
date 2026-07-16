# Public API Review Checklist

Apply to every PR touching `PublicAPI.Unshipped.txt`.

## Necessity
- [ ] Could this be internal? (Default answer: yes — justify public.)
- [ ] Does an existing type/member/extension already express this?
- [ ] Is it needed by a documented use case or contract test, not "might be useful"?

## Shape
- [ ] Follows [naming-guidelines](naming-guidelines.md) (suffixes, Async, parameter order).
- [ ] Async has `CancellationToken` last, defaulted; returns `Task`/`ValueTask`/`IAsyncEnumerable`.
- [ ] No boolean parameters where an options type/enum reads better.
- [ ] No third-party types in the signature (BCL + sanctioned framework namespaces only).
- [ ] Nullability annotations reflect real contracts (no `!` suppressions in public paths).
- [ ] Thread-safety documented if the type holds state.
- [ ] XML docs on every public member: summary + params + exceptions + example where non-obvious.

## Evolution
- [ ] Interface change? → must be a new interface or extension method (interfaces are frozen).
- [ ] Could a future provider/feature force a breaking change here? (Prefer extensible-enum
      record structs, options types, and `AdditionalProperties` escape hatches.)
- [ ] Errors surface as `AiException` with an existing `AiErrorCode` (new codes need docs).
- [ ] Defaults are safe (secure, bounded, non-surprising) and documented.

## Verification
- [ ] `PublicAPI.Unshipped.txt` diff matches intent exactly (nothing accidental).
- [ ] Unit tests cover the new surface incl. failure paths and cancellation.
- [ ] Feature guide / API reference updated; sample updated if the 5-minute path changed.
- [ ] CHANGELOG entry under Unreleased.

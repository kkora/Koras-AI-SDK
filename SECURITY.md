# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| latest minor of the current major | ✅ security fixes |
| previous major (12 months after next major GA) | ✅ critical fixes |
| older | ❌ |

## Reporting a vulnerability

**Please do not open public issues for security vulnerabilities.**

Report privately via **GitHub Security Advisories**
(https://github.com/korastechnologies/koras-ai-sdk/security/advisories/new) or email
`security@korastechnologies.example`.

Include: affected package(s) and version(s), reproduction steps or PoC, and impact assessment.

You can expect: acknowledgement within 3 business days, a triage decision within 10, and
coordinated disclosure after a fix ships. Credit is given unless you prefer otherwise.

## Scope notes

- The SDK never stores or transmits credentials except in the authenticated request to the
  configured provider endpoint; secrets are excluded from logs, exceptions, and telemetry by
  design — leaks of that kind are vulnerabilities, please report them.
- Prompt-injection resistance of *models* is out of scope; SDK behaviors that make injection
  worse (e.g., executing undeclared tools) are in scope.
- See `docs/security/threat-model.md` for the full threat model.

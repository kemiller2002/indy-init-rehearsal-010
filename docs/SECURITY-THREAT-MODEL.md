# Security, Privacy, and Threat Model

Status: normative security architecture direction.

The platform may contain sensitive operational, customer, engineering, research, or organizational information. Security cannot be treated as a GitHub-authentication detail.

This document defines threat boundaries that must remain valid as the product moves from hackathon deployment to long-term use.

## 1. Security objectives

Protect:

- confidentiality of investigations and evidence
- integrity of authoritative investigation state
- integrity of provenance and history
- authentication credentials and AI/provider secrets
- authorization boundaries
- extension/domain-pack trust boundaries
- report/export confidentiality
- local offline data
- availability/recoverability
- user control over external data disclosure

## 2. Trust boundaries

Primary trust boundaries:

```text
User
  ↓
Browser/UI
  ↓
Limen/domain runtime
  ↓
Local durable store
  ↓
Persistence adapter
  ↓
GitHub / future provider

Browser/domain runtime
  ↓
AI adapter
  ↓
AI provider

Browser/domain runtime
  ↓
External evidence/import adapter
  ↓
Untrusted external content

Domain runtime
  ↓
Domain pack / extension
```

Data crossing a boundary must carry explicit trust assumptions.

## 3. Threat actors

Consider:

- unauthorized external attacker
- malicious or compromised external evidence source
- compromised browser/session
- user with legitimate access exceeding intended scope
- malicious collaborator
- compromised AI/tool provider
- malicious/buggy domain pack
- supply-chain compromise
- accidental user error
- stale/offline client
- compromised persistence credentials

## 4. Authentication

Production-capable GitHub deployment should prefer:

- GitHub App or OAuth
- short-lived/session-scoped credentials where practical
- PKCE for browser-based OAuth where applicable
- explicit repository authorization
- revocation support

Long-lived personal access tokens are a temporary/developer mode, not the target product authentication model.

## 5. Credential handling

Credentials must never be:

- committed to repositories
- written into investigation events
- embedded in reports
- included in AI prompts unnecessarily
- logged in telemetry
- placed in shareable URLs
- persisted in plaintext local storage when a safer supported alternative exists

## 6. Local credential posture

If browser-only architecture cannot safely retain a credential long-term, prefer reauthentication or a minimal backend/token-exchange design over pretending LocalStorage is secure.

The hackathon may use an explicitly documented temporary approach only if organizer constraints require it.

## 7. Authorization

Authorization should be evaluated independently from semantic legality.

Access dimensions may include:

- workspace read
- investigation read
- evidence/artifact read
- propose
- authoritative write
- confirm cause
- accept residual uncertainty
- approve/review
- manage policy/domain pack
- export
- redact/delete
- administer integrations

Exact role taxonomy remains deployment-specific.

## 8. Least privilege

Persistence and integration credentials request the minimum required capabilities.

For GitHub, avoid broad account/repository access when one selected repository is sufficient.

## 9. Repository selection

The UI must clearly identify which repository/workspace will receive investigation data before first write.

Switching repositories must not silently move or duplicate investigation data.

## 10. Data classification

The platform should support classification metadata such as:

- Public
- Internal
- Confidential
- Restricted
- Custom domain classification

Classification can influence:

- AI use
- export
- external links/tools
- retention
- collaboration
- report sharing

## 11. AI disclosure policy

Before sending investigation data to an AI provider, the system should evaluate:

- data classification
- provider policy/configuration
- organization/domain restrictions
- user authorization
- minimum necessary context

A deployment may forbid AI use for certain classifications.

## 12. Context minimization

The bounded context builder is also a privacy control.

Do not send:

- unrelated investigations
- hidden workspace metadata
- secrets
- unnecessary full artifacts
- personally identifying fields not needed for the task

when a smaller projection is sufficient.

## 13. Prompt injection

All external/imported evidence is untrusted.

Threats include evidence containing:

- "ignore system instructions"
- fake tool calls
- fake policy text
- malicious markdown/HTML
- instructions to exfiltrate data

Mitigations:

- strong instruction/data separation
- structured evidence framing
- tool allowlists
- narrow write tools
- domain command validation
- output schema validation
- no trust promotion from evidence text

## 14. Indirect prompt injection via URLs

Fetching a URL for evidence must not grant the fetched content authority.

AI/tool adapters should treat content as untrusted data regardless of source domain.

## 15. External URL safety

Evidence URLs can expose users to phishing or malicious content.

UI should:

- show target origin/domain
- avoid executing arbitrary embedded content
- open external content with appropriate browser protections
- avoid passing secrets in query strings/referrers when controllable

## 16. HTML/Markdown rendering

User/imported/AI-generated rich text must be sanitized.

Do not allow arbitrary scripts, event handlers, unsafe URLs, or trusted HTML injection through evidence/comments/reports.

Prefer semantic plain text/limited markdown where possible.

## 17. Content Security Policy

Static/web deployments should use a restrictive CSP compatible with required features.

Goals:

- prevent inline arbitrary script
- restrict script origins
- restrict network destinations
- restrict frame embedding
- restrict object/embed
- constrain form destinations

Any required exceptions must be documented.

## 18. Cross-site scripting

XSS is especially dangerous because the browser may hold:

- investigation data
- auth/session state
- pending offline commands

All rendered external/user/AI text is untrusted.

Forma components must not create unsafe HTML sinks.

## 19. CSRF and request integrity

If a service-backed deployment uses cookies/session credentials, state-changing endpoints require appropriate CSRF protection.

Browser-only bearer-token deployments need different protections but remain vulnerable to XSS/token theft.

## 20. Clickjacking

Where appropriate, prevent the application from being framed by untrusted origins through CSP/frame-ancestors or equivalent.

## 21. Local durable data

Offline storage may contain sensitive investigation state.

The UI must expose that the device holds local data when relevant.

Future deployments may require:

- encrypted local data
- user/device lock integration
- configurable offline disablement
- local purge/logout behavior

The first release must not falsely claim secure-at-rest guarantees it does not provide.

## 22. Shared-device risk

Logging out should:

- end active authentication/session
- make credential state unusable
- provide a defined policy for local cached investigation data

Deleting local data on logout may be configurable because unsynced work could otherwise be destroyed.

The product must explain the tradeoff.

## 23. Persistence integrity

Attackers or accidental edits may modify repository files outside the app.

On load, validate:

- manifest versions
- event identity/order
- required extension schemas
- snapshot/history agreement where possible
- integrity digests where enabled

Do not ask AI to "repair" malformed authority automatically.

## 24. GitHub manual edits

Human-readable GitHub persistence is useful, but direct repository edits create integrity risks.

Policy should define:

- supported direct edits, if any
- detection of unsupported modifications
- reconciliation path
- whether signed/validated events are required in stronger deployments

## 25. Event tampering

If stronger tamper evidence is required, use:

- append-only repository governance
- protected branches
- signed commits where appropriate
- event hash chaining
- server-side authorization

These are deployment controls, not replacements for semantic validation.

## 26. Branch protection

Production GitHub-backed deployments should consider protected branches or a dedicated data branch when human Git editing could corrupt investigation data.

Exact configuration is deployment policy.

## 27. Extension/domain-pack security

A pack declares:

- semantic capabilities
- external network capabilities
- AI/tool capabilities
- data access scope
- executable code requirements

The host must not grant undeclared capability implicitly.

## 28. Executable extension isolation

If dynamic executable packs are supported later:

- sandbox where practical
- narrow host APIs
- resource/time limits
- explicit permissions
- signed/trusted distribution policy
- failure isolation
- no direct secret access
- no direct persistence mutation

For the initial product, compile-time/trusted packs are preferred.

## 29. Supply chain

Pin/verify important dependencies and reusable Echelon components.

Maintain:

- dependency inventory
- version provenance
- update policy
- vulnerability response
- reproducible/build evidence where practical

Avoid unnecessary dependencies.

## 30. AI tool security

AI tools should be capability-scoped.

Examples:

Safe:
- read selected evidence
- propose hypothesis
- request GitHub read for configured repo

Higher risk:
- send external message
- mutate external system
- execute remediation

Higher-risk actions require explicit product requirements and review.

## 31. SSRF

If a backend fetches user-supplied evidence URLs, protect against SSRF:

- restrict protocols
- block local/private metadata ranges
- enforce redirect policy
- limit response size/time
- sanitize content types

Browser-only URL opening has a different threat model.

## 32. Malicious file import

Imported files may be malformed or huge.

Requirements:

- type/size limits
- parsing limits
- fail closed on malformed critical structures
- no macro/code execution
- preserve raw source provenance when allowed
- isolate parser failure

## 33. Denial of service

Bound:

- imported artifact sizes
- event batch size
- number of AI proposals returned
- context size
- rendering lists
- recursive extension depth
- graph traversal
- policy evaluation complexity

Large investigations should degrade predictably rather than hang the client.

## 34. Sensitive logging

Operational logs must not routinely contain:

- full evidence text
- credentials
- complete AI prompts
- personal/sensitive artifacts
- raw report contents

Use identifiers/correlation IDs where possible.

## 35. AI prompt retention

Provider-side prompt/data retention varies by provider/configuration.

Deployment policy must record which provider/configuration is approved for which data classifications.

Do not make generic privacy promises that depend on an unverified provider configuration.

## 36. Reports

Reports can leak more than the app because they are easy to share.

Before report generation/export:

- apply access policy
- apply redaction rules
- indicate classification where configured
- avoid embedding secrets/internal auth URLs
- preserve source version
- distinguish external artifact links

## 37. Export control

Complete exports may contain all investigation history, including superseded/falsified material.

Export capability should be separately authorizable from ordinary read access in deployments that need stronger controls.

## 38. Redaction

Redaction should be a privileged action.

It must preserve enough audit metadata to explain:

- what record was affected
- who authorized redaction
- why
- when
- whether content was actually deleted versus hidden

## 39. Deletion

Privacy/legal deletion may conflict with audit reconstruction.

The product must support explicit policy choices and must not claim both complete deletion and complete reconstructability when those are incompatible.

## 40. Retention

Retention should be configurable by deployment/workspace.

Possible policy targets:

- investigations
- chat transcripts
- AI traces
- local cache
- reports
- external artifact metadata
- audit logs

## 41. Multi-tenancy

Future managed service must enforce tenant/workspace isolation at storage/query/authorization boundaries.

Do not rely on UI filtering alone.

Multi-tenant implementation remains deferred, but core IDs and APIs must not assume one global trusted user.

## 42. Collaboration trust

A collaborator with write access may introduce misleading evidence or conclusions.

Mitigate with:

- provenance
- review policy
- immutable history
- permission separation
- evidence verification state
- independent review when required

The system does not treat "authorized user" as "factually correct."

## 43. Session expiration

Expired authorization during offline work must not destroy local commands.

On reconnect, commands may require reauthentication and reauthorization before sync.

## 44. Revocation

If repository/workspace access is revoked:

- remote access stops
- queued remote writes stop
- UI explains authorization state
- local sensitive cache follows configured policy

Do not repeatedly retry forbidden writes indefinitely.

## 45. Rate limits

GitHub/AI/provider rate limits are operational faults, not semantic rejection.

Queued work remains recoverable.

## 46. Replay

Authenticated command interfaces should protect against accidental or malicious replay through:

- command IDs
- idempotency keys
- expected aggregate versions
- authorization checks
- expiry where appropriate

## 47. Audit

Security-sensitive events may include:

- login/session changes where available
- provider connection changes
- permission/policy changes
- domain-pack activation
- export
- redaction/deletion
- review/approval
- investigation reopen after resolution
- conflict recovery

Security audit is distinct from semantic investigation history but may reference the same entities.

## 48. Fault information disclosure

Aegis faults shown to ordinary users must be useful without exposing:

- tokens
- internal stack traces
- secret configuration
- unrelated tenant/workspace data

Detailed diagnostics can be separately authorized.

## 49. Safe defaults

Default posture:

- least privilege
- no external write tools
- no arbitrary executable extensions
- sanitize all untrusted content
- explicit AI data disclosure
- explicit export
- no silent semantic repair
- preserve offline work
- fail closed for unknown required semantics

## 50. Hackathon security profile

The hackathon may use a narrower deployment profile.

Any shortcut must be recorded with:

- exact shortcut
- risk
- demo-only limitation
- production replacement
- evidence that the shortcut does not become core semantic dependency

## 51. Threat-model review triggers

Re-review this model when adding:

- new persistence provider
- dynamic domain packs
- service backend
- multi-tenancy
- external URL fetch
- binary file ingestion
- messaging/actions into external systems
- automatic remediation
- new AI provider/tool
- public sharing
- authentication changes

## 52. Completion criterion

The architecture is security-ready when a compromise or mistake in one boundary cannot silently become authoritative investigation truth across all boundaries.

In particular:

- evidence cannot become instructions
- AI cannot bypass policy
- pack cannot bypass core invariants
- stale client cannot overwrite newer state
- report/export cannot silently ignore access policy
- provider metadata cannot redefine semantic identity


## 53. Tool execution security

Tool execution must follow least privilege.

Required controls:

- host-issued scoped capabilities
- declared resource/network scope
- time/resource limits
- input/output schema validation
- data-classification enforcement
- secret isolation
- invocation provenance
- side-effect classification
- review gates

The model cannot elevate a read capability into write authority.

## 54. Autonomous-session security

Autonomous sessions must have explicit:

- allowed tools
- allowed resources
- command classes
- budget
- expiry
- review gates
- stop conditions

A compromised or confused model therefore remains bounded by host enforcement.

High-consequence remediation is outside the default competition autonomy profile.

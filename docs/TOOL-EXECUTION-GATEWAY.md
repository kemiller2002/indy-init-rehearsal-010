# Tool Execution Gateway

Status: ambitious reusable execution architecture.

The investigation platform should support tools that collect evidence, execute bounded experiments, and perform analysis without giving AI unrestricted access to external systems.

## 1. Principle

AI does not receive arbitrary shell/network/system access.

It receives explicit capabilities through a Tool Execution Gateway.

Every tool declares:

- ToolId/version
- purpose
- input schema
- output schema
- read/write/side-effect classification
- required authorization
- required review
- timeout/resource limits
- allowed network/resource scope
- data-classification limits
- provenance behavior
- reproducibility metadata

## 2. Tool classes

### Pure analysis
No external side effects.

Examples:

- hash artifact
- compare files
- parse logs
- calculate metrics
- graph analysis
- semantic diff
- search current investigation state

### External read
Reads external state.

Examples:

- GitHub file/commit
- observability query
- database read-only query
- HTTP evidence fetch
- issue tracker lookup

### Reversible write
Changes external state with clear rollback.

Examples:
- create temporary branch
- create sandbox test artifact

### Irreversible/high consequence
Examples:
- production configuration change
- external notification
- data deletion
- remediation execution

These require explicit future product requirements and strong review. They are not implied by generic investigator access.

## 3. Capability token

A tool invocation should receive a scoped capability:

```text
ToolCapability
- ToolId
- Actor/AgentRunId
- InvestigationId
- AllowedResourceScope
- AllowedOperation
- Expiry
- MaxInvocations
- DataClassification
- ReviewRef optional
```

The capability is host-generated, not model-generated authority.

## 4. Invocation record

Record:

- InvocationId
- ToolId/version
- investigation/version
- actor/agent
- input digest or safe summary
- start/end
- outcome
- output artifact refs
- cost/resource usage
- fault
- capability used

Secrets are redacted.

## 5. Tool output semantics

Tool output is not automatically authoritative evidence.

Pipeline:

```text
Tool Output
  -> Finding
  -> validation/inspection
  -> Evidence admission
  -> Evidence relationships
```

Policy may auto-admit certain deterministic low-risk tool outputs if explicitly configured.

## 6. Dry run

Tools with side effects should support dry-run where meaningful.

The user/agent can inspect:

- intended resources
- expected effect
- permissions
- rollback
- risks

before execution.

## 7. Deterministic tool preference

When a question can be answered deterministically, prefer tool/local computation over model inference.

Examples:

- checksum equality
- event ordering
- file diff
- graph cycle detection
- numeric comparison
- schema validation

## 8. Tool provenance as evidence lineage

If Evidence E comes from ToolInvocation T, preserve:

```text
SourceArtifact -> ToolInvocation -> Finding -> Evidence
```

This allows reproduction and audit.

## 9. Tool failure

Failure yields:

- structured execution result
- Aegis-compatible fault
- optional InvestigationAction blocked/failed state
- no fabricated finding

The planner may propose a fallback tool/action.

## 10. Tool result integrity

Where output artifacts are stable, record digest.

If output is a remote query:

- query/input identity
- source system
- source version/time
- result digest when practical

## 11. Read-only database query

Future adapter may execute parameterized/read-only queries.

Requirements:

- no arbitrary write statements
- query timeout/row limit
- connection scope
- secrets isolated
- query recorded
- result artifact/provenance
- data classification

## 12. GitHub evidence tool

Useful competition tools:

- fetch repository file
- fetch commit/diff
- search commits/issues
- read workflow status
- read test/metric artifacts

Results enter as external records/findings.

## 13. Observability tool

Future:

- logs
- metrics
- traces
- deployment history

Query result should be captured as artifact/evidence with time range and query provenance.

## 14. Browser/local tool

Limen/WASM can execute:

- parsing
- hashing
- compression
- graph algorithms
- local search
- semantic projections
- report data preparation

No network needed.

## 15. Tool registry

The semantic compiler can generate/validate:

- ToolId
- schema
- role permissions
- AI description
- capability requirements
- test fixtures

## 16. AI tool description

Model sees:

- what tool does
- allowed inputs
- constraints
- side-effect level
- expected output

Model does not see secrets.

## 17. Tool selection

Planner/AI may propose a tool invocation as an InvestigationAction.

Normal command/policy flow authorizes it.

## 18. Competition demonstration

Example:

1. AI proposes "compare configuration at good vs bad commit."
2. user accepts action.
3. GitHub read tool fetches both files.
4. local WASM diff computes deterministic changes.
5. Finding captured.
6. Evidence relationship proposed.
7. hypothesis posture updates.

This is much stronger than asking the LLM to imagine what changed.

# Persistence, Portability, and Recovery Contract

Status: normative persistence architecture direction.

The product must preserve investigation meaning independently of any storage provider. GitHub is the first provider and is useful because it gives auditable history, but GitHub must not become the semantic model.

## 1. Three layers of persisted information

Separate:

1. **Authoritative semantic history**: what state changes occurred.
2. **Current projection/snapshot**: efficient representation of current state.
3. **Provider metadata**: GitHub SHA, database etag, service revision, local cache keys.

Provider metadata must not leak into generic domain identity.

## 2. Canonical portable package

A complete portable investigation package should be able to contain:

```text
manifest.json
investigation/
  snapshot.json
  events.ndjson
extensions/
  ...
reports/
  manifest.json
artifacts/
  manifest.json
integrity/
  manifest.json
```

The exact physical layout is ReversibleChoice.

The semantic contents and round-trip guarantees are the important contract.

## 3. Package manifest

The manifest should identify:

- package format version
- InvestigationId
- exported aggregate version
- export timestamp
- exporter identity/version
- core schema/semantic version
- domain packs
- policy references
- required/optional semantic dependencies
- included sections
- external artifact references
- integrity information
- redaction status
- encryption status if applicable

## 4. Canonical serialization

Requirements:

- deterministic field semantics
- stable IDs
- explicit versions
- explicit null/unknown semantics where material
- extension passthrough
- no provider-specific identity requirement
- documented date/time representation
- documented ordering rules where order has meaning
- unknown fields preserved when possible

Serialization convenience must not redefine domain meaning.

## 5. Event envelope

Every semantic event should serialize with:

- EventId
- InvestigationId
- AggregateVersion
- EventType semantic identifier
- occurred/recorded timestamps as applicable
- Actor
- PolicyRefs
- SemanticManifest
- CorrelationId optional
- CausationId optional
- Payload
- Provenance
- Integrity metadata optional

## 6. Event ordering

AggregateVersion is the authoritative logical order within one investigation.

Wall-clock timestamps must not be relied upon to establish exact semantic order.

## 7. Snapshot envelope

A snapshot should identify:

- InvestigationId
- AggregateVersion represented
- projection version
- semantic manifest
- snapshot generated-at
- integrity metadata

A snapshot is invalid if its aggregate version does not match its declared source history.

## 8. Snapshot authority

Snapshots are convenience, not independent truth.

When authoritative event history is available:

- a snapshot may be regenerated
- a stale snapshot may be discarded
- a snapshot cannot introduce events that do not exist in authoritative history

A persistence provider that chooses snapshot-only storage must explicitly accept the reduced reconstruction/audit guarantees.

## 9. Provider contract

Logical provider capabilities:

```text
LoadCurrent(investigationId)
LoadHistory(investigationId, range)
Append(expectedVersion, events)
StoreSnapshot(expectedVersion, snapshot)
ListInvestigations(workspace/query)
StoreArtifactReference(...)
Export(...)
Import(...)
Health(...)
```

Not every provider must implement every optional capability, but capability support must be discoverable.

## 10. Provider capability declaration

A provider should declare capabilities such as:

- EventHistory
- Snapshots
- OptimisticConcurrency
- AtomicAppend
- Search
- Transactions
- OfflineSync
- ArtifactStorage
- ImmutableHistory
- BackupRestore
- EncryptionAtRest
- ServerSideAuthorization

Product behavior adapts explicitly rather than assuming every backend behaves like GitHub.

## 11. GitHub provider mapping

Initial GitHub design may use:

```text
investigations/<id>/
  manifest.json
  current.json
  events.ndjson
  extensions/
  artifacts.json
  reports/
```

But the adapter owns these paths.

The generic domain receives:

- investigation data
- aggregate version
- provider version token

It does not receive GitHub-specific paths as domain concepts.

## 12. GitHub concurrency

GitHub adapter may use file/blob SHA or commit base as provider version token.

A write must compare against the expected remote provider version.

If it changed:

- do not overwrite
- return provider conflict
- preserve local commands
- load remote state
- revalidate queued commands semantically

## 13. Atomicity

A provider should make a semantic transition appear atomically committed.

If one domain decision emits multiple events, partial remote persistence must not be treated as success.

For GitHub, this may require committing all related file changes in one commit/tree operation rather than sequential independent writes.

## 14. Commit message semantics

GitHub commit messages are useful audit metadata but are not the semantic event source.

The semantic event payload remains authoritative.

## 15. Append integrity

Where events are append-oriented:

- prior event sequence cannot be silently rewritten
- AggregateVersion must be monotonic
- EventId must be unique
- duplicate idempotent append is detectable
- gaps/duplicates must be detectable

## 16. Hash chaining

Optional integrity enhancement:

Each event or event batch may include a digest referencing prior authoritative digest.

This can detect accidental or malicious rewriting in exports/providers that do not themselves guarantee immutable history.

Hash chaining is an integrity mechanism, not a substitute for signatures or access control.

## 17. Artifact references

Large evidence artifacts should use typed references.

Fields may include:

- ArtifactId
- URI/location
- media type
- size
- digest
- captured-at
- provenance
- access classification
- availability state
- retention metadata

A missing external artifact does not erase the evidence record; availability becomes explicit.

## 18. Artifact integrity

When practical, externally referenced evidence should record a cryptographic digest at admission time.

Later retrieval can distinguish:

- available and matching
- available but changed
- unavailable
- digest not recorded

Changed external content must not silently replace what was originally evaluated.

## 19. Offline local store

The browser local store should contain:

- last confirmed authoritative snapshot
- provider version token
- local queued commands
- optimistic local projection
- synchronization state
- necessary extension payloads/manifests
- minimal metadata required to recover after restart

Secrets should not be stored there unless explicitly justified by the authentication design.

## 20. Local truth distinction

The client should distinguish:

- RemoteConfirmed
- LocalAppliedPending
- Conflict
- SyncFailed
- RevalidationRequired

A user should be able to tell whether a change exists only on the device.

## 21. Restart recovery

After application restart:

1. load last durable local state
2. show last known remote confirmation state
3. recover queued commands
4. reconnect provider when possible
5. fetch remote version
6. revalidate queue
7. sync or surface conflicts

Unsynced work must not disappear merely because the browser restarted.

## 22. Queue durability

A queued command should not be removed until:

- remote append is confirmed, or
- the user explicitly discards it, or
- reconciliation replaces it with a new command while preserving history of the failed/stale intent when policy requires

## 23. Conflict classes

Conflicts should distinguish:

- ProviderVersionConflict
- SemanticConflict
- ExtensionVersionConflict
- PolicyVersionConflict
- PermissionConflict
- ArtifactConflict

Not every conflict is a text merge.

## 24. Semantic merge

The product should prefer command/state reconciliation over raw JSON merge.

Examples:

- two users independently add evidence: often commutative
- one user falsifies H1 while another edits H1 wording: requires semantic review
- one user resolves an unknown while another marks it blocking: requires policy/state reconciliation

Generic merge algorithms must not silently decide semantic conflicts.

## 25. Commutativity metadata

Commands/events may declare whether operations are known to commute safely.

This can enable automatic reconciliation for independent additions.

Example candidates:

- add separate Observation
- add unrelated Evidence
- add comment/discussion

Protected transitions should default to revalidation.

## 26. Import

Import must validate:

- package format
- schema compatibility
- semantic dependencies
- IDs
- event sequence
- integrity metadata
- extension availability
- policy references
- collisions with existing InvestigationId

Import cannot silently assign new semantic meaning.

## 27. Import identity collision

If InvestigationId already exists, options should be explicit:

- reject duplicate
- recognize same history/version
- import as related copy with a new ID and provenance
- reconcile through supported merge flow

Never silently overwrite.

## 28. Export

Export should support:

- complete authoritative package
- current snapshot only when user understands reduced history
- redacted package
- report-only output

These are different products and must be labeled accurately.

## 29. Round-trip guarantee

Complete canonical export followed by import into a compatible implementation must preserve:

- InvestigationId unless intentionally copied
- authoritative events
- entity IDs
- extension envelopes
- semantic/policy versions
- historical conclusions
- unresolved uncertainty
- provenance
- report snapshot references
- artifact metadata

## 30. Unknown field/extension round-trip

A compatible reader that does not understand optional extension semantics must preserve them if it permits save.

If it cannot preserve them, it must be read-only.

## 31. Migration

Migration must operate at explicit boundaries:

- package format migration
- core schema migration
- event upcast
- domain extension migration
- policy reassessment

These are not one generic "migration."

## 32. Backup

Provider-specific backup strategy is required for production use.

Backups should preserve:

- authoritative history
- manifests
- extension data
- artifact metadata
- provider-independent export ability

## 33. Restore

Restore must validate integrity before replacing authoritative state.

Restoring an older version over newer authoritative state requires explicit destructive/branching semantics.

## 34. Fork/branch investigation

The platform should support creating a new investigation derived from another at a known version.

Use cases:

- research comparison
- alternate policy analysis
- what-if reasoning
- anonymized copy

The derived investigation records:

- new InvestigationId
- source InvestigationId
- source AggregateVersion
- derivation reason
- copied semantic manifest
- subsequent independent history

This is different from rewriting the original.

## 35. Research snapshots

Research experiments should be able to pin an immutable evidence/investigation snapshot.

The experiment records the exact investigation version and semantic/policy versions used.

Later investigation changes do not alter the research input.

## 36. Deletion and archival

Archival is domain lifecycle.

Physical deletion is storage/governance behavior.

They must not be conflated.

Deletion behavior must account for:

- audit requirements
- legal/privacy requirements
- shared external artifacts
- derived indexes
- backups
- reports already issued

## 37. Redaction

Redaction may replace or hide sensitive content while preserving:

- that redaction occurred
- authority
- reason/category
- affected record
- timing
- integrity implications

Where policy requires true deletion, the system must not falsely claim complete historical reconstruction is still possible.

## 38. Search projections

Search indexes are derived.

They may contain:

- investigation metadata
- entity summaries
- tags
- current states
- unresolved obligations
- domain-specific indexed fields

Index corruption/loss cannot corrupt authoritative investigation state.

## 39. Report persistence

Issued report metadata should preserve:

- ReportId
- source InvestigationId
- source AggregateVersion
- report template/version
- domain pack/report projection versions
- generation timestamp
- digest/location if retained

Reports are historical outputs, not the source of domain truth.

## 40. Cross-provider migration

Moving an investigation from GitHub to another provider should be:

```text
Export canonical package
  -> validate
  -> import
  -> verify aggregate/version
  -> optionally switch provider pointer
```

Provider metadata changes.

Investigation semantics do not.

## 41. Availability degradation

If persistence is unavailable:

- active local investigation may continue within allowed offline policy
- changes remain clearly pending
- operations requiring remote-only data/capabilities are unavailable with explanation
- reports state whether they include unsynced local state when relevant

## 42. Provider failure

Provider failures should map to Aegis-compatible structured faults.

Distinguish:

- authentication
- authorization
- unavailable
- rate limited
- conflict
- integrity failure
- malformed persisted data
- unsupported provider capability

## 43. Integrity verification

On load, the adapter should validate enough structure to detect:

- impossible aggregate version
- malformed required manifest
- broken required extension payload
- duplicate event IDs
- missing required event sequence
- snapshot/event mismatch

Failure should not be papered over by AI reconstruction.

## 44. Corruption recovery

When persisted data is inconsistent:

1. do not silently "fix" authoritative history
2. identify the inconsistency
3. preserve recoverable raw data
4. reconstruct from trusted source where possible
5. produce recovery evidence
6. require explicit acceptance if recovery changes authoritative state

## 45. Canonical data contract ownership

The core investigation package belongs to the Root-Cause Investigator platform.

GitHub adapter, EDF pack, Folio, Forma, Limen, and AI providers consume it but do not independently redefine it.

## 46. First-release pragmatic subset

The hackathon implementation may choose:

- GitHub as only remote provider
- current snapshot + append event file
- IndexedDB/local browser persistence
- URL-referenced external artifacts
- simple export package

provided it preserves the boundaries required to add another provider later.

## 47. Completion criterion

Persistence architecture is healthy when:

- an investigation can move providers without changing its ID/meaning
- offline work survives restart
- stale writes never silently overwrite
- semantic conflicts are not reduced to text merge
- unknown extensions survive compatible round trips
- issued reports retain source version identity
- old investigations remain interpretable as schemas and policies evolve

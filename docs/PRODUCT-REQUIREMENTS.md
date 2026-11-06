# Product Requirements: Root-Cause Investigator Platform

Status: canonical long-term product requirements.

The Indy Init hackathon is the first externally constrained implementation of this product. Competition-specific constraints are an implementation profile and must not leak into the reusable investigation kernel.

## P0. Product identity and longevity

### P0.1 Reusable product
The Root-Cause Investigator must be designed as a reusable product that can operate after the hackathon without requiring a rewrite of its core domain model.

### P0.2 Competition as a profile
Hackathon-specific timing, provenance, demo, and blank-repository constraints must remain separable from core product behavior.

### P0.3 Domain-neutral kernel
The core investigation kernel must not depend on EDF, software engineering, hackathon, or Echelon-specific vocabulary.

### P0.4 First specialization
EDF should be supportable as the first domain specialization after the generic product exists, without forking the core investigation engine.

### P0.5 No disposable shortcuts
A hackathon shortcut may be accepted only when it is explicitly recorded as a temporary deviation with a defined replacement path.

## P1. Investigation model

### P1.1 Stable investigation identity
Every investigation must have a stable identifier independent of storage location.

### P1.2 Human-readable title and observation
Every investigation must retain both a concise human-readable title and the original observation/problem statement.

### P1.3 Investigation lifecycle
Investigations must support explicit persisted lifecycle states including Draft, Active, AwaitingEvidence, AwaitingVerification, Resolved, Closed, and Archived. Reopening must be represented as an explicit historical event that returns current lifecycle to an active state rather than erasing the prior resolution/closure.

### P1.4 Reopening
A resolved or closed investigation may be reopened only with an explicit reason and provenance.

### P1.5 Related investigations
Investigations must be linkable to predecessors, duplicates, regressions, related incidents, follow-ups, and superseding investigations.

### P1.6 Immutable historical meaning
Historical records must preserve the meaning they had when recorded. Later schema or interpretation changes must not silently rewrite prior facts.

## P2. Observations and claims

### P2.1 Observation is not inference
Direct observation, reported observation, interpretation, hypothesis, assumption, and conclusion must be distinct concepts.

### P2.2 Source preservation
The exact source statement from a user or imported artifact must be retained when the system creates a normalized or structured projection.

### P2.3 Claims
The system must represent material claims explicitly so evidence can support, contradict, qualify, or leave them unresolved.

### P2.4 Claim scope
Claims should identify the subject, applicable time or version range, and context when those dimensions materially affect meaning.

## P3. Hypotheses

### P3.1 Multiple competing hypotheses
The system must support multiple simultaneously plausible explanations.

### P3.2 Explicit lifecycle
Hypotheses must have explicit state and transition history.

### P3.3 No silent deletion
Rejected, weakened, falsified, duplicate, or superseded hypotheses must remain in history.

### P3.4 Reopening
A falsified or rejected hypothesis may be reopened only when new evidence or a corrected premise is explicitly linked.

### P3.5 Dependencies
A hypothesis may depend on other claims or hypotheses, and those dependencies must be visible.

### P3.6 Scope
The system must distinguish a proposed root cause from a contributing factor, triggering condition, enabling condition, symptom, and unrelated coincidence.

## P4. Evidence

### P4.1 First-class evidence
Evidence must be represented as first-class records rather than free-form notes only.

### P4.2 Provenance
Evidence should record source, author or origin when known, capture time, applicable system/version, and verification status.

### P4.3 Evidence type
The model should distinguish at least direct observation, measurement, log, document, experiment result, testimony/report, external reference, derived calculation, and AI-generated interpretation.

### P4.4 Evidence quality
Evidence may carry structured quality metadata such as reliability, completeness, precision, freshness, directness, and independence.

### P4.5 Evidence relationships
Evidence must be linkable to one or more claims or hypotheses with explicit relationship semantics.

### P4.6 Contradiction
Conflicting evidence must be retained and surfaced rather than resolved by overwriting one record.

### P4.7 Dependency
The system should record when multiple evidence items derive from the same underlying source so correlated evidence is not falsely treated as independent confirmation.

### P4.8 Supersession
Evidence may be superseded or corrected without disappearing from history.

### P4.9 External artifacts
Large or binary artifacts may be referenced externally while preserving metadata, integrity information where practical, and source location.

### P4.10 Chain of custody
Domains that require it must be able to record who captured, transformed, or verified evidence.

## P5. Unknowns, assumptions, and negative knowledge

### P5.1 Unknown is first-class
Unknown must be represented explicitly and must not collapse into false, missing, or unsupported.

### P5.2 Assumptions
Assumptions must be explicit and distinguishable from facts and evidence.

### P5.3 Assumption risk
An assumption may be blocking, non-blocking, accepted, rejected, or scheduled for verification.

### P5.4 Negative knowledge
Known false, ruled-out, unsupported, or eliminated explanations must be preserved.

### P5.5 Repetition prevention
Negative knowledge should prevent repeated pursuit of disproven paths unless new evidence changes the state.

### P5.6 Expiry
Unknowns, assumptions, and negative knowledge may have scope or expiry when their validity depends on version, environment, or time.

## P6. Experiments, checks, and questions

### P6.1 Unified investigation action
A test, inspection, query, measurement, interview question, comparison, simulation, or experiment must be representable as an investigation action.

### P6.2 Purpose
Every proposed action should identify what uncertainty, claim, or competing hypotheses it is intended to resolve.

### P6.3 Prediction before observation
Where meaningful, expected outcomes under competing hypotheses should be recorded before results are known.

### P6.4 Discrimination
The system should prefer actions that distinguish among plausible alternatives rather than only accumulating confirmatory evidence.

### P6.5 Cost and risk
An action may record cost, time, reversibility, operational risk, and required permissions.

### P6.6 Expected information value
The system should be able to rank or explain candidate next actions by expected discriminating value, cost, urgency, and risk without pretending to have unjustified numeric precision.

### P6.7 Result preservation
Inputs, conditions, expected outcomes, actual findings, interpretation, and anomalies must be retained.

### P6.8 Reproducibility
Where applicable, an experiment should preserve enough procedure and environment information to be repeated.

## P7. Causality and conclusion quality

### P7.1 Correlation is insufficient
The system must not treat temporal sequence, correlation, or plausibility alone as proof of causation.

### P7.2 Alternative explanations
Before confirming a root cause, materially plausible alternative explanations must either be addressed, falsified, or explicitly retained as residual uncertainty.

### P7.3 Counterfactual reasoning
Where practical, the investigation should record what would be expected if the proposed cause were absent or changed.

### P7.4 Mechanism
A confirmed root cause should include a defensible mechanism connecting the cause to the observed outcome when the domain permits one.

### P7.5 Contradictory evidence
Unresolved contradictory evidence must block confirmation when policy marks it as material.

### P7.6 Confidence semantics
Any confidence representation must be derived from explicit evidence, unresolved uncertainty, and policy. It must not be an unexplained LLM probability.

### P7.7 Residual uncertainty
A conclusion must preserve what remains unknown or weakly supported.

### P7.8 Conclusion status
The system should distinguish plausible explanation, leading explanation, supported cause, confirmed cause, and accepted-with-residual-uncertainty when domain policy requires those distinctions.

## P8. Corrective action and verification

### P8.1 Distinct concepts
Mitigation, workaround, corrective action, preventive action, and root-cause correction must be distinguishable.

### P8.2 Action ownership
Corrective actions may record owner, due date, dependencies, status, and verification responsibility.

### P8.3 Verification criteria
A corrective action must define observable success criteria before it can be considered verified.

### P8.4 Verification evidence
Completion of work is not verification. Evidence must show whether the intended outcome occurred.

### P8.5 Regression monitoring
A resolved investigation may define follow-up observation criteria or a monitoring window.

### P8.6 Failed correction
If a correction fails verification, the investigation must be able to return to an active state without losing the failed attempt.

## P9. Investigation strategy and obligations

### P9.1 Explicit obligations
Missing work that blocks progress must be represented as obligations.

### P9.2 Priority
Obligations may be prioritized by blocking impact, urgency, expected information value, cost, or risk.

### P9.3 Stop conditions
An investigation may define stopping conditions for insufficient value, unacceptable cost, safety limits, or accepted residual uncertainty.

### P9.4 No false completion
An investigation may not enter a completion state while blocking obligations remain unresolved unless the user explicitly accepts the residual uncertainty under an allowed policy.

### P9.5 Decision trace
The reason an investigation advanced, paused, accepted uncertainty, or stopped must remain inspectable.

## P10. Domain packs

### P10.1 Extensibility without fork
Domain-specific behavior must be addable without forking the generic investigation kernel.

### P10.2 Domain vocabulary
A domain pack may define terminology, labels, evidence categories, hypothesis categories, experiment templates, report sections, and help content.

### P10.3 Domain rules
A domain pack may add invariants, legal transitions, completion policies, required evidence, and verification rules.

### P10.4 Domain UI extensions
A domain pack may add domain-specific projections and fields while preserving core accessibility and state semantics.

### P10.5 Domain AI guidance
A domain pack may add domain-specific AI instructions and tools, but those instructions may not override core safety, provenance, or state-integrity rules.

### P10.6 Versioning
Every domain pack must be versioned and its version recorded with affected investigations.

### P10.7 Migration
Domain-pack upgrades that change persisted meaning must provide an explicit migration or compatibility strategy.

### P10.8 EDF pack
The EDF specialization should model experiments, assumptions, regressions, metrics, unknown effects, negative knowledge, and evidence dependencies using the generic extension mechanism.

## P11. Templates and playbooks

### P11.1 Investigation templates
Users must be able to start from reusable templates without losing the ability to deviate as evidence demands.

### P11.2 Playbooks
A playbook may suggest domain-appropriate questions, checks, evidence sources, or transitions.

### P11.3 No forced narrative
A template or playbook must not force evidence into a predetermined cause.

### P11.4 Organization templates
Organizations should be able to define their own approved templates and policies.

## P12. Workspace and portfolio

### P12.1 Multiple investigations
The product must manage many investigations, not only one active case.

### P12.2 Workspaces
Investigations should be groupable into workspaces, projects, clients, products, systems, or other configured scopes.

### P12.3 Search
Users must be able to search investigations, hypotheses, evidence, unknowns, actions, and conclusions.

### P12.4 Filtering
Users should be able to filter by lifecycle state, domain, owner, date, tag, unresolved obligation, and related attributes.

### P12.5 Cross-investigation learning
The system may surface related prior investigations, known disproven explanations, recurring causes, and reusable experiments.

### P12.6 Context boundary
Cross-investigation reuse must preserve workspace, permission, and domain boundaries so unrelated or unauthorized information is not mixed.

### P12.7 Duplicate detection
The system should be able to suggest possible duplicate or related investigations without automatically merging them.

## P13. Collaboration and review

### P13.1 Authorship
Material changes must preserve author or actor provenance when identity is available.

### P13.2 Concurrent work
The product must prevent silent overwrites when multiple users or agents modify the same investigation.

### P13.3 Review workflow
Organizations may require review or approval before a root cause, corrective action, or final report becomes authoritative.

### P13.4 Comments and discussion
Users should be able to discuss evidence, hypotheses, experiments, and conclusions without turning comments into authoritative facts automatically.

### P13.5 Assignment
Investigations, obligations, experiments, and corrective actions may be assigned.

### P13.6 Agent identity
AI-generated proposals must record provider/model/runtime identity and remain distinguishable from human-authored state.

## P14. Persistence and portability

### P14.1 Storage abstraction
The investigation domain must not depend directly on GitHub-specific storage semantics.

### P14.2 GitHub adapter
GitHub must be supported as an auditable storage provider.

### P14.3 Additional providers
The architecture must allow future file, database, service, or customer-hosted persistence providers.

### P14.4 Canonical format
A documented, versioned canonical serialization must exist for portable investigation data.

### P14.5 Import/export
Users must be able to export a complete investigation with enough metadata to re-import it.

### P14.6 Human readability
At least one supported export/storage representation should remain reasonably human-readable.

### P14.7 Version metadata
Persisted state must record schema version, domain-pack version, and relevant policy versions.

### P14.8 Migration
Schema migrations must be explicit, testable, and recoverable.

### P14.9 Backup and restore
Long-term deployments must have a documented backup and restore path appropriate to the selected storage provider.

## P15. Integration surface

### P15.1 Integration boundary
External systems must interact through explicit adapters or integration contracts rather than reaching into internal state arbitrarily.

### P15.2 Import adapters
The platform should support adapters for importing evidence or observations from external systems.

### P15.3 Event publication
Material state changes should be publishable as structured integration events.

### P15.4 Idempotency
Integration operations that may be retried must support idempotent behavior where practical.

### P15.5 Provenance
Imported data must preserve its external origin.

### P15.6 No trust escalation
Imported content is data, not authority. It must not redefine system policy, agent instructions, or transition rules.

## P16. AI architecture

### P16.1 Provider independence
The product must not hard-code its domain model to one AI provider or model.

### P16.2 AI as proposer
AI may propose hypotheses, questions, experiments, relationships, interpretations, and actions, but authoritative mutation must pass domain validation.

### P16.3 Inspectable proposal
The user must be able to inspect what an AI proposes before consequential state changes occur when policy requires review.

### P16.4 Prompt and model provenance
Material AI-generated proposals should retain model/provider and prompt-policy version metadata sufficient for audit.

### P16.5 Tool provenance
When AI uses tools or external data, the resulting proposal should retain relevant source/tool provenance.

### P16.6 Cost accounting
The platform should be able to record AI usage and cost by investigation, action, or workflow when the provider exposes it.

### P16.7 Failure tolerance
AI unavailability must not corrupt investigation state or make existing evidence inaccessible.

### P16.8 Deterministic domain rules
Legal transitions, invariants, completion rules, and persistence integrity must not depend solely on probabilistic model output.

## P17. Security, privacy, and governance

### P17.1 Least privilege
Every integration must request the minimum practical permissions.

### P17.2 Secrets
Secrets and authentication material must not be persisted in investigation records.

### P17.3 Data classification
Deployments should be able to classify investigations or evidence by sensitivity.

### P17.4 Access boundaries
Workspace and investigation access must be enforceable independently of UI visibility.

### P17.5 Audit trail
Security-sensitive and authoritative state changes must be auditable.

### P17.6 Retention
Organizations should be able to define retention and archival policies.

### P17.7 Deletion
When policy permits deletion, the system must distinguish deletion from historical falsification or ordinary archival and record the action as required by the storage model.

### P17.8 Redaction
Authorized users should be able to redact sensitive material while preserving an audit indication that redaction occurred when required.

## P18. User experience

### P18.1 Conversation is not the product
Natural language may be the easiest input method, but the authoritative investigation must remain inspectable as structured state.

### P18.2 No state-machine expertise required
Users must understand what is known, disputed, unknown, blocked, and next without learning Ordo terminology.

### P18.3 Explain unavailable actions
An unavailable consequential action must explain why it is unavailable and, where possible, what resolves the blocker.

### P18.4 Preserve comparison
The UI must make competing hypotheses and discriminating evidence easy to compare.

### P18.5 Progressive disclosure
Detailed provenance and history may be progressively disclosed, but blocking uncertainty and contradictions must not be hidden.

### P18.6 Recovery
Unsaved, pending, conflicted, or failed persistence state must be visible and recoverable.

### P18.7 Multi-device
The architecture should support use on tablet, phone, and desktop without changing investigation semantics.

## P19. Accessibility and internationalization

### P19.1 Accessibility baseline
The application must support keyboard navigation, screen readers, sufficient contrast, visible focus, text scaling, semantic structure, reduced motion, and non-color-only status representation.

### P19.2 High zoom and reflow
Critical investigation tasks must remain usable under high text zoom and narrow viewport reflow.

### P19.3 Localization readiness
User-facing text, date/time, number formatting, and domain labels should be structured so localization can be added without rewriting core logic.

### P19.4 Accessible reports
Generated reports must preserve accessible document structure where the output format supports it.

## P20. Reporting and sharing

### P20.1 Interim and final reports
The system must support both interim reports with unresolved work and final reports.

### P20.2 Snapshot semantics
A report must identify the investigation version or state snapshot from which it was produced.

### P20.3 Evidence traceability
Material conclusions in a report should be traceable back to supporting evidence and unresolved uncertainty.

### P20.4 Versioned reports
Regenerated reports must not erase the existence of earlier issued versions when audit requirements apply.

### P20.5 Domain report sections
Domain packs may add report sections without replacing required core provenance and uncertainty sections.

### P20.6 Folio
Folio should be the preferred Echelon report rendering system where appropriate.

## P21. Observability and operability

### P21.1 Health
Deployments must expose enough health information to distinguish application, AI, persistence, and integration failures.

### P21.2 Structured faults
Operational and domain faults must use structured fault representations compatible with Aegis.

### P21.3 Correlation
A user-visible failure should be traceable to diagnostic events without exposing secrets.

### P21.4 Metrics
The product should capture useful metrics such as investigation volume, time in state, unresolved obligation age, sync failures, AI usage, and verification completion.

### P21.5 Audit versus telemetry
Product telemetry must be distinct from authoritative investigation history.

### P21.6 Recovery procedures
Operational recovery for interrupted writes, corrupt local cache, failed migrations, and unavailable integrations must be documented and tested.

## P22. Performance and scale

### P22.1 Large investigation
The UI and state model must remain usable when an investigation contains hundreds or thousands of evidence and event records.

### P22.2 Incremental loading
History and secondary artifacts should support incremental loading so active work is not blocked by loading the entire archive.

### P22.3 Search scale
Portfolio search must not require opening every investigation client-side.

### P22.4 Bounded AI context
The AI layer must build scoped context from authoritative state rather than blindly sending an entire long investigation to a model.

### P22.5 Derived projections
Expensive derived views may be cached or projected, but they must remain reconstructable from authoritative data.

## P23. Versioning and compatibility

### P23.1 Schema version
Every persisted artifact must identify the schema version required to interpret it.

### P23.2 Forward incompatibility
A client must not silently write data it cannot fully understand.

### P23.3 Backward reading
Where practical, newer clients should read older investigations through explicit migrations or compatibility projections.

### P23.4 Policy versioning
Changes to confirmation rules, evidence requirements, or domain policies must be versioned.

### P23.5 Reproducible historical decision
The product should preserve enough version metadata to explain why a transition was legal under the policy in force at the time.

## P24. Extension contracts

### P24.1 Explicit extension types
Supported extensions should have explicit contracts, including domain packs, persistence providers, AI providers, evidence importers, report projections, and integration publishers.

### P24.2 Isolation
An extension must not be allowed to corrupt authoritative state when it fails.

### P24.3 Capability declaration
Extensions must declare the capabilities and permissions they require.

### P24.4 Compatibility declaration
Extensions must declare compatible core versions.

### P24.5 Diagnostics
Extension failures must be attributable to the extension and surfaced through structured faults.

## P25. Quality and completion

### P25.1 Traceability
Every implemented product requirement must be traceable to acceptance evidence.

### P25.2 Tests
Core domain transitions, invariants, persistence conflict behavior, migrations, and security boundaries require deterministic tests.

### P25.3 UI contracts
Consequential screens require application-specific UI contracts that verify product meaning, not only component presence.

### P25.4 Accessibility evidence
Accessibility requirements require automated and human-verifiable evidence appropriate to the feature.

### P25.5 Agent claims are not evidence
An AI or coding agent reporting that work is complete is not sufficient completion evidence.

### P25.6 Praxis
Praxis should track code metrics, thresholds, and repeated-change/churn evidence across product evolution.

### P25.7 Regression
A previously satisfied critical requirement that loses its evidence must be treated as a regression, not silently remain complete.

## P26. Deployment models

### P26.1 Static-capable client
The client should remain compatible with static hosting when authentication and AI architecture permit it.

### P26.2 Service-backed mode
The architecture must allow a service-backed deployment for stronger authentication, collaboration, search, persistence, or organizational controls.

### P26.3 Self-hosting
A future self-hosted deployment must be possible without changing investigation semantics.

### P26.4 Managed service
A future managed-service deployment may add operational convenience but must use the same canonical domain rules and portable data model.

## P27. Product success criteria

The long-term product is successful when:

1. a user can start with an unstructured problem statement;
2. the system creates and maintains explicit investigation state;
3. competing explanations remain visible;
4. evidence provenance and contradictions are inspectable;
5. the system asks for or proposes discriminating evidence rather than merely producing plausible prose;
6. unsupported causal conclusions are blocked or clearly qualified;
7. corrective action is separated from verification;
8. investigation history remains defensible after the fact;
9. the same core can support different domains without forks;
10. data can move between supported storage/deployment models;
11. users remain in control of consequential state changes;
12. the product remains useful after the hackathon constraints are removed.


## P28. Semantic conformance

### P28.1 Canonical semantics
The authoritative meanings of observation, claim, interpretation, hypothesis, assumption, evidence, prediction, finding, unknown, negative knowledge, contradiction, obligation, causal mechanism, cause roles, corrective action, verification, and conclusion must conform to [INVESTIGATION-SEMANTICS.md](INVESTIGATION-SEMANTICS.md).

### P28.2 Semantic invariants
Implementation must enforce the generic investigation invariants defined in the canonical semantics rather than relying on UI convention or agent prompting.

### P28.3 Transition legality
Hypothesis, unknown, assumption, contradiction, action, corrective-action, and investigation lifecycle transitions must be validated by explicit domain rules.

### P28.4 Cause confirmation gate
A cause may not enter ConfirmedCause unless the active confirmation policy's required evidence, contradiction, unknown, assumption, alternative-explanation, mechanism, provenance, and invariant conditions are satisfied.

### P28.5 Resolution gate
An investigation may not enter Resolved unless the active resolution policy is satisfied, including required verification and blocking-obligation conditions.

### P28.6 Semantic acceptance scenarios
The scenarios in [INVESTIGATION-ACCEPTANCE-SCENARIOS.md](INVESTIGATION-ACCEPTANCE-SCENARIOS.md) are normative acceptance behavior and should become executable tests where practical.

### P28.7 No narrative override
Neither a human-authored narrative nor an AI-generated narrative may override contradictory authoritative state, blocking obligations, or illegal transition rules.

### P28.8 Domain-pack compatibility
Domain packs may strengthen domain semantics and policies but may not violate core evidence/inference separation, provenance, unknown semantics, proposal/authority separation, or historical integrity.


## P29. Research and semantic evolution

### P29.1 Stable core boundary
Only cross-domain epistemic invariants should be promoted into the generic kernel. Domain methodology and research constructs must remain in versioned extensions until evidence justifies stronger stability.

### P29.2 Stability classification
Semantic constructs must support explicit stability classification such as CoreStable, DomainStable, Experimental, Deprecated, and Retired.

### P29.3 Promotion discipline
New semantic constructs must follow an explicit research-to-core promotion process. The default is to keep uncertain constructs outside the core.

### P29.4 Versioned semantic identity
Evolving domain constructs must have qualified, versioned semantic identities independent of display labels.

### P29.5 Strong typing without closed-world lock-in
The F# model must use closed discriminated unions for genuinely stable core semantics and versioned typed extension references/envelopes for evolving domain semantics.

### P29.6 Unknown extension preservation
A client that encounters an optional extension it does not understand must preserve it without semantic loss and must not silently drop it during unrelated edits.

### P29.7 Required extension protection
If safe editing requires semantics the client does not understand, the client must enter degraded or read-only mode rather than risk corruption.

### P29.8 Schema-policy separation
Schema versions, domain-pack versions, semantic-extension versions, and policy versions must be independently identifiable when their meanings can evolve independently.

### P29.9 Historical policy preservation
A later policy change must not retroactively change why an earlier transition was legal. Re-evaluation must be represented as a new reassessment.

### P29.10 Non-destructive migration
Meaning-changing migrations must preserve original historical meaning. Compatibility projection, upcasting, additive migration, or side-by-side semantics should be preferred over destructive rewrites.

### P29.11 Experimental isolation
Experimental semantics must be namespaced, versioned, explicitly activated, visible as experimental when material, and removable from new work without making old investigations unreadable.

### P29.12 Rollback plan
Every experimental semantic extension must define rollback and historical-data behavior before it is used in authoritative investigations.

### P29.13 Core invariant firewall
No extension, domain pack, policy, or experimental profile may bypass the generic core invariants.

### P29.14 Generic fallback
Domain-specific information must have a safe generic fallback projection so critical information does not disappear when specialized UI or code is unavailable.

### P29.15 Runtime AI composition
AI guidance must be composed from the active core, domain-pack, policy, and explicitly enabled experimental versions rather than assuming all historical domain concepts are current.

### P29.16 EDF research posture
EDF-specific concepts that remain under active research must remain versioned EDF semantics rather than generic-kernel constructs unless and until promotion criteria are satisfied.

### P29.17 Architecture fitness
The evolution-safety scenarios and fitness tests in [SEMANTIC-EVOLUTION.md](SEMANTIC-EVOLUTION.md) must be treated as architecture requirements.


## P30. Design-decision governance

### P30.1 Decision stability classification
Consequential design decisions must be classifiable as LockedInvariant, StableDirection, ReversibleChoice, Experimental, or Deferred.

### P30.2 No accidental promotion
Implementation work must not silently convert an Experimental, Deferred, or ReversibleChoice item into a permanent persisted semantic dependency.

### P30.3 Reversible default
When a requirement can be satisfied by multiple architectures and no stable decision exists, the implementation should prefer the option with the lowest semantic lock-in and document it as reversible.

### P30.4 Recorded promotion
Promoting a decision to a stronger stability class requires explicit rationale/evidence and compatibility, migration, and acceptance-test analysis.

### P30.5 Agent compliance
Engineering agents must consult [DESIGN-DECISION-BOUNDARIES.md](DESIGN-DECISION-BOUNDARIES.md) before resolving architecture ambiguity.

### P30.6 Minimal core conformance
The generic investigation aggregate should conform to [CORE-DOMAIN-MODEL.md](CORE-DOMAIN-MODEL.md) unless a recorded decision justifies a change.

### P30.7 Domain-pack conformance
Domain specializations must conform to [DOMAIN-PACK-CONTRACT.md](DOMAIN-PACK-CONTRACT.md) rather than introducing alternate authoritative state models.


## P31. Policy and transition execution

### P31.1 Central transition authority
Consequential authoritative state changes must pass through a domain transition engine rather than be independently enforced by UI, AI, or persistence code.

### P31.2 Explicit decisions
Transition requests must return explicit Allowed, Blocked, RequiresReview, UnsupportedSemantics, Conflict, or equivalent typed outcomes instead of relying on generic exceptions.

### P31.3 Explainable blockers
Every unavailable consequential capability must expose machine-readable and human-readable blockers with policy/rule provenance and, where possible, a path to resolution.

### P31.4 Policy layering
Core, domain, experimental, organization, and investigation-local policy must compose through explicit precedence and relaxation rules.

### P31.5 Core invariant firewall
No policy layer may relax CoreStable invariants.

### P31.6 Deterministic legality
Authoritative transition legality must be deterministic for a given state, semantic manifest, policy set, and actor capability context.

### P31.7 Policy attribution
Authoritative events must preserve the policy/version references under which the transition was allowed.

### P31.8 Capability projection
Legal actions and blocking reasons should be projected by the domain engine for UI consumption rather than duplicated in presentation logic.

### P31.9 Offline revalidation
Offline queued commands must be revalidated against current authoritative state and policy before remote application.

### P31.10 Idempotency
Retryable commands must support idempotent handling so unstable networks do not create duplicate semantic events.

### P31.11 Policy comparison
Research workflows should support evaluating an immutable investigation snapshot under multiple policy versions without mutating authoritative state.

### P31.12 Policy pinning
Research investigations must be able to pin applicable policy/domain versions for reproducibility.

### P31.13 Aegis faults
Invalid transitions, unsupported semantics, invariant violations, and policy-engine failures must map to structured Aegis-compatible faults.

### P31.14 Engine contract
Implementation should conform to [POLICY-ENGINE.md](POLICY-ENGINE.md).

## P32. Persistence, portability, and recovery

### P32.1 Semantic/provider separation
Authoritative investigation identity and meaning must remain independent of GitHub SHAs, paths, database keys, or other provider metadata.

### P32.2 Canonical portable package
The platform must define a versioned canonical portable representation sufficient for complete compatible import/export.

### P32.3 Event order
Authoritative event order must use aggregate sequence/version rather than wall-clock timestamp alone.

### P32.4 Provider capability discovery
Persistence providers must expose which capabilities they support when product behavior depends on them.

### P32.5 Atomic semantic commit
A domain decision that emits multiple related events must not be represented as remotely committed if only part of the semantic transition persisted.

### P32.6 Optimistic concurrency
Providers must support or emulate conflict detection sufficient to prevent silent stale overwrite.

### P32.7 Semantic conflict handling
Conflicts must be reconciled at command/domain-state level rather than blindly merging serialized text.

### P32.8 Local durability
Active offline work and queued commands should survive browser/application restart.

### P32.9 Explicit local versus remote truth
The UI must distinguish local pending state from remotely confirmed authoritative state.

### P32.10 Artifact integrity
Externally referenced material evidence should support integrity metadata such as cryptographic digest where practical.

### P32.11 Missing/changed artifact state
Unavailable or changed external artifacts must be surfaced explicitly rather than silently substituted.

### P32.12 Complete round trip
A compatible complete export/import must preserve authoritative events, entity IDs, extension data, semantic/policy versions, provenance, uncertainty, and historical conclusions.

### P32.13 Import collision
An imported InvestigationId collision must require explicit handling rather than silently overwrite an existing investigation.

### P32.14 Cross-provider migration
Moving between GitHub and another persistence provider must not change InvestigationId or domain semantics.

### P32.15 Research snapshot pinning
Experiments must be able to bind to an immutable investigation/evidence version for reproducibility.

### P32.16 Corruption detection
Persistence loading must detect malformed manifests, impossible versions, duplicate/missing event sequence, and snapshot/history inconsistencies where the provider supports those guarantees.

### P32.17 Recovery evidence
Repair of inconsistent authoritative state must be explicit and auditable rather than silently synthesized by an AI.

### P32.18 Provider contract
Implementation should conform to [PERSISTENCE-CONTRACT.md](PERSISTENCE-CONTRACT.md).


## P33. AI investigation orchestration

### P33.1 AI as collaborator
AI must assist investigation through proposals, analysis, questions, and explanations without becoming the source of authoritative domain truth.

### P33.2 Scoped context
AI context must be assembled from explicit scoped projections appropriate to the reasoning task rather than blindly serializing the entire investigation.

### P33.3 Context provenance
Material AI reasoning should retain enough context/version provenance to determine which semantic/domain/policy state it used.

### P33.4 Structured proposals
Material AI suggestions that could become authoritative state must use typed structured proposals validated against semantic/domain schemas.

### P33.5 Evidence references
AI proposals relying on authoritative evidence should reference EvidenceIds or other traceable entity IDs.

### P33.6 Source category preservation
AI context and output must distinguish observation, evidence, finding, interpretation, assumption, unknown, negative knowledge, and historical conclusion.

### P33.7 Prompt injection boundary
External evidence and imported artifacts must be treated as untrusted content and cannot redefine platform/domain instructions.

### P33.8 Narrow write tools
AI must not receive unrestricted mutation access to investigation persistence. Consequential writes must use narrow validated commands.

### P33.9 Question value
AI-generated questions should identify the uncertainty, blocker, contradiction, or competing hypotheses they are intended to resolve.

### P33.10 Challenge behavior
The product must support deliberate challenge of the leading explanation, including overlooked alternatives, hidden assumptions, evidence dependency, and falsifying checks.

### P33.11 Negative knowledge use
AI must receive relevant scoped negative knowledge before proposing repeated explanations/actions when practical.

### P33.12 Stale proposal detection
AI proposals whose material inputs or semantic/policy versions change before acceptance must be revalidated or marked stale.

### P33.13 AI outage tolerance
Loss of AI service must not make authoritative investigation state inaccessible or corrupt existing work.

### P33.14 Multi-model provenance
If multiple models/providers are used, their roles, context versions, and outputs must remain distinguishable.

### P33.15 AI research reproducibility
Research-mode AI runs should support frozen investigation snapshot, semantic/policy version, prompt version, model/provider version, and tool configuration.

### P33.16 Orchestration contract
Implementation should conform to [AI-ORCHESTRATION.md](AI-ORCHESTRATION.md).

## P34. Investigation workspace

### P34.1 Structured state primary
The primary workspace must make authoritative structured investigation state understandable without requiring the user to reconstruct meaning from chat history.

### P34.2 Primary question
The workspace must help users understand what is known, what could explain it, what conflicts with those explanations, and what should be done next.

### P34.3 Required semantic regions
The workspace must expose observation, hypotheses, evidence, unknowns/assumptions, contradictions, obligations/blockers, next actions, persistence state, and history access.

### P34.4 Proposal interaction
Users must be able to inspect, accept, edit, reject, defer, or ask why for material AI proposals according to policy.

### P34.5 Hypothesis comparison
Users must be able to compare active hypotheses by predictions, support, contradiction, unknowns, assumptions, and discriminating actions.

### P34.6 Blocking unknown visibility
Blocking unknowns and contradictions must not be hidden in secondary history or settings.

### P34.7 Recommended versus legal
The UI must distinguish recommended actions from merely legal actions and from unavailable actions.

### P34.8 Discoverable blocked transitions
Important unavailable transitions must remain discoverable with explicit blockers rather than simply disappearing.

### P34.9 Semantic conflict UX
Persistence conflicts must be explained in terms of conflicting investigation intent/state rather than raw serialized merge by default.

### P34.10 Conclusion review
High-consequence conclusion transitions should have a review surface exposing evidence, alternatives, contradictions, unknowns, assumptions, policy gate state, and required reviews.

### P34.11 Verification UX
Corrective action and verification must remain visually and semantically distinct.

### P34.12 Mobile completeness
Core investigation work must remain possible on iPad and narrow phone layouts without hover-dependent functionality.

### P34.13 Accessibility
The workspace must satisfy keyboard, focus, screen-reader, text scaling, reduced motion, semantic status, and non-color-only requirements.

### P34.14 Workspace contract
Implementation should conform to [INVESTIGATION-WORKSPACE.md](INVESTIGATION-WORKSPACE.md).

### P34.15 URL is the source of navigable state
The workspace derives the view from the URL and writes the URL when navigable state changes. Navigable state is the screen, the entity IDs and the view parameters (as-of version, comparison pair, filters, sort, search, tab, page). A copied URL opened in another browser reproduces the view, subject to that user's repository access. Transient UI state is never in the URL: open menus, focus, unsaved Ask/Add text, draft evidence and unsubmitted forms. This is the portfolio standard Praxis SAF-URL-1..10, implemented with Limen's routing module (LCP-088..112).

### P34.16 Every entity, branch and version is addressable
Each of these has a canonical route, listed in [Screen Contracts](SCREEN-CONTRACTS.md#url-contract):

- investigation;
- hypothesis;
- evidence item;
- evidence dependency view;
- unknown, assumption and contradiction;
- action and experiment;
- finding;
- cause review;
- corrective action;
- verification;
- counterfactual branch (by BranchId);
- historical version (by aggregate version);
- semantic diff (by version pair);
- report.

A historical or branch URL opens that exact version or branch and is visibly read-only or sandboxed (S14, S16). It never silently shows the current state instead.

### P34.17 Canonical URLs and history
URLs use one canonical form: declared parameters only, in declaration order, defaults omitted, sets sorted and de-duplicated, `%20` with upper-case hex. Moving to another screen or entity pushes a history entry. Refining the current view (filter, sort, search, tab, page, version scrubbing) replaces it. Back, Forward and reload restore the view from the URL alone, and adopting a URL never pushes.

### P34.18 Hash routes on static hosting
The application uses hash routes (`index.html#/investigations/{investigationId}/evidence/{evidenceId}`), with relative links and no `<base href>`, so a reloaded deep link never 404s on GitHub Pages (D-203, Limen DF-LIMEN-2026-0006). Sign-in keeps and resumes the target, and a return target never leaves the application.

### P34.19 Unknown, deleted and forbidden targets
A route to an unknown entity, a repository the user cannot read, or an investigation not available locally while offline shows an explicit not-found, not-permitted or unavailable-offline state. That state names what was requested and offers a way back. It is never blank and never shows another record. A malformed identifier is a typed invalid outcome, not an exception. Old routes keep working through legacy redirects when routes change.

### P34.20 Route inventory, privacy and sharing
The application publishes `.echelon/routes.json` (`echelon.routes/v1`), which `praxis foundations verify` checks. Pure `parse`/`format` functions with property-based round-trip tests own every route. URLs carry only GUIDs and view parameters, never tokens, keys, OAuth codes, evidence content or personal data (P35.3). Investigation, evidence, hypothesis, branch, version, diff and report views offer Copy link.


## P35. Security and privacy

### P35.1 Explicit trust boundaries
The architecture must define trust boundaries among browser/UI, domain runtime, local storage, persistence providers, AI providers, external evidence, and domain packs.

### P35.2 Production authentication direction
Production GitHub deployments should prefer GitHub App/OAuth-style authorization with least privilege over long-lived personal tokens.

### P35.3 Credential exclusion
Credentials must not be committed, placed in investigation events/reports, included in AI prompts unnecessarily, or logged in telemetry.

### P35.4 Authorization separate from semantic legality
A user may be authorized but semantically blocked, or semantically allowed but unauthorized. The system must preserve that distinction.

### P35.5 Data classification
Deployments should support classification metadata that can constrain AI use, export, sharing, retention, and integrations.

### P35.6 AI disclosure controls
Sending investigation data to AI must respect classification, provider configuration, organization/domain policy, and minimum-necessary context.

### P35.7 Untrusted evidence
Imported/external evidence, URLs, files, and AI-generated rich text must be treated as untrusted content.

### P35.8 Rendering safety
User/imported/AI content must be sanitized and must not create executable HTML/script injection paths.

### P35.9 Browser hardening
Web deployments should use a restrictive Content Security Policy and appropriate framing/request protections consistent with the deployment architecture.

### P35.10 Local-data honesty
Offline/local storage guarantees must be stated accurately; the product must not claim secure-at-rest properties it does not implement.

### P35.11 Extension least privilege
Domain packs/extensions must declare required capabilities and may not gain undeclared access to secrets, network resources, or persistence mutation.

### P35.12 Artifact integrity
External evidence artifacts should support digest/integrity verification where practical.

### P35.13 Sensitive logging
Operational telemetry must minimize sensitive investigation content and credentials.

### P35.14 Export/redaction authorization
Export, redaction, deletion, and policy/pack administration may require privileges distinct from ordinary investigation read/write.

### P35.15 Deletion honesty
The product must not simultaneously claim complete deletion and complete historical reconstructability when policy makes those incompatible.

### P35.16 Audit separation
Security audit events must be distinguishable from authoritative semantic investigation history while remaining correlatable.

### P35.17 Safe defaults
Default deployment posture should favor least privilege, sanitized untrusted content, no arbitrary external write tools, no arbitrary executable packs, explicit AI disclosure, and fail-closed behavior for unknown required semantics.

### P35.18 Threat-model conformance
Implementation should conform to [SECURITY-THREAT-MODEL.md](SECURITY-THREAT-MODEL.md).


## P36. Open-question governance and staged delivery

### P36.1 Open questions remain explicit
Material unresolved product, semantic, architecture, security, AI, persistence, or EDF research questions must be recorded rather than silently resolved by implementation.

### P36.2 No deletion of resolved questions
When an open question is resolved, preserve the decision history and update affected requirements, decision boundaries, and acceptance scenarios.

### P36.3 Vertical slices
Implementation should follow coherent vertical slices that leave the system usable and evidenced at each stage.

### P36.4 Core before breadth
Typed semantic integrity, policy/capability enforcement, workspace truthfulness, persistence truth, and verification semantics take priority over broad feature count.

### P36.5 Stop-the-line
Implementation must stop for correction when a layer bypasses core boundaries, including UI-owned legality, AI-owned authority, provider-specific domain types, dropped extensions, fabricated migration data, or false synchronization state.

### P36.6 Implementation plan
The build should follow [IMPLEMENTATION-SLICES.md](IMPLEMENTATION-SLICES.md) unless a recorded decision justifies resequencing.

### P36.7 Research register
Unresolved issues must be maintained in [OPEN-QUESTIONS.md](OPEN-QUESTIONS.md), with a bias toward reversible defaults.


## P37. Command, event, and type contract

### P37.1 Intent-based commands
Authoritative writes must use explicit semantic commands rather than generic state setters or raw document replacement.

### P37.2 Immutable semantic events
Allowed commands must produce narrow past-tense semantic events that preserve what happened without embedding UI-only state.

### P37.3 No generic setters
The core must not expose generic SetState, SetRootCause, SetConfidence, ReplaceInvestigationJson, or equivalent bypasses around domain semantics.

### P37.4 Derived comparison posture
The first implementation must derive comparative hypothesis posture such as Leading rather than persisting it as historical lifecycle state.

### P37.5 Cause/hypothesis separation
Causal confirmation must belong to CausalAssertion rather than HypothesisState so causal sets and multi-factor explanations remain representable.

### P37.6 Explicit reopening
Reopening a confirmed cause or resolved investigation must append an explicit event and preserve the prior confirmation/resolution as history.

### P37.7 Transition tables
Persisted lifecycle transitions must conform to [STATE-TRANSITION-TABLES.md](STATE-TRANSITION-TABLES.md) unless a recorded semantic decision changes the profile.

### P37.8 Command/event catalog
Core authoritative command and event names/intent should conform to [COMMAND-EVENT-CATALOG.md](COMMAND-EVENT-CATALOG.md).

### P37.9 Typed F# profile
The first implementation should follow [FSHARP-TYPE-SHAPES.md](FSHARP-TYPE-SHAPES.md), preserving typed IDs, explicit lifecycle unions, semantic manifests, extension envelopes, pure decision/evolution functions, and dependency direction.

### P37.10 Pure evolution
Projecting authoritative state from an event must be deterministic and must not call network, AI, clock, random, or persistence services.

### P37.11 Pure domain decision
Transition legality should be testable as a pure function of state, command, policies, actor context, and semantic versions.

### P37.12 Wire extensibility
Wire event/extension identity must remain versioned/extensible even when a given core assembly uses typed discriminated unions internally.

### P37.13 Event correction
Incorrect semantic history must be corrected by append/supersession events rather than editing previously committed semantic events.

### P37.14 Proposal acceptance revalidation
Accepting a proposal must generate/re-run the target authoritative command under current state and policy rather than replay the proposal's earlier legality.

### P37.15 Implementation dependency direction
Domain must not depend on Web, AI, GitHub/persistence adapters, or provider-specific authentication types.

### P37.16 Domain test matrix
Core command/event/state implementation must be verified against [DOMAIN-TEST-MATRIX.md](DOMAIN-TEST-MATRIX.md), subject to organizer rules about when executable competition tests may be created.


## P38. Advanced competition capabilities

The competition profile should deliberately exercise the strongest capabilities that can be built without weakening the durable product model.

### P38.1 Time travel
The product should reconstruct investigation state at any authoritative aggregate version supported by the persistence history.

### P38.2 Semantic diff
Users should be able to compare two investigation versions in terms of state/evidence/blocker/capability changes rather than raw serialized text.

### P38.3 What changed my mind
For material posture changes, the product should explain which events, evidence, blockers, policy rules, or contradictions caused the change.

### P38.4 Counterfactual sandbox
Users should be able to create a non-authoritative branch from an immutable investigation version and apply explicitly hypothetical changes without mutating source state.

### P38.5 Counterfactual promotion safety
A branch may become a new real investigation only through explicit promotion/copy semantics; hypothetical evidence must never silently become fact.

### P38.6 Sensitivity analysis
The product should identify which evidence, assumptions, unknowns, or policy rules are pivotal to current conclusions/capabilities through explainable branch-based analysis.

### P38.7 Evidence dependency visualization
The UI should visualize source lineage and common dependencies so repeated artifacts from one source are not mistaken for independent confirmation.

### P38.8 Causal-set visualization
The product should visualize multi-member causal assertions, roles, mechanisms, contradictions, and verification targets without forcing a universal DAG ontology.

### P38.9 Uncertainty map
The workspace should provide an inspectable derived view of blocking unknowns, assumptions, contradictions, unsupported causal links, and verification gaps.

### P38.10 Hypothesis battlefield
The product should offer a dense comparison surface showing competing hypotheses, support, contradiction, assumptions, predictions, negative knowledge, and falsifying actions.

### P38.11 Blind multi-agent panel
The system should support multiple specialized AI roles operating independently on the same frozen state before reconciliation.

### P38.12 Multi-agent independence
Agents that see or derive from another agent's output must not be counted as independent corroboration.

### P38.13 Agent role diversity
Multi-agent value must come from distinct objectives such as generation, skepticism, evidence auditing, experiment design, causal analysis, and verification auditing rather than repeated identical prompts.

### P38.14 Reconciliation round
After blind analysis, the system may run a reconciliation round that focuses on explicit disagreements and proposes discriminating evidence/actions.

### P38.15 Model/provider tournament
Research mode should support comparing models/providers on the same frozen task with provenance, cost, latency, proposal quality, and downstream usefulness.

### P38.16 Policy/domain tournament
Research mode should support comparing policy or domain-pack versions against the same immutable investigation snapshot without mutating authoritative state.

### P38.17 Prompt tournament
Prompt/instruction versions should be comparable against identical frozen contexts.

### P38.18 What would falsify this
Each active hypothesis should support an operation that proposes its strongest or cheapest meaningful falsifier and/or the best test against a competing hypothesis.

### P38.19 What are we missing
The system should be able to challenge completeness by finding unexplained observations, hidden assumptions, unlinked evidence, evidence dependencies, unmodeled contradictions, scope gaps, and instrument/measurement failure.

### P38.20 Orphan diagnostics
The product should detect semantically suspicious orphan state such as evidence linked to nothing, hypotheses without predictions, verification without criteria, or corrective action without a causal target.

### P38.21 Circular-reasoning diagnostics
The system should identify candidate evidence/inference cycles and self-supporting reasoning paths for review.

### P38.22 Contradiction mining
AI may propose contradictions not yet explicitly represented, but users/policy control authoritative acceptance.

### P38.23 Assumption mining
AI may identify hidden assumptions in hypotheses, actions, interpretations, and conclusions and propose them as explicit state.

### P38.24 Experiment pre-registration
Investigation actions should support freezing prospective predictions, procedure, and interpretation criteria before results are observed.

### P38.25 Reproducibility bundle
A high-value investigation action should be exportable as a reproducibility bundle identifying exact source state, procedure, predictions, artifacts, result, interpretation, and semantic/policy versions.

### P38.26 Investigation replay
The UI should support deterministic replay driven by authoritative events, including pause/step and reduced-motion alternatives.

### P38.27 Spectator mode
The competition profile should include a read-only presentation mode displaying real investigation state rather than scripted fake state.

### P38.28 Factory telemetry
The product/demo should expose software-factory evidence such as ROS work, requirements traceability, tests, Praxis metrics, Aegis faults, commits, and deployment state.

### P38.29 Requirements proof graph
The competition profile should support tracing requirement -> acceptance scenario -> work item -> source -> test -> commit -> runtime evidence.

### P38.30 Build provenance proof bundle
The system should generate a Folio-compatible proof bundle showing kickoff baseline, framework versions, commit/test/metric history, known deviations, and final build provenance.

### P38.31 Self-diagnostic mode
The investigator should be able to ingest its own Aegis/Praxis/ROS/build evidence to investigate an actual application or deployment failure.

### P38.32 Semantic search
Natural-language queries should resolve to structured investigation retrieval rather than free-form model recollection where authoritative state can answer the question.

### P38.33 Command palette
Advanced operations should remain discoverable through a mobile/keyboard-friendly command palette.

### P38.34 Mobile evidence capture
Supported devices should allow rapid photo/screenshot/file/text/URL evidence capture with provenance and without auto-promoting AI extraction to fact.

### P38.35 Immutable snapshot share
A read-only share/review artifact should bind to an exact InvestigationId/AggregateVersion and must not silently update.

### P38.36 Expert review packet
The system should be able to package selected state/questions for an external expert and ingest the response with testimony/report provenance.

### P38.37 Demo health check
The competition profile should run a deterministic pre-demo readiness check covering deployment, persistence, AI, offline fallback, reports, secrets, and smoke tests.

### P38.38 Graceful degradation
Advanced competition features must degrade to semantically correct simpler views rather than causing the investigation to become unusable.

### P38.39 Ambition profile
Competition implementation should follow [COMPETITION-AMBITION.md](COMPETITION-AMBITION.md), with orchestration/branching/visualization behavior governed by the corresponding advanced-profile documents.


## P39. Semantic compiler and verification amplification

### P39.1 Semantic registry
The first implementation should maintain a versioned semantic registry sufficient to describe core commands, events, lifecycle transitions, capabilities, payload types, semantic IDs, and ownership.

### P39.2 Generated structural tests
Where practical, structural transition tests should be generated from the semantic registry so every registered state/command combination has an explicit outcome.

### P39.3 Generated capability metadata
UI/AI capability descriptors should be generated or validated from the same semantic registry rather than maintained independently.

### P39.4 Generated AI schemas
Structured AI proposal schemas should be generated or validated from authoritative semantic payload definitions where practical.

### P39.5 Generated event registry
Event serialization identifiers, codecs, and compatibility metadata should be checked against the semantic registry.

### P39.6 Generated Aegis metadata
Stable invariant/transition fault codes and default explanations should be generated or validated against semantic command/policy metadata where practical.

### P39.7 Generated diagrams
Lifecycle/state diagrams and transition tables should be derivable from executable semantic metadata after kickoff.

### P39.8 Drift detection
CI must detect registered commands/events lacking handlers, evolve cases, codecs, structural tests, or required capability metadata.

### P39.9 Semantic contract snapshot
Each build should be able to emit a semantic-contract manifest identifying commands, events, capabilities, schemas, policies, domain packs, extension IDs, and version/digest information.

### P39.10 Semantic contract diff
Builds should identify added/removed/changed semantic contracts and flag breaking meaning changes without required version/migration treatment.

### P39.11 Compatibility gate
Release/build validation should fail when persisted semantic meaning changes incompatibly without explicit versioning/deprecation/migration.

### P39.12 State-space exploration
The build should systematically explore lifecycle state/command combinations and protected-transition policy combinations beyond hand-written examples.

### P39.13 Property validation
The test suite should include properties for replay determinism, prefix/time-travel consistency, version monotonicity, no silent deletion, extension preservation, cause-confirmation safety, resolution safety, and proposal authority separation.

### P39.14 Mutation testing
Critical semantic/policy mutations should be introduced deliberately and required tests should detect them.

### P39.15 Semantic mutation stop-the-line
A surviving mutation that weakens a CoreStable invariant or protected transition is a stop-the-line defect.

### P39.16 Offline chaos
The product should test network loss, acknowledgement loss, remote advancement, auth expiry, rate limits, restarts, and queue corruption.

### P39.17 Concurrency simulation
The product should test both commutative concurrent work and semantic conflicts across multiple clients.

### P39.18 AI contract fuzzing
Structured AI proposal validation should be tested with malformed, stale, oversized, unknown-semantic, and adversarial payloads.

### P39.19 Prompt-injection corpus
Security validation should maintain representative indirect/direct prompt-injection evidence cases.

### P39.20 Replay equivalence
Snapshot+tail replay must produce equivalent semantic state to full event replay for the same aggregate version.

### P39.21 Cross-provider equivalence
Canonical export/import between providers should preserve equivalent semantic projection and history.

### P39.22 Domain-pack isolation
The core invariant suite must pass unchanged under generic, EDF, EDF-experimental, and test-pack compositions.

### P39.23 Generated proof data
Formal/state-space validation should emit machine-readable evidence that can feed the engineering proof bundle.

### P39.24 Semantic compiler contract
Implementation should follow [SEMANTIC-COMPILER.md](SEMANTIC-COMPILER.md).

### P39.25 Formal validation contract
Verification should follow [FORMAL-VALIDATION.md](FORMAL-VALIDATION.md).

## P40. Engineering proof and screen contracts

### P40.1 Build-bound proof bundle
Every competition-ready build should be able to generate a proof bundle bound to exact repository/commit/build/deployment identity.

### P40.2 Requirement evidence states
Proof data must distinguish NotStarted, InProgress, ImplementedUnverified, Verified, Deferred, and Deviated rather than collapsing everything to complete/incomplete.

### P40.3 Requirement-to-runtime traceability
Where evidence exists, the bundle should connect requirement, acceptance scenario, ROS work, source, test, commit, and runtime evidence.

### P40.4 Honest gaps
Missing traceability/evidence, unfinished requirements, known shortcuts, known defects, and test gaps must remain visible.

### P40.5 AI engineering provenance
Agent-generated engineering work should record provider/model/task/commit/test provenance where available without requiring private reasoning traces.

### P40.6 Semantic contract evidence
The proof bundle should include semantic/policy/domain/extension versions and semantic-contract digest.

### P40.7 Accessibility/security evidence
The proof bundle should include actual accessibility/security validation performed, not inferred claims.

### P40.8 Proof immutability
An issued proof bundle is a snapshot of one build and must not silently change with later commits.

### P40.9 Screen semantic contracts
Major screens must conform to [SCREEN-CONTRACTS.md](SCREEN-CONTRACTS.md) and application-specific Percepta contracts.

### P40.10 Screen independence from layout
Required screen meaning/actions/state visibility are normative; exact pixel layout/component composition remains replaceable.

### P40.11 Responsive semantic parity
iPad, phone, desktop, high-zoom, and reduced-motion projections must preserve critical blockers, authority state, and legal-action meaning.

### P40.12 Proof bundle contract
Build evidence should follow [ENGINEERING-PROOF-BUNDLE.md](ENGINEERING-PROOF-BUNDLE.md).


## P41. Proof-carrying proposals and investigation planning

### P41.1 Proof-carrying proposal envelope
Material AI proposals should include inspectable evidence references, assumptions, scope, rationale, contradictions considered, expected state impact, falsifier/challenge condition, model/prompt provenance, and limitations as applicable.

### P41.2 Proposal-kind obligations
Hypothesis, contradiction, unknown, assumption, investigation-action, causal, corrective, and verification proposals must each satisfy proposal-kind-specific evidence/inspection requirements.

### P41.3 Dependency-aware staleness
Proposal staleness should be based on material dependencies such as referenced evidence/assumptions/policy/semantic versions rather than invalidating every proposal on every unrelated aggregate change.

### P41.4 Proposal regeneration diff
When a proposal is regenerated, the product should show meaningful changes in evidence, assumptions, falsifier, scope, or expected impact.

### P41.5 Proposal cache provenance
Cached research/proposal results must remain attributable to exact source state, model/provider, prompt version, role, policy/domain version, and tool configuration.

### P41.6 Planner legality
The investigation planner may recommend only actions that are semantically legal and authorized or clearly marked as requiring review/permission.

### P41.7 Planner multi-objective explanation
Planner recommendations should explain discrimination, blocker resolution, cost, risk, time, reversibility, urgency, and other relevant tradeoffs rather than hiding them in an unexplained score.

### P41.8 Pareto alternatives
When candidate actions trade off dimensions, the planner should expose a small set such as fastest useful, cheapest useful, most discriminating, and safest useful options.

### P41.9 Falsification preference
When one explanation dominates prematurely, the planner should actively seek a cheap/safe falsifying or discriminating action.

### P41.10 Operational versus epistemic goals
The planner must distinguish immediate mitigation from investigation learning and causal verification, and may recommend parallel tracks.

### P41.11 Investigation budget
Users/policy may constrain time, monetary cost, operational risk, AI spend, or human effort; planner recommendations must respect those constraints.

### P41.12 Stop recommendation
The planner may recommend stopping/accepting uncertainty when policy permits and marginal information value is too low, but must not manufacture certainty.

### P41.13 Proof-carrying proposal contract
AI proposal behavior should follow [PROOF-CARRYING-PROPOSALS.md](PROOF-CARRYING-PROPOSALS.md).

### P41.14 Investigation planner contract
Planner behavior should follow [INVESTIGATION-PLANNER.md](INVESTIGATION-PLANNER.md).

## P42. Cross-investigation intelligence and research harness

### P42.1 Prior case is not current evidence
A prior investigation conclusion may seed a hypothesis or action template but must not become direct evidence for a new investigation automatically.

### P42.2 Similarity retrieval provenance
Related-case suggestions must explain why the prior case was retrieved and preserve workspace/classification/permission boundaries.

### P42.3 Anti-confirmation-bias retrieval
When prior similar cases are surfaced, the system should also seek materially similar cases with different causes or falsified explanations where available.

### P42.4 Reusable experiments
Prior experiment/action procedures may become templates, but predictions and scope must be rebound to the current investigation.

### P42.5 Recurrence analytics
Cross-investigation projections may identify repeated mechanisms, corrective failures, verification failures, assumptions, unknowns, or contributing factors.

### P42.6 Knowledge cards
Reusable knowledge artifacts may summarize mechanisms, applicability scope, required evidence, known counterexamples, and source investigations, but are not current-case evidence by themselves.

### P42.7 Research experiment identity
Research comparisons must define experiment question/hypothesis, frozen source cases, treatments, outcome measures, configuration, results, and limitations.

### P42.8 Treatment dimensions
Research treatments may vary model/provider, prompt, agent role, reconciliation, domain-pack version, policy version, planner version, UI projection, or context strategy.

### P42.9 Blinded evaluation
Where practical, research comparisons should blind evaluator from model/provider identity and randomize output order.

### P42.10 Ground-truth and open-world distinction
Synthetic/known cases may use ground truth; real investigations without certain ground truth must be evaluated on process/evidence/verification quality without fabricated correctness labels.

### P42.11 Treatment isolation
Treatments must use the same frozen source snapshot unless the experiment explicitly studies context differences.

### P42.12 Research reproducibility
Research runs must preserve enough configuration to reproduce or accurately explain non-reproducibility later.

### P42.13 Regression gates
New prompt/domain/policy/planner versions may be blocked from default promotion when benchmark evidence shows material regressions under agreed criteria.

### P42.14 EDF research harness
The platform should support controlled comparisons of evolving EDF constructs such as ContextCoverage, EvidenceClosure, UnknownEffect, topology, control-point ranking, and calibration.

### P42.15 Research report
Research experiments should be renderable through Folio with hypotheses, methods, treatments, results, limitations, and evidence.

### P42.16 Cross-investigation contract
Cross-case learning should follow [CROSS-INVESTIGATION-INTELLIGENCE.md](CROSS-INVESTIGATION-INTELLIGENCE.md).

### P42.17 Research harness contract
Research experiments should follow [RESEARCH-HARNESS.md](RESEARCH-HARNESS.md).


## P43. Governed tools and bounded autonomy

### P43.1 Tool gateway
AI/investigation tooling must execute through an explicit Tool Execution Gateway rather than unrestricted shell/network/system access.

### P43.2 Tool declaration
Every tool must declare version, purpose, input/output schema, side-effect class, required authorization/review, resource limits, data-classification limits, and provenance behavior.

### P43.3 Tool capability
Tool execution authority must come from a host-issued scoped capability, not from model-generated instructions.

### P43.4 Invocation provenance
Each tool invocation must preserve tool/version, investigation/version, actor/agent, inputs or safe digests, timing, outcome, output refs, resource/cost data where relevant, and faults.

### P43.5 Tool output is not automatically evidence
Tool output must flow through Finding and evidence-admission semantics unless explicit policy safely auto-admits a deterministic low-risk result.

### P43.6 Dry run
Side-effecting tools should support dry-run where meaningful and expose target resources, permissions, risks, and rollback.

### P43.7 Deterministic tool preference
When a question is answerable deterministically through local/tool computation, the system should prefer that over model inference where practical.

### P43.8 Tool provenance lineage
Evidence derived from tools must preserve source artifact -> invocation -> finding -> evidence lineage.

### P43.9 Bounded autonomous session
The platform should support a bounded autonomous investigation session with explicit goal, budget, allowed tools, allowed command classes, review gates, stop conditions, and semantic/policy versions.

### P43.10 Autonomy levels
The product must distinguish analysis-only, read-only tool autonomy, low-risk semantic automation, reversible external actions, and high-consequence actions rather than treating autonomy as one binary toggle.

### P43.11 Competition autonomy target
The competition target should support read-only autonomous investigation and may support narrow policy-approved low-risk semantic automation.

### P43.12 Autonomous budget
Autonomous sessions may be constrained by wall time, AI cost, model calls, tool calls, queries, data volume, and human interruption limits.

### P43.13 Autonomous stop conditions
Sessions must stop at human-review gates, unauthorized side effects, budget exhaustion, no useful legal action, unresolved protected contradictions, incompatible semantics/policy, or user stop.

### P43.14 Human checkpoints
When autonomy stops for human input, the system must summarize evidence collected, state changes/proposals, blockers, spend, and why review is required.

### P43.15 Protected transitions remain protected
Autonomy cannot silently bypass ConfirmCause, ResolveInvestigation, AcceptResidualUncertainty, corrective approval, or external high-consequence gates when policy requires human review.

### P43.16 Loop prevention
Autonomous orchestration must detect repeated actions, repeated falsified paths, identical proposals, non-improving retries, and state oscillation.

### P43.17 Tool gateway contract
Execution should follow [TOOL-EXECUTION-GATEWAY.md](TOOL-EXECUTION-GATEWAY.md).

### P43.18 Autonomous investigation contract
Autonomy should follow [BOUNDED-AUTONOMOUS-INVESTIGATION.md](BOUNDED-AUTONOMOUS-INVESTIGATION.md).

## P44. Local WASM analytics and evidence ingestion

### P44.1 Local deterministic analytics
Limen/WASM should perform deterministic event replay, semantic diff, capability projection, graph analysis, hashing, parsing, filtering, and other suitable work locally.

### P44.2 Privacy-first context preparation
Before AI disclosure, local processing should identify relevant entities and minimize/redact context according to policy.

### P44.3 Offline analytical capability
Without network, the product should retain time travel, semantic diff, counterfactual branches, evidence graph, local hashing, capability projection, and queued command workflows where locally available.

### P44.4 Large-data prefilter
Large logs/artifacts should be filterable/chunkable locally so only relevant content is sent to AI where possible.

### P44.5 Content integrity
Evidence/artifacts, exports, reports, and proof bundles should support local cryptographic digest generation where useful.

### P44.6 Evidence ingestion pipeline
Raw evidence capture must preserve original source and transformation lineage through capture, parse/extraction, finding, admission, and relationship stages.

### P44.7 Capture record
Evidence capture should record source type, actor, time, location/ref, digest, media type, size, classification, scope, and availability as applicable.

### P44.8 Transformation provenance
Every parser/extractor transformation must identify tool/parser version, input/output references/digests, warnings/errors, and whether the transformation is lossy.

### P44.9 Extraction proposals
Local parsers/AI may propose observations/findings/metadata/relationships but may not silently rewrite raw captures.

### P44.10 Mobile media evidence
Screenshot/photo/file capture must preserve the original artifact and treat extracted text/interpretation as derived.

### P44.11 Log source anchors
Extracted log findings should retain line/time/chunk anchors sufficient to return to the raw source.

### P44.12 Structured engineering evidence
Aegis faults, Praxis metrics, ROS records, tests, and GitHub diffs should be ingestible through the same provenance-preserving pipeline.

### P44.13 Deduplication
Exact/near-duplicate detection may suggest reuse/merging but must not silently merge semantically separate captures.

### P44.14 Local analytics contract
Local processing should follow [LOCAL-WASM-ANALYTICS.md](LOCAL-WASM-ANALYTICS.md).

### P44.15 Ingestion contract
Evidence capture/transformation should follow [EVIDENCE-INGESTION-PIPELINE.md](EVIDENCE-INGESTION-PIPELINE.md).


## P45. Factory consumption and kickoff determinism

### P45.1 Canonical kickoff manifest
The competition implementation must have one machine-readable kickoff manifest that identifies required components, normative documents, priority slices, screen contracts, stop-the-line conditions, success gates, and the agent execution protocol.

### P45.2 Single agent entry
A freshly initialized implementation repository must give an implementation agent one obvious canonical entry point rather than requiring the agent to discover the planning corpus ad hoc.

### P45.3 Immutable planning baseline
At competition kickoff, the planning repository baseline consumed by the factory must be pinned to an exact commit and recorded in build provenance.

### P45.4 Verified UI contract package
Percepta application/screen contracts promoted into the competition preset must be pinned to a revision that passed Percepta contract compilation/verification.

### P45.5 Conditor contract materialization
Conditor must materialize the canonical kickoff contract and referenced requirement/UI contract artifacts into the blank implementation repository without copying application implementation code.

### P45.6 Missing contract is a hard failure
The factory must not generate an agent handoff that points to a canonical execution contract that does not exist or will not be materialized by the same initialization plan.

### P45.7 Shared AGENTS surface
When Conditor writes agent guidance into a repository-level AGENTS.md shared with users or other tooling, it must own only a bounded managed region and preserve content outside that region.

### P45.8 Fresh-agent rehearsal
Before competition day, the factory must be rehearsed against a non-competition sacrificial application from an empty repository with no oral/private architecture briefing to the implementation agent.

### P45.9 Independent rehearsal scoring
A fresh-agent rehearsal does not pass solely because the provider exits successfully or marks a work item complete. Independent scoring must verify build, tests, architecture boundaries, evidence paths, and ROS completion.

### P45.10 Rehearsal failure ownership
Generic rehearsal failures must be classified back to the reusable owner such as Conditor, Ordo, ROS, Percepta, Forma, Limen, Aegis, Praxis, Folio, architecture, or external tooling rather than patched only in the sacrificial application.

### P45.11 No competition implementation before kickoff
Factory rehearsal artifacts may exercise generic/scaffold behavior and a sacrificial application, but must not contain the competition application's implementation source before the allowed coding window.

### P45.12 Rehearsal evidence
Each rehearsal should preserve factory version/commit, manifest digest, component versions, agent/provider identity, elapsed time, interventions, test/build results, and the independent score.

### P45.13 Fresh-agent acceptance target
Competition readiness requires a capable fresh agent to discover product intent, architecture boundaries, work sequencing, UI semantics, persistence boundaries, AI authority, test strategy, and completion evidence from the initialized repository itself.

### P45.14 Factory contracts
The canonical factory-consumption artifacts are [kickoff/indy-init.kickoff.json](../kickoff/indy-init.kickoff.json), [KICKOFF-EXECUTION-PACKET.md](KICKOFF-EXECUTION-PACKET.md), [FACTORY-REHEARSAL.md](FACTORY-REHEARSAL.md), and [FRESH-AGENT-FACTORY-ACCEPTANCE.md](FRESH-AGENT-FACTORY-ACCEPTANCE.md).

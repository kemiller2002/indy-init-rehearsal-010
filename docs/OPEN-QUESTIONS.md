# Open Questions and Research Register

Status: active planning register.

This file records questions we intentionally have not resolved. Its purpose is to prevent agents and implementation pressure from silently turning uncertainty into architecture.

## OQ-001 Product name

Question:
What is the durable product name after the Indy Init competition?

Current posture:
Deferred.

Constraint:
Do not encode "Indy Init" into semantic namespaces or persisted domain identity.

## OQ-002 Universal confidence model

Question:
Should the generic platform ever expose numeric confidence?

Current posture:
Deferred/Experimental.

Default:
Use multidimensional conclusion posture and qualitative evidence-linked language.

Promotion evidence:
Validated calibration behavior across relevant domains.

## OQ-003 Causal graph formalism

Question:
Should causal assertions become a formal DAG/hypergraph model?

Current posture:
Deferred.

Default:
Support references and causal sets without requiring universal graph semantics.

Reason:
Different domains may require different causal representations.

## OQ-004 Claim proposition AST

Question:
Do claims/hypotheses require a formal proposition language?

Current posture:
Deferred.

Default:
Typed identity/scope/provenance plus textual proposition and structured relationships.

Trigger:
Repeated inability to validate or compare claims without formal structure.

## OQ-005 Policy representation

Question:
Should policies remain F# functions or gain a declarative DSL?

Current posture:
ReversibleChoice.

Default:
Strongly typed F# first.

Trigger:
Need for non-code policy authoring, independent pack loading, or better policy diff/explainability.

## OQ-006 Physical event storage

Question:
Single NDJSON event file, per-event file, event batches, or other?

Current posture:
ReversibleChoice.

Decision criteria:
GitHub API write cost, conflict behavior, append atomicity, human readability, scale.

Do not make semantic event identity depend on file location.

## OQ-007 Snapshot frequency

Question:
When and how should current snapshots be regenerated?

Current posture:
ReversibleChoice.

Default:
Simple current snapshot after successful semantic commit for first release.

## OQ-008 Local browser storage

Question:
IndexedDB versus another durable local mechanism?

Current posture:
ReversibleChoice.

Default:
Use the simplest robust browser mechanism compatible with Limen/iPad Safari.

Domain model must not depend on it.

## OQ-009 Authentication for hackathon

Question:
Exact GitHub auth method under event constraints?

Current posture:
Open until organizer/deployment constraints are confirmed.

Long-term target:
GitHub App/OAuth/least privilege.

Do not normalize a PAT shortcut into production architecture.

## OQ-010 AI invocation architecture

Question:
Direct browser-to-provider, minimal service proxy, or other?

Current posture:
ReversibleChoice dependent on credential/security needs.

Constraint:
Provider secret handling may force a backend.

## OQ-011 Domain pack runtime packaging

Question:
Compiled-in F# module, declarative package, dynamic plugin, service-hosted?

Current posture:
ReversibleChoice.

Default:
Compile-time/trusted composition first.

Do not build dynamic arbitrary-code loading for the hackathon.

## OQ-012 Evidence relation core set

Question:
Which evidence relationships truly deserve CoreStable closed cases?

Current posture:
Under review.

Current candidate core:
Supports, Contradicts, Discriminates, Qualifies, Corroborates, FailsToSupport, Irrelevant, Supersedes, DependsOn, DerivedFrom.

Research task:
Attempt to reduce the core set and push domain-specific distinctions into extensions.

## OQ-013 Hypothesis lifecycle

Question:
Are Proposed/Plausible/Supported/Weakened/Falsified/Superseded the minimal stable persisted states?

Current posture:
First implementation decision made; empirical validation remains open.

First implementation:
- Leading is derived rather than persisted.
- Confirmed cause belongs to CausalAssertion rather than HypothesisState.
- Hypothesis history remains independent from causal confirmation.

Research task:
Test whether any remaining hypothesis states are better derived or can be reduced further.

## OQ-014 Claim epistemic state

Question:
Should claim epistemic states be persisted or derived?

Current posture:
Likely derived.

Research task:
Test deterministic projection from evidence relationships and policy.

## OQ-015 Contradiction entity

Question:
Should every contradiction be explicit durable state or can some remain derived?

Current posture:
Hybrid likely.

Default:
Material contradictions requiring lifecycle/resolution become durable; obvious computed conflicts may be derived until accepted/triaged.

## OQ-016 Obligation entity

Question:
Which obligations are persisted versus derived?

Current posture:
Hybrid.

Default:
Policy derives candidates; accepted/assigned/lifecycle-bearing obligations become durable.

## OQ-017 Evidence quality dimensions

Question:
Which dimensions belong in generic core versus domain extension?

Current posture:
Active research.

Current candidates:
Directness, Reliability, Independence, Precision, Completeness, Freshness, Reproducibility, VerificationStatus.

Constraint:
Do not create universal weighted score.

## OQ-018 Materiality model

Question:
Should Blocking/Material/NonMaterial/Informational be generic state?

Current posture:
StableDirection.

Research:
Test whether materiality is better represented primarily as policy relationship rather than intrinsic entity field.

## OQ-019 Expected information value

Question:
Can next-action prioritization use a meaningful quantitative score?

Current posture:
Experimental.

Default:
Explain qualitative discrimination, cost, risk, urgency.

Do not fake precision.

## OQ-020 Hypothesis generation diversity

Question:
How do we measure whether AI generated meaningfully different causal mechanisms instead of paraphrases?

Current posture:
Research question.

Potential evidence:
Mechanism category diversity, independent human evaluation, downstream discrimination value.

## OQ-021 Challenge-mode effectiveness

Question:
Does explicit adversarial challenge reduce premature convergence?

Current posture:
Testable product/research hypothesis.

Measure:
falsified leading hypotheses, discovered assumptions, contradiction detection, conclusion revision quality.

## OQ-022 Negative knowledge effectiveness

Question:
Does scoped negative knowledge reduce repeated investigation work without suppressing legitimate reopening?

Current posture:
Testable hypothesis, especially relevant to EDF.

## OQ-023 Evidence dependency inference

Question:
How much evidence dependency can/should AI infer automatically?

Current posture:
Experimental.

Default:
AI proposes dependency; human/policy accepts when material.

## OQ-024 Cross-investigation learning

Question:
When should a prior investigation influence a new one?

Current posture:
Deferred beyond initial product.

Invariant:
Prior conclusion may propose a hypothesis but is not direct evidence for the new case.

## OQ-025 Report authority

Question:
Should issued reports support approval/signoff status?

Current posture:
Likely future requirement.

Core now:
report has source investigation/version identity.

## OQ-026 Collaboration granularity

Question:
Investigation-level, entity-level, or workspace-level permissions?

Current posture:
Deployment-specific/deferred.

Core must preserve actor provenance and permission checks without hard-coding enterprise roles.

## OQ-027 Encryption of local offline state

Question:
What level of local encryption is practical/valuable in browser deployments?

Current posture:
Future security decision.

Constraint:
Do not claim encrypted-at-rest until actually provided.

## OQ-028 GitHub direct editing

Question:
Should users be allowed/supported to edit human-readable investigation files directly?

Current posture:
Open.

Default:
Treat unsupported direct semantic edits as potential integrity conflict.

Possible future:
validated import/edit workflow.

## OQ-029 Event tamper evidence

Question:
Do we need hash chaining/signatures in first production use?

Current posture:
Optional security enhancement.

Trigger:
Stronger audit/compliance/customer requirements.

## OQ-030 Artifact storage

Question:
Where should binary evidence live long-term?

Current posture:
Deferred provider/integration problem.

Core:
typed external artifact references with integrity metadata.

## OQ-031 EDF version adoption

Question:
Which EDF version should the first EDF pack target?

Current posture:
Do not assume future draft wins.

Default:
Use the then-current EDF source of truth and explicitly version the pack.

## OQ-032 EDF ContextCoverage

Question:
What exact semantics survive current EDF research?

Current posture:
Experimental/domain-specific.

Rule:
Never promote current meaning into generic investigator core.

## OQ-033 EDF EvidenceClosure

Question:
Should it be a metric, policy gate, derived projection, or multiple constructs?

Current posture:
Experimental.

Research:
Keep semantic identity versioned until role stabilizes.

## OQ-034 EDF UnknownEffect

Question:
Is UnknownEffect a specialized Unknown, effect relation, policy construct, or separate concept?

Current posture:
Experimental.

Default:
Represent through generic Unknown plus EDF extension.

## OQ-035 EDF topology

Question:
Do origin/propagation/topology constructs need a specialized graph model?

Current posture:
Experimental.

Default:
Domain extension/projection, not generic core.

## OQ-036 EDF control-point ranking

Question:
What evidence supports ranking interventions by influence/controllability/cost/confidence?

Current posture:
Active EDF research.

Default:
Derived decision aid, not core causal truth.

## OQ-037 EDF confidence/calibration

Question:
How should EDF confidence evolve as independent validation arrives?

Current posture:
Research.

Constraint:
Investigator must permit old and new calibration semantics side by side.

## OQ-038 Model ensemble

Question:
Do multiple independent models improve investigation quality enough to justify cost?

Current posture:
Experimental.

Need:
controlled comparison, blinded where practical.

## OQ-039 Automated relationship acceptance

Question:
Which low-risk AI proposals can eventually be auto-accepted?

Current posture:
Deferred until error rates are measured.

Default:
Human review for consequential proposals.

## OQ-040 Reassessment trigger

Question:
When should new evidence automatically create a reassessment obligation for prior conclusions?

Current posture:
Needs policy design.

Default:
Material contradiction or invalidated dependency creates explicit reevaluation work.

## OQ-041 Workspace layout

Question:
Exact card/panel/navigation composition?

Current posture:
ReversibleChoice governed by semantic UI contract.

Default:
Test multiple implementations against the same contract rather than lock screenshot design prematurely.

## OQ-042 Demo scenario

Question:
Which domain-neutral scenario best demonstrates falsification, contradiction, unknowns, offline state, and verification in the event time?

Current posture:
Needs experimentation.

Requirement:
Scenario must not be so contrived that the product only works for it.

## OQ-043 Minimum hackathon scope

Question:
Which slices can be omitted if time runs short?

Current posture:
See IMPLEMENTATION-SLICES.md.

Priority:
semantic kernel, policy, workspace, persistence truth, AI proposal loop, cause/verification.

## OQ-044 Long-term storage service

Question:
Do we build our own hosted persistence/API or rely on customer providers?

Current posture:
Post-hackathon product decision.

Architecture must permit both.

## OQ-045 Product commercialization

Question:
Single-user tool, organization product, consulting instrument, embedded engine, or combinations?

Current posture:
Business strategy question.

Architecture implication:
Do not hard-code assumptions that preclude embedding or organization deployment.

## OQ-046 Semantic core empirical validation

Question:
Do the generic investigation semantics actually improve root-cause work versus good unstructured analysis?

Current posture:
Must be tested.

Potential experiment:
matched investigation cases comparing free-form AI/human workflow with explicit semantic investigator.

## Register rule

When an open question is resolved:

- record the decision
- record evidence/rationale
- update DESIGN-DECISION-BOUNDARIES.md
- update affected requirements
- add/modify acceptance scenarios
- identify migrations if persisted meaning changes
- preserve the old question/decision history

Do not simply delete the question.


## OQ-047 Causal assertion support posture

Question:
Should CausalAssertion have a persisted Supported state or should support remain entirely derived until ConfirmCause?

Current posture:
First implementation derives support and persists Proposed/Confirmed/Reopened/Superseded only.

Reason:
Avoid hardening an intermediate causal-confidence ontology while EDF and cross-domain causal semantics are still being researched.

Trigger for change:
Evidence that a durable supported-but-not-confirmed causal assertion has independent workflow/history value that cannot be represented by evidence relationships, blockers, or conclusion posture.


## OQ-048 Repository identity in deep links

Question:
How should a deep link name the GitHub repository that holds an investigation, so that a copied URL resolves in another browser without leaking more than an identifier?

Current posture:
Investigation routes carry the repository as the `repo` query parameter (`owner/name`) inside the hash route ([Screen Contracts](SCREEN-CONTRACTS.md#url-contract)). A repository name is an identifier, not a credential. The fragment is not sent in HTTP requests or the `Referer` header, but it does stay in browser history and in any copied link. No token, OAuth code or key ever appears (R9.9). A recipient without read access sees a not-permitted state (R9.8).

Reason:
Every investigation, evidence item, branch and screen must be addressable by URL (R9.6, Praxis SAF-URL-1..10). Without the repository, a link opens only in the browser that already has that repository configured.

Trigger for change:
If private repository names themselves must not appear in shared links, replace `repo` with an opaque, server-resolved workspace ID. That needs a backend, which static hosting (D-203) does not provide. Record any such change as a legacy redirect (SAF-URL-7).

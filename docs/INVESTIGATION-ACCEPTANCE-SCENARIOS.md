# Investigation Acceptance Scenarios

Status: normative semantic acceptance scenarios.

These scenarios are intended to become executable domain and UI tests. They verify investigation meaning, not visual styling.

## A1. Preserve observation and interpretation separately

Given a user enters:
"Orders started failing after the 2 PM deployment."

When the agent structures the input

Then the original statement is preserved
And "orders started failing" may be represented as an Observation
And "the 2 PM deployment caused the failures" must not be represented as established Evidence
And a deployment-causality hypothesis may be proposed separately.

## A2. Multiple hypotheses remain active

Given an observation can plausibly be explained by:
- deployment regression
- upstream outage
- exhausted connection pool

When the investigation begins

Then all materially plausible hypotheses can coexist
And selecting a leading hypothesis does not remove the others.

## A3. Failure to support is not contradiction

Given H1 predicts an elevated error signature
And a log sample does not contain that signature
But the sample is incomplete

Then the evidence may FailToSupport H1
But must not automatically Contradict or Falsify H1.

## A4. Necessary prediction can falsify within scope

Given H1 states that every failing request must contain header X
And a verified failing request does not contain header X
And the record is within H1's declared scope

Then H1 may transition to Falsified
And the falsifying evidence and scope are recorded.

## A5. Reopening requires new basis

Given H1 is Falsified

When a user attempts to reopen H1 without new evidence, corrected evidence, corrected scope, or an invalidated premise

Then ReopenHypothesis is unavailable
And the blocking reason is visible.

When corrected evidence invalidates the earlier falsification

Then ReopenHypothesis becomes available
And reopening records that basis.

## A6. Two records from one source are not two confirmations

Given chart C and table T both derive from query Q
And both support H1

Then C and T may both be Evidence
But the system records their common dependency
And must not represent them as independent corroboration.

## A7. Contradictory evidence remains visible

Given E1 supports H1
And E2 materially contradicts H1
And both are verified

Then H1 may not be projected as unambiguously supported
And the contradiction is first-class
And any blocked conclusion capability explains the contradiction.

## A8. Unknown blocks transition

Given U1 is a Blocking Unknown required by the cause-confirmation policy

When the user attempts ConfirmCause

Then the transition is illegal
And U1 is identified as a blocker
And the UI offers a path to resolve or explicitly accept U1 when policy permits.

## A9. Unknown is not false

Given no evidence exists for Claim C

Then C may be Unknown or Unsupported
But it must not become Falsified solely because evidence is absent.

## A10. Assumption dependency is visible

Given H1 relies on assumption A1
And A1 is ProvisionallyAccepted

Then H1 exposes that dependency
And confirmation policy may require A1 verification.

When A1 is Rejected

Then hypotheses and conclusions materially depending on A1 require reevaluation.

## A11. Retrospective prediction is not prospective

Given an experiment result is already known

When a prediction matching that result is added afterward

Then it is marked retrospective
And cannot be presented as a prospective prediction.

## A12. Prefer discriminating action

Given H1 and H2 both predict X for Test A
And H1 predicts Y while H2 predicts Z for Test B

Then the investigator identifies Test B as more discriminating when cost/risk do not override that preference
And it can explain why.

## A13. Temporal sequence is not sufficient causation

Given deployment D occurred before failure F

Then the system may create a causality hypothesis
But must not confirm D as cause solely because D preceded F.

## A14. Counterfactual strengthens causal reasoning

Given H1 claims deployment D causes F
And a rollback returns the system to the prior version while relevant conditions remain materially comparable
And F disappears

Then the finding can provide intervention/counterfactual support for H1
But alternative explanations and confounders remain assessable.

## A15. Contributing factor is not root cause

Given condition C increases severity but cannot explain the failure without cause R

Then C may be represented as ContributingFactor
And R may be RootCause if confirmation policy is satisfied
And the two roles remain distinct.

## A16. Causal set

Given failure F requires conditions A and B together
And neither A nor B alone produces F

Then the model can represent {A,B} as a causal set
And does not force one member to be the single deepest root cause.

## A17. Negative knowledge is scoped

Given network latency is ruled out for version 2 under environment E

Then the system records scoped NegativeKnowledge
And later evidence from version 3 does not automatically inherit the same conclusion.

## A18. Agent proposal is not evidence

Given an agent says:
"The parser probably changed denominator semantics."

Then that statement may create a proposed Hypothesis or Interpretation
But it must not be stored as direct Evidence unless an external evidence source supports it.

## A19. User rejects agent proposal

Given an AgentProposal for H1

When the user rejects H1

Then authoritative state does not include H1 as an accepted hypothesis
And the rejected proposal may remain in proposal/audit history according to policy.

## A20. Corrective action is not verification

Given a code fix is deployed

Then CorrectiveAction may become Implemented
But the investigation does not become Resolved if verification is required.

## A21. Verification failure reopens work

Given a corrective action is AwaitingVerification
And the verification criterion fails

Then the action transitions to VerificationFailed or ReworkRequired
And the investigation returns to an active state as required
And the failed attempt remains in history.

## A22. Material contradiction blocks confirmation

Given a policy requires all material contradictions resolved
And contradiction C1 is unresolved

Then ConfirmCause is unavailable
And C1 is named in the blocking reasons.

## A23. Accepted residual uncertainty

Given policy permits acceptance of a non-blocking unknown
And the authorized human explicitly accepts it with rationale

Then the investigation may proceed
And the accepted residual uncertainty remains visible in reports and conclusion posture.

## A24. Falsified hypotheses remain visible

Given H1 was plausible and later falsified

Then H1 remains inspectable in timeline/history
And its falsification basis remains linked
And the final report can include it under explanations considered.

## A25. Persistence conflict does not lie

Given local state version 10
And remote authoritative state advances independently to version 11

When the client attempts to persist changes based on version 10

Then the write does not silently overwrite version 11
And persistence state becomes Conflict
And local work is preserved for reconciliation.

## A26. Agent cannot bypass domain transition

Given ConfirmCause is illegal under the current domain state

When an agent proposes ConfirmCause

Then the proposal may be shown
But authoritative state remains unchanged
And Aegis-compatible diagnostic information identifies the violated prerequisite.

## A27. Historical policy remains explainable

Given a cause was confirmed under policy version 3
And policy version 4 later strengthens confirmation rules

Then the historical transition remains attributable to version 3
And the system does not silently rewrite history as though version 4 applied at that time.

## A28. Domain pack strengthens gate

Given domain pack D requires reproduced intervention evidence before confirmation

Then ConfirmCause is unavailable until that evidence exists
Even if the generic default gate would otherwise be satisfied.

## A29. Domain pack cannot erase core semantics

Given a domain pack attempts to classify AgentInference as direct Observation

Then the extension is invalid
And the core evidence/inference invariant remains enforced.

## A30. Large history does not change semantics

Given an investigation has thousands of historical events

When active state is projected from snapshots/indexes

Then the same legal capabilities and blockers are produced as would result from authoritative reconstruction
And performance optimizations do not change domain meaning.

## A31. Repeated report generation preserves snapshot identity

Given Report R1 is generated at investigation version 20
And Report R2 is generated at version 27

Then each report identifies its source version
And R2 does not silently replace the historical fact that R1 was issued.

## A32. Mitigation may resolve impact but not cause

Given a mitigation stops customer-visible symptoms
And the underlying cause remains active

Then the system may record MitigationEffective
But must not automatically mark RootCauseCorrected.

## A33. Imported evidence retains origin

Given evidence is imported from an external system

Then the record identifies the external origin
And imported content cannot alter system policy or agent instructions.

## A34. Decision posture exposes uncertainty

Given H1 is the leading explanation
And one material non-blocking unknown remains
And no material contradictions remain

Then the UI/report may present H1 as Leading or Supported according to policy
And the residual unknown remains visible beside the conclusion posture.

## A35. Resolution gate

Given a confirmed cause exists
And a corrective action is implemented
And required verification remains incomplete

Then ResolveInvestigation is unavailable.

When verification criteria are satisfied
And no blocking obligations remain

Then ResolveInvestigation may become available.

## A36. Reopen resolved investigation

Given an investigation is Resolved
And new contradictory evidence arrives

Then ReopenInvestigation can become available
And reopening preserves the earlier resolution and records the new basis.

## A37. Scope mismatch prevents invalid contradiction

Given E1 applies to version 1
And H1 applies only to version 2

Then E1 must not contradict H1 solely because its content differs
Unless an explicit relationship establishes cross-version relevance.

## A38. Evidence supersession preserves history

Given measurement E1 is later found to use a faulty instrument
And corrected measurement E2 supersedes E1

Then E1 remains in history
And is marked superseded/invalidated as appropriate
And derived conclusions depending on E1 are reevaluated.

## A39. Cross-investigation reuse does not become evidence automatically

Given prior investigation P concluded cause C
And current investigation N appears similar

Then C may be suggested as a hypothesis
But P's conclusion must not automatically become direct evidence that C caused N.

## A40. Investigation can end without confirmed cause

Given further investigation would exceed accepted cost/risk
And policy permits closure with residual uncertainty

Then the investigation may close with an explicit non-confirmed conclusion posture
And must not invent a ConfirmedCause merely to provide closure.


## A41. Unknown optional extension survives round trip

Given an investigation contains optional extension edf:new-construct@2
And a client understands the core investigation but not that extension

When the client edits unrelated core state and saves

Then the extension is preserved without semantic loss
And it is not silently removed, rewritten, or reclassified.

## A42. Unknown required extension protects writes

Given an investigation requires edf:critical-policy-construct@3 to interpret legal transitions
And the client does not understand that extension

Then the investigation may be shown in degraded or read-only form
And the client must not permit authoritative writes whose correctness depends on the missing semantics.

## A43. Experimental feature can be disabled safely

Given experimental construct experiment:edf-topology@0.3 was used in investigation I1
And the experiment is later disabled for new investigations

Then I1 remains readable
And its experimental records remain identifiable
And new investigations do not require the construct.

## A44. Material semantic change creates a new identity/version

Given edf:context-coverage@1 and a proposed replacement have materially different meanings

Then the replacement receives a distinct semantic version or identifier
And historical @1 records are not silently interpreted as the new meaning.

## A45. Policy change does not rewrite historical legality

Given transition T was legal under EDF policy P1
And EDF policy P2 later changes its prerequisites

Then T remains historically attributable to P1
And the system does not mark T as though P2 had governed it at the time.

## A46. Reassessment creates new state

Given conclusion C1 was reached under policy P1
And newer evidence or policy P2 warrants reevaluation

When the investigation is reassessed

Then C1 remains historical fact
And a new reassessment record links C1, P1, the new basis, and the current conclusion posture.

## A47. EDF metric replacement requires no core schema change

Given EDF retires metric M1 and introduces semantically different M2

Then M2 can be introduced through the EDF domain pack
And existing M1 observations remain interpretable
And the generic investigation kernel does not require a schema change solely because the EDF metric changed.

## A48. Retired construct has generic fallback

Given an investigation contains an EDF construct whose specialized renderer is no longer installed

Then the client can still show its semantic identifier, version, provenance, stability status, and safe generic content
And critical information does not disappear from the investigation.

## A49. Extension cannot weaken core invariant

Given an experimental EDF profile attempts to permit an AgentProposal to become direct Evidence without the required authoritative transition

Then activation or transition fails
And an Aegis-compatible fault identifies the violated core invariant.

## A50. AI proposal is revalidated after domain version change

Given an agent creates a proposal under EDF domain pack version D1
And the active domain pack changes to D2 before acceptance
And D2 changes semantics relevant to the proposal

Then the proposal is revalidated or marked stale
And it is not accepted automatically under D2.

## A51. Migration cannot invent unavailable historical data

Given a newer semantic model requires field F
And an old investigation never recorded enough information to derive F

When the old investigation is loaded or migrated

Then F remains Unknown, unavailable, or explicitly not recorded
And the migration must not synthesize a false historical value.

## A52. EDF construct does not become core by convenience

Given a new EDF construct is useful for multiple EDF experiments
But cross-domain necessity and stability have not been established

Then it remains a versioned EDF extension
And the generic kernel continues to operate without understanding the construct.


## A53. New EDF construct needs no generic aggregate change

Given EDF introduces experimental construct X
And X can be represented through a versioned extension envelope

When X is added to the EDF pack

Then the generic Investigation aggregate does not require a new core field or union case solely to support X.

## A54. Domain pack cannot create alternate history

Given an EDF pack wants specialized events

Then those events participate in the same authoritative investigation event/history contract
And the pack must not create a parallel authoritative history that can disagree silently with the core investigation.

## A55. Pack policy adds blocker without duplicating UI logic

Given EDF policy requires evidence E before ConfirmCause
And E is absent

Then the domain capability projection returns the blocker
And the UI consumes that blocker
And the UI does not need a hard-coded EDF-specific transition rule.

## A56. Optional pack removal preserves core investigation

Given an investigation contains optional EDF extensions
And the EDF pack is unavailable

Then generic core observations, evidence, hypotheses, unknowns, obligations, and conclusions remain inspectable
And optional EDF extension information is preserved through generic fallback.

## A57. Required pack removal fails safely

Given an investigation's legal interpretation depends on a required EDF extension
And the EDF pack is unavailable

Then unsafe semantic writes are disabled
And the system explains which required semantics are missing.

## A58. Reversible persistence choice

Given GitHub is the active persistence provider
And a future deployment uses a service/database provider

Then InvestigationId, domain semantics, event meaning, and canonical export remain unchanged
And GitHub-specific paths or SHAs are not required by the generic domain model.

## A59. Deferred question is not silently settled

Given universal causal DAG semantics are classified Deferred
And a feature needs to display causal relationships

Then the implementation may use a reversible projection or extension
But must not make a universal DAG representation mandatory persisted core state without an explicit promotion decision.

## A60. Experimental EDF result can be rejected cleanly

Given experimental EDF construct X was used in research investigations
And later research rejects X

Then new investigations can stop producing X
And historical X records remain readable and attributable
And no generic core migration is required solely to remove X from future EDF use.


## A61. UI consumes domain blocker

Given ConfirmCause is blocked by contradiction C7
When the workspace renders the ConfirmCause capability
Then the blocker originates from the policy/transition engine
And the UI shows the reason
And no duplicate UI-specific EDF legality rule is required.

## A62. Actor permission differs from semantic legality

Given AddEvidence is semantically legal
And actor A lacks permission
Then the capability explains the permission blocker.

Given actor B has permission
And the investigation is in a state where AddEvidence is semantically illegal under policy
Then the capability explains the domain blocker.

## A63. Policy exception is auditable

Given policy allows an authorized user to accept a non-blocking residual unknown
When the user exercises the exception
Then actor, reason, authority, scope, policy version, and residual uncertainty are recorded
And the core Unknown semantics are not changed.

## A64. Retry does not duplicate events

Given command C with idempotency key K was remotely applied
And network loss prevents the client from seeing the acknowledgement
When C with K is retried
Then the prior result is returned or recognized
And no duplicate semantic event is appended.

## A65. Stale offline command is revalidated

Given command C was queued offline against aggregate version 20
And remote state changes to version 24
When the client reconnects
Then C is not blindly replayed
And it is re-evaluated against version 24 and current policy
And any changed blockers are shown.

## A66. Policy comparison does not mutate investigation

Given immutable investigation snapshot V30
When EDF policy P1 and P2 are both evaluated against V30
Then two derived policy-evaluation results are produced
And V30 authoritative state is unchanged.

## A67. Policy pin supports reproducibility

Given research investigation I pins EDF policy P1
And EDF P2 later becomes default
Then reloading I uses or explicitly identifies P1 for its historical/continued research behavior
And P2 does not silently replace the pinned version.

## A68. Multi-event transition is atomic remotely

Given one allowed command emits events E1, E2, and E3
And the provider cannot persist the complete event set
Then the transition is not represented as remotely committed
And partial provider writes are recovered or treated as failure according to the provider contract.

## A69. GitHub SHA remains provider metadata

Given a GitHub-backed investigation moves to another provider
Then its InvestigationId and semantic AggregateVersion remain meaningful
And no core entity requires the former GitHub SHA to retain identity.

## A70. Offline restart preserves pending work

Given the user records evidence offline
And the application is closed before synchronization
When the application restarts
Then the last remote-confirmed state is identifiable
And the pending evidence command is recoverable
And the UI shows that it has not yet been remotely confirmed.

## A71. External artifact changed

Given evidence E references artifact A with digest H1
And later retrieval of A produces digest H2
Then the product marks the artifact as changed or integrity-mismatched
And does not silently treat H2 content as the originally evaluated artifact.

## A72. External artifact unavailable

Given evidence E references external artifact A
And A can no longer be retrieved
Then E remains in investigation history
And artifact availability is shown as unavailable
And the system does not delete E solely because A is unavailable.

## A73. Complete export/import round trip

Given investigation I has core events, EDF extensions, unresolved unknowns, historical conclusions, and report references
When a complete canonical export is imported into a compatible empty deployment
Then entity IDs, semantic/policy versions, extension envelopes, authoritative event order, provenance, and uncertainty are preserved.

## A74. Import collision cannot overwrite silently

Given target deployment already contains InvestigationId I
When a package with InvestigationId I is imported
Then the system requires an explicit duplicate/same-history/copy/reconciliation choice
And does not silently overwrite the existing investigation.

## A75. Cross-provider migration preserves meaning

Given investigation I is exported from GitHub provider G
And imported into service provider S
Then InvestigationId, AggregateVersion, semantic event meanings, domain-pack references, and historical conclusions remain unchanged
And only provider metadata changes.

## A76. Research experiment pins immutable source

Given experiment X consumes investigation version 44
And the investigation later advances to version 51
Then X remains attributable to version 44
And rerunning X as a reproduction can resolve the same source state.

## A77. Snapshot mismatch is detected

Given snapshot S declares aggregate version 40
And authoritative event history establishes a different state at version 40
Then S is rejected or marked invalid
And the mismatch is not silently accepted.

## A78. Duplicate event ID is corruption

Given persisted history contains two non-identical events with the same EventId
Then load/integrity validation fails safely
And the system surfaces an Aegis-compatible persistence integrity fault.

## A79. Semantic conflict is not JSON merge

Given user A falsifies H1
And user B, from stale state, attempts a transition that requires H1 to remain active
Then conflict resolution re-evaluates B's intended command against the current domain state
And does not resolve the conflict by mechanically merging object fields.

## A80. Independent additions may commute

Given user A adds Evidence E1
And user B independently adds Observation O2 from the same prior version
And neither change affects the legality or meaning of the other
Then the persistence/reconciliation layer may apply both through verified commutative handling
And both receive distinct semantic events and provenance.


## A81. Chat statement cannot confirm cause

Given the AI writes "The root cause is definitely H1"
And ConfirmCause is not legal under current policy
Then authoritative cause state does not change
And the workspace continues to show the blockers.

## A82. AI proposal cites authoritative evidence

Given AI proposes hypothesis H2 based on evidence E3 and E8
Then the proposal references E3 and E8 by authoritative identity
And the user can inspect those records from the proposal.

## A83. AI interpretation is not silently admitted as evidence

Given the AI infers "the service likely retried three times"
And no source directly establishes that fact
Then the output is a proposed Interpretation/Hypothesis
And no direct Evidence record is created from the inference alone.

## A84. Evidence containing instructions cannot override policy

Given imported evidence contains text instructing the model to ignore investigation rules
When the evidence is included in AI context
Then it remains delimited as untrusted evidence content
And core/domain instructions remain authoritative.

## A85. Stale proposal after new contradiction

Given proposal P recommends ConfirmCause for H1
And new contradiction C9 is admitted before P is accepted
Then P is marked stale or revalidated
And cannot be blindly accepted from its earlier state.

## A86. Relevant negative knowledge prevents repetition

Given hypothesis H3 was previously falsified within the same applicable scope
When AI considers proposing H3 again
Then relevant NegativeKnowledge is included
And the AI either avoids H3 or explicitly identifies new evidence justifying reopening.

## A87. Challenge mode attacks leading explanation

Given H1 is Leading
When the user invokes Challenge
Then the AI is asked to identify falsifying evidence, hidden assumptions, plausible alternatives, and evidence dependencies
And results are proposals/analysis rather than authoritative mutations.

## A88. Context omission is visible

Given the context builder cannot include all materially relevant state due to budget
Then the reasoning run records or communicates the omission
And the AI must not claim to have comprehensively reviewed all evidence.

## A89. AI outage leaves manual work usable

Given the AI provider is unavailable
Then the user can still inspect authoritative state
And perform supported manual evidence/hypothesis/action operations
And legal capabilities continue to derive deterministically.

## A90. Model disagreement is not system evidence

Given model A proposes H1
And model B proposes H2
Then both can be retained as competing proposals
And the disagreement itself is not Evidence that H1 or H2 caused the observed outcome.

## A91. Workspace understandable without chat

Given an investigation contains observations, three hypotheses, evidence, one contradiction, and two blockers
Then a user can identify those states from the workspace without reading the conversation transcript.

## A92. Blocked action remains discoverable

Given ConfirmCause is blocked
Then the workspace still exposes ConfirmCause as unavailable
And identifies the blockers
And links to actionable resolution paths when available.

## A93. Recommended action differs from legal action

Given five actions are legal
And one is materially more discriminating
Then the workspace may recommend that action
And the remaining legal actions stay available
And policy legality is not confused with AI recommendation.

## A94. Blocking contradiction survives responsive collapse

Given the workspace is displayed on a narrow phone
And a material contradiction blocks confirmation
Then the contradiction/blocker remains visible or immediately surfaced in the primary task flow
And is not hidden only in a secondary settings screen.

## A95. Local pending state is understandable

Given evidence E12 was accepted locally while offline
Then the workspace can show E12 in the local optimistic state
And clearly indicates synchronization is pending
And does not present it as remotely confirmed.

## A96. Original observation remains inspectable after normalization

Given the user entered original text O
And the AI creates normalized observation N
Then the workspace allows inspection of O and N separately
And editing N does not modify O.

## A97. Hypothesis comparison exposes assumptions

Given H1 and H2 depend on different material assumptions
Then comparison view exposes those assumption dependencies
And does not rank hypotheses solely on count of supporting evidence.

## A98. Cause review exposes alternatives

Given H1 is eligible for confirmation
And H2 remains a materially plausible but addressed alternative
Then Cause Review shows how H2 was addressed before confirmation.

## A99. Corrective implementation does not look verified

Given corrective action CA1 is Implemented
And no verification result exists
Then the workspace displays Awaiting Verification or equivalent
And does not display the investigation as fully resolved when verification is required.

## A100. Screen reader receives material state change

Given H2 transitions to Falsified
When the workspace updates
Then assistive technology receives an appropriate semantic announcement
And the change is not communicated only by color or animation.


## A101. Evidence cannot become instructions

Given external evidence contains prompt-like instructions
When AI analyzes that evidence
Then those instructions are treated as untrusted content
And they cannot override core/domain instructions or tool permissions.

## A102. Authorized user can still be semantically blocked

Given user U has write permission
And ConfirmCause violates an unresolved core/domain blocker
Then U cannot confirm the cause
And the UI explains the semantic blocker rather than treating authorization as sufficient.

## A103. Semantically legal user can still be unauthorized

Given AddEvidence is legal in the current investigation state
And user U lacks the required workspace permission
Then the command is rejected for authorization
And domain legality remains distinguishable from the authorization failure.

## A104. Credential never enters investigation state

Given the application has a GitHub or AI credential
When investigation state is saved, exported, reported, or sent for AI reasoning
Then the credential is absent unless a specific secure transport mechanism explicitly requires it outside semantic state.

## A105. Malicious rich text is inert

Given imported or AI-generated content contains script/event-handler/unsafe HTML
When rendered in the application
Then executable content is sanitized or rejected
And the content cannot execute with application privileges.

## A106. Restricted data can block AI disclosure

Given an investigation is classified Restricted
And current deployment policy forbids sending Restricted content to the configured AI provider
Then AI invocation requiring that content is unavailable or degraded with explanation
And the data is not transmitted.

## A107. Export permission is distinct

Given user U may read an investigation
And deployment policy requires separate export privilege
Then U can inspect the investigation
But complete export is unavailable with an authorization explanation.

## A108. Pack cannot access undeclared capability

Given domain pack D declares no external-network capability
When D attempts external network access through the host
Then the host denies the operation
And surfaces a structured extension/security fault.

## A109. External artifact digest protects original meaning

Given artifact A was admitted with digest H1
And the source later serves different bytes H2
Then the mismatch is visible
And H2 is not silently substituted as the original evidentiary artifact.

## A110. Session expiry preserves unsynced work

Given the user has unsynced local commands
And authentication expires
Then local work remains recoverable
And remote synchronization pauses until reauthentication/authorization succeeds.

## A111. Revoked access stops remote writes

Given user access to the persistence workspace is revoked
When pending commands attempt to sync
Then remote writes stop
And the application surfaces authorization state
And it does not retry indefinitely as though the provider were merely offline.

## A112. Security fault does not leak secrets

Given an authentication or provider failure occurs
When an Aegis fault is shown to a normal user
Then the explanation is actionable
And tokens, secrets, unrelated data, and internal stack traces are not exposed.

## A113. Report respects redaction/classification

Given evidence E is redacted from a report audience
When Folio generates the report
Then the restricted content is not embedded
And the report can indicate that redaction occurred according to policy.

## A114. Direct repository tampering is detected

Given a GitHub-backed investigation file is manually modified into an invalid event sequence
When the application loads it
Then integrity validation fails safely
And AI is not asked to reconstruct a plausible history silently.

## A115. Domain pack cannot bypass persistence

Given a domain pack attempts to write directly to provider files
Then authoritative mutation is denied unless performed through the host persistence/command contract
And pack code cannot create an alternate hidden source of truth.


## A116. Leading posture is derived

Given H1 and H2 are both Supported
And current evidence causes the comparison projection to prefer H1
Then H1 may be shown as Leading
And no HypothesisLeading authoritative event is required.

When later evidence makes H2 preferable
Then the derived Leading posture may change
And historical hypothesis lifecycle events remain unchanged.

## A117. Cause confirmation does not confirm a hypothesis state

Given supported hypotheses H1 and H2 jointly form causal assertion C1
When C1 satisfies the cause-confirmation gate
Then CauseConfirmed is recorded for C1
And H1/H2 remain their historical hypothesis states
And the system does not require a one-to-one HypothesisConfirmed transition.

## A118. Causal set confirmation

Given causal assertion C1 contains members A and B
And neither A nor B alone is sufficient
When C1 satisfies policy
Then C1 can become Confirmed as a causal set
And the model does not force A or B to become the single root cause.

## A119. Reopen confirmed cause preserves confirmation

Given causal assertion C1 was Confirmed
And new material contradictory evidence arrives
When ReopenConfirmedCause is accepted
Then C1 becomes Reopened/currently under investigation
And the prior CauseConfirmed event remains in history.

## A120. Generic state setter is unavailable

Given a caller attempts to set HypothesisState or InvestigationLifecycle directly
Then no generic authoritative command exists for that mutation
And the caller must use a semantic command whose prerequisites are evaluated.

## A121. Proposal acceptance revalidates

Given proposal P was legal when generated at version 12
And investigation state advances to version 16
When the user accepts P
Then the target command is evaluated against version 16/current policy
And ProposalAccepted is emitted only if that target command succeeds.

## A122. Conclusion cannot upgrade causal authority

Given no confirmed causal assertion exists
When a caller records a narrative conclusion saying "Confirmed Cause"
Then the conclusion command is blocked or restricted to a legally supported strength
And narrative text cannot create causal authority.

## A123. Event correction is append-only

Given EvidenceAdmitted event E1 contains metadata later found incorrect
When the correction is accepted
Then a corrective/superseding event is appended
And E1 is not edited in place.

## A124. Observation original content is immutable

Given observation O1 preserves the user's original statement
When normalization or correction is needed
Then a normalization/supersession event is appended
And O1's original source text remains unchanged.

## A125. Pure event evolution is deterministic

Given identical prior state S and event envelope E
When evolve is executed repeatedly
Then it produces the same resulting projection
And the result does not depend on clock, network, AI, or random state.

## A126. Pure command decision is deterministic

Given identical state, command, semantic manifest, policy set, and actor context
When decide is executed repeatedly
Then the semantic decision and blocker set are equivalent.

## A127. Structurally impossible transition cannot be enabled by policy

Given H1 is Falsified
And a policy attempts to allow MarkHypothesisSupported directly without reopening
Then the structural transition remains illegal
And policy cannot override the core lifecycle table.

## A128. Policy can block structurally legal transition

Given H1 is Plausible
And MarkHypothesisSupported is structurally possible
But required evidence is missing under active policy
Then the command is Blocked
And the blocker identifies the evidence requirement.

## A129. Historical event carries policy attribution

Given command C is allowed under policy P3
When its semantic event is committed
Then the event envelope identifies P3 or immutable equivalent references sufficient to explain historical legality later.

## A130. Extension command does not expand core command union

Given EDF introduces domain command X
And X is representable through the versioned extension command boundary
Then X can be processed by the EDF pack
And no new generic CoreCommand case is required solely for X.

## A131. Provider type cannot enter domain identity

Given GitHub persistence assigns SHA S to a commit
Then S may exist in persistence/application state
But core InvestigationId, EventId, HypothesisId, and AggregateVersion remain provider-independent.

## A132. Reopened investigation returns active while history remains

Given investigation I is Resolved
When ReopenInvestigation succeeds
Then current lifecycle becomes Active
And the earlier InvestigationResolved event remains authoritative history
And Reopened need not be a permanent lifecycle enum case.

## A133. Corrective verification has no shortcut setter

Given CA1 is Implemented
When a caller attempts to mark the investigation Resolved without verification required by policy
Then ResolveInvestigation is blocked
And no SetVerified or SetResolved bypass exists outside semantic commands.

## A134. Unknown extension event is preserved

Given event history contains an optional extension event unknown to the generic client
When the client loads and later saves unrelated compatible state
Then the extension event/envelope is preserved
And no core event case is invented by guessing its meaning.

## A135. Domain assembly has no infrastructure dependency

Given the generic domain project is built
Then it can be compiled/tested without GitHub, browser, AI-provider, network, or persistence adapter assemblies
And infrastructure depends inward toward Domain rather than the reverse.


## A136. Time travel reconstructs authoritative state

Given investigation I has authoritative history through version 42
When the user selects version 18
Then the system reconstructs the state that followed event 18
And later evidence/events do not appear as though they were already known at version 18.

## A137. Time travel is read-only by default

Given the user is viewing version 18 while current is 42
When the user attempts an authoritative mutation
Then the product requires returning to current state or explicitly creating a branch/fork
And it does not append a command against the historical projection silently.

## A138. Semantic diff is not raw JSON diff

Given versions 18 and 24 differ
When the user requests a comparison
Then the primary diff identifies semantic additions/transitions/blocker/capability changes
And raw serialization is not required to understand the change.

## A139. What changed my mind cites real causes

Given H1 was Leading at version 18 and not Leading at version 24
When the user asks why
Then the explanation references the actual events/evidence/policy changes that altered the projection
And it does not invent a narrative unsupported by state.

## A140. Counterfactual branch cannot mutate source

Given branch B starts from investigation I version 24
When hypothetical evidence is invalidated inside B
Then I remains unchanged
And B records its hypothetical modification separately.

## A141. Counterfactual evidence never becomes fact automatically

Given branch B contains hypothetical evidence H
When B is promoted to a follow-up investigation
Then H is not admitted as real Evidence unless separately sourced/admitted
And branch provenance remains visible.

## A142. Evidence sensitivity is explainable

Given current conclusion depends materially on E7
When the user runs evidence-removal sensitivity
Then the product shows which derived posture/capabilities change without E7
And labels the result as hypothetical analysis.

## A143. Assumption sensitivity exposes fragility

Given causal assertion C depends on assumption A4
When branch analysis treats A4 as rejected
Then affected hypotheses/causal assertions/blockers are shown
And source state remains unchanged.

## A144. Blind agents do not see each other

Given six agent roles run in blind phase
Then each initial run is bound to the same frozen investigation version
And no initial run context includes another role's output.

## A145. Reconciliation is not independent evidence

Given reconciliation agent R has read Generator G's proposal
When R produces a similar proposal
Then the UI/provenance does not count R as independent corroboration of G.

## A146. Multi-agent disagreement produces a discriminating question

Given agents materially disagree about whether H1 or H2 better explains E
When reconciliation runs
Then it should be able to propose evidence/action that would distinguish the disagreement where practical
And the disagreement is not hidden by majority vote.

## A147. One agent failure does not corrupt panel

Given Evidence Auditor returns invalid structured output
When the multi-agent panel completes
Then the failed run is marked diagnosably
And other agent runs/proposals remain available
And authoritative investigation state is unchanged.

## A148. Tournament uses frozen source

Given models M1 and M2 are compared
When both run on research task T
Then both runs identify the same frozen InvestigationId/AggregateVersion and semantic/policy manifest
And later investigation changes do not alter the recorded comparison input.

## A149. Tournament does not declare arbitrary winner

Given M1 and M2 differ
Then the product may show metrics and experimental evaluation results
But it does not invent an overall winner absent a defined evaluation rule.

## A150. Evidence dependency graph reveals common source

Given E1, E2, and E3 all derive from source S
When dependency view renders
Then the common lineage is visible
And the UI does not imply three independent confirmations.

## A151. Graph has accessible alternative

Given a graph visualization is available
Then equivalent material information is accessible through a keyboard/screen-reader usable list/table/tree projection
And core work does not require interpreting visual geometry.

## A152. Causal-set visualization does not force single root

Given causal assertion C has members A and B
Then the causal view can show both members and roles
And it does not force either member into a false single-root hierarchy.

## A153. Uncertainty map cells are explainable

Given an uncertainty heatmap marks H1 as high contradiction burden
When the user activates that cell
Then the UI lists the contradictions/evidence causing the state
And color/intensity alone is not the explanation.

## A154. Hypothesis Battlefield exposes falsifier

Given active hypothesis H1
Then Battlefield view exposes or can request a falsifying prediction/action
And it does not only show supporting evidence.

## A155. What would falsify this remains a proposal

Given the AI proposes test T as the strongest falsifier
Then T is a ProposedInvestigationAction
And it does not execute or become authoritative without normal acceptance/policy.

## A156. What are we missing finds orphan evidence

Given evidence E9 is admitted but linked to no claim/hypothesis/action
When completeness diagnostics run
Then E9 is surfaced as orphan/uninterpreted evidence
And the diagnostic itself does not invent a relationship.

## A157. Assumption miner does not create facts

Given AI identifies implicit assumption A
Then A is a proposal
And it is not treated as an accepted assumption or fact until normal acceptance rules succeed.

## A158. Contradiction miner does not resolve contradiction

Given AI proposes candidate contradiction C
Then the user can inspect participating entities/reason
And accepting C records the contradiction
And AI does not silently decide which side is correct.

## A159. Circular reasoning warning is derived

Given a support chain forms a candidate reasoning cycle
When diagnostics detect it
Then the system raises an inspectable warning/proposal
And does not rewrite evidence relationships automatically.

## A160. Prediction pre-registration preserves timing

Given action T is about to run
When prospective predictions are pre-registered
Then their text, author, and aggregate version are frozen before result capture
And later edits require explicit supersession/correction.

## A161. Reproducibility bundle pins source

Given experiment/action A produces a reproducibility bundle
Then the bundle identifies exact investigation version, predictions, procedure, artifacts, semantic/domain/policy versions, and result
And later state changes do not alter the bundle silently.

## A162. Replay follows event order

Given investigation history has aggregate versions 1..N
When replay runs
Then steps follow authoritative aggregate order
And timestamps do not reorder events contrary to aggregate version.

## A163. Replay supports reduced motion

Given user prefers reduced motion
Then replay remains usable through step/static transitions
And no material information depends on animation.

## A164. Spectator mode is read-only

Given the application is in Spectator mode
Then investigation state can be explored/replayed
And no authoritative mutation control is available unless the user explicitly leaves Spectator mode with appropriate permission.

## A165. Factory telemetry uses real evidence

Given Factory View shows 120 passing tests
Then that count comes from actual engineering evidence/telemetry
And is not a hard-coded presentation number.

## A166. Requirements proof graph links real artifacts

Given requirement R is shown as implemented
Then the proof view can navigate to its acceptance evidence/work item/test/commit/runtime evidence where available
And missing links remain visible rather than fabricated.

## A167. Build proof bundle binds exact artifact

Given proof bundle P is generated
Then P identifies the exact final build/commit/deployment/version it describes
And later commits do not silently update P.

## A168. Self-diagnostic mode uses normal investigator semantics

Given the application's own deployment fails
When self-diagnostic mode creates an investigation
Then build/Aegis/Praxis/ROS records enter through normal observation/evidence/import semantics
And the self-diagnostic path does not bypass the generic kernel.

## A169. Semantic search returns authoritative entities

Given the user asks "show evidence that contradicts H1"
When authoritative relationships answer the query
Then results are retrieved from structured state
And the model does not substitute remembered/free-form claims.

## A170. Demo health check fails visibly

Given GitHub authorization is missing before the demo
When readiness check runs
Then the GitHub persistence check is red/blocked with actionable explanation
And the system does not display a false all-green state.


## A171. Registry command requires handler

Given core command C is registered
When semantic compiler validation runs
Then a decision handler/route exists for C
And the build fails contract validation if it does not.

## A172. Emitted event requires evolve handler

Given command C may emit event E
When semantic compiler validation runs
Then E has a projection/evolve handler
And E has a supported serialization identity/codec.

## A173. Registered transition has structural test

Given lifecycle transition T is registered
Then generated/manual test evidence exists for its valid and invalid source states
And missing structural coverage is reported.

## A174. UI capability metadata cannot drift silently

Given capability ConfirmCause exists in domain registry
When UI contract generation/validation runs
Then ConfirmCause has display/accessibility/blocker metadata
And a renamed/removed capability creates an explicit contract change.

## A175. AI schema matches semantic payload

Given ProposedHypothesis structured output schema is generated from semantic definition
When AI emits a payload outside that schema
Then validation rejects it as authoritative proposal data
And free-form explanation cannot bypass validation.

## A176. Semantic contract diff flags breaking removal

Given build B1 contains persisted event semantic E@1
And build B2 removes support without deprecation/migration
When contract diff runs
Then B2 is flagged incompatible
And release validation fails according to policy.

## A177. Semantic version changes on meaning change

Given a semantic identifier's payload meaning changes materially
Then the semantic/compiler compatibility gate requires a new version/identifier or explicit compatible migration
And display-label reuse alone is insufficient.

## A178. Full state-command matrix has defined outcomes

Given lifecycle L has states S and commands C
When state-space exploration runs
Then every relevant S x C combination has an explicit Allowed/Invalid/Blocked/Unsupported outcome
And no combination falls through to accidental behavior.

## A179. ConfirmCause blocker combination is explored

Given protected transition ConfirmCause
When policy state-space sampling varies contradiction, unknown, assumption, evidence, review, and permission conditions
Then expected blocker composition is verified across representative combinations.

## A180. Replay determinism property

Given valid event stream E
When projected repeatedly
Then the same semantic state results
And property testing can generate multiple valid streams to verify this.

## A181. Prefix equals time travel

Given valid event stream 1..N
And prefix 1..K
Then projecting prefix 1..K equals TimeTravel(K)
And later events cannot leak into the historical projection.

## A182. Semantic mutation is killed

Given mutation M removes the rule that blocking unknowns block ConfirmCause
When the protected semantic test suite runs
Then at least one test fails
And M is counted as killed.

## A183. Critical surviving mutation stops release

Given a mutation weakens a CoreStable invariant
And no test fails
Then validation reports a critical surviving mutation
And the build is not considered semantically verified.

## A184. Offline acknowledgement-loss retry is idempotent

Given remote append succeeds
And acknowledgement is lost
When client retries the same idempotent command
Then no duplicate semantic event is created
And local queue converges to Applied.

## A185. Auth expiry preserves local queue

Given pending commands exist
And authentication expires during synchronization
Then commands remain durable
And state becomes authorization-blocked rather than discarded/failed semantically.

## A186. Concurrent semantic conflict is revalidated

Given client A falsifies H1
And stale client B attempts ConfirmCause relying on H1
Then B's command is revalidated and blocked/conflicted
And no field-level merge makes both states simultaneously authoritative.

## A187. AI unknown semantic proposal fails closed

Given AI emits ExtensionProposal semantic id X unknown to all active packs
Then authoritative acceptance is unavailable
And the unknown proposal can at most remain raw analysis/proposal evidence according to policy.

## A188. Prompt-injection corpus remains data

Given each adversarial prompt-injection fixture is ingested as evidence
Then model/tool behavior remains constrained by platform/domain instructions
And none of the fixtures gains command authority.

## A189. Snapshot-tail replay equals full replay

Given snapshot at version K and events K+1..N
Then projecting snapshot+tail yields state equivalent to full replay 1..N
And mismatch is an integrity failure.

## A190. Cross-provider semantic equivalence

Given complete investigation is exported from GitHub provider and imported to compatible file/in-memory provider
Then projected semantic state/event identity is equivalent
And provider metadata differences do not affect domain meaning.

## A191. Core invariants survive EDF pack

Given the complete core invariant suite
When run with EDF pack active
Then all core invariants still pass unchanged.

## A192. Core invariants survive EDF experimental profile

Given the complete core invariant suite
When run with EDF experimental semantics/policy
Then all core invariants still pass unchanged
And experimental policy cannot weaken them.

## A193. Proof bundle does not claim unrun tests

Given accessibility manual screen-reader testing was not performed
When proof bundle is generated
Then it does not mark that evidence as Verified
And the gap remains visible.

## A194. Proof bundle requirement trace is navigable

Given requirement P7.5 is Verified
Then proof data identifies acceptance/test/runtime evidence supporting that status
And a user can inspect the supporting artifacts.

## A195. Proof bundle records deviations

Given hackathon shortcut D is active
Then the proof bundle lists D, rationale, risk, and replacement path
And does not imply production-complete conformance.

## A196. Proof bundle semantic digest changes with contract

Given semantic registry changes between builds
Then semantic contract digest differs
And the proof bundle identifies the contract version/diff.

## A197. Screen contract preserves blocker on phone

Given S02 Workspace is rendered at narrow phone width
And ConfirmCause has a material blocker
Then the blocker remains discoverable in primary flow
And responsive layout cannot satisfy the contract by hiding it in inaccessible overflow.

## A198. Historical screen mode is visibly read-only

Given S14 Time Travel shows non-current aggregate version
Then authoritative write actions are unavailable or redirect to branch/current mode
And the UI clearly identifies historical mode.

## A199. Counterfactual screen always identifies sandbox

Given S16 Counterfactual Sandbox is active
Then source version and WHAT-IF/SANDBOX status remain visible
And its presentation cannot be mistaken for authoritative current state.

## A200. Factory screen cannot invent evidence

Given S20 Factory View lacks Praxis data for metric M
Then M is shown unavailable/missing
And the view does not estimate or hard-code a substitute.


## A201. Hypothesis proposal carries falsifier

Given AI proposes hypothesis H
Then the proposal includes or explicitly states absence of a meaningful mechanism, prediction, and strongest plausible falsifier
And the user can inspect evidence/assumptions supporting H.

## A202. Proposal cites material dependencies

Given proposal P depends on evidence E4 and assumption A2
Then P records those dependencies
And unrelated entity changes do not need to mark P stale.

## A203. Superseded evidence invalidates dependent proposal

Given proposal P depends materially on E4
When E4 is superseded/invalidated
Then P is marked stale or requires revalidation
And it cannot be accepted blindly.

## A204. Rejected assumption invalidates dependent proposal

Given proposal P depends materially on A2
When A2 becomes Rejected
Then P becomes stale/requires revalidation.

## A205. Proposal regeneration diff is semantic

Given P1 and regenerated P2 differ
Then the UI can show changed evidence, assumption, scope, falsifier, or expected state impact
And does not require raw JSON comparison.

## A206. Cached proposal exposes cache provenance

Given a proposal result is reused from cache
Then the UI/provenance identifies the frozen source/configuration that produced it
And cached output is not presented as a fresh model run.

## A207. Planner filters illegal actions

Given candidate T1 would be discriminating but is semantically illegal
Then T1 is not presented as directly executable recommendation
And the planner may explain what prerequisite would make it available.

## A208. Planner exposes tradeoffs

Given T1 is fastest and T2 is more discriminating but riskier
Then planner output exposes those dimensions
And does not hide the tradeoff behind one unexplained score.

## A209. Planner can present Pareto alternatives

Given no action dominates all others
Then the planner may present fastest useful, safest useful, cheapest useful, and most discriminating options
And explains why no single action dominates.

## A210. Planner challenges premature leader

Given H1 is derived Leading
And a cheap safe falsifying action exists
Then planner can prioritize that action over another merely confirmatory action.

## A211. Planner distinguishes mitigation and learning

Given an incident is actively harming users
And a mitigation M can stop harm while experiment T best identifies cause
Then planner may recommend M and T as distinct/parallel objectives
And mitigation is not presented as root-cause confirmation.

## A212. Planner respects budget

Given remaining investigation budget excludes high-cost test T1
Then T1 is not recommended as feasible primary action
And the planner can explain the constraint.

## A213. Stop recommendation preserves uncertainty

Given further useful investigation exceeds accepted cost/risk
And policy permits stopping with residual uncertainty
Then planner may recommend stop/accept residual
And no ConfirmedCause is invented solely to close the investigation.

## A214. Prior case does not become evidence

Given prior investigation I1 concluded cause C
And current I2 looks similar
When cross-investigation retrieval surfaces I1
Then C may be proposed as a hypothesis in I2
And I1's conclusion is not admitted as direct Evidence for I2.

## A215. Similarity retrieval shows basis

Given prior investigation I1 is suggested
Then the UI can show which observation/system/mechanism/scope features caused retrieval
And similarity is not presented as causation.

## A216. Cross-case retrieval respects permissions

Given user cannot access workspace W2
Then investigations in W2 are not used as visible or hidden AI retrieval context
And no derived suggestion leaks their contents.

## A217. Anti-confirmation retrieval includes counterexample

Given two prior similar cases had cause C and one similar case had materially different cause D
When prior-case intelligence is used
Then the system should be capable of surfacing the counterexample D case
And does not only reinforce C.

## A218. Reused experiment rebinds predictions

Given experiment template T is reused from prior case
Then current predictions/scope are recorded against current hypotheses
And prior predictions are not copied as if prospectively registered for the new case.

## A219. Knowledge card is not current evidence

Given knowledge card K describes mechanism M from prior cases
Then K may inform hypotheses/actions
And K alone does not establish that M occurred in the current investigation.

## A220. Research treatments share frozen case

Given experiment compares treatment A and B on case C
Then both treatments use the same frozen InvestigationId/AggregateVersion unless experiment design explicitly states otherwise.

## A221. Blind evaluation hides model identity

Given research experiment enables evaluator blinding
Then proposal display/order does not reveal model/provider identity before evaluation
And identity is recoverable in provenance after unblinding.

## A222. Real-world uncertain case is not assigned fake ground truth

Given a real investigation lacks definitively verified cause
Then research harness does not label model proposals correct/incorrect solely from the current conclusion
And uses process/verification measures instead.

## A223. Prompt regression can block promotion

Given prompt version P2 performs materially worse than P1 on agreed benchmark criteria
Then research governance may prevent P2 becoming default
And benchmark evidence is recorded.

## A224. EDF experiment compares semantics without rewrite

Given EDF semantic treatment S1 and S2 are compared on frozen cases
Then each produces separate derived/reassessment results
And source investigation histories remain unchanged.

## A225. Research report identifies limitations

Given a research experiment uses only synthetic software incidents
When Folio report is generated
Then the limitation is stated
And results are not generalized automatically to unrelated domains.


## A226. Tool authority comes from host capability

Given the model requests tool T against resource R
And its issued capability does not authorize R
Then the host denies the invocation
And model text cannot expand the capability.

## A227. Tool output becomes Finding first

Given read tool T returns result X
Then X is captured as tool output/Finding with provenance
And X is not automatically authoritative Evidence unless admission policy permits it.

## A228. Deterministic comparison prefers local tool

Given two artifact hashes can answer whether content is identical
Then the system can use deterministic hashing
And need not ask an AI model to infer equality.

## A229. Tool failure does not fabricate finding

Given GitHub read tool fails
Then a structured fault/result is recorded
And no plausible file content/Finding is invented.

## A230. Tool invocation lineage reaches evidence

Given tool invocation T reads artifact A and produces Finding F that becomes Evidence E
Then provenance can trace E <- F <- T <- A.

## A231. Side-effect tool dry-run reveals target

Given reversible side-effect tool W supports dry-run
When dry-run executes
Then intended resources, effect, required permission, and rollback information are inspectable
And no real side effect occurs.

## A232. Autonomous read-only session cannot write external system

Given autonomous session is Level 1
When agent requests a write-capable tool
Then invocation is denied/requires a new approved capability
And session cannot self-escalate.

## A233. Autonomous budget stops work

Given session budget permits 10 tool calls
When 10 calls have been consumed
Then additional automatic tool work stops
And the checkpoint explains budget exhaustion.

## A234. Autonomous protected transition stops for human

Given autonomous session clears all cause blockers
And policy requires human confirmation
Then ConfirmCause is not auto-executed
And the session stops/checkpoints for human review.

## A235. Autonomous loop detector stops repeated action

Given agent repeatedly proposes the same action with no changed input/state
Then loop prevention stops or redirects the session
And repeated execution is not allowed indefinitely.

## A236. Autonomous crash preserves valid authority

Given a session crashes after a completed tool invocation but before next AI call
Then authoritative investigation state remains valid
And completed invocation/finding provenance remains recoverable
And session may resume after revalidation.

## A237. Local semantic diff works offline

Given network is unavailable
And required history is cached locally
When user compares versions
Then semantic diff executes locally
And no remote AI call is required.

## A238. Local hash establishes artifact integrity metadata

Given file F is captured
When local WASM computes digest H
Then H is stored as artifact integrity metadata
And later retrieval can be compared against H.

## A239. AI receives minimized context

Given investigation contains sensitive unrelated evidence
And task only requires H1/E4/E7
Then local context builder can exclude unrelated evidence according to policy
And AI provenance records the bounded context manifest.

## A240. Large log is locally prefiltered

Given log L is too large for reasonable AI context
When ingestion filters relevant time/entity range locally
Then selected chunks retain source anchors/digests
And AI does not receive the entire log by default.

## A241. Raw capture survives extraction

Given screenshot S is captured
And parser/OCR/AI extracts text T
Then S remains preserved as original artifact
And T is represented as derived content/proposal rather than replacing S.

## A242. Parser transformation is attributable

Given parser version P extracts records from JSON artifact A
Then output records link to P and A
And parser warnings/errors remain available.

## A243. Malformed parser output does not destroy source

Given parser P fails on artifact A
Then A remains available
And the fault is recorded
And manual/alternate parsing remains possible.

## A244. Git diff ingestion preserves base/target

Given commit diff D is ingested
Then base commit, target commit, files/hunks or equivalent artifact refs, and diff digest remain attributable
And AI analysis cannot rewrite D.

## A245. Aegis/Praxis/ROS ingestion uses normal evidence pipeline

Given engineering artifact X from Aegis/Praxis/ROS is imported
Then capture/transformation/finding/evidence provenance applies normally
And factory telemetry does not gain privileged truth status.

## A246. Exact duplicate capture is suggested not silently merged

Given artifact A is captured twice with same digest
Then the system may identify exact duplication
And preserves distinct capture provenance unless explicit merge/deduplication semantics are chosen.

## A247. Offline counterfactual remains available

Given network is unavailable
And source state is cached
Then user can create/analyze a local counterfactual branch
And authoritative remote state remains untouched.

## A248. Local graph cycle detection is deterministic

Given evidence dependency graph contains cycle C
When local graph analysis runs
Then the same cycle result is produced independent of AI provider
And it is shown as a derived diagnostic.

## A249. Tool secret never enters model context

Given tool T requires secret credential S
When model requests T
Then S is supplied only to the trusted execution layer
And S is absent from model prompt/tool description and investigation state.

## A250. Autonomous checkpoint explains work

Given an autonomous session pauses
Then checkpoint identifies actions executed, evidence/findings created, blockers changed, spend/budget state, and why it stopped
And does not require exposing private chain-of-thought.

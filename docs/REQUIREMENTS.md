# Requirements

Status: hackathon release-profile requirements. These are planning artifacts, not application implementation.

The canonical long-term product requirements are in [PRODUCT-REQUIREMENTS.md](PRODUCT-REQUIREMENTS.md). This file defines the first constrained implementation profile. Hackathon-specific constraints must not leak into or narrow the reusable investigation kernel.

## R0. Hackathon integrity

### R0.1 Blank application implementation
The competition application's source implementation must begin at hackathon kickoff from a blank application repository or the organizer-approved equivalent.

### R0.2 Pre-event preparation
Before kickoff, the team may prepare requirements, architecture, state models, acceptance criteria, UX designs, engineering standards, framework capabilities, reusable libraries, test strategy, threat model, and experiment plans, subject to the organizer's rules.

### R0.3 No hidden prebuilt application
The pre-event repository must not contain a completed or substantially implemented copy of the competition application intended to be reproduced at kickoff.

### R0.4 Provenance
The project must preserve evidence showing what existed before kickoff and what was created during the event.

## R1. Investigation creation

### R1.1 Initial observation
A user must be able to begin with a free-form description of an observed failure, anomaly, or unexpected result.

### R1.2 Structured projection
The system must transform the conversation into an explicit investigation model without losing the original source statement.

### R1.3 Investigation identity
Every investigation must have a stable identifier and creation timestamp.

## R2. Hypotheses

### R2.1 Multiple hypotheses
The system must support multiple simultaneously plausible hypotheses.

### R2.2 Explicit lifecycle
Every hypothesis must have an explicit state.

### R2.3 Transition evidence
A hypothesis state change must record why the change occurred and what evidence caused it.

### R2.4 No silent deletion
Falsified or weakened hypotheses must remain in investigation history.

### R2.5 Reopening
A falsified hypothesis may be reopened only when new evidence is explicitly linked to the reopening decision.

## R3. Evidence

### R3.1 Evidence records
Evidence must be represented as first-class records.

### R3.2 Provenance
Evidence should record its source, capture time when known, and verification status.

### R3.3 Relationships
Evidence must be linkable to one or more hypotheses with an explicit relationship such as support, contradiction, discrimination, or insufficiency.

### R3.4 Conflicting evidence
The system must permit contradictory evidence and surface the contradiction instead of forcing premature resolution.

### R3.5 Attachments
The architecture must permit evidence artifacts to be referenced by URL without requiring binary artifacts to be stored directly in the application repository.

## R4. Experiments and checks

### R4.1 Proposed experiment
The system must be able to propose a test, inspection, query, comparison, or experiment intended to change the investigation state.

### R4.2 Prediction before result
Where practical, the expected result under competing hypotheses should be recorded before an experiment result is entered.

### R4.3 Discriminating evidence
The investigator should prefer checks that distinguish among competing hypotheses rather than merely accumulating supportive evidence.

### R4.4 Result preservation
Experiment inputs, expected outcomes, actual findings, and interpretation must be preserved.

## R5. Unknowns and negative knowledge

### R5.1 Unknown is first-class
Unknown information must be represented explicitly.

### R5.2 Unknown obligations
An unknown may create required work before a transition is legal.

### R5.3 Negative knowledge
Known false, disproven, eliminated, or unsupported claims must be recordable.

### R5.4 Repetition prevention
The investigator should use negative knowledge to avoid repeatedly proposing already-disproven explanations.

## R6. Root-cause conclusion

### R6.1 No unsupported completion
The system must not represent a root cause as established when required evidence remains unresolved.

### R6.2 Confidence is evidence-linked
Any confidence representation must be traceable to evidence and uncertainty. It must not be a decorative LLM probability.

### R6.3 Contributing factors
The model must distinguish root cause from contributing factors.

### R6.4 Residual uncertainty
A conclusion must preserve unresolved uncertainty.

## R7. Corrective action and verification

### R7.1 Corrective actions
The system must represent one or more proposed corrective actions.

### R7.2 Mitigation distinction
Temporary mitigation must be distinguishable from root-cause correction.

### R7.3 Verification criteria
Corrective actions must have explicit success criteria.

### R7.4 Verified outcome
Resolution must require evidence that verification criteria were satisfied.

## R8. State-directed behavior

### R8.1 Legal actions
At every state, the UI must be able to show which actions are currently legal.

### R8.2 Illegal actions
Unavailable actions should remain explainable. The user should be able to see why an action cannot currently be taken.

### R8.3 Aegis integration
Invalid transitions and violated invariants must be represented through Aegis-compatible faults rather than hidden exceptions or generic error text.

### R8.4 Obligations
Outstanding work must be explicitly visible.

## R9. User interface

### R9.1 iPad first
The primary target is iPad Safari in both portrait and landscape orientations.

### R9.2 Mobile compatibility
The interface must remain usable on smaller mobile screens.

### R9.3 Accessibility
The application must support keyboard navigation, screen readers, sufficient contrast, visible focus, semantic structure, reduced motion preferences, and text scaling.

### R9.4 Investigation visualization
The UI should make hypotheses, evidence, contradictions, unknowns, and transitions understandable without requiring knowledge of state machines.

### R9.5 Conversation plus state
Natural-language interaction may be central, but the structured investigation state must remain visible and inspectable.

### R9.6 URL-addressable views
Every screen and every addressable thing has a URL, and pasting that URL into another browser opens the same view. Addressable things are investigations, hypotheses, evidence items, unknowns, assumptions, contradictions, actions and experiments, findings, causes, corrective actions, verifications, counterfactual branches, historical versions and reports. The URL also carries the view's navigable state: the selected version or comparison pair, filters, sort, search, tab and page. This is the portfolio deep-linking standard (Praxis SAF-URL-1..10), and the route table is in [Screen Contracts](SCREEN-CONTRACTS.md#url-contract).

### R9.7 Deep links survive reload, sign-in and offline
Routes are hash-based (`index.html#/...`), with relative links and no `<base href>`. A reloaded or pasted deep link therefore never 404s on GitHub Pages (D-203; Limen DF-LIMEN-2026-0006). When GitHub sign-in is needed, the application keeps the target, returns to it afterwards, and never returns outside the application. A deep link to state that is already held locally opens offline, and its persistence state is shown truthfully.

### R9.8 Missing, superseded or forbidden targets
A URL naming an entity that does not exist, or a repository the user cannot read, shows a not-found or not-permitted state with a way back to the investigation or home. It never shows a blank page and never shows a different investigation or record. A superseded or rejected entity is still shown as such, because history is never deleted (R2.4).

### R9.9 No secrets or evidence content in URLs
URLs never contain a GitHub token, an OAuth code, an AI provider key, evidence text or attachment content, or personal data typed about anyone. Entities are addressed by their GUID identifiers. Limen's reserved parameter names (`token`, `code`, `key`, `session` and the rest) are refused.

### R9.10 Route inventory, codec and sharing
The application publishes `.echelon/routes.json` (`echelon.routes/v1`). Pure `parse` and `format` functions own every route, and generated tests prove `format (parse url)` is the canonical URL. The application uses Limen's routing module once Limen 0.9.0 is installed. Screens that people discuss or hand off offer a Copy link action.


## R10. GitHub persistence

### R10.1 Repository-backed persistence
An investigation may be stored as structured text artifacts in a GitHub repository.

### R10.2 Human-readable artifacts
Stored records should remain reasonably understandable outside the application.

### R10.3 Commit provenance
Changes to remote investigation state should result in auditable repository history.

### R10.4 Conflict detection
The application must detect stale writes or conflicting remote changes and must not silently overwrite them.

### R10.5 Offline tolerance
The active investigation must remain usable during temporary loss of network access.

### R10.6 Pending remote state
A remote write that has not completed must be represented as pending, not successful.

### R10.7 Recovery
Pending writes must be recoverable after application restart when practical.

## R11. Authentication and security

### R11.1 GitHub authentication
Production-grade design should prefer GitHub App or organizer-appropriate OAuth authentication over embedding long-lived personal tokens.

### R11.2 Least privilege
GitHub access must request only the repository permissions required by the application.

### R11.3 Token handling
Authentication material must never be committed into repositories or investigation artifacts.

### R11.4 Prompt/data boundaries
External evidence must be treated as untrusted input. It must not be able to silently redefine system instructions or engineering policy.

## R12. Reporting

### R12.1 Folio output
The system must be able to produce a professional investigation report through Folio.

### R12.2 Report contents
A final report should include the observation, timeline, hypotheses considered, evidence, experiments, falsified explanations, root-cause reasoning, uncertainty, corrective action, and verification.

### R12.3 Incomplete report
The user must be able to produce an interim report that clearly identifies unresolved work.

## R13. Engineering factory

### R13.1 Ordo
Engineering and investigation state must follow Ordo principles for explicit state, legal transitions, capabilities, obligations, evidence, and unknowns.

### R13.2 ROS
Implementation work must be executed and documented through the current ROS process.

### R13.3 Limen
The browser application should use the current Limen architecture unless the hackathon experiment records a justified deviation.

### R13.4 Forma
User-interface primitives should use Forma where appropriate.

### R13.5 Folio
Printable/shareable document output should use Folio.

### R13.6 Aegis
Application and engineering faults should use Aegis.

### R13.7 Praxis
Praxis must capture code metrics, thresholds, and repeated-change/churn evidence across the build.

### R13.8 Evidence of completion
A feature must not be considered complete solely because an agent reports completion.

## R14. Observability and audit

### R14.1 State history
Material investigation-state changes must be inspectable.

### R14.2 Engineering history
The hackathon build should preserve requirements-to-implementation-to-test evidence.

### R14.3 Metrics
The final demo should include factory metrics such as requirements coverage, test results, faults, build status, code metrics, and unresolved obligations where available.

## R15. AI behavior

### R15.1 Questions before conclusions
The AI should ask for missing discriminating information when a conclusion would otherwise be premature.

### R15.2 Separate observation from inference
AI-generated inference must not be stored as though it were user-provided evidence.

### R15.3 Challenge preferred explanation
The system should actively seek evidence that could falsify the current leading explanation.

### R15.4 No history rewriting
The model must not rewrite earlier investigation history to make the final cause appear inevitable.

### R15.5 Human control
The user must be able to inspect, reject, correct, or supplement AI-generated hypotheses and interpretations.

## R16. Hackathon demonstration

### R16.1 Understandable in seconds
A judge should be able to understand the core problem and UI without learning Ordo terminology.

### R16.2 Visible state change
The demonstration must show an investigation materially changing as evidence arrives.

### R16.3 Visible constraint
At least one action should be unavailable because required evidence or obligations are incomplete, with the reason shown.

### R16.4 Visible falsification
The demonstration should show at least one plausible hypothesis being weakened or falsified.

### R16.5 Factory reveal
The demo must show the engineering machinery and evidence used to build the app, not only the app itself.

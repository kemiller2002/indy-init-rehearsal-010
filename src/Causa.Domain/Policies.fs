// Owner: domain. Decisions, blockers and the structural transition tables (STATE-TRANSITION-TABLES.md); one decision
// path: Decide and Capabilities read these tables, nothing else duplicates them (section 17).
namespace Causa.Domain

/// Frozen (00-DOMAIN-STUBS.fs).
type Blocker = { Code: string; Message: string }

/// Frozen.
type Decision =
    | Allowed of EventPayload list
    | Blocked of Blocker list
    | RequiresReview of string list
    | UnsupportedSemantics of string list
    | Conflict of expected: AggregateVersion * actual: AggregateVersion
    | Duplicate of EventId list

/// Frozen.
type PolicySet = { Id: string }

/// Frozen.
type ActorContext = { ActorId: string }

/// Frozen.
type ProjectionError = { At: AggregateVersion; Reason: string }

/// The structural transition tables. Each answers which states a command may start from.
[<RequireQualifiedAccess>]
module Transitions =
    /// The core policy every command is decided under until policy composition lands (T13).
    let corePolicy : PolicySet = { Id = "causa.core/1" }

    /// STATE-TRANSITION-TABLES section 1, as far as causa-storage v1.0.0 has events for it.
    let activateFrom =
        [ Lifecycle.Draft; Lifecycle.AwaitingEvidence; Lifecycle.AwaitingVerification; Lifecycle.Archived ]

    let resolveFrom = [ Lifecycle.Active; Lifecycle.AwaitingVerification ]
    let reopenFrom = [ Lifecycle.Resolved; Lifecycle.Closed ]

    /// Lifecycles in which the investigation's record may still grow. A closed or archived investigation is read-only
    /// until it is reopened or reactivated.
    let recordingAllowedIn (l: Lifecycle) =
        match l with
        | Lifecycle.Closed
        | Lifecycle.Archived -> false
        | Lifecycle.Draft
        | Lifecycle.Active
        | Lifecycle.AwaitingEvidence
        | Lifecycle.AwaitingVerification
        | Lifecycle.Resolved -> true

    /// STATE-TRANSITION-TABLES section 2.
    let markPlausibleFrom = [ HypothesisState.Proposed; HypothesisState.Weakened ]
    let markSupportedFrom = [ HypothesisState.Plausible; HypothesisState.Weakened ]

    let falsifyFrom =
        [ HypothesisState.Proposed; HypothesisState.Plausible; HypothesisState.Supported; HypothesisState.Weakened ]

    let reopenHypothesisFrom = [ HypothesisState.Falsified ]

    /// Section 10.
    let confirmCauseFrom = [ CauseState.Proposed; CauseState.Reopened ]

    /// Section 11.
    let implementFrom = [ CorrectiveActionState.InProgress ]

    /// Section 12 (causa-storage v1.0.0 has only active, accepted and rejected).
    let decideProposalFrom = [ ProposalState.Active ]

    let blocker code message : Blocker = { Code = code; Message = message }

/// The current evidence assessments (CONTRACT section 8.3 keeps every relationship): a reassessment is a new
/// EvidenceRelationshipRecorded for the same evidence and hypothesis, so the latest one is current and the earlier ones
/// stay as history.
[<RequireQualifiedAccess>]
module Assessments =
    /// Every assessment of one evidence item against one hypothesis, oldest first.
    let history (s: Investigation) (evidence: EvidenceId) (hypothesis: HypothesisId) : Recorded<EvidenceRelationship> list =
        s.Relationships
        |> Map.toList
        |> List.map snd
        |> List.filter (fun r -> r.Value.EvidenceId = evidence && r.Value.HypothesisId = hypothesis)
        |> List.sortBy (fun r -> AggregateVersion.value r.Provenance.AtVersion)

    /// The current assessment of every (evidence, hypothesis) pair.
    let current (s: Investigation) : Recorded<EvidenceRelationship> list =
        s.Relationships
        |> Map.toList
        |> List.map snd
        |> List.groupBy (fun r -> r.Value.EvidenceId, r.Value.HypothesisId)
        |> List.map (fun (_, rs) -> rs |> List.maxBy (fun r -> AggregateVersion.value r.Provenance.AtVersion))
        |> List.sortBy (fun r -> AggregateVersion.value r.Provenance.AtVersion)

    /// The current assessments of a hypothesis by evidence that has not been superseded.
    let ofHypothesis (s: Investigation) (hypothesis: HypothesisId) : Recorded<EvidenceRelationship> list =
        current s
        |> List.filter (fun r ->
            r.Value.HypothesisId = hypothesis
            && (s.Evidence.TryFind r.Value.EvidenceId |> Option.exists (fun e -> e.SupersededBy.IsNone)))

/// Composable policy (POLICY-ENGINE.md; FSHARP-TYPE-SHAPES section 40). A PolicySet id names its components joined by
/// '+', for example "causa.core/1+causa.mechanism-required/1". The core policy is always in force; a component can only
/// add blockers (a policy cannot make a structurally impossible transition legal, STATE-TRANSITION-TABLES section 16).
/// An unknown component is unsupported semantics, never ignored.
[<RequireQualifiedAccess>]
module Policy =
    /// A rule: the blockers it raises for a structurally legal command against the current state (all of them, not the
    /// first, so the interface can list every reason, POLICY-ENGINE section 24).
    type Rule =
        { Id: string
          Check: Investigation -> CoreCommand -> Blocker list }

    let private b = Transitions.blocker
    let private txt (g: System.Guid) = Ids.text g
    let private openContradiction (c: ContradictionEntry) =
        match c.State with
        | ContradictionState.Detected
        | ContradictionState.Triaged
        | ContradictionState.Investigating -> true
        | ContradictionState.Resolved
        | ContradictionState.AcceptedResidual
        | ContradictionState.Superseded -> false

    let private material (m: Materiality) =
        match m with
        | Materiality.Blocking
        | Materiality.Material -> true
        | Materiality.NonMaterial
        | Materiality.Informational -> false

    let private openUnknown (u: UnknownEntry) =
        match u.State with
        | UnknownState.Identified
        | UnknownState.Investigating -> true
        | UnknownState.Resolved
        | UnknownState.AcceptedResidual
        | UnknownState.Superseded -> false

    /// Blockers shared by the cause-confirmation and resolution gates: unresolved material contradictions, open
    /// blocking unknowns and unverified material assumptions (INVESTIGATION-SEMANTICS sections 15 and 22).
    let private openUncertainty (gate: string) (s: Investigation) : Blocker list =
        [ for KeyValue(ContradictionId id, c) in s.Contradictions do
              if material c.Contradiction.Materiality && openContradiction c then
                  yield b $"{gate}.contradiction-unresolved" $"Contradiction {txt id} ({c.Contradiction.Statement.Value}) is unresolved."
          for KeyValue(UnknownId id, u) in s.Unknowns do
              if u.Unknown.Materiality = Materiality.Blocking && openUnknown u then
                  yield b $"{gate}.unknown-open" $"Blocking unknown {txt id} ({u.Unknown.Question.Value}) is open."
          for KeyValue(AssumptionId id, a) in s.Assumptions do
              if material a.Assumption.Materiality && a.State = AssumptionState.ExplicitUnverified then
                  yield b $"{gate}.assumption-unverified" $"Assumption {txt id} ({a.Assumption.Proposition.Value}) is neither verified nor accepted." ]

    /// The cause-confirmation gate (INVESTIGATION-SEMANTICS section 15; DOMAIN-TEST-MATRIX ConfirmCause): every member
    /// hypothesis supported, material supporting evidence, no unaddressed alternative, and no open uncertainty.
    let causeConfirmationGate: Rule =
        { Id = "causa.core/1:cause-confirmation"
          Check =
            fun s command ->
                match command with
                | ConfirmCause p ->
                    match s.Causes.TryFind p.CauseId with
                    | None -> []
                    | Some cause ->
                        let members = cause.Cause.Members |> List.map (fun m -> m.HypothesisId)
                        [ for HypothesisId id as h in members do
                              match s.Hypotheses.TryFind h with
                              | Some e when e.State <> HypothesisState.Supported ->
                                  yield b "cause.member-unsupported" $"Hypothesis {txt id} ({e.Hypothesis.Statement.Value}) is {WireEnum.toWire Enums.hypothesisState e.State}, not supported."
                              | _ -> ()
                          let supporting =
                              members
                              |> List.collect (Assessments.ofHypothesis s)
                              |> List.filter (fun r -> r.Value.Kind = RelationshipKind.Supports)
                          if supporting.IsEmpty then
                              yield b "cause.no-supporting-evidence" "No current evidence supports a member hypothesis."
                          for KeyValue(HypothesisId id as h, e) in s.Hypotheses do
                              if not (List.contains h members) then
                                  match e.State with
                                  | HypothesisState.Proposed
                                  | HypothesisState.Plausible
                                  | HypothesisState.Supported ->
                                      yield b "cause.alternative-unaddressed" $"Alternative hypothesis {txt id} ({e.Hypothesis.Statement.Value}) is still {WireEnum.toWire Enums.hypothesisState e.State}."
                                  | HypothesisState.Weakened
                                  | HypothesisState.Falsified
                                  | HypothesisState.Superseded -> ()
                          yield! openUncertainty "cause" s ]
                | _ -> [] }

    /// The resolution gate (INVESTIGATION-SEMANTICS section 22; DOMAIN-TEST-MATRIX ResolveInvestigation): a recorded
    /// conclusion, every verification criterion satisfied, no corrective action left between implementation and a
    /// verified effect, and no open uncertainty.
    let resolutionGate: Rule =
        { Id = "causa.core/1:resolution"
          Check =
            fun s command ->
                match command with
                | ResolveInvestigation ->
                    [ if s.Conclusions.IsEmpty then
                          yield b "resolution.no-conclusion" "No conclusion is recorded."
                      for KeyValue(VerificationCriterionId id, c) in s.Criteria do
                          let satisfied =
                              s.Verifications
                              |> Map.exists (fun _ v -> v.Value.CriterionId = c.Value.CriterionId && v.Value.Outcome = VerificationOutcome.Satisfied)
                          if not satisfied then
                              yield b "resolution.criterion-unverified" $"Verification criterion {txt id} ({c.Value.ExpectedOutcome.Value}) is not satisfied."
                      for KeyValue(CorrectiveActionId id, a) in s.CorrectiveActions do
                          match a.State with
                          | CorrectiveActionState.Implemented
                          | CorrectiveActionState.AwaitingVerificationOfEffect
                          | CorrectiveActionState.VerificationFailed
                          | CorrectiveActionState.ReworkRequired ->
                              yield b "resolution.corrective-action-unverified" $"Corrective action {txt id} ({a.CorrectiveAction.Description.Value}) has no verified effect."
                          | CorrectiveActionState.Proposed
                          | CorrectiveActionState.Approved
                          | CorrectiveActionState.InProgress
                          | CorrectiveActionState.VerifiedEffective
                          | CorrectiveActionState.Cancelled -> ()
                      yield! openUncertainty "resolution" s ]
                | _ -> [] }

    /// An optional strengthening: a cause is confirmed only with a stated mechanism (DOMAIN-TEST-MATRIX: "missing
    /// required mechanism").
    let mechanismRequired: Rule =
        { Id = "causa.mechanism-required/1"
          Check =
            fun s command ->
                match command with
                | ConfirmCause p ->
                    match s.Causes.TryFind p.CauseId with
                    | Some c when c.Cause.Mechanism.IsNone ->
                        [ b "cause.mechanism-missing" $"Causal assertion {txt (Ids.cause p.CauseId)} states no mechanism." ]
                    | _ -> []
                | _ -> [] }

    let core = Transitions.corePolicy

    /// Every known policy component and its rules.
    let components: (string * Rule list) list =
        [ core.Id, [ causeConfirmationGate; resolutionGate ]
          mechanismRequired.Id, [ mechanismRequired ] ]

    /// The rules a PolicySet composes: the core rules plus each named component. Error lists unknown components.
    let rules (policies: PolicySet) : Result<Rule list, string list> =
        let names =
            policies.Id.Split('+', System.StringSplitOptions.RemoveEmptyEntries ||| System.StringSplitOptions.TrimEntries)
            |> List.ofArray
        let unknown = names |> List.filter (fun n -> not (components |> List.exists (fst >> (=) n)))
        if not unknown.IsEmpty then Error(unknown |> List.map (fun n -> $"policy {n}"))
        else
            Ok(
                (core.Id :: names)
                |> List.distinct
                |> List.collect (fun n -> components |> List.find (fst >> (=) n) |> snd)
            )

    /// All blockers the composed rules raise for a command.
    let evaluate (rules: Rule list) (s: Investigation) (command: CoreCommand) : Blocker list =
        rules |> List.collect (fun r -> r.Check s command)

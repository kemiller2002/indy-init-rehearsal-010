// Owner: domain. The pure evolution function and replay (FSHARP-TYPE-SHAPES section 38; CONTRACT section 8 fold).
// Evolve refuses, rather than skips, anything a history cannot contain: an event before InvestigationStarted, a second
// start, a version gap, another investigation's event, a reused correlation, a duplicate or missing entity, a dangling
// reference and a transition whose `from` is not the replayed state. Admissibility rules (gates, policies) are Decide's.
namespace Causa.Domain

open System

[<RequireQualifiedAccess>]
module Evolve =
    let private txt (g: Guid) = Ids.text g

    let private create what (id: 'K) (key: Guid) (value: 'V) (m: Map<'K, 'V>) : Result<Map<'K, 'V>, string> =
        if m.ContainsKey id then Error $"{what} {txt key} already exists" else Ok(m.Add(id, value))

    let private update what (id: 'K) (key: Guid) (f: 'V -> Result<'V, string>) (m: Map<'K, 'V>) : Result<Map<'K, 'V>, string> =
        match m.TryFind id with
        | Some v -> f v |> Result.map (fun v' -> m.Add(id, v'))
        | None -> Error $"{what} {txt key} does not exist"

    let private references (checks: (string * Guid * bool) list) : Result<unit, string> =
        match checks |> List.tryFind (fun (_, _, ok) -> not ok) with
        | Some(what, id, _) -> Error $"cites missing {what} {txt id}"
        | None -> Ok()

    let private evidence (s: Investigation) (ids: EvidenceId list) =
        ids |> List.map (fun id -> "evidence", Ids.evidence id, s.Evidence.ContainsKey id)

    let private hypotheses (s: Investigation) (ids: HypothesisId list) =
        ids |> List.map (fun id -> "hypothesis", Ids.hypothesis id, s.Hypotheses.ContainsKey id)

    let private fromMatches what (show: 'S -> string) (expected: 'S) (actual: 'S) : Result<unit, string> =
        if expected = actual then Ok()
        else Error $"{what} 'from' {show expected} but the replayed state is {show actual}"

    let private start (env: EventEnvelope) (p: InvestigationStarted) : Investigation =
        let prov = { Actor = env.Provenance.Actor; RecordedAt = env.Provenance.OccurredAt; AtVersion = env.Version }
        let observation: Observation = { ObservationId = p.ObservationId; Original = p.Original; ObservedAt = None }
        { Id = env.Investigation
          Version = env.Version
          Title = p.Title
          Lifecycle = Lifecycle.Draft
          CreatedAt = env.Provenance.OccurredAt
          UpdatedAt = env.Provenance.OccurredAt
          Observations = Map.ofList [ p.ObservationId, { Value = observation; Provenance = prov } ]
          Hypotheses = Map.empty
          Evidence = Map.empty
          Relationships = Map.empty
          Unknowns = Map.empty
          Assumptions = Map.empty
          Contradictions = Map.empty
          Predictions = Map.empty
          Actions = Map.empty
          Findings = Map.empty
          NegativeKnowledge = Map.empty
          Causes = Map.empty
          CorrectiveActions = Map.empty
          Criteria = Map.empty
          Verifications = Map.empty
          Conclusions = Map.empty
          Proposals = Map.empty
          Correlations = Map.ofList [ env.Provenance.CorrelationId, [ env.EventId ] ]
          History = [ env ] }

    /// One step of the fold for an investigation that exists.
    let private step (s: Investigation) (env: EventEnvelope) (e: CoreEvent) : Result<Investigation, string> =
        let prov = { Actor = env.Provenance.Actor; RecordedAt = env.Provenance.OccurredAt; AtVersion = env.Version }
        let recorded v = { Value = v; Provenance = prov }
        let ( >>= ) r f = Result.bind f r
        match e with
        | InvestigationStarted _ -> Error "a second InvestigationStarted"
        | ExtensionAttached _ -> Error "ExtensionAttached has no causa-storage v1.0.0 form"
        | InvestigationActivated _
        | InvestigationMarkedAwaitingEvidence _
        | InvestigationMarkedAwaitingVerification _
        | InvestigationResolved _
        | InvestigationClosed _
        | InvestigationReopened _
        | InvestigationVerificationFailed _ ->
            let from, target = (Events.lifecycleChange e).Value
            fromMatches "lifecycle" (WireEnum.toWire Enums.lifecycle) from s.Lifecycle
            |> Result.map (fun () -> { s with Lifecycle = target })
        | ObservationRecorded p ->
            create "observation" p.ObservationId (Ids.observation p.ObservationId) (recorded p) s.Observations
            |> Result.map (fun m -> { s with Observations = m })
        | HypothesisRecorded p ->
            let h = { Hypothesis = p; State = HypothesisState.Proposed; History = []; Provenance = prov }
            create "hypothesis" p.HypothesisId (Ids.hypothesis p.HypothesisId) h s.Hypotheses
            |> Result.map (fun m -> { s with Hypotheses = m })
        | HypothesisMarkedPlausible _
        | HypothesisMarkedSupported _
        | HypothesisWeakened _
        | HypothesisFalsified _
        | HypothesisSuperseded _
        | HypothesisReopened _ ->
            let t, target = (Events.hypothesisChange e).Value
            references (evidence s t.EvidenceIds)
            >>= fun () ->
                update "hypothesis" t.HypothesisId (Ids.hypothesis t.HypothesisId) (fun (h: HypothesisEntry) ->
                    fromMatches "hypothesis" (WireEnum.toWire Enums.hypothesisState) t.From h.State
                    |> Result.map (fun () ->
                        let change =
                            { From = t.From; To = target; Reason = t.Reason; EvidenceIds = t.EvidenceIds
                              At = env.Provenance.OccurredAt; Version = env.Version }
                        { h with State = target; History = h.History @ [ change ] })) s.Hypotheses
            |> Result.map (fun m -> { s with Hypotheses = m })
        | EvidenceAdmitted p ->
            let x = { Evidence = p; Verification = VerificationStatus.Unverified; SupersededBy = None; Provenance = prov }
            create "evidence" p.EvidenceId (Ids.evidence p.EvidenceId) x s.Evidence
            |> Result.map (fun m -> { s with Evidence = m })
        | EvidenceSuperseded p ->
            references (evidence s [ p.EvidenceId; p.ReplacementEvidenceId ])
            >>= fun () ->
                update "evidence" p.EvidenceId (Ids.evidence p.EvidenceId) (fun (x: EvidenceEntry) -> Ok { x with SupersededBy = Some p.ReplacementEvidenceId }) s.Evidence
            |> Result.map (fun m -> { s with Evidence = m })
        | EvidenceVerificationAssessed p ->
            update "evidence" p.EvidenceId (Ids.evidence p.EvidenceId) (fun (x: EvidenceEntry) -> Ok { x with Verification = p.Verification }) s.Evidence
            |> Result.map (fun m -> { s with Evidence = m })
        | EvidenceRelationshipRecorded p ->
            references (evidence s [ p.EvidenceId ] @ hypotheses s [ p.HypothesisId ])
            >>= fun () -> create "relationship" p.RelationshipId (Ids.relationship p.RelationshipId) (recorded p) s.Relationships
            |> Result.map (fun m -> { s with Relationships = m })
        | UnknownIdentified p ->
            let x = { Unknown = p; State = UnknownState.Identified; Basis = None; Provenance = prov }
            create "unknown" p.UnknownId (Ids.unknown p.UnknownId) x s.Unknowns
            |> Result.map (fun m -> { s with Unknowns = m })
        | UnknownTransitioned p ->
            update "unknown" p.UnknownId (Ids.unknown p.UnknownId) (fun (x: UnknownEntry) -> Ok { x with State = p.State; Basis = Some p.Basis }) s.Unknowns
            |> Result.map (fun m -> { s with Unknowns = m })
        | AssumptionRecorded p ->
            let x = { Assumption = p; State = AssumptionState.ExplicitUnverified; Provenance = prov }
            create "assumption" p.AssumptionId (Ids.assumption p.AssumptionId) x s.Assumptions
            |> Result.map (fun m -> { s with Assumptions = m })
        | AssumptionTransitioned p ->
            update "assumption" p.AssumptionId (Ids.assumption p.AssumptionId) (fun (x: AssumptionEntry) -> Ok { x with State = p.State }) s.Assumptions
            |> Result.map (fun m -> { s with Assumptions = m })
        | ContradictionRecorded p ->
            let x = { Contradiction = p; State = ContradictionState.Detected; Basis = None; Provenance = prov }
            references (evidence s p.EvidenceIds)
            >>= fun () -> create "contradiction" p.ContradictionId (Ids.contradiction p.ContradictionId) x s.Contradictions
            |> Result.map (fun m -> { s with Contradictions = m })
        | ContradictionTransitioned p ->
            update "contradiction" p.ContradictionId (Ids.contradiction p.ContradictionId) (fun (x: ContradictionEntry) -> Ok { x with State = p.State; Basis = Some p.Basis }) s.Contradictions
            |> Result.map (fun m -> { s with Contradictions = m })
        | PredictionRecorded p ->
            references (hypotheses s [ p.HypothesisId ])
            >>= fun () -> create "prediction" p.PredictionId (Ids.prediction p.PredictionId) (recorded p) s.Predictions
            |> Result.map (fun m -> { s with Predictions = m })
        | InvestigationActionRecorded p ->
            let x = { Action = p; State = ActionState.Proposed; Provenance = prov }
            references (hypotheses s p.TargetHypothesisIds)
            >>= fun () -> create "action" p.ActionId (Ids.action p.ActionId) x s.Actions
            |> Result.map (fun m -> { s with Actions = m })
        | InvestigationActionTransitioned p ->
            update "action" p.ActionId (Ids.action p.ActionId) (fun (x: ActionEntry) -> Ok { x with State = p.State }) s.Actions
            |> Result.map (fun m -> { s with Actions = m })
        | FindingRecorded p ->
            references [ "action", Ids.action p.ActionId, s.Actions.ContainsKey p.ActionId ]
            >>= fun () -> create "finding" p.FindingId (Ids.finding p.FindingId) (recorded p) s.Findings
            |> Result.map (fun m -> { s with Findings = m })
        | NegativeKnowledgeEstablished p ->
            references (evidence s p.BasisEvidenceIds)
            >>= fun () -> create "negativeKnowledge" p.NegativeKnowledgeId (Ids.negativeKnowledge p.NegativeKnowledgeId) (recorded p) s.NegativeKnowledge
            |> Result.map (fun m -> { s with NegativeKnowledge = m })
        | CausalAssertionRecorded p ->
            let x = { Cause = p; State = CauseState.Proposed; ConfirmedUnder = None; Provenance = prov }
            references (hypotheses s (p.Members |> List.map (fun m -> m.HypothesisId)))
            >>= fun () -> create "cause" p.CauseId (Ids.cause p.CauseId) x s.Causes
            |> Result.map (fun m -> { s with Causes = m })
        | CauseConfirmed p ->
            update "cause" p.CauseId (Ids.cause p.CauseId) (fun (x: CauseEntry) -> Ok { x with State = CauseState.Confirmed; ConfirmedUnder = Some p.Policy }) s.Causes
            |> Result.map (fun m -> { s with Causes = m })
        | ConfirmedCauseReopened(id, _) ->
            update "cause" id (Ids.cause id) (fun (x: CauseEntry) -> Ok { x with State = CauseState.Reopened }) s.Causes
            |> Result.map (fun m -> { s with Causes = m })
        | CorrectiveActionRecorded p ->
            let x = { CorrectiveAction = p; State = CorrectiveActionState.Proposed; AwaitingSince = None; Provenance = prov }
            references (p.TargetCauseIds |> List.map (fun c -> "cause", Ids.cause c, s.Causes.ContainsKey c))
            >>= fun () -> create "correctiveAction" p.CorrectiveActionId (Ids.correctiveAction p.CorrectiveActionId) x s.CorrectiveActions
            |> Result.map (fun m -> { s with CorrectiveActions = m })
        | CorrectiveActionApproved _
        | CorrectiveActionStarted _
        | CorrectiveActionImplemented _
        | CorrectiveActionAwaitingVerificationOfEffect _
        | CorrectiveActionVerifiedEffective _
        | CorrectiveActionVerificationFailed _
        | CorrectiveActionReworkRequired _
        | CorrectiveActionCancelled _ ->
            let id, target = (Events.correctiveActionChange e).Value
            let awaiting (x: CorrectiveActionEntry) =
                if target = CorrectiveActionState.AwaitingVerificationOfEffect then Some env.Version else x.AwaitingSince
            update "correctiveAction" id (Ids.correctiveAction id) (fun (x: CorrectiveActionEntry) -> Ok { x with State = target; AwaitingSince = awaiting x }) s.CorrectiveActions
            |> Result.map (fun m -> { s with CorrectiveActions = m })
        | VerificationCriterionRecorded p ->
            references [ "correctiveAction", Ids.correctiveAction p.CorrectiveActionId, s.CorrectiveActions.ContainsKey p.CorrectiveActionId ]
            >>= fun () -> create "criterion" p.CriterionId (Ids.criterion p.CriterionId) (recorded p) s.Criteria
            |> Result.map (fun m -> { s with Criteria = m })
        | VerificationRecorded p ->
            references (("criterion", Ids.criterion p.CriterionId, s.Criteria.ContainsKey p.CriterionId) :: evidence s p.EvidenceIds)
            >>= fun () -> create "verification" p.VerificationId (Ids.verification p.VerificationId) (recorded p) s.Verifications
            |> Result.map (fun m -> { s with Verifications = m })
        | ConclusionRecorded p ->
            references (p.CauseIds |> List.map (fun c -> "cause", Ids.cause c, s.Causes.ContainsKey c))
            >>= fun () -> create "conclusion" p.ConclusionId (Ids.conclusion p.ConclusionId) (recorded p) s.Conclusions
            |> Result.map (fun m -> { s with Conclusions = m })
        | ProposalRecorded p ->
            let x = { Proposal = p; State = ProposalState.Active; AcceptedAs = None; RejectionReason = None; Provenance = prov }
            create "proposal" p.ProposalId (Ids.proposal p.ProposalId) x s.Proposals
            |> Result.map (fun m -> { s with Proposals = m })
        | ProposalAccepted(id, target) ->
            update "proposal" id (Ids.proposal id) (fun (x: ProposalEntry) -> Ok { x with State = ProposalState.Accepted; AcceptedAs = Some target }) s.Proposals
            |> Result.map (fun m -> { s with Proposals = m })
        | ProposalRejected(id, reason) ->
            update "proposal" id (Ids.proposal id) (fun (x: ProposalEntry) -> Ok { x with State = ProposalState.Rejected; RejectionReason = Some reason }) s.Proposals
            |> Result.map (fun m -> { s with Proposals = m })

    /// A correlation (command) groups contiguous events only (CONTRACT section 11.1 stage 14): once another command's
    /// event follows, the correlation may not recur.
    let private correlationFree (s: Investigation) (env: EventEnvelope) =
        let c = env.Provenance.CorrelationId
        match s.Correlations.ContainsKey c, s.History with
        | false, _ -> true
        | true, last :: _ -> last.Provenance.CorrelationId = c
        | true, [] -> false

    /// IF41-DOMAIN Evolve.
    let evolve (state: Investigation option) (env: EventEnvelope) : Result<Investigation, ProjectionError> =
        let fail reason = Error { At = env.Version; Reason = reason }
        match env.Payload, state with
        | ExtensionEvent(id, _), _ -> fail $"extension event {id.Namespace.Value}/{id.Name.Value} is not a causa-storage v1.0.0 event"
        | CoreEvent(InvestigationStarted p), None ->
            if env.Version = AggregateVersion 1L then Ok(start env p)
            else fail "InvestigationStarted must be version 1"
        | CoreEvent e, None -> fail $"{Events.caseName e} before InvestigationStarted"
        | CoreEvent e, Some s ->
            if env.Investigation <> s.Id then fail "the event belongs to another investigation"
            elif env.Version <> AggregateVersion.next s.Version then
                fail $"version {AggregateVersion.value env.Version} does not follow {AggregateVersion.value s.Version}"
            elif not (correlationFree s env) then fail $"correlation {txt env.Provenance.CorrelationId} reused"
            else
                match step s env e with
                | Error reason -> fail $"{Events.caseName e}: {reason}"
                | Ok next ->
                    let c = env.Provenance.CorrelationId
                    let ids = s.Correlations.TryFind c |> Option.defaultValue []
                    Ok
                        { next with
                            Version = env.Version
                            UpdatedAt = env.Provenance.OccurredAt
                            Correlations = next.Correlations.Add(c, ids @ [ env.EventId ])
                            History = env :: next.History }

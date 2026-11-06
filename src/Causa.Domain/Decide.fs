// Owner: domain. The pure decision function (FSHARP-TYPE-SHAPES section 37; STATE-TRANSITION-TABLES section 16):
// semantics, expected version, duplicate command, actor, structural transition, then the events to append.
// No clock, randomness, network, GitHub or browser: every value an event needs comes from the command.
namespace Causa.Domain

[<RequireQualifiedAccess>]
module Decide =
    let private allowed (events: CoreEvent list) = Allowed(events |> List.map CoreEvent)

    /// Allowed with the events when every check holds; otherwise Blocked with every failing check's blocker.
    let private whenAll (checks: (bool * Blocker) list) (events: unit -> CoreEvent list) : Decision =
        match checks |> List.filter (fst >> not) |> List.map snd with
        | [] -> allowed (events ())
        | blockers -> Blocked blockers

    let private b = Transitions.blocker
    let private txt (g: System.Guid) = Ids.text g

    let private recording (s: Investigation) =
        Transitions.recordingAllowedIn s.Lifecycle,
        b "investigation.read-only" $"The investigation is {WireEnum.toWire Enums.lifecycle s.Lifecycle}; reopen it before recording."

    let private isNew what (exists: bool) (id: System.Guid) = not exists, b $"{what}.exists" $"The {what} {txt id} already exists."
    let private present what (exists: bool) (id: System.Guid) = exists, b $"{what}.missing" $"The {what} {txt id} does not exist."

    let private evidencePresent (s: Investigation) (ids: EvidenceId list) =
        ids |> List.map (fun id -> present "evidence" (s.Evidence.ContainsKey id) (Ids.evidence id))

    let private hypothesesPresent (s: Investigation) (ids: HypothesisId list) =
        ids |> List.map (fun id -> present "hypothesis" (s.Hypotheses.ContainsKey id) (Ids.hypothesis id))

    let private inState what (allowedFrom: 'S list) (current: 'S option) (show: 'S -> string) (command: string) =
        match current with
        | Some c when List.contains c allowedFrom -> true, b "transition.illegal" ""
        | Some c -> false, b "transition.illegal" $"{command} is not allowed for a {what} that is {show c}."
        | None -> true, b "transition.illegal" "" // the missing entity is reported by its presence check

    let private hypothesisTransition (s: Investigation) (allowedFrom: HypothesisState list) command (m: MarkHypothesis) (make: HypothesisTransition -> CoreEvent) =
        let current = s.Hypotheses.TryFind m.HypothesisId |> Option.map (fun h -> h.State)
        whenAll
            ([ recording s
               present "hypothesis" current.IsSome (Ids.hypothesis m.HypothesisId)
               inState "hypothesis" allowedFrom current (WireEnum.toWire Enums.hypothesisState) command ]
             @ evidencePresent s m.EvidenceIds)
            (fun () ->
                let t: HypothesisTransition =
                    { HypothesisId = m.HypothesisId
                      From = current.Value
                      Reason = m.Reason
                      EvidenceIds = m.EvidenceIds }
                [ make t ])

    let private lifecycleCommand (s: Investigation) (allowedFrom: Lifecycle list) command (event: Lifecycle -> CoreEvent) =
        whenAll
            [ List.contains s.Lifecycle allowedFrom,
              b "transition.illegal" $"{command} is not allowed while the investigation is {WireEnum.toWire Enums.lifecycle s.Lifecycle}." ]
            (fun () -> [ event s.Lifecycle ])

    /// The structural decision for a command against an existing investigation.
    let private onExisting (s: Investigation) (command: CoreCommand) : Decision =
        match command with
        | StartInvestigation _ ->
            Blocked [ b "investigation.exists" $"The investigation {txt (Ids.investigation s.Id)} has already started." ]
        | ActivateInvestigation -> lifecycleCommand s Transitions.activateFrom "ActivateInvestigation" InvestigationActivated
        | ResolveInvestigation -> lifecycleCommand s Transitions.resolveFrom "ResolveInvestigation" InvestigationResolved
        | ReopenInvestigation r ->
            lifecycleCommand s Transitions.reopenFrom "ReopenInvestigation" (fun from -> InvestigationReopened(from, r.Reason))
        | RecordObservation p ->
            whenAll
                [ recording s; isNew "observation" (s.Observations.ContainsKey p.ObservationId) (Ids.observation p.ObservationId) ]
                (fun () -> [ ObservationRecorded p ])
        | RecordHypothesis p ->
            whenAll
                [ recording s; isNew "hypothesis" (s.Hypotheses.ContainsKey p.HypothesisId) (Ids.hypothesis p.HypothesisId) ]
                (fun () -> [ HypothesisRecorded p ])
        | MarkHypothesisPlausible m ->
            hypothesisTransition s Transitions.markPlausibleFrom "MarkHypothesisPlausible" m HypothesisMarkedPlausible
        | MarkHypothesisSupported m ->
            hypothesisTransition s Transitions.markSupportedFrom "MarkHypothesisSupported" m HypothesisMarkedSupported
        | FalsifyHypothesis m -> hypothesisTransition s Transitions.falsifyFrom "FalsifyHypothesis" m HypothesisFalsified
        | ReopenHypothesis m -> hypothesisTransition s Transitions.reopenHypothesisFrom "ReopenHypothesis" m HypothesisReopened
        | AdmitEvidence p ->
            whenAll
                [ recording s; isNew "evidence" (s.Evidence.ContainsKey p.EvidenceId) (Ids.evidence p.EvidenceId) ]
                (fun () -> [ EvidenceAdmitted p ])
        | RelateEvidence p ->
            whenAll
                [ recording s
                  isNew "relationship" (s.Relationships.ContainsKey p.RelationshipId) (Ids.relationship p.RelationshipId)
                  present "evidence" (s.Evidence.ContainsKey p.EvidenceId) (Ids.evidence p.EvidenceId)
                  present "hypothesis" (s.Hypotheses.ContainsKey p.HypothesisId) (Ids.hypothesis p.HypothesisId) ]
                (fun () -> [ EvidenceRelationshipRecorded p ])
        | IdentifyUnknown p ->
            whenAll
                [ recording s; isNew "unknown" (s.Unknowns.ContainsKey p.UnknownId) (Ids.unknown p.UnknownId) ]
                (fun () -> [ UnknownIdentified p ])
        | RecordAssumption p ->
            whenAll
                [ recording s; isNew "assumption" (s.Assumptions.ContainsKey p.AssumptionId) (Ids.assumption p.AssumptionId) ]
                (fun () -> [ AssumptionRecorded p ])
        | RecordContradiction p ->
            whenAll
                ([ recording s
                   isNew "contradiction" (s.Contradictions.ContainsKey p.ContradictionId) (Ids.contradiction p.ContradictionId) ]
                 @ evidencePresent s p.EvidenceIds)
                (fun () -> [ ContradictionRecorded p ])
        | RecordPrediction p ->
            whenAll
                [ recording s
                  isNew "prediction" (s.Predictions.ContainsKey p.PredictionId) (Ids.prediction p.PredictionId)
                  present "hypothesis" (s.Hypotheses.ContainsKey p.HypothesisId) (Ids.hypothesis p.HypothesisId) ]
                (fun () -> [ PredictionRecorded p ])
        | RecordInvestigationAction p ->
            whenAll
                ([ recording s; isNew "action" (s.Actions.ContainsKey p.ActionId) (Ids.action p.ActionId) ]
                 @ hypothesesPresent s p.TargetHypothesisIds)
                (fun () -> [ InvestigationActionRecorded p ])
        | RecordFinding p ->
            whenAll
                [ recording s
                  isNew "finding" (s.Findings.ContainsKey p.FindingId) (Ids.finding p.FindingId)
                  present "action" (s.Actions.ContainsKey p.ActionId) (Ids.action p.ActionId) ]
                (fun () -> [ FindingRecorded p ])
        | EstablishNegativeKnowledge p ->
            whenAll
                ([ recording s
                   isNew "negativeKnowledge" (s.NegativeKnowledge.ContainsKey p.NegativeKnowledgeId) (Ids.negativeKnowledge p.NegativeKnowledgeId) ]
                 @ evidencePresent s p.BasisEvidenceIds)
                (fun () -> [ NegativeKnowledgeEstablished p ])
        | RecordCausalAssertion p ->
            whenAll
                ([ recording s
                   isNew "cause" (s.Causes.ContainsKey p.CauseId) (Ids.cause p.CauseId)
                   not p.Members.IsEmpty, b "cause.members" "A causal assertion names at least one hypothesis." ]
                 @ hypothesesPresent s (p.Members |> List.map (fun m -> m.HypothesisId)))
                (fun () -> [ CausalAssertionRecorded p ])
        | ConfirmCause p ->
            let current = s.Causes.TryFind p.CauseId |> Option.map (fun c -> c.State)
            whenAll
                [ recording s
                  present "cause" current.IsSome (Ids.cause p.CauseId)
                  inState "cause" Transitions.confirmCauseFrom current (WireEnum.toWire Enums.causeState) "ConfirmCause" ]
                (fun () -> [ CauseConfirmed p ])
        | RecordCorrectiveAction p ->
            whenAll
                ([ recording s
                   isNew "correctiveAction" (s.CorrectiveActions.ContainsKey p.CorrectiveActionId) (Ids.correctiveAction p.CorrectiveActionId) ]
                 @ (p.TargetCauseIds |> List.map (fun c -> present "cause" (s.Causes.ContainsKey c) (Ids.cause c))))
                (fun () -> [ CorrectiveActionRecorded p ])
        | RecordVerificationCriterion p ->
            whenAll
                [ recording s
                  isNew "criterion" (s.Criteria.ContainsKey p.CriterionId) (Ids.criterion p.CriterionId)
                  present "correctiveAction" (s.CorrectiveActions.ContainsKey p.CorrectiveActionId) (Ids.correctiveAction p.CorrectiveActionId) ]
                (fun () -> [ VerificationCriterionRecorded p ])
        | MarkCorrectiveActionImplemented id ->
            let current = s.CorrectiveActions.TryFind id |> Option.map (fun c -> c.State)
            whenAll
                [ recording s
                  present "correctiveAction" current.IsSome (Ids.correctiveAction id)
                  inState "corrective action" Transitions.implementFrom current (WireEnum.toWire Enums.correctiveActionState) "MarkCorrectiveActionImplemented" ]
                (fun () -> [ CorrectiveActionImplemented id ])
        | RecordVerification p ->
            whenAll
                ([ recording s
                   isNew "verification" (s.Verifications.ContainsKey p.VerificationId) (Ids.verification p.VerificationId)
                   present "criterion" (s.Criteria.ContainsKey p.CriterionId) (Ids.criterion p.CriterionId) ]
                 @ evidencePresent s p.EvidenceIds)
                (fun () -> [ VerificationRecorded p ])
        | RecordProposal p ->
            whenAll
                [ recording s; isNew "proposal" (s.Proposals.ContainsKey p.ProposalId) (Ids.proposal p.ProposalId) ]
                (fun () -> [ ProposalRecorded p ])
        | AcceptProposal id ->
            // STATE-TRANSITION-TABLES section 12: "Accepted only if target command succeeds". The proposal's target
            // command is decoded by the proposal loop (T27); until then acceptance is refused, never faked.
            Blocked [ b "proposal.target-unavailable" $"The proposal {txt (Ids.proposal id)} has no decodable target command yet." ]
        | RejectProposal(id, reason) ->
            let current = s.Proposals.TryFind id |> Option.map (fun p -> p.State)
            whenAll
                [ recording s
                  present "proposal" current.IsSome (Ids.proposal id)
                  inState "proposal" Transitions.decideProposalFrom current (WireEnum.toWire Enums.proposalState) "RejectProposal" ]
                (fun () -> [ ProposalRejected(id, reason) ])
        | AttachExtension _ -> UnsupportedSemantics [ "AttachExtension: causa-storage v1.0.0 has no extension namespace" ]

    /// IF41-DOMAIN Decide. Total over every command and state. Order (STATE-TRANSITION-TABLES section 16): semantics
    /// (extensions, unknown policy components), duplicate command, expected version, actor, structural transition, then
    /// the composed policy's gates, whose blockers are all returned.
    let decide (policies: PolicySet) (actor: ActorContext) (state: Investigation option) (command: CommandEnvelope) : Decision =
        let actual = state |> Option.map (fun s -> s.Version) |> Option.defaultValue AggregateVersion.zero
        match command.Command, Policy.rules policies with
        | Extension(id, _), _ ->
            UnsupportedSemantics [ $"{id.Namespace.Value}/{id.Name.Value}@{id.Version.Value}" ]
        | Core _, Error unknown -> UnsupportedSemantics unknown
        | Core core, Ok rules ->
            match state |> Option.bind (fun s -> s.Correlations.TryFind command.CommandId) with
            | Some events -> Duplicate events
            | None ->
                match command.ExpectedVersion with
                | Some expected when expected <> actual -> Conflict(expected, actual)
                | _ ->
                    if (NonEmptyText.tryCreate actor.ActorId).IsNone then
                        Blocked [ b "actor.unidentified" "The command has no identified actor." ]
                    else
                        match core, state with
                        | AttachExtension _, _ -> UnsupportedSemantics [ "AttachExtension: causa-storage v1.0.0 has no extension namespace" ]
                        | StartInvestigation p, None -> allowed [ InvestigationStarted p ]
                        | _, None ->
                            Blocked [ b "investigation.missing" $"{Commands.name core} needs a started investigation." ]
                        | _, Some s when s.Id <> command.Investigation ->
                            Blocked [ b "investigation.mismatch" "The command names a different investigation." ]
                        | _, Some s ->
                            match onExisting s core with
                            | Allowed events ->
                                match Policy.evaluate rules s core with
                                | [] -> Allowed events
                                | blockers -> Blocked blockers
                            | other -> other

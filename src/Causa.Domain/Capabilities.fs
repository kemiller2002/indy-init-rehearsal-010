// Owner: domain. Capability projection (FSHARP-TYPE-SHAPES section 39; REHEARSAL-AGENT-INTERFACE-FREEZE boundary B:
// "permitted capabilities and reasons for blocked actions"). The domain engine decides availability: each capability
// is the decision Decide gives a probe of that command, so the interface never re-derives legality or a gate.
namespace Causa.Domain

open System

/// Whether a command can be issued now, and if not, every reason the engine gives.
[<RequireQualifiedAccess>]
type CapabilityState =
    | Available
    | Unavailable of Blocker list
    | ReviewRequired of string list
    | SemanticsUnavailable of string list

/// One command the interface may offer, optionally for one entity (its id), with the engine's verdict.
type Capability =
    { Command: string
      Target: Guid option
      State: CapabilityState }

[<RequireQualifiedAccess>]
module Capabilities =
    /// The id probes use for a new entity and for their command identity. It is never written: a probe is decided,
    /// never applied.
    let private probeId = Guid("ffffffff-ffff-4fff-bfff-fffffffffffe")

    let private placeholder =
        match NonEmptyText.create "capability probe" with
        | Ok t -> t
        | Error e -> failwith e

    let private mark (h: HypothesisId) : MarkHypothesis = { HypothesisId = h; Reason = placeholder; EvidenceIds = [] }

    /// The probe of every offered command: global commands with a new entity, and per-entity commands for each entity
    /// they act on.
    let private probes (s: Investigation) : (string * Guid option * CoreCommand) list =
        let firstEvidence =
            s.Evidence |> Map.toList |> List.tryFind (fun (_, e) -> e.SupersededBy.IsNone) |> Option.map fst
        [ yield "ActivateInvestigation", None, ActivateInvestigation
          yield "ResolveInvestigation", None, ResolveInvestigation
          yield "ReopenInvestigation", None, ReopenInvestigation { Reason = placeholder }
          yield "RecordObservation", None, RecordObservation { ObservationId = ObservationId probeId; Original = placeholder; ObservedAt = None }
          yield "RecordHypothesis", None, RecordHypothesis { HypothesisId = HypothesisId probeId; Statement = placeholder; Mechanism = None }
          yield
              "AdmitEvidence",
              None,
              AdmitEvidence
                  { EvidenceId = EvidenceId probeId; ContentKind = ContentKind.Text; Content = placeholder
                    Category = EvidenceCategory.DirectObservation; Source = placeholder; CapturedAt = None }
          yield "IdentifyUnknown", None, IdentifyUnknown { UnknownId = UnknownId probeId; Question = placeholder; Materiality = Materiality.Material }
          yield "RecordAssumption", None, RecordAssumption { AssumptionId = AssumptionId probeId; Proposition = placeholder; Materiality = Materiality.Material }
          yield
              "RecordContradiction",
              None,
              RecordContradiction { ContradictionId = ContradictionId probeId; Statement = placeholder; EvidenceIds = []; Materiality = Materiality.Material }
          yield
              "RecordInvestigationAction",
              None,
              RecordInvestigationAction { ActionId = InvestigationActionId probeId; Kind = ActionKind.Inspection; Purpose = placeholder; TargetHypothesisIds = [] }
          yield
              "EstablishNegativeKnowledge",
              None,
              EstablishNegativeKnowledge { NegativeKnowledgeId = NegativeKnowledgeId probeId; Statement = placeholder; Scope = placeholder; BasisEvidenceIds = [] }
          yield
              "RecordCorrectiveAction",
              None,
              RecordCorrectiveAction { CorrectiveActionId = CorrectiveActionId probeId; Kind = CorrectiveActionKind.Correction; Description = placeholder; TargetCauseIds = [] }
          yield "RecordProposal", None, RecordProposal { ProposalId = ProposalId probeId; Kind = ProposalKind.Hypothesis; ProposalText = placeholder; Rationale = None }
          for KeyValue(HypothesisId g as h, _) in s.Hypotheses do
              yield "MarkHypothesisPlausible", Some g, MarkHypothesisPlausible(mark h)
              yield "MarkHypothesisSupported", Some g, MarkHypothesisSupported(mark h)
              yield "FalsifyHypothesis", Some g, FalsifyHypothesis(mark h)
              yield "ReopenHypothesis", Some g, ReopenHypothesis(mark h)
              yield
                  "RecordPrediction",
                  Some g,
                  RecordPrediction
                      { PredictionId = PredictionId probeId; HypothesisId = h; Statement = placeholder
                        Strength = PredictionStrength.Expected; Timing = PredictionTiming.Prospective }
              yield
                  "RecordCausalAssertion",
                  Some g,
                  RecordCausalAssertion
                      { CauseId = CausalAssertionId probeId; Outcome = placeholder
                        Members = [ { HypothesisId = h; Role = CauseRole.RootCause } ]; Mechanism = None }
              yield
                  "RelateEvidence",
                  Some g,
                  RelateEvidence
                      { RelationshipId = EvidenceRelationshipId probeId
                        EvidenceId = firstEvidence |> Option.defaultValue (EvidenceId probeId)
                        HypothesisId = h; Kind = RelationshipKind.Supports; Rationale = None }
          for KeyValue(InvestigationActionId g as a, _) in s.Actions do
              yield "RecordFinding", Some g, RecordFinding { FindingId = FindingId probeId; ActionId = a; OriginalResult = placeholder; Interpretation = None }
          for KeyValue(CausalAssertionId g as c, _) in s.Causes do
              yield "ConfirmCause", Some g, ConfirmCause { CauseId = c; Policy = placeholder }
          for KeyValue(CorrectiveActionId g as c, _) in s.CorrectiveActions do
              yield "MarkCorrectiveActionImplemented", Some g, MarkCorrectiveActionImplemented c
              yield
                  "RecordVerificationCriterion",
                  Some g,
                  RecordVerificationCriterion { CriterionId = VerificationCriterionId probeId; CorrectiveActionId = c; ExpectedOutcome = placeholder; Method = None }
          for KeyValue(VerificationCriterionId g as c, _) in s.Criteria do
              yield
                  "RecordVerification",
                  Some g,
                  RecordVerification { VerificationId = VerificationId probeId; CriterionId = c; Outcome = VerificationOutcome.Satisfied; EvidenceIds = [] }
          for KeyValue(ProposalId g as p, _) in s.Proposals do
              yield "AcceptProposal", Some g, AcceptProposal p
              yield "RejectProposal", Some g, RejectProposal(p, placeholder) ]

    /// Every capability of the investigation under the composed policy, for this actor, with the engine's reasons.
    let evaluate (policies: PolicySet) (actor: ActorContext) (s: Investigation) : Capability list =
        probes s
        |> List.map (fun (name, target, command) ->
            let envelope = { CommandId = probeId; Investigation = s.Id; ExpectedVersion = Some s.Version; Command = Core command }
            let state =
                match Decide.decide policies actor (Some s) envelope with
                | Allowed _ -> CapabilityState.Available
                | Blocked blockers -> CapabilityState.Unavailable blockers
                | RequiresReview reviews -> CapabilityState.ReviewRequired reviews
                | UnsupportedSemantics semantics -> CapabilityState.SemanticsUnavailable semantics
                | Conflict _
                | Duplicate _ -> CapabilityState.Unavailable [ Transitions.blocker "capability.probe" "The probe could not be decided." ]
            { Command = name; Target = target; State = state })

    /// The command names available for at least one target under the core policy (the frozen projection's list).
    let available (s: Investigation) : string list =
        evaluate Transitions.corePolicy { ActorId = "causa" } s
        |> List.filter (fun c -> c.State = CapabilityState.Available)
        |> List.map (fun c -> c.Command)
        |> List.distinct

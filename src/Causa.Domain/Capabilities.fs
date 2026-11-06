// Owner: domain. Capability projection (FSHARP-TYPE-SHAPES section 39): which commands the current state admits, read
// from the same transition tables Decide uses, so the UI never re-derives legality.
namespace Causa.Domain

[<RequireQualifiedAccess>]
module Capabilities =
    /// The command names structurally available on this investigation, in CoreCommand order.
    let available (s: Investigation) : string list =
        let recording = Transitions.recordingAllowedIn s.Lifecycle
        let anyHypothesisIn states = s.Hypotheses |> Map.exists (fun _ h -> List.contains h.State states)
        [ "ActivateInvestigation", List.contains s.Lifecycle Transitions.activateFrom
          "RecordObservation", recording
          "RecordHypothesis", recording
          "MarkHypothesisPlausible", recording && anyHypothesisIn Transitions.markPlausibleFrom
          "MarkHypothesisSupported", recording && anyHypothesisIn Transitions.markSupportedFrom
          "FalsifyHypothesis", recording && anyHypothesisIn Transitions.falsifyFrom
          "ReopenHypothesis", recording && anyHypothesisIn Transitions.reopenHypothesisFrom
          "AdmitEvidence", recording
          "RelateEvidence", recording && not s.Evidence.IsEmpty && not s.Hypotheses.IsEmpty
          "IdentifyUnknown", recording
          "RecordAssumption", recording
          "RecordContradiction", recording
          "RecordPrediction", recording && not s.Hypotheses.IsEmpty
          "RecordInvestigationAction", recording
          "RecordFinding", recording && not s.Actions.IsEmpty
          "EstablishNegativeKnowledge", recording
          "RecordCausalAssertion", recording && not s.Hypotheses.IsEmpty
          "ConfirmCause", recording && (s.Causes |> Map.exists (fun _ c -> List.contains c.State Transitions.confirmCauseFrom))
          "RecordCorrectiveAction", recording
          "RecordVerificationCriterion", recording && not s.CorrectiveActions.IsEmpty
          "MarkCorrectiveActionImplemented",
          recording && (s.CorrectiveActions |> Map.exists (fun _ c -> List.contains c.State Transitions.implementFrom))
          "RecordVerification", recording && not s.Criteria.IsEmpty
          "ResolveInvestigation", List.contains s.Lifecycle Transitions.resolveFrom
          "ReopenInvestigation", List.contains s.Lifecycle Transitions.reopenFrom
          "RecordProposal", recording
          "RejectProposal", recording && (s.Proposals |> Map.exists (fun _ p -> List.contains p.State Transitions.decideProposalFrom)) ]
        |> List.filter snd
        |> List.map fst

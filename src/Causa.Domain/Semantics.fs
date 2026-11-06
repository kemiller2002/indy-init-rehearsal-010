// Owner: domain. Semantic identity, scope and the closed causa-storage v1.0.0 enumerations (CONTRACT section 6;
// FSHARP-TYPE-SHAPES sections 4, 6, 8-18 as narrowed to the wire).
namespace Causa.Domain

open System

/// Frozen name (FSHARP-TYPE-SHAPES section 4).
type SemanticId =
    { Namespace: NonEmptyText
      Name: NonEmptyText
      Version: NonEmptyText }

type StabilityClass =
    | CoreStable
    | DomainStable
    | Experimental
    | Deprecated
    | Retired

type Requiredness =
    | Required
    | Optional

/// Frozen name (FSHARP-TYPE-SHAPES section 6). causa-storage v1.0.0 has no extension namespace (CONTRACT section
/// 13.5), so no stored event carries one; Decide refuses AttachExtension as unsupported semantics.
type ExtensionEnvelope =
    { SemanticId: SemanticId
      SchemaId: string
      Stability: StabilityClass
      Requiredness: Requiredness
      PayloadJson: string }

type TimeScope =
    { Start: DateTimeOffset option
      EndExclusive: DateTimeOffset option }

/// Frozen name (FSHARP-TYPE-SHAPES section 6). Not stored by causa-storage v1.0.0 (no event payload has a scope; the
/// v4.1.5 HypothesisFalsified no longer carries one).
type Scope =
    { Time: TimeScope option
      Systems: string list
      Versions: string list
      Environments: string list
      Subjects: string list }

/// Frozen name. The investigation lifecycle (STATE-TRANSITION-TABLES section 1; wire enum `lifecycle`).
[<RequireQualifiedAccess>]
type Lifecycle =
    | Draft
    | Active
    | AwaitingEvidence
    | AwaitingVerification
    | Resolved
    | Closed
    | Archived

[<RequireQualifiedAccess>]
type HypothesisState =
    | Proposed
    | Plausible
    | Supported
    | Weakened
    | Falsified
    | Superseded

[<RequireQualifiedAccess>]
type UnknownState =
    | Identified
    | Investigating
    | Resolved
    | AcceptedResidual
    | Superseded

[<RequireQualifiedAccess>]
type Materiality =
    | Blocking
    | Material
    | NonMaterial
    | Informational

[<RequireQualifiedAccess>]
type AssumptionState =
    | ExplicitUnverified
    | ProvisionallyAccepted
    | Verified
    | Rejected
    | Expired

[<RequireQualifiedAccess>]
type ContradictionState =
    | Detected
    | Triaged
    | Investigating
    | Resolved
    | AcceptedResidual
    | Superseded

[<RequireQualifiedAccess>]
type ActionState =
    | Proposed
    | Approved
    | InProgress
    | Completed
    | Interpreted
    | Rejected
    | Cancelled

[<RequireQualifiedAccess>]
type ActionKind =
    | Test
    | Inspection
    | Query
    | Comparison
    | Experiment

[<RequireQualifiedAccess>]
type CauseRole =
    | RootCause
    | ContributingFactor
    | Trigger
    | EnablingCondition

[<RequireQualifiedAccess>]
type CauseState =
    | Proposed
    | Confirmed
    | Reopened
    | Superseded

[<RequireQualifiedAccess>]
type CorrectiveActionState =
    | Proposed
    | Approved
    | InProgress
    | Implemented
    | AwaitingVerificationOfEffect
    | VerifiedEffective
    | VerificationFailed
    | ReworkRequired
    | Cancelled

[<RequireQualifiedAccess>]
type CorrectiveActionKind =
    | Mitigation
    | Workaround
    | Correction
    | Prevention

[<RequireQualifiedAccess>]
type EvidenceCategory =
    | DirectObservation
    | Measurement
    | Log
    | Document
    | ExperimentResult
    | Testimony
    | ExternalReference

[<RequireQualifiedAccess>]
type VerificationStatus =
    | Unverified
    | PartiallyVerified
    | Verified
    | Invalidated

[<RequireQualifiedAccess>]
type RelationshipKind =
    | Supports
    | Contradicts
    | Discriminates
    | Insufficient

[<RequireQualifiedAccess>]
type PredictionStrength =
    | Necessary
    | Expected
    | Discriminating
    | NonDiscriminating

[<RequireQualifiedAccess>]
type PredictionTiming =
    | Prospective
    | Retrospective

[<RequireQualifiedAccess>]
type VerificationOutcome =
    | Satisfied
    | Failed
    | Inconclusive

[<RequireQualifiedAccess>]
type ConclusionStrength =
    | PlausibleExplanation
    | LeadingExplanation
    | SupportedCause
    | ConfirmedCause
    | AcceptedWithResidualUncertainty

[<RequireQualifiedAccess>]
type ProposalKind =
    | Hypothesis
    | Unknown
    | Action

[<RequireQualifiedAccess>]
type ProposalState =
    | Active
    | Accepted
    | Rejected

[<RequireQualifiedAccess>]
type ContentKind =
    | Text
    | Artifact

/// A closed wire enumeration: the exact lowerCamelCase wire value of every case (CONTRACT section 6), with no
/// fallback value. Persistence's codec reads and writes enum fields only through these tables.
type WireEnum<'T when 'T: equality> = { Name: string; Table: (string * 'T) list }

[<RequireQualifiedAccess>]
module WireEnum =
    let tryParse (e: WireEnum<'T>) (wire: string) : 'T option =
        e.Table |> List.tryFind (fun (w, _) -> String.Equals(w, wire, StringComparison.Ordinal)) |> Option.map snd

    let toWire (e: WireEnum<'T>) (v: 'T) : string = e.Table |> List.find (snd >> (=) v) |> fst
    let values (e: WireEnum<'T>) : 'T list = e.Table |> List.map snd

/// The wire tables of every enumeration (CONTRACT section 6), value for value.
[<RequireQualifiedAccess>]
module Enums =
    let private create name table : WireEnum<'T> = { Name = name; Table = table }

    let lifecycle =
        create "lifecycle"
            [ "draft", Lifecycle.Draft; "active", Lifecycle.Active; "awaitingEvidence", Lifecycle.AwaitingEvidence
              "awaitingVerification", Lifecycle.AwaitingVerification; "resolved", Lifecycle.Resolved; "closed", Lifecycle.Closed
              "archived", Lifecycle.Archived ]

    let hypothesisState =
        create "hypothesisState"
            [ "proposed", HypothesisState.Proposed; "plausible", HypothesisState.Plausible; "supported", HypothesisState.Supported
              "weakened", HypothesisState.Weakened; "falsified", HypothesisState.Falsified; "superseded", HypothesisState.Superseded ]

    let unknownState =
        create "unknownState"
            [ "identified", UnknownState.Identified; "investigating", UnknownState.Investigating; "resolved", UnknownState.Resolved
              "acceptedResidual", UnknownState.AcceptedResidual; "superseded", UnknownState.Superseded ]

    let materiality =
        create "materiality"
            [ "blocking", Materiality.Blocking; "material", Materiality.Material; "nonMaterial", Materiality.NonMaterial
              "informational", Materiality.Informational ]

    let assumptionState =
        create "assumptionState"
            [ "explicitUnverified", AssumptionState.ExplicitUnverified; "provisionallyAccepted", AssumptionState.ProvisionallyAccepted
              "verified", AssumptionState.Verified; "rejected", AssumptionState.Rejected; "expired", AssumptionState.Expired ]

    let contradictionState =
        create "contradictionState"
            [ "detected", ContradictionState.Detected; "triaged", ContradictionState.Triaged; "investigating", ContradictionState.Investigating
              "resolved", ContradictionState.Resolved; "acceptedResidual", ContradictionState.AcceptedResidual
              "superseded", ContradictionState.Superseded ]

    let actionState =
        create "actionState"
            [ "proposed", ActionState.Proposed; "approved", ActionState.Approved; "inProgress", ActionState.InProgress
              "completed", ActionState.Completed; "interpreted", ActionState.Interpreted; "rejected", ActionState.Rejected
              "cancelled", ActionState.Cancelled ]

    let actionKind =
        create "actionKind"
            [ "test", ActionKind.Test; "inspection", ActionKind.Inspection; "query", ActionKind.Query; "comparison", ActionKind.Comparison
              "experiment", ActionKind.Experiment ]

    let causeRole =
        create "causeRole"
            [ "rootCause", CauseRole.RootCause; "contributingFactor", CauseRole.ContributingFactor; "trigger", CauseRole.Trigger
              "enablingCondition", CauseRole.EnablingCondition ]

    let causeState =
        create "causeState"
            [ "proposed", CauseState.Proposed; "confirmed", CauseState.Confirmed; "reopened", CauseState.Reopened
              "superseded", CauseState.Superseded ]

    let correctiveActionState =
        create "correctiveActionState"
            [ "proposed", CorrectiveActionState.Proposed; "approved", CorrectiveActionState.Approved
              "inProgress", CorrectiveActionState.InProgress; "implemented", CorrectiveActionState.Implemented
              "awaitingVerificationOfEffect", CorrectiveActionState.AwaitingVerificationOfEffect
              "verifiedEffective", CorrectiveActionState.VerifiedEffective; "verificationFailed", CorrectiveActionState.VerificationFailed
              "reworkRequired", CorrectiveActionState.ReworkRequired; "cancelled", CorrectiveActionState.Cancelled ]

    let correctiveActionKind =
        create "correctiveActionKind"
            [ "mitigation", CorrectiveActionKind.Mitigation; "workaround", CorrectiveActionKind.Workaround
              "correction", CorrectiveActionKind.Correction; "prevention", CorrectiveActionKind.Prevention ]

    let evidenceCategory =
        create "evidenceCategory"
            [ "directObservation", EvidenceCategory.DirectObservation; "measurement", EvidenceCategory.Measurement
              "log", EvidenceCategory.Log; "document", EvidenceCategory.Document; "experimentResult", EvidenceCategory.ExperimentResult
              "testimony", EvidenceCategory.Testimony; "externalReference", EvidenceCategory.ExternalReference ]

    let verificationStatus =
        create "verificationStatus"
            [ "unverified", VerificationStatus.Unverified; "partiallyVerified", VerificationStatus.PartiallyVerified
              "verified", VerificationStatus.Verified; "invalidated", VerificationStatus.Invalidated ]

    let relationshipKind =
        create "relationshipKind"
            [ "supports", RelationshipKind.Supports; "contradicts", RelationshipKind.Contradicts
              "discriminates", RelationshipKind.Discriminates; "insufficient", RelationshipKind.Insufficient ]

    let predictionStrength =
        create "predictionStrength"
            [ "necessary", PredictionStrength.Necessary; "expected", PredictionStrength.Expected
              "discriminating", PredictionStrength.Discriminating; "nonDiscriminating", PredictionStrength.NonDiscriminating ]

    let predictionTiming =
        create "predictionTiming" [ "prospective", PredictionTiming.Prospective; "retrospective", PredictionTiming.Retrospective ]

    let verificationOutcome =
        create "verificationOutcome"
            [ "satisfied", VerificationOutcome.Satisfied; "failed", VerificationOutcome.Failed; "inconclusive", VerificationOutcome.Inconclusive ]

    let conclusionStrength =
        create "conclusionStrength"
            [ "plausibleExplanation", ConclusionStrength.PlausibleExplanation; "leadingExplanation", ConclusionStrength.LeadingExplanation
              "supportedCause", ConclusionStrength.SupportedCause; "confirmedCause", ConclusionStrength.ConfirmedCause
              "acceptedWithResidualUncertainty", ConclusionStrength.AcceptedWithResidualUncertainty ]

    let proposalKind =
        create "proposalKind" [ "hypothesis", ProposalKind.Hypothesis; "unknown", ProposalKind.Unknown; "action", ProposalKind.Action ]

    let proposalState =
        create "proposalState" [ "active", ProposalState.Active; "accepted", ProposalState.Accepted; "rejected", ProposalState.Rejected ]

    let contentKind = create "contentKind" [ "text", ContentKind.Text; "artifact", ContentKind.Artifact ]

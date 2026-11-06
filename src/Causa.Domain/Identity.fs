// Owner: domain. IF41-DOMAIN identity (FSHARP-TYPE-SHAPES sections 2-3; frozen names of 00-DOMAIN-STUBS.fs).
namespace Causa.Domain

open System

/// Frozen (00-DOMAIN-STUBS.fs). A causa-storage v1.0.0 UUID (lowercase canonical text on the wire, CONTRACT section 2).
type InvestigationId = InvestigationId of Guid
type EventId = EventId of Guid
/// Frozen. The aggregate version: the number of applied events (contiguous from 1; 0 means "no investigation yet").
type AggregateVersion = AggregateVersion of int64

/// Frozen typed identities (00-DOMAIN-STUBS.fs).
type HypothesisId = HypothesisId of Guid
type CorrectiveActionId = CorrectiveActionId of Guid
type ProposalId = ProposalId of Guid
type CausalAssertionId = CausalAssertionId of Guid

/// The other entity identities of the causa-storage v1.0.0 payloads (CONTRACT section 7), one wrapper per id field.
type ObservationId = ObservationId of Guid
type EvidenceId = EvidenceId of Guid
type EvidenceRelationshipId = EvidenceRelationshipId of Guid
type UnknownId = UnknownId of Guid
type AssumptionId = AssumptionId of Guid
type ContradictionId = ContradictionId of Guid
type PredictionId = PredictionId of Guid
type InvestigationActionId = InvestigationActionId of Guid
type FindingId = FindingId of Guid
type NegativeKnowledgeId = NegativeKnowledgeId of Guid
type VerificationCriterionId = VerificationCriterionId of Guid
type VerificationId = VerificationId of Guid
type ConclusionId = ConclusionId of Guid

/// Frozen name. Domain text (CONTRACT section 2.6): stored verbatim, never trimmed or normalised, with at least one
/// non-whitespace character. Its representation is private, so every value went through NonEmptyText.create.
[<CustomEquality; CustomComparison>]
type NonEmptyText =
    private
    | NonEmptyText of string

    member t.Value = let (NonEmptyText s) = t in s
    override t.ToString() = t.Value
    override t.Equals(o) =
        match o with
        | :? NonEmptyText as u -> String.Equals(t.Value, u.Value, StringComparison.Ordinal)
        | _ -> false
    override t.GetHashCode() = StringComparer.Ordinal.GetHashCode t.Value
    interface IComparable with
        member t.CompareTo(o) =
            match o with
            | :? NonEmptyText as u -> String.CompareOrdinal(t.Value, u.Value)
            | _ -> invalidArg "o" "not a NonEmptyText"

[<RequireQualifiedAccess>]
module NonEmptyText =
    /// The causa-storage engine whitespace set (CONTRACT section 2.6): Unicode White_Space.
    let private isWhitespace (c: char) =
        (c >= '\u0009' && c <= '\u000D') || c = ' ' || c = '\u0085' || c = ' ' || c = ' '
        || (c >= ' ' && c <= ' ') || c = ' ' || c = ' ' || c = ' ' || c = ' ' || c = '　'

    /// Ok only for text with at least one non-whitespace character; the text is kept exactly as given.
    let create (s: string) : Result<NonEmptyText, string> =
        if s |> Seq.exists (isWhitespace >> not) then Ok(NonEmptyText s)
        else Error "text must contain a non-whitespace character"

    let tryCreate (s: string) : NonEmptyText option =
        match create s with
        | Ok t -> Some t
        | Error _ -> None

    let value (t: NonEmptyText) : string = t.Value

[<RequireQualifiedAccess>]
module AggregateVersion =
    let zero = AggregateVersion 0L
    let value (AggregateVersion v) = v
    let next (AggregateVersion v) = AggregateVersion(v + 1L)

[<RequireQualifiedAccess>]
module Ids =
    let investigation (InvestigationId g) = g
    let event (EventId g) = g
    let hypothesis (HypothesisId g) = g
    let correctiveAction (CorrectiveActionId g) = g
    let proposal (ProposalId g) = g
    let cause (CausalAssertionId g) = g
    let observation (ObservationId g) = g
    let evidence (EvidenceId g) = g
    let relationship (EvidenceRelationshipId g) = g
    let unknown (UnknownId g) = g
    let assumption (AssumptionId g) = g
    let contradiction (ContradictionId g) = g
    let prediction (PredictionId g) = g
    let action (InvestigationActionId g) = g
    let finding (FindingId g) = g
    let negativeKnowledge (NegativeKnowledgeId g) = g
    let criterion (VerificationCriterionId g) = g
    let verification (VerificationId g) = g
    let conclusion (ConclusionId g) = g

    /// The wire text of a UUID: lowercase canonical form (CONTRACT section 2.8).
    let text (g: Guid) = g.ToString("D")

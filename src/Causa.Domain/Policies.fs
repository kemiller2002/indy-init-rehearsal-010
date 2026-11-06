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

// Owner: integration. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.App

open Causa.Domain
open Causa.Persistence

/// IF41-APP: the Limen engine boundary (App <-> browser host). The host relays only what the engine asks for.
/// The credential is opaque (IF41-WIRE Credential): created once from the Connect form, kept only in the model's
/// connection context, never rendered, logged or stored.
/// SM-CONNECTION states.
type ConnectionState =
    | Disconnected
    | Connecting of RequestId
    | Connected of canWrite: bool
    | CredentialRefused
    | Reauthenticating

/// SM-CONNECTION events.
[<RequireQualifiedAccess>]
type ConnectionInput =
    | ConnectSubmitted of Repository * Credential.T
    | ConnectSucceeded of canWrite: bool
    | ConnectRefused of ProviderError
    | AuthFailed
    | ReconnectSubmitted of Credential.T
    | DisconnectConfirmed

/// SM-NETWORK states.
[<RequireQualifiedAccess>]
type NetworkState =
    | Online
    | Offline

/// SM-NETWORK events.
[<RequireQualifiedAccess>]
type NetworkInput =
    | OfflineObserved
    | OnlineObserved

/// What a host timer is for.
type TimerKey = ReconcileTimer of InvestigationRef

type BrowserSignal =
    | ConnectivityChanged of online: bool
    | TimerFired of TimerKey
    | HttpCompleted of HttpOutcome
    | LeaveAttempted

type HostEffect =
    | Http of HttpRequest
    | Timer of TimerKey * milliseconds: int
    | SetLeaveGuard of bool
    | Download of fileName: string * content: string
    | LiveAnnounce of Politeness * text: string

type Route = Route of string

type Msg =
    | Navigate of Route
    | Browser of BrowserSignal
    | Connection of ConnectionInput
    | Network of NetworkInput
    | SessionInput of InvestigationRef * SaveInput
    | ConflictStep of InvestigationRef * ConflictInput
    | LoadStep of InvestigationRef * LoadInput
    /// v4.1.5 L1, W1: the user selected what to list (EXT-DATA-003: an explicit selection of the workspace).
    | ListRequested of ListingTarget
    /// v4.1.5 C1: the user asked to create an investigation in a working copy (EXT-DATA-006: an explicit action).
    | CreateRequested of Writable.T * CommandEnvelope
    | DomainCommand of CommandEnvelope

/// The model keeps the connection context after Connect succeeds and the next request identity of the tab.
type Model =
    { Connection: ConnectionState
      Context: ConnectionContext option
      NextRequestId: RequestId
      Network: NetworkState
      Route: Route
      Loads: Map<InvestigationRef, LoadState>
      Sessions: Map<InvestigationRef, InvestigationSession>
      /// v4.1.5 L1: the latest listing of each workspace the user selected, and the listing reads in flight.
      Listings: Map<WorkspaceAddress.T, Listing>
      ListingsInFlight: Map<RequestId, ListingTarget * ListingStep> }

type Engine =
    { Init: Route -> Model * HostEffect list
      Update: Msg -> Model -> Model * HostEffect list }

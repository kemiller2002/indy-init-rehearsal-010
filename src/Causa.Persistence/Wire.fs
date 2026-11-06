// Owner: persistence. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.Persistence

/// IF41-WIRE: HTTP as data. Persistence builds requests and decodes answers; the App runtime only executes them
/// through Limen and returns the raw result (decoding stays inside persistence: ODR-V4-06, approved in APR-V4-OWNER-1).
type RequestId = RequestId of int64

type HttpMethod =
    | Get
    | Post
    | Put
    | Patch

type HttpRequest =
    { Id: RequestId
      Method: HttpMethod
      Url: string
      Headers: (string * string) list
      Body: string option }

type TransportFailure =
    | FailedBeforeSending
    | LostAfterSending
    | TimedOut

type HttpOutcome =
    | Answered of RequestId * status: int * headers: (string * string) list * body: string
    | NotAnswered of RequestId * TransportFailure

/// The host's only network operation: execute one request exactly once and return its raw outcome; it never retries.
type Execute = HttpRequest -> HttpOutcome

/// The credential is opaque everywhere: its representation is private to this module, it prints as "<credential>",
/// and its only use is the Authorization header persistence adds to each request (PF-015, INV-21).
[<RequireQualifiedAccess>]
module Credential =
    type T =
        private
        | Token of string
        override _.ToString() = "<credential>"

    let create (token: string) : T = Token token

    let authorization (Token token) : string * string = "Authorization", "Bearer " + token

type Repository = { Owner: string; Name: string; Branch: string }

/// What every request needs after Connect: the repository and the credential. The engine keeps it in its model.
type ConnectionContext = { Repository: Repository; Credential: Credential.T }

/// What an operation may use to emit a request. Every operation emits at most one request, which carries NextId; the
/// engine advances NextId after each operation that emitted one, so identities are unique across the tab.
/// MutationInFlightElsewhere says whether another investigation in this tab has a mutation in flight (T-S04, T-S53).
type RequestContext =
    { Connection: ConnectionContext
      NextId: RequestId
      MutationInFlightElsewhere: bool
      /// v4.1.2 (ODR-V412-01): the time of this operation, from the host clock (the engine supplies it; the machines
      /// stay pure). SM-SAVE's reconciliation-read guards count the reads sent in the 60 s before it (INV-04).
      Now: System.DateTimeOffset }

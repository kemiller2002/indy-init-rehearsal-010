// Owner: persistence. Written at bootstrap by requirements-compiler v41; the owner replaces this content.
namespace Causa.Persistence

/// IF41-ERRORS: the classification of github-error-matrix.json. Every provider answer maps to exactly one case.
type RateLimitKind =
    | Primary of resetAtEpochSeconds: int64 option
    | Secondary of retryAfterSeconds: int option

type ProviderError =
    | AuthenticationFailed
    | PermissionDenied of operation: string
    | RateLimited of RateLimitKind
    | StaleHead of expected: CommitOid * actual: CommitOid option
    | ValidationFailed of messages: string list
    | NotFound of what: string
    | HistoryRewritten of expected: CommitOid
    | NetworkUnavailable of TransportFailure
    | Malformed of detail: string
    | ReadBackMismatch of commit: CommitOid
    | Unrecognised of status: int

/// A 2xx answer that decoded: the request it answers and its body, for the caller to decode further.
type ProviderAnswer = { Request: RequestId; Status: int; Body: string }

type Classifier = HttpOutcome -> Result<ProviderAnswer, ProviderError>

/// The answer to Connect (EXT-DATA-005): write access comes only from the permissions GitHub states for the
/// repository, never from a successful read.
type ConnectResult =
    | ConnectAccepted of canWrite: bool
    | ConnectRejected of ProviderError
    | ConnectSuperseded

/// Persistence builds the Connect request and decodes its answer; the App maps ConnectResult to SM-CONNECTION's events.
type Connector =
    { ConnectRequest: RequestId -> ConnectionContext -> HttpRequest
      InterpretConnect: RequestId -> HttpOutcome -> ConnectResult }

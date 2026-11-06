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

/// The classification of github-error-matrix.json, applied in its precedence order: GH-AUTH, GH-RATE-PRIMARY,
/// GH-RATE-SECONDARY, GH-STALE-HEAD, GH-LARGE-FILE, GH-PERMISSION, GH-VALIDATION, GH-NOT-FOUND, then the rest. Every
/// decision reads the answer's typed content (headers, GraphQL error types and messages, JSON fields), never the HTTP
/// status alone where GitHub gives that status several meanings (PERSIST-008-O2, INV-22).
[<RequireQualifiedAccess>]
module Classify =
    open System
    open System.Text.Json
    open System.Text.RegularExpressions

    let private header (name: string) (headers: (string * string) list) =
        headers
        |> List.tryPick (fun (k, v) -> if String.Equals(k, name, StringComparison.OrdinalIgnoreCase) then Some(v.Trim()) else None)

    let private tryParse (body: string) : JsonElement option =
        try
            use doc = JsonDocument.Parse(body)
            Some(doc.RootElement.Clone())
        with _ -> None

    let private prop (name: string) (e: JsonElement) : JsonElement option =
        if e.ValueKind = JsonValueKind.Object then
            match e.TryGetProperty(name) with
            | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
            | _ -> None
        else
            None

    let private str (e: JsonElement) : string option =
        if e.ValueKind = JsonValueKind.String then
            match e.GetString() with
            | null -> None
            | s -> Some s
        else
            None

    let private message (body: JsonElement option) =
        body |> Option.bind (prop "message") |> Option.bind str |> Option.defaultValue ""

    /// One GraphQL error as GitHub sends it: `type` (absent on schema-validation errors), `message`, and whether it
    /// carries `extensions.problems` (graphql input validation).
    type GraphQlError = { Type: string option; Message: string; InputProblems: bool }

    let graphQlErrors (body: JsonElement option) : GraphQlError list =
        match body |> Option.bind (prop "errors") with
        | Some errors when errors.ValueKind = JsonValueKind.Array ->
            [ for e in errors.EnumerateArray() ->
                  { Type = prop "type" e |> Option.bind str
                    Message = prop "message" e |> Option.bind str |> Option.defaultValue ""
                    InputProblems =
                      prop "extensions" e |> Option.bind (prop "problems") |> Option.isSome
                      || (prop "message" e |> Option.bind str |> Option.defaultValue "").StartsWith("Variable $") } ]
        | _ -> []

    let private oidPattern = "[0-9a-fA-F]{40}"

    /// The moved-head FORBIDDEN form observed live in Rehearsal 7 (KD-022): `... is at <actual> but expected <expected>`.
    let private movedHead = Regex(sprintf "is at (%s) but expected (%s)" oidPattern oidPattern, RegexOptions.CultureInvariant)

    /// The STALE_DATA form: `Expected branch to point to "<expected>" but it did not`.
    let private expectedBranch = Regex(sprintf "Expected branch to point to \\\\?\"?(%s)?" oidPattern, RegexOptions.CultureInvariant)

    let private secondaryMessage =
        Regex("secondary rate limit|too quickly|abuse detection|abuse mechanism", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

    let private primaryMessage = Regex("API rate limit exceeded", RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)

    /// GH-STALE-HEAD from one GraphQL error, if it is one of its forms.
    let staleHeadOf (expectedHead: CommitOid) (e: GraphQlError) : ProviderError option =
        let moved = movedHead.Match e.Message
        if moved.Success then
            Some(StaleHead(CommitOid(moved.Groups[2].Value.ToLowerInvariant()), Some(CommitOid(moved.Groups[1].Value.ToLowerInvariant()))))
        elif e.Type = Some "STALE_DATA" || expectedBranch.IsMatch e.Message then
            Some(StaleHead(expectedHead, None))
        else
            None

    let private operationName (operation: GitHubOperation) =
        match operation with
        | ConnectRepository -> "read the repository"
        | ReadBranchHead -> "read the branch"
        | ReadFileAt(RepoPath p, _) -> "read " + p
        | ReadBlob _ -> "read a blob"
        | ReadCommit _ -> "read a commit"
        | CompareFrom _ -> "compare commits"
        | CommitOnBranch _ -> "write to the branch"
        | CreateBranch -> "create a branch"

    let private whatOf (operation: GitHubOperation) =
        match operation with
        | ReadFileAt(RepoPath p, CommitOid c) -> sprintf "%s at %s" p c
        | ReadBranchHead -> "branch"
        | ReadCommit(CommitOid c) -> "commit " + c
        | ReadBlob sha -> "blob " + sha
        | CompareFrom(CommitOid c) -> "commit " + c
        | ConnectRepository -> "repository"
        | CommitOnBranch _ -> "branch"
        | CreateBranch -> "branch"

    /// GH-LARGE-FILE: the contents API refused a file over 1 MB (403 `too_large`) or answered `encoding: none`. Not an
    /// error: the caller re-reads the file once through the Git blobs API.
    let isLargeFile (status: int) (body: string) : bool =
        let parsed = tryParse body
        let errorCode =
            parsed
            |> Option.bind (prop "errors")
            |> Option.filter (fun e -> e.ValueKind = JsonValueKind.Array)
            |> Option.map (fun e -> [ for x in e.EnumerateArray() -> prop "code" x |> Option.bind str |> Option.defaultValue "" ])
            |> Option.defaultValue []
        (status = 403 && (List.contains "too_large" errorCode || (message parsed).Contains("too_large")))
        || (status = 200 && (parsed |> Option.bind (prop "encoding") |> Option.bind str) = Some "none")

    /// Rate-limit evidence (GH-RATE-PRIMARY, then GH-RATE-SECONDARY), for any status.
    let private rateLimit (status: int) (headers: (string * string) list) (parsed: JsonElement option) (errors: GraphQlError list) =
        let remaining = header "x-ratelimit-remaining" headers
        let retryAfter = header "retry-after" headers |> Option.bind (fun v -> match Int32.TryParse v with | true, n -> Some n | _ -> None)
        let reset = header "x-ratelimit-reset" headers |> Option.bind (fun v -> match Int64.TryParse v with | true, n -> Some n | _ -> None)
        let msg = message parsed
        let graphQlLimited = errors |> List.exists (fun e -> e.Type = Some "RATE_LIMITED")
        let limitedStatus = status = 403 || status = 429
        if (limitedStatus || graphQlLimited) && remaining = Some "0" then
            Some(RateLimited(Primary reset))
        elif (limitedStatus && (retryAfter.IsSome || secondaryMessage.IsMatch msg)) || graphQlLimited
             || errors |> List.exists (fun e -> secondaryMessage.IsMatch e.Message) then
            Some(RateLimited(Secondary retryAfter))
        elif limitedStatus && primaryMessage.IsMatch msg then
            Some(RateLimited(Primary reset))
        else
            None

    let private validationMessages (parsed: JsonElement option) =
        let main = message parsed
        let details =
            match parsed |> Option.bind (prop "errors") with
            | Some e when e.ValueKind = JsonValueKind.Array ->
                [ for x in e.EnumerateArray() do
                      match x.ValueKind with
                      | JsonValueKind.String ->
                          match str x with
                          | Some m -> yield m
                          | None -> ()
                      | JsonValueKind.Object ->
                          match prop "message" x |> Option.bind str with
                          | Some m -> yield m
                          | None -> ()
                      | _ -> () ]
            | _ -> []
        (if main = "" then [] else [ main ]) @ details

    /// GraphQL answers come with HTTP 200; their errors carry the meaning.
    let private classifyGraphQl operation (expectedHead: CommitOid) (answer: ProviderAnswer) (parsed: JsonElement option) (errors: GraphQlError list) =
        match errors |> List.tryPick (staleHeadOf expectedHead) with
        | Some stale -> Error stale
        | None ->
            if errors |> List.exists (fun e -> e.Type = Some "FORBIDDEN") then
                Error(PermissionDenied(operationName operation))
            elif errors |> List.exists (fun e -> e.Type = Some "UNPROCESSABLE" || e.Type = Some "INVALID" || e.Type = Some "ARGUMENT_ERROR" || (e.Type.IsNone && e.InputProblems)) then
                Error(ValidationFailed(errors |> List.map _.Message))
            elif errors |> List.exists (fun e -> e.Type = Some "NOT_FOUND") then
                Error(NotFound(whatOf operation))
            elif not errors.IsEmpty then
                // GH-UNKNOWN-OUTCOME: an error type the classifier does not recognise.
                Error(Unrecognised answer.Status)
            else
                let oid =
                    parsed
                    |> Option.bind (prop "data")
                    |> Option.bind (prop "createCommitOnBranch")
                    |> Option.bind (prop "commit")
                    |> Option.bind (prop "oid")
                    |> Option.bind str
                match oid with
                | Some o when Regex.IsMatch(o, "^" + oidPattern + "$") -> Ok answer
                | _ -> Error(Malformed "createCommitOnBranch answered without a commit oid")

    /// Classifies one outcome of a request performing `operation`. Ok only for an answer that decoded and means
    /// success; every other outcome is exactly one ProviderError (IF41-ERRORS).
    let answer (operation: GitHubOperation) : Classifier =
        fun outcome ->
            match outcome with
            | NotAnswered(_, failure) -> Error(NetworkUnavailable failure)
            | Answered(id, status, headers, body) ->
                let parsed = tryParse body
                let errors = graphQlErrors parsed
                let result = { Request = id; Status = status; Body = body }
                if status = 401 then
                    Error AuthenticationFailed
                else
                    match rateLimit status headers parsed errors with
                    | Some limited -> Error limited
                    | None ->
                        match operation, status with
                        | CommitOnBranch expected, s when s >= 200 && s < 300 ->
                            if parsed.IsNone then Error(Malformed "the mutation answer is not JSON")
                            else classifyGraphQl operation expected result parsed errors
                        | ReadFileAt _, 403 when isLargeFile status body -> Ok result
                        | _, 403 -> Error(PermissionDenied(operationName operation))
                        | ConnectRepository, 404 -> Error(PermissionDenied(operationName operation))
                        | CompareFrom expected, 404 -> Error(HistoryRewritten expected)
                        | _, 404 -> Error(NotFound(whatOf operation))
                        | _, 422 -> Error(ValidationFailed(validationMessages parsed))
                        | _, 409 -> Error(NotFound(whatOf operation))
                        | _, s when s >= 200 && s < 300 ->
                            match parsed with
                            | None -> Error(Malformed(sprintf "the answer to %s is not JSON" (operationName operation)))
                            | Some root ->
                                match operation with
                                | CompareFrom expected ->
                                    match prop "status" root |> Option.bind str with
                                    | Some("diverged" | "behind") -> Error(HistoryRewritten expected)
                                    | Some("ahead" | "identical") -> Ok result
                                    | _ -> Error(Malformed "the compare answer has no status")
                                | ReadBranchHead ->
                                    match prop "object" root |> Option.bind (prop "sha") |> Option.bind str with
                                    | Some _ -> Ok result
                                    | None -> Error(Malformed "the ref answer has no object sha")
                                | ReadFileAt _ when root.ValueKind = JsonValueKind.Object ->
                                    match prop "content" root, prop "encoding" root |> Option.bind str with
                                    | _, Some "none" -> Ok result
                                    | Some _, _ -> Ok result
                                    | None, _ -> Error(Malformed "the file answer has no content")
                                | _ -> Ok result
                        | _ -> Error(Unrecognised status)

    /// The new commit of a classified mutation answer.
    let committedOid (answer: ProviderAnswer) : CommitOid option =
        tryParse answer.Body
        |> Option.bind (prop "data")
        |> Option.bind (prop "createCommitOnBranch")
        |> Option.bind (prop "commit")
        |> Option.bind (prop "oid")
        |> Option.bind str
        |> Option.map CommitOid

    /// The head commit of a classified branch-head answer.
    let headOid (answer: ProviderAnswer) : CommitOid option =
        tryParse answer.Body
        |> Option.bind (prop "object")
        |> Option.bind (prop "sha")
        |> Option.bind str
        |> Option.map CommitOid

    /// The decoded bytes of a classified file answer.
    let fileBytes (answer: ProviderAnswer) : byte[] option =
        tryParse answer.Body
        |> Option.bind (prop "content")
        |> Option.bind str
        |> Option.bind GitHubRequests.decodeBase64

    /// The Connect request and its interpretation (EXT-DATA-005): write access only from `permissions.push`.
    let connector: Connector =
        { ConnectRequest = GitHubRequests.connect
          InterpretConnect =
            fun expected outcome ->
                let id =
                    match outcome with
                    | Answered(id, _, _, _) -> id
                    | NotAnswered(id, _) -> id
                if id <> expected then
                    ConnectSuperseded
                else
                    match answer ConnectRepository outcome with
                    | Error e -> ConnectRejected e
                    | Ok a ->
                        let push =
                            tryParse a.Body
                            |> Option.bind (prop "permissions")
                            |> Option.bind (prop "push")
                            |> Option.map (fun p -> p.ValueKind = JsonValueKind.True)
                            |> Option.defaultValue false
                        ConnectAccepted push }

    /// A user-facing sentence for each error (github-error-matrix.json userVisible); never names a credential.
    let describe (error: ProviderError) : string =
        match error with
        | AuthenticationFailed -> "GitHub refused the credential; your changes are kept in this tab."
        | PermissionDenied operation -> sprintf "This credential cannot %s; your changes are kept in this tab." operation
        | RateLimited(Primary reset) ->
            let at =
                reset
                |> Option.map (fun r -> DateTimeOffset.FromUnixTimeSeconds(r).ToString("u"))
                |> Option.defaultValue "the reset time"
            sprintf "GitHub's rate limit was reached; try again after %s. Your changes are kept in this tab." at
        | RateLimited(Secondary retry) ->
            let wait = retry |> Option.map (sprintf "%d seconds") |> Option.defaultValue "a minute"
            sprintf "GitHub asked us to slow down; try again in %s. Your changes are kept in this tab." wait
        | StaleHead _ -> "The branch moved on GitHub since this investigation was read; nothing was written."
        | ValidationFailed messages ->
            sprintf "GitHub refused the change as invalid: %s. Your changes are kept in this tab." (String.Join("; ", messages))
        | NotFound what -> sprintf "Not found in the repository: %s." what
        | HistoryRewritten _ -> "The branch history on GitHub was rewritten; nothing is reported as saved. Your changes are kept in this tab."
        | NetworkUnavailable FailedBeforeSending -> "Your changes exist only in this tab and cannot be saved until the connection returns."
        | NetworkUnavailable _ -> "The save may or may not have reached GitHub; checking."
        | Malformed _ -> "GitHub's answer could not be read; nothing is reported as saved."
        | ReadBackMismatch _ -> "GitHub holds different bytes than this save wrote; nothing is reported as saved. Your changes are kept in this tab."
        | Unrecognised status -> sprintf "GitHub gave an unrecognised answer (HTTP %d); the outcome is being checked." status

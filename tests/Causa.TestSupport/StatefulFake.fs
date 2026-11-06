// Owner: persistence.
namespace Causa.TestSupport

open System
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Causa.Persistence

/// What a credential may do in the fake: GitHub states read or write per repository.
type FakeAccess =
    | ReadOnly
    | ReadWrite

type FakeCommit =
    { Oid: string
      Parent: string option
      Tree: Map<string, byte[]>
      Message: string
      /// Who made it: "app" for commits made through requests, or the simulated other writer's name.
      Author: string }

/// A fault the fake applies to the next request it matches (PERSIST-025-O2: lose an answer before or after applying).
type FakeFault =
    /// The mutation is not applied and no answer comes back (the request was sent; the connection dropped first).
    | LoseAnswerBeforeApplying
    /// The mutation is applied, then the answer is lost.
    | LoseAnswerAfterApplying
    /// The request never leaves the browser.
    | FailBeforeSending
    /// The request times out before it is applied.
    | TimeOutBeforeApplying
    /// GitHub answers this recorded form instead of processing the request (rate limits, 5xx...). Nothing is applied.
    | AnswerWith of status: int * headers: (string * string) list * body: string
    /// A stale expected head is refused in the FORBIDDEN moved-head form (KD-022) instead of STALE_DATA.
    | MovedHeadRace

type FaultTarget =
    | NextMutation
    | NextRequest

/// One request the fake received, with the Authorization value redacted.
type FakeLogEntry =
    { Method: HttpMethod
      Url: string
      Headers: (string * string) list
      Body: string option
      Operation: GitHubOperation option }

type FakeState =
    { Owner: string
      Name: string
      Commits: Map<string, FakeCommit>
      Branches: Map<string, string>
      Tokens: Map<string, FakeAccess>
      Faults: (FaultTarget * FakeFault) list
      Log: FakeLogEntry list
      Counter: int64
      /// GraphQL Blob.text is truncated above this many bytes (GitHub truncates large blobs), so a reader must fall back
      /// to the Git blobs API.
      TruncateTextAbove: int }

/// A stateful fake of the GitHub API the application uses: REST repository, refs, contents, commits and compare, and
/// GraphQL createCommitOnBranch. It keeps real commit ancestry, enforces expectedHeadOid, accepts commits from simulated
/// other writers and can lose an answer before or after applying a mutation (PERSIST-025-O2). Its answers are the
/// recorded GitHub forms of Recordings (PF-013, P-03), so classification of a fake answer is the classification of the
/// real one; the live suite checks that the two agree.
[<RequireQualifiedAccess>]
module StatefulFake =

    let private sha1Hex (text: string) =
        SHA1.HashData(Encoding.UTF8.GetBytes text) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

    /// Git's blob id of some bytes.
    let blobSha (bytes: byte[]) =
        let header = Encoding.ASCII.GetBytes(sprintf "blob %d\000" bytes.Length)
        SHA1.HashData(Array.append header bytes) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

    let private treeDigest (tree: Map<string, byte[]>) =
        tree |> Map.toList |> List.map (fun (p, b) -> p + ":" + blobSha b) |> String.concat "\n"

    /// A repository with one branch whose root commit holds `files`.
    let initial (owner: string) (name: string) (branch: string) (files: (string * byte[]) list) (tokens: (string * FakeAccess) list) : FakeState =
        let tree = Map.ofList files
        let oid = sha1Hex ("root\n" + treeDigest tree)
        { Owner = owner
          Name = name
          Commits = Map.ofList [ oid, { Oid = oid; Parent = None; Tree = tree; Message = "Initial commit"; Author = "setup" } ]
          Branches = Map.ofList [ branch, oid ]
          Tokens = Map.ofList tokens
          Faults = []
          Log = []
          Counter = 0L
          TruncateTextAbove = 1048576 }

    let private newCommit (state: FakeState) (parent: string) (tree: Map<string, byte[]>) (message: string) (author: string) =
        let counter = state.Counter + 1L
        let oid = sha1Hex (sprintf "%s\n%s\n%s\n%d" parent (treeDigest tree) message counter)
        let commit = { Oid = oid; Parent = Some parent; Tree = tree; Message = message; Author = author }
        { state with Commits = state.Commits.Add(oid, commit); Counter = counter }, oid

    /// A commit by a simulated other writer (another tab, device or person): it advances the branch directly.
    let otherWriterCommits (branch: string) (writer: string) (additions: (string * byte[]) list) (deletions: string list) (state: FakeState) : FakeState * string =
        let head = state.Branches[branch]
        let tree =
            additions |> List.fold (fun (t: Map<string, byte[]>) (p, b) -> t.Add(p, b)) state.Commits[head].Tree
            |> fun t -> deletions |> List.fold (fun (t: Map<string, byte[]>) p -> t.Remove p) t
        let state, oid = newCommit state head tree (sprintf "Other writer %s" writer) writer
        { state with Branches = state.Branches.Add(branch, oid) }, oid

    /// Rewrites the branch to a commit that does not descend from the old head (a force-push by someone else).
    let rewriteHistory (branch: string) (state: FakeState) : FakeState * string =
        let head = state.Commits[state.Branches[branch]]
        let counter = state.Counter + 1L
        let oid = sha1Hex (sprintf "rewritten\n%s\n%d" (treeDigest head.Tree) counter)
        let commit = { Oid = oid; Parent = None; Tree = head.Tree; Message = "Rewritten"; Author = "rewriter" }
        { state with Commits = state.Commits.Add(oid, commit); Branches = state.Branches.Add(branch, oid); Counter = counter }, oid

    let injectFault (target: FaultTarget) (fault: FakeFault) (state: FakeState) : FakeState =
        { state with Faults = state.Faults @ [ target, fault ] }

    /// The commits from `oid` back to the root, newest first.
    let ancestry (state: FakeState) (oid: string) : string list =
        let rec walk acc (o: string option) =
            match o |> Option.bind (fun x -> state.Commits.TryFind x) with
            | Some c -> walk (c.Oid :: acc) c.Parent
            | None -> List.rev acc
        walk [] (Some oid)

    let isAncestor (state: FakeState) (ancestor: string) (descendant: string) =
        ancestry state descendant |> List.contains ancestor

    let private headers (state: FakeState) =
        Recordings.standardHeaders (int (max 1L (5000L - state.Counter - int64 state.Log.Length)))

    let private credentialOf (request: HttpRequest) =
        request.Headers
        |> List.tryPick (fun (k, v) ->
            if String.Equals(k, "Authorization", StringComparison.OrdinalIgnoreCase) && v.StartsWith("Bearer ", StringComparison.Ordinal) then
                Some(v.Substring 7)
            else
                None)

    let private redact (request: HttpRequest) : FakeLogEntry =
        { Method = request.Method
          Url = request.Url
          Headers =
            request.Headers
            |> List.map (fun (k, v) -> if String.Equals(k, "Authorization", StringComparison.OrdinalIgnoreCase) then k, "<redacted>" else k, v)
          Body = request.Body
          Operation = GitHubRequests.operationOf request }

    let private queryValue (name: string) (uri: Uri) =
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryPick (fun kv ->
            match kv.Split('=', 2) with
            | [| k; v |] when k = name -> Some(Uri.UnescapeDataString v)
            | _ -> None)

    let private resolve (state: FakeState) (refOrOid: string) =
        match state.Branches.TryFind refOrOid with
        | Some oid -> Some oid
        | None -> if state.Commits.ContainsKey refOrOid then Some refOrOid else None

    let private isOid (s: string) = s.Length = 40 && s |> Seq.forall Uri.IsHexDigit

    type private Reply =
        | Reply of status: int * body: string
        | Lost of TransportFailure

    let private getString (e: JsonElement) (name: string) =
        match e.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.String ->
            match v.GetString() with
            | null -> None
            | s -> Some s
        | _ -> None

    let private mutate (state: FakeState) (body: string) (race: bool) : FakeState * Reply =
        let parsed =
            try
                use doc = JsonDocument.Parse body
                Some(doc.RootElement.Clone())
            with _ -> None
        match parsed with
        | None -> state, Reply(400, """{"message":"Problems parsing JSON","documentation_url":"https://docs.github.com/graphql"}""")
        | Some root ->
            let input = root.GetProperty("variables").GetProperty("input")
            let branch = input.GetProperty("branch")
            let nameWithOwner = getString branch "repositoryNameWithOwner" |> Option.defaultValue ""
            let branchName = getString branch "branchName" |> Option.defaultValue ""
            let expected = getString input "expectedHeadOid" |> Option.defaultValue ""
            if not (isOid expected) then
                state, Reply(200, Recordings.graphQlInputInvalid.Replace("not-an-oid", expected))
            elif nameWithOwner <> state.Owner + "/" + state.Name then
                state, Reply(200, Recordings.graphQlNotFound branchName)
            else
                match state.Branches.TryFind branchName with
                | None -> state, Reply(200, Recordings.graphQlNotFound branchName)
                | Some head when head <> expected ->
                    if race then
                        state, Reply(200, Recordings.movedHeadForbidden branchName head expected)
                    else
                        state, Reply(200, Recordings.staleData expected)
                | Some head ->
                    let message = input.GetProperty("message")
                    let text =
                        let h = getString message "headline" |> Option.defaultValue ""
                        match getString message "body" with
                        | Some b when b <> "" -> h + "\n\n" + b
                        | _ -> h
                    let changes = input.GetProperty("fileChanges")
                    let list (name: string) =
                        match changes.TryGetProperty name with
                        | true, v when v.ValueKind = JsonValueKind.Array -> [ for x in v.EnumerateArray() -> x ]
                        | _ -> []
                    let additions =
                        list "additions"
                        |> List.map (fun a -> getString a "path" |> Option.defaultValue "", getString a "contents" |> Option.bind GitHubRequests.decodeBase64)
                    if additions |> List.exists (fun (p, c) -> p = "" || c.IsNone) then
                        state, Reply(200, """{"data":{"createCommitOnBranch":null},"errors":[{"type":"UNPROCESSABLE","path":["createCommitOnBranch"],"message":"A path or its contents is invalid."}]}""")
                    else
                        let deletions = list "deletions" |> List.map (fun d -> getString d "path" |> Option.defaultValue "")
                        let tree =
                            additions |> List.fold (fun (t: Map<string, byte[]>) (p, c) -> t.Add(p, c.Value)) state.Commits[head].Tree
                            |> fun t -> deletions |> List.fold (fun (t: Map<string, byte[]>) p -> t.Remove p) t
                        let state, oid = newCommit state head tree text "app"
                        { state with Branches = state.Branches.Add(branchName, oid) }, Reply(200, Recordings.commitCreated oid)

    /// The GraphQL read of one investigation's three files at a commit or at the branch head (GitHubRequests.investigationFiles).
    let private filesQuery (state: FakeState) (body: string) : FakeState * Reply =
        use doc = JsonDocument.Parse body
        let v = doc.RootElement.GetProperty("variables")
        let get (n: string) = getString v n |> Option.defaultValue ""
        let rev = get "rev"
        let target =
            if rev.StartsWith "refs/heads/" then state.Branches.TryFind(rev.Substring 11)
            elif state.Commits.ContainsKey rev then Some rev
            else None
        match target, get "owner" = state.Owner && get "name" = state.Name with
        | _, false -> state, Reply(200, """{"data":{"repository":null},"errors":[{"type":"NOT_FOUND","path":["repository"],"message":"Could not resolve to a Repository."}]}""")
        | None, true -> state, Reply(200, """{"data":{"repository":{"object":null}}}""")
        | Some oid, true ->
            let tree = state.Commits[oid].Tree
            let entry (path: string) =
                match tree.TryFind path with
                | Some bytes when bytes.Length > state.TruncateTextAbove ->
                    sprintf
                        """{"object":{"oid":"%s","text":%s,"isTruncated":true,"byteSize":%d}}"""
                        (blobSha bytes)
                        (JsonSerializer.Serialize(Encoding.UTF8.GetString(bytes, 0, state.TruncateTextAbove)))
                        bytes.Length
                | Some bytes ->
                    sprintf
                        """{"object":{"oid":"%s","text":%s,"isTruncated":false,"byteSize":%d}}"""
                        (blobSha bytes)
                        (JsonSerializer.Serialize(Encoding.UTF8.GetString bytes))
                        bytes.Length
                | None -> "null"
            state,
            Reply(
                200,
                sprintf
                    """{"data":{"repository":{"object":{"oid":"%s","committedDate":"2026-11-06T17:00:00Z","m":%s,"e":%s,"s":%s}}}}"""
                    oid
                    (entry (get "m"))
                    (entry (get "e"))
                    (entry (get "s"))
            )

    let private read (state: FakeState) (request: HttpRequest) (access: FakeAccess) : FakeState * Reply =
        let uri = Uri request.Url
        let segments = uri.AbsolutePath.Trim('/').Split('/') |> Array.map Uri.UnescapeDataString |> List.ofArray
        match request.Method, segments with
        | Post, [ "graphql" ] when (request.Body |> Option.defaultValue "").Contains "\"rev\"" -> filesQuery state (request.Body |> Option.defaultValue "{}")
        | Post, [ "graphql" ] ->
            if access <> ReadWrite then state, Reply(200, Recordings.graphQlForbidden)
            else mutate state (request.Body |> Option.defaultValue "") false
        | _, "repos" :: owner :: name :: _ when owner <> state.Owner || name <> state.Name -> state, Reply(404, Recordings.notFound)
        | Get, [ "repos"; _; _ ] -> state, Reply(200, Recordings.repositoryAnswer state.Owner state.Name (access = ReadWrite))
        | Get, "repos" :: _ :: _ :: "git" :: "ref" :: "heads" :: branch ->
            let b = String.Join("/", branch)
            match state.Branches.TryFind b with
            | Some oid -> state, Reply(200, Recordings.refAnswer b oid)
            | None -> state, Reply(404, Recordings.notFound)
        | Post, [ "repos"; _; _; "git"; "refs" ] ->
            if access <> ReadWrite then
                state, Reply(403, Recordings.resourceNotAccessible)
            else
                use doc = JsonDocument.Parse(request.Body |> Option.defaultValue "{}")
                let r = getString doc.RootElement "ref" |> Option.defaultValue ""
                let sha = getString doc.RootElement "sha" |> Option.defaultValue ""
                let b = if r.StartsWith "refs/heads/" then r.Substring 11 else r
                if state.Branches.ContainsKey b then state, Reply(422, Recordings.referenceExists)
                elif not (state.Commits.ContainsKey sha) then state, Reply(422, """{"message":"Object does not exist","status":"422"}""")
                else { state with Branches = state.Branches.Add(b, sha) }, Reply(201, Recordings.refAnswer b sha)
        | Get, "repos" :: _ :: _ :: "contents" :: path ->
            let p = String.Join("/", path)
            match queryValue "ref" uri |> Option.bind (resolve state) with
            | None -> state, Reply(404, """{"message":"No commit found for the ref","status":"404"}""")
            | Some oid ->
                let tree = state.Commits[oid].Tree
                match tree.TryFind p with
                | Some bytes -> state, Reply(200, Recordings.fileAnswer p (blobSha bytes) bytes)
                | None ->
                    let prefix = if p = "" then "" else p + "/"
                    let entries =
                        tree
                        |> Map.toList
                        |> List.filter (fun (k, _) -> k.StartsWith(prefix, StringComparison.Ordinal))
                        |> List.map (fun (k, _) ->
                            let rest = k.Substring(prefix.Length)
                            match rest.IndexOf '/' with
                            | -1 -> rest, "file"
                            | i -> rest.Substring(0, i), "dir")
                        |> List.distinct
                    if entries.IsEmpty then
                        state, Reply(404, Recordings.notFound)
                    else
                        let items =
                            entries
                            |> List.map (fun (n, t) -> sprintf """{"name":%s,"path":%s,"type":"%s"}""" (JsonSerializer.Serialize n) (JsonSerializer.Serialize(prefix + n)) t)
                        state, Reply(200, "[" + String.Join(",", items) + "]")
        | Get, [ "repos"; _; _; "git"; "blobs"; sha ] ->
            let found =
                state.Commits |> Map.toSeq |> Seq.collect (fun (_, c) -> c.Tree |> Map.toSeq |> Seq.map snd) |> Seq.tryFind (fun b -> blobSha b = sha)
            match found with
            | Some bytes ->
                state, Reply(200, sprintf """{"sha":"%s","node_id":"B","size":%d,"url":"u","content":"%s","encoding":"base64"}""" sha bytes.Length (Convert.ToBase64String bytes))
            | None -> state, Reply(404, Recordings.notFound)
        | Get, [ "repos"; _; _; "commits"; oid ] ->
            match resolve state oid with
            | Some o ->
                let c = state.Commits[o]
                let before = c.Parent |> Option.map (fun p -> state.Commits[p].Tree) |> Option.defaultValue Map.empty
                let changed =
                    Set.union (c.Tree |> Map.keys |> set) (before |> Map.keys |> set)
                    |> Set.filter (fun p -> c.Tree.TryFind p <> before.TryFind p)
                    |> Set.toList
                let files = changed |> List.map (fun p -> sprintf """{"filename":%s}""" (JsonSerializer.Serialize p)) |> String.concat ","
                let body = Recordings.commitAnswer c.Oid c.Message c.Parent
                state, Reply(200, body.Substring(0, body.Length - 1) + sprintf ""","files":[%s]}""" files)
            | None -> state, Reply(422, sprintf """{"message":"No commit found for SHA: %s","status":"422"}""" oid)
        | Get, "repos" :: _ :: _ :: "compare" :: range ->
            let r = String.Join("/", range)
            match r.Split("...") with
            | [| b; h |] ->
                match resolve state b, resolve state h with
                | Some baseOid, Some headOid ->
                    let perPage = queryValue "per_page" uri |> Option.map int |> Option.defaultValue 250
                    let page = queryValue "page" uri |> Option.map int |> Option.defaultValue 1
                    let headLine = ancestry state headOid
                    let baseLine = ancestry state baseOid
                    let commitsOf (oids: string list) =
                        oids |> List.map (fun o -> let c = state.Commits[o] in c.Oid, c.Message, c.Parent)
                    if baseOid = headOid then
                        state, Reply(200, Recordings.compareAnswer "identical" 0 0 [])
                    elif List.contains baseOid headLine then
                        let after = headLine |> List.takeWhile ((<>) baseOid) |> List.rev
                        let pageItems = after |> List.skip (min after.Length ((page - 1) * perPage)) |> List.truncate perPage
                        state, Reply(200, (Recordings.compareAnswer "ahead" after.Length 0 (commitsOf pageItems)))
                    elif List.contains headOid baseLine then
                        state, Reply(200, Recordings.compareAnswer "behind" 0 (baseLine |> List.takeWhile ((<>) headOid) |> List.length) [])
                    else
                        let common = headLine |> List.filter (fun o -> List.contains o baseLine)
                        let ahead = headLine |> List.takeWhile (fun o -> not (List.contains o common)) |> List.rev
                        state, Reply(200, Recordings.compareAnswer "diverged" ahead.Length 1 (commitsOf ahead))
                | _ -> state, Reply(404, Recordings.notFound)
            | _ -> state, Reply(404, Recordings.notFound)
        | _ -> state, Reply(404, Recordings.notFound)

    let private isMutation (request: HttpRequest) =
        match GitHubRequests.operationOf request with
        | Some(CommitOnBranch _)
        | Some CreateBranch -> true
        | Some(ReadInvestigationAt _) -> false
        | _ -> request.Method = Post

    let private isStaleMutation (state: FakeState) (request: HttpRequest) =
        match GitHubRequests.operationOf request, request.Body with
        | Some(CommitOnBranch(CommitOid expected)), Some body ->
            try
                use doc = JsonDocument.Parse body
                let branch = doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("branch")
                match getString branch "branchName" |> Option.bind state.Branches.TryFind with
                | Some head -> head <> expected
                | None -> false
            with _ -> false
        | _ -> false

    /// One request, as GitHub would process and answer it. Pure: the new state and the outcome.
    let step (state: FakeState) (request: HttpRequest) : FakeState * HttpOutcome =
      if (Uri request.Url).Host <> "api.github.com" then
        // Like the network: a host that does not resolve fails before anything is sent; nothing is logged or consumed.
        state, NotAnswered(request.Id, FailedBeforeSending)
      else
        let logged = { state with Log = state.Log @ [ redact request ] }
        let fault, rest =
            match logged.Faults with
            // The moved-head race form only applies to a mutation that is stale; it waits for one.
            | (_, MovedHeadRace) :: _ when not (isStaleMutation logged request) -> None, logged.Faults
            | (target, f) :: tail when target = NextRequest || (target = NextMutation && isMutation request) -> Some f, tail
            | faults -> None, faults
        let state = { logged with Faults = rest }
        let answer (s: FakeState) reply =
            match reply with
            | Reply(status, body) -> s, Answered(request.Id, status, headers s, body)
            | Lost failure -> s, NotAnswered(request.Id, failure)
        match fault with
        | Some FailBeforeSending -> { state with Log = logged.Log |> List.truncate (logged.Log.Length - 1) }, NotAnswered(request.Id, FailedBeforeSending)
        | Some LoseAnswerBeforeApplying -> state, NotAnswered(request.Id, LostAfterSending)
        | Some TimeOutBeforeApplying -> state, NotAnswered(request.Id, TimedOut)
        | Some(AnswerWith(status, h, body)) -> state, Answered(request.Id, status, h, body)
        | _ ->
            match credentialOf request |> Option.bind state.Tokens.TryFind with
            | None -> state, Answered(request.Id, 401, [ "content-type", "application/json; charset=utf-8" ], Recordings.badCredentials)
            | Some access ->
                let race = fault = Some MovedHeadRace
                let applied, reply =
                    match request.Method, race with
                    | Post, true when (Uri request.Url).AbsolutePath = "/graphql" ->
                        if access <> ReadWrite then state, Reply(200, Recordings.graphQlForbidden)
                        else mutate state (request.Body |> Option.defaultValue "") true
                    | _ -> read state request access
                match fault with
                | Some LoseAnswerAfterApplying -> applied, NotAnswered(request.Id, LostAfterSending)
                | _ -> answer applied reply

    /// A running fake: the only mutable cell, at the host boundary of the tests.
    type Running =
        { Execute: Execute
          Current: unit -> FakeState
          Update: (FakeState -> FakeState) -> unit }

    let start (initialState: FakeState) : Running =
        let cell = ref initialState
        let gate = obj ()
        { Execute =
            fun request ->
                lock gate (fun () ->
                    let next, outcome = step cell.Value request
                    cell.Value <- next
                    outcome)
          Current = fun () -> lock gate (fun () -> cell.Value)
          Update = fun f -> lock gate (fun () -> cell.Value <- f cell.Value) }

/// Which provider answered. A fake answer is never live evidence (PERSIST-025-O4, PF-005, AR-010-O2).
type ProviderMode =
    | FakeProvider
    | LiveProvider

/// The provider boundary the GitHub adapter sits behind (PERSIST-025-O1): IF41-WIRE's Execute plus the connection.
/// The same scenario code runs against the stateful fake and against the real test repository.
type GitHubProvider =
    { Mode: ProviderMode
      Execute: Execute
      Connection: ConnectionContext
      /// The secrets to redact from any evidence (the live credential); empty for the fake.
      Secrets: string list }

/// Provider-neutral operations built only from GitHubRequests and Classify, so a scenario exercises the adapter itself.
[<RequireQualifiedAccess>]
module Scenarios =
    let mutable private counter = 0L

    let nextId () = RequestId(Threading.Interlocked.Increment(&counter))

    let send (provider: GitHubProvider) (operation: GitHubOperation) (request: HttpRequest) =
        let outcome = provider.Execute request
        outcome, Classify.answer operation outcome

    let readHead (provider: GitHubProvider) : Result<CommitOid, ProviderError> =
        let _, result = send provider ReadBranchHead (GitHubRequests.branchHead (nextId ()) provider.Connection)
        result |> Result.bind (fun a -> Classify.headOid a |> Option.map Ok |> Option.defaultValue (Error(Malformed "no head")))

    /// A file's bytes at a commit, or None when the commit does not hold it.
    let readFile (provider: GitHubProvider) (commit: CommitOid) (path: string) : Result<byte[] option, ProviderError> =
        let operation = ReadFileAt(RepoPath path, commit)
        let _, result = send provider operation (GitHubRequests.fileAt (nextId ()) provider.Connection commit (RepoPath path))
        match result with
        | Ok a -> Ok(Classify.fileBytes a)
        | Error(NotFound _) -> Ok None
        | Error e -> Error e

    let commitRequest (expected: CommitOid) (files: (string * string) list) (marker: string) : CommitRequest =
        { ExpectedHead = expected
          Headline = "Causa provider conformance"
          Body = ""
          Marker = Some marker
          Additions = files |> List.map (fun (p, text) -> { Path = RepoPath p; Contents = GitHubRequests.utf8 text })
          Deletions = [] }

    /// One createCommitOnBranch: the raw outcome and its classification (Ok = the new commit).
    let commit (provider: GitHubProvider) (request: CommitRequest) : HttpOutcome * Result<CommitOid, ProviderError> =
        let operation = CommitOnBranch request.ExpectedHead
        let outcome, result = send provider operation (GitHubRequests.commitOnBranch (nextId ()) provider.Connection request)
        outcome, result |> Result.bind (fun a -> Classify.committedOid a |> Option.map Ok |> Option.defaultValue (Error(Malformed "no oid")))

    /// Whether `oid` is reachable from the branch head (compare oid...branch is ahead or identical).
    let reachable (provider: GitHubProvider) (CommitOid oid) : Result<bool, ProviderError> =
        let operation = CompareFrom(CommitOid oid)
        let _, result = send provider operation (GitHubRequests.compare (nextId ()) provider.Connection (CommitOid oid) 1)
        match result with
        | Ok _ -> Ok true
        | Error(HistoryRewritten _) -> Ok false
        | Error e -> Error e

    /// The commit message of a commit (to find a write marker).
    let commitMessage (provider: GitHubProvider) (oid: CommitOid) : Result<string, ProviderError> =
        let _, result = send provider (ReadCommit oid) (GitHubRequests.commit (nextId ()) provider.Connection oid)
        result
        |> Result.bind (fun a ->
            try
                use doc = JsonDocument.Parse a.Body
                match doc.RootElement.GetProperty("commit").GetProperty("message").GetString() with
                | null -> Error(Malformed "no message")
                | m -> Ok m
            with _ -> Error(Malformed "no message"))

    /// The case of a classification, for comparing the fake's with GitHub's (payloads such as oids differ).
    let kind (result: Result<'a, ProviderError>) : string =
        match result with
        | Ok _ -> "Ok"
        | Error e ->
            match e with
            | RateLimited(Primary _) -> "RateLimited.Primary"
            | RateLimited(Secondary _) -> "RateLimited.Secondary"
            | NetworkUnavailable f -> "NetworkUnavailable." + (sprintf "%A" f)
            | other -> (sprintf "%A" other).Split([| ' '; '\n'; '(' |])[0]

    /// The top-level JSON keys of a body (to check the fake's answer has GitHub's shape).
    let shape (body: string) : string list =
        try
            use doc = JsonDocument.Parse body
            if doc.RootElement.ValueKind = JsonValueKind.Object then
                [ for p in doc.RootElement.EnumerateObject() -> p.Name ] |> List.sort
            else
                [ string doc.RootElement.ValueKind ]
        with _ -> [ "<not json>" ]

    let errorTypes (outcome: HttpOutcome) : string list =
        match outcome with
        | Answered(_, _, _, body) ->
            try
                use doc = JsonDocument.Parse body
                match doc.RootElement.TryGetProperty "errors" with
                | true, errors when errors.ValueKind = JsonValueKind.Array ->
                    [ for e in errors.EnumerateArray() ->
                          match e.TryGetProperty "type" with
                          | true, t -> string (t.GetString())
                          | _ -> "<none>" ]
                | _ -> []
            with _ -> []
        | NotAnswered _ -> []

    /// The fake provider over a fresh repository with one token of each access.
    let fakeProvider (branch: string) (files: (string * byte[]) list) : GitHubProvider * StatefulFake.Running =
        let token = "fake-write-credential"
        let running =
            StatefulFake.start (StatefulFake.initial "causa-fake" "causa-acceptance" branch files [ token, ReadWrite; "fake-read-credential", ReadOnly ])
        { Mode = FakeProvider
          Execute = running.Execute
          Connection =
            { Repository = { Owner = "causa-fake"; Name = "causa-acceptance"; Branch = branch }
              Credential = Credential.create token }
          Secrets = [ token ] },
        running

/// The real GitHub test repository (PERSIST-025-O3). The credential and repository come only from the environment; a
/// missing credential is BLOCKED and fails the test, never a pass and never a fallback to the fake (PF-015, AR-010-O2).
[<RequireQualifiedAccess>]
module LiveGitHub =
    open System.Net.Http

    [<Literal>]
    let TokenVariable = "CAUSA_GITHUB_TOKEN"

    [<Literal>]
    let RepositoryVariable = "CAUSA_TEST_REPOSITORY"

    [<Literal>]
    let BranchVariable = "CAUSA_TEST_BRANCH"

    /// The designated test repository of the Rehearsal 010 toolchain lock (testRepository), used when the environment
    /// names none.
    let defaultRepository = "kemiller2002/causa-acceptance-r10"
    let defaultBranch = "causa-acceptance"

    let private env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null -> None
        | v when String.IsNullOrWhiteSpace v -> None
        | v -> Some(v.Trim())

    let blockedMessage =
        sprintf
            "BLOCKED: %s is not set, so the live GitHub suite cannot run against the test repository. This is a failure, not a pass: a live test never substitutes the fake (PF-015, PF-020, AR-010-O2, PERSIST-025-O4)."
            TokenVariable

    /// The live configuration from an environment lookup, or the BLOCKED reason. Only CAUSA_GITHUB_TOKEN is read:
    /// no other variable (GH_TOKEN, GITHUB_TOKEN, ...) is ever used as a fallback credential (AR-014-O1, PF-015).
    let configurationFrom (lookup: string -> string option) : Result<string * Repository, string> =
        match lookup TokenVariable with
        | None -> Error blockedMessage
        | Some token ->
            let full = lookup RepositoryVariable |> Option.defaultValue defaultRepository
            match full.Split('/') with
            | [| owner; name |] -> Ok(token, { Owner = owner; Name = name; Branch = lookup BranchVariable |> Option.defaultValue defaultBranch })
            | _ -> Error(sprintf "%s must be owner/repo" RepositoryVariable)

    /// The live configuration from the process environment, or the BLOCKED reason.
    let configuration () : Result<string * Repository, string> = configurationFrom env

    let private client = lazy (new HttpClient(Timeout = Threading.Timeout.InfiniteTimeSpan))

    let private methodOf (m: Causa.Persistence.HttpMethod) : Net.Http.HttpMethod =
        match m with
        | Causa.Persistence.Get -> Net.Http.HttpMethod.Get
        | Causa.Persistence.Post -> Net.Http.HttpMethod.Post
        | Causa.Persistence.Put -> Net.Http.HttpMethod.Put
        | Causa.Persistence.Patch -> Net.Http.HttpMethod.Patch

    /// The host's Execute over HttpClient, with a per-request timeout: exactly one attempt, never retried (IF41-WIRE).
    /// A failure to connect is FailedBeforeSending; a timeout is TimedOut; any other loss is LostAfterSending.
    let executeWithin (timeout: TimeSpan) : Execute =
        fun request ->
            use message = new HttpRequestMessage(methodOf request.Method, request.Url)
            for (k, v) in request.Headers do
                if not (String.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) then
                    message.Headers.TryAddWithoutValidation(k, v) |> ignore
            message.Headers.TryAddWithoutValidation("User-Agent", "causa-provider-conformance") |> ignore
            match request.Body with
            | Some body -> message.Content <- new StringContent(body, Encoding.UTF8, "application/json")
            | None -> ()
            use cts = new Threading.CancellationTokenSource(timeout)
            try
                use response = client.Value.SendAsync(message, cts.Token).GetAwaiter().GetResult()
                let body = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult()
                let headers =
                    [ for h in response.Headers do
                          for v in h.Value -> h.Key.ToLowerInvariant(), v
                      for h in response.Content.Headers do
                          for v in h.Value -> h.Key.ToLowerInvariant(), v ]
                Answered(request.Id, int response.StatusCode, headers, body)
            with
            | :? OperationCanceledException -> NotAnswered(request.Id, TimedOut)
            | :? HttpRequestException as e when (e.InnerException :? Net.Sockets.SocketException) -> NotAnswered(request.Id, FailedBeforeSending)
            | :? HttpRequestException as e when e.HttpRequestError = HttpRequestError.NameResolutionError || e.HttpRequestError = HttpRequestError.ConnectionError ->
                NotAnswered(request.Id, FailedBeforeSending)
            | _ -> NotAnswered(request.Id, LostAfterSending)

    let execute: Execute = executeWithin (TimeSpan.FromSeconds 30.0)

    /// The live provider on the designated branch, or the BLOCKED failure.
    let provider () : Result<GitHubProvider, string> =
        configuration ()
        |> Result.map (fun (token, repository) ->
            { Mode = LiveProvider
              Execute = execute
              Connection = { Repository = repository; Credential = Credential.create token }
              Secrets = [ token ] })

    /// Fails the calling test with the BLOCKED message when the live credential is absent.
    let require () : GitHubProvider =
        match provider () with
        | Ok p -> p
        | Error reason -> failwith reason

    /// A fresh branch of the test repository for one test (the suite's fixture: one fresh branch per test), created
    /// at the designated branch's head.
    let freshBranch (provider: GitHubProvider) (label: string) : GitHubProvider =
        match Scenarios.readHead provider with
        | Error e -> failwithf "live: cannot read the head of %s: %s" provider.Connection.Repository.Branch (Classify.describe e)
        | Ok head ->
            let name = sprintf "causa-live-%s-%s-%s" label (DateTime.UtcNow.ToString "yyyyMMddHHmmss") (Guid.NewGuid().ToString("N").Substring(0, 8))
            let request = GitHubRequests.createBranch (Scenarios.nextId ()) provider.Connection name head
            match Scenarios.send provider CreateBranch request with
            | _, Ok _ -> { provider with Connection = { provider.Connection with Repository = { provider.Connection.Repository with Branch = name } } }
            | _, Error e -> failwithf "live: cannot create the test branch %s: %s" name (Classify.describe e)

/// Evidence of a provider run: which mode answered and which real commits it created (PERSIST-025-O5). Written as one
/// JSON line per scenario to CAUSA_PROVIDER_EVIDENCE (or the runner's temp directory), every secret redacted.
[<RequireQualifiedAccess>]
module ProviderEvidence =
    let modeName (mode: ProviderMode) =
        match mode with
        | FakeProvider -> "fake"
        | LiveProvider -> "live"

    let path () =
        match Environment.GetEnvironmentVariable "CAUSA_PROVIDER_EVIDENCE" with
        | null
        | "" ->
            let dir =
                match Environment.GetEnvironmentVariable "RUNNER_TEMP" with
                | null
                | "" -> IO.Path.GetTempPath()
                | d -> d
            IO.Path.Combine(dir, "causa-provider-evidence.ndjson")
        | p -> p

    /// The evidence line for one scenario. Only a live provider may state `verified: live`.
    let line (provider: GitHubProvider) (test: string) (commits: CommitOid list) (facts: (string * string) list) : string =
        let repo = provider.Connection.Repository
        let o = Nodes.JsonObject()
        o["test"] <- Nodes.JsonValue.Create(test)
        o["mode"] <- Nodes.JsonValue.Create(modeName provider.Mode)
        o["verified"] <- Nodes.JsonValue.Create(match provider.Mode with LiveProvider -> "live" | FakeProvider -> "fake-only, not live verification")
        o["repository"] <- Nodes.JsonValue.Create(repo.Owner + "/" + repo.Name)
        o["branch"] <- Nodes.JsonValue.Create(repo.Branch)
        o["at"] <- Nodes.JsonValue.Create(DateTimeOffset.UtcNow.ToString "o")
        let arr = Nodes.JsonArray()
        for CommitOid c in commits do
            arr.Add(Nodes.JsonValue.Create(c))
        o["commits"] <- arr
        let f = Nodes.JsonObject()
        for (k, v) in facts do
            f[k] <- Nodes.JsonValue.Create(v)
        o["facts"] <- f
        Recordings.sanitize provider.Secrets (o.ToJsonString())

    let record (provider: GitHubProvider) (test: string) (commits: CommitOid list) (facts: (string * string) list) : string =
        let text = line provider test commits facts
        try
            IO.File.AppendAllText(path (), text + "\n")
        with _ -> ()
        text

/// The provider-conformance probes (PAT-03 and the PAT-02..08 core): each runs unchanged against the fake and against
/// the real test repository, so the two can be compared (PERSIST-008-O4, PERSIST-025-O5, PF-013).
[<RequireQualifiedAccess>]
module Probes =

    let ok (what: string) (result: Result<'a, ProviderError>) : 'a =
        match result with
        | Ok v -> v
        | Error e -> failwithf "%s: %A (%s)" what e (Classify.describe e)

    /// A fresh investigation directory of a working copy (causa-app- namespace), unique per call.
    let investigationPrefix (label: string) =
        sprintf "workspaces/demo/causa-app-%s/investigations/%s" label (Guid.NewGuid().ToString())

    let marker (label: string) = sprintf "%s.%d" (Guid.NewGuid().ToString("N")) (Environment.TickCount64 % 100000L) + "." + label

    /// Another writer's commit, made at the current head (another tab, device or person).
    let otherWriter (p: GitHubProvider) (path: string) (text: string) : CommitOid =
        let head = Scenarios.readHead p |> ok "other writer: head"
        Scenarios.commit p (Scenarios.commitRequest head [ path, text ] (marker "other")) |> snd |> ok "other writer: commit"

    type StaleProbe =
        { Base: CommitOid
          OtherHead: CommitOid
          Outcome: HttpOutcome
          Result: Result<CommitOid, ProviderError>
          HeadAfter: CommitOid
          MineAtHeadAfter: byte[] option
          Resent: CommitOid
          ResentBytes: byte[] option
          Change: CommitRequest }

    /// PAT-03: another writer advances the branch after the tab read it; the tab's commit at the old head is refused,
    /// nothing of it is written, and the same change (kept unsaved in memory) saves when sent at the new head.
    let staleHead (p: GitHubProvider) (prefix: string) : StaleProbe =
        let baseHead = Scenarios.readHead p |> ok "stale: base head"
        let other = otherWriter p (prefix + "-other/other.json") "{\"writer\":\"other\"}"
        let change = Scenarios.commitRequest baseHead [ prefix + "/mine.json", "{\"writer\":\"tab\"}" ] (marker "tab")
        let outcome, result = Scenarios.commit p change
        let headAfter = Scenarios.readHead p |> ok "stale: head after"
        let mine = Scenarios.readFile p headAfter (prefix + "/mine.json") |> ok "stale: read mine"
        let resentOutcome = Scenarios.commit p { change with ExpectedHead = headAfter } |> snd |> ok "stale: resend"
        let resentBytes = Scenarios.readFile p resentOutcome (prefix + "/mine.json") |> ok "stale: read back"
        { Base = baseHead
          OtherHead = other
          Outcome = outcome
          Result = result
          HeadAfter = headAfter
          MineAtHeadAfter = mine
          Resent = resentOutcome
          ResentBytes = resentBytes
          Change = change }

    /// A request with an invalid credential (GH-AUTH).
    let authFailure (p: GitHubProvider) : Result<CommitOid, ProviderError> =
        let bad = { p with Connection = { p.Connection with Credential = Credential.create "invalid-credential-for-conformance" } }
        Scenarios.readHead bad

    /// A validation refusal GitHub gives without side effects: creating a branch that already exists (REST 422).
    let validationFailure (p: GitHubProvider) : Result<ProviderAnswer, ProviderError> =
        let head = Scenarios.readHead p |> ok "validation: head"
        Scenarios.send p CreateBranch (GitHubRequests.createBranch (Scenarios.nextId ()) p.Connection p.Connection.Repository.Branch head) |> snd

    /// A file that does not exist at a commit (GH-NOT-FOUND).
    let notFound (p: GitHubProvider) (prefix: string) : Result<ProviderAnswer, ProviderError> =
        let head = Scenarios.readHead p |> ok "not found: head"
        let path = RepoPath(prefix + "/absent.json")
        Scenarios.send p (ReadFileAt(path, head)) (GitHubRequests.fileAt (Scenarios.nextId ()) p.Connection head path) |> snd

    /// The save mutation sent while the network cannot reach GitHub (the host does not resolve): GH-NETWORK-BEFORE.
    let networkBefore (p: GitHubProvider) (prefix: string) : HttpOutcome * Result<ProviderAnswer, ProviderError> * CommitOid * CommitOid =
        let head = Scenarios.readHead p |> ok "network: head"
        let change = Scenarios.commitRequest head [ prefix + "/offline.json", "{}" ] (marker "offline")
        let request = GitHubRequests.commitOnBranch (Scenarios.nextId ()) p.Connection change
        let unreachable = { request with Url = request.Url.Replace("https://api.github.com", "https://causa-unreachable.invalid") }
        let outcome = p.Execute unreachable
        let after = Scenarios.readHead p |> ok "network: head after"
        outcome, Classify.answer (CommitOnBranch head) outcome, head, after

    type LostProbe =
        { Expected: CommitOid
          Marker: string
          Outcome: HttpOutcome
          Classified: Result<ProviderAnswer, ProviderError>
          HeadAfter: CommitOid
          /// Reconciliation (PERSIST-015): whether the head commit carries this save's marker.
          MarkerAtHead: bool }

    /// A save whose answer is lost (the given executor loses it), then reconciled by reading the head and its marker.
    let lostAnswer (p: GitHubProvider) (losing: Execute) (prefix: string) : LostProbe =
        let head = Scenarios.readHead p |> ok "lost: head"
        let mark = marker "lost"
        let change = Scenarios.commitRequest head [ prefix + "/lost.json", "{\"lost\":true}" ] mark
        let outcome = losing (GitHubRequests.commitOnBranch (Scenarios.nextId ()) p.Connection change)
        let classified = Classify.answer (CommitOnBranch head) outcome
        // The answer may still be applying on the provider; reconciliation reads until the head settles.
        Threading.Thread.Sleep(if p.Mode = LiveProvider then 3000 else 0)
        let after = Scenarios.readHead p |> ok "lost: head after"
        let carries =
            after <> head
            && (Scenarios.commitMessage p after |> ok "lost: message" |> GitHubRequests.markerOf) = Some mark
        { Expected = head
          Marker = mark
          Outcome = outcome
          Classified = classified
          HeadAfter = after
          MarkerAtHead = carries }

/// Fixtures and a driver for the save machines: a real investigation history encoded and verified through the
/// causa-storage codec, a stateful fake holding it in a working copy, and a loop that executes the machines' requests
/// against the fake exactly as the App engine does with Limen (send, interpret, step).
[<RequireQualifiedAccess>]
module SaveKit =
    open Causa.Domain

    let guid (n: int) = Guid(sprintf "00000000-0000-4000-8000-%012d" n)

    let text (s: string) =
        match NonEmptyText.create s with
        | Ok t -> t
        | Error e -> failwith e

    let now = DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)
    let investigationId = InvestigationId(guid 1)
    let actor: Actor = { ActorId = text "causa-web"; Kind = ActorKind.Human }

    let envelope (version: int64) (correlation: Guid) (event: CoreEvent) : EventEnvelope =
        { EventId = EventId(Canonical.eventId (guid 1) version correlation)
          Investigation = investigationId
          Version = AggregateVersion version
          Provenance =
            { OccurredAt = now.AddSeconds(float version)
              Actor = actor
              CorrelationId = correlation
              FormatVersion = Events.formatVersion
              SemanticVersion = Events.semanticVersion }
          Payload = CoreEvent event }

    let started: InvestigationStarted =
        { Title = text "Payment failures"; ObservationId = ObservationId(guid 2); Original = text "Checkout fails" }

    let history = [ envelope 1L (guid 500) (InvestigationStarted started) ]

    let observation (n: int) (words: string) =
        ObservationRecorded { ObservationId = ObservationId(guid (3000 + n)); Original = text words; ObservedAt = None }

    /// An unsaved command: one more observation.
    let edit (n: int) : CommandEnvelope =
        { CommandId = guid (2000 + n)
          Investigation = investigationId
          ExpectedVersion = None
          Command = Core(RecordObservation { ObservationId = ObservationId(guid (3000 + n)); Original = text $"edit {n}"; ObservedAt = None }) }

    let startCommand (id: InvestigationId) : CommandEnvelope =
        let (InvestigationId g) = id
        { CommandId = Guid.NewGuid()
          Investigation = id
          ExpectedVersion = None
          Command = Core(StartInvestigation { Title = text "New investigation"; ObservationId = ObservationId(Guid.NewGuid()); Original = text (sprintf "Problem %O" g) }) }

    let run = "kit"

    let writable =
        WorkspaceAddress.workingCopy [] run |> Option.bind Writable.ofAddress |> Option.defaultWith (fun () -> failwith "working copy refused")

    let address = Writable.address writable
    let workspace = address.Workspace

    let dir (InvestigationId g) =
        let (Workspace w) = workspace
        sprintf "%s/investigations/%s" w (g.ToString("D"))

    let filesOf (h: EventEnvelope list) : FileSet =
        match FileSets.ofHistory h with
        | Ok fs -> fs
        | Error e -> failwithf "%A" e

    /// A version of the investigation at `commit`, verified exactly as a load verifies it.
    let verified (commit: CommitOid) (h: EventEnvelope list) : VerifiedInvestigation =
        match StorageReader.verify Kernel.domain (Some investigationId) address commit (filesOf h) with
        | Ok v -> v
        | Error e -> failwithf "fixture does not verify: %A" e

    let repoFiles (id: InvestigationId) (fs: FileSet) =
        let d = dir id
        [ d + "/manifest.json", fs.Manifest; d + "/events.ndjson", fs.Events; d + "/snapshot.json", fs.Snapshot ]

    /// A fake repository whose branch head holds the fixture investigation, and its verified version at that head.
    let repository () =
        let running =
            StatefulFake.start (
                StatefulFake.initial "owner" "repo" "main" (("README.md", "x"B) :: repoFiles investigationId (filesOf history)) [ "kit-token", ReadWrite ]
            )
        let head = CommitOid((running.Current()).Branches["main"])
        running, verified head history

    let connection: ConnectionContext =
        { Repository = { Owner = "owner"; Name = "repo"; Branch = "main" }
          Credential = Credential.create "kit-token" }

    /// Request contexts with increasing identities (the engine's NextId).
    type Clock =
        { mutable Next: int64
          mutable Now: DateTimeOffset
          mutable ElsewhereInFlight: bool
          mutable Connection: ConnectionContext }

    let clock () = { Next = 100L; Now = now; ElsewhereInFlight = false; Connection = connection }

    let context (c: Clock) : RequestContext =
        { Connection = c.Connection; NextId = RequestId c.Next; MutationInFlightElsewhere = c.ElsewhereInFlight; Now = c.Now }

    let advance (c: Clock) (effects: SaveEffect list) =
        if effects |> List.exists (function Send _ -> true | _ -> false) then c.Next <- c.Next + 1L

    let requests (effects: SaveEffect list) = effects |> List.choose (function Send r -> Some r | _ -> None)

    let isMutation (r: HttpRequest) = GitHubRequests.operationOf r |> Option.exists (function CommitOnBranch _ -> true | _ -> false)

    let stepOk (m: SaveMachine) (c: Clock) (s: InvestigationSession) (input: SaveInput) =
        match m.Step (context c) s input with
        | Ok(next, effects) ->
            advance c effects
            next, effects
        | Error f -> failwithf "%s refused: %s" f.Input f.Reason

    /// Executes the session's outstanding request and every follow-up request through `execute` until the session
    /// awaits nothing; returns the final session, every event stepped, every request sent and every other effect.
    let driveAll (m: SaveMachine) (c: Clock) (execute: Execute) (session: InvestigationSession) (first: HttpRequest list) =
        let rec loop (s: InvestigationSession) (pending: HttpRequest list) events sent (others: SaveEffect list) (budget: int) =
            match pending with
            | [] -> s, List.rev events, List.rev sent, others
            | _ when budget = 0 -> failwith "the save did not settle"
            | r :: rest ->
                let outcome = execute r
                match m.Interpret (context c) s outcome with
                | Interpretation.NextRequest(next, request) ->
                    c.Next <- c.Next + 1L
                    loop next (rest @ [ request ]) events (r :: sent) others (budget - 1)
                | MachineEvent input ->
                    match m.Step (context c) s input with
                    | Ok(next, effects) ->
                        advance c effects
                        let more =
                            match next.State with
                            | SaveState.Conflict _ -> []
                            | _ -> requests effects
                        let rest' = effects |> List.filter (function Send _ -> false | _ -> true)
                        loop next (rest @ more) (input :: events) (r :: sent) (others @ rest') (budget - 1)
                    | Error f -> failwithf "%s refused in %s: %s" f.Input f.State f.Reason
        loop session first [] [] [] 40

    let drive (m: SaveMachine) (c: Clock) (execute: Execute) (session: InvestigationSession) (first: HttpRequest list) =
        let s, events, sent, _ = driveAll m c execute session first
        s, events, sent

/// The SM-SAVE / SM-CONFLICT / SM-LOAD transition-test fixture (state-machine-contracts.json).
module MachineKit =
    open Causa.Domain

    let m () : SaveMachine = saveMachine
    let baseHead = CommitOid "c0"
    let baseVerified = SaveKit.verified baseHead SaveKit.history
    let remoteHistory = SaveKit.history @ [ SaveKit.envelope 2L (SaveKit.guid 501) (SaveKit.observation 99 "other writer") ]
    let remoteVerified = SaveKit.verified (CommitOid "c2") remoteHistory
    let outstanding = RequestId 7L

    let ctxWith (next: int64) (elsewhere: bool) (at: DateTimeOffset) : RequestContext =
        { Connection = SaveKit.connection; NextId = RequestId next; MutationInFlightElsewhere = elsewhere; Now = at }

    let ctx = ctxWith 40L false SaveKit.now

    let confirmation (commit: string) =
        let fs = SaveKit.filesOf SaveKit.history
        match Confirmation.ofReadBack SaveKit.investigationId (CommitOid commit) SaveKit.now fs fs with
        | Some c -> c
        | None -> failwith "equal bytes confirm"

    let saved () = (m ()).Initial SaveKit.writable baseVerified
    let unsaved () = { saved () with State = SaveState.Unsaved; Unsaved = [ SaveKit.edit 1 ] }
    let saving (awaiting: Awaiting) = { unsaved () with State = SaveState.Saving; Outstanding = Some(outstanding, awaiting); Mutations = 1 }
    let offline () = { unsaved () with State = SaveState.Offline }
    let conflict (view: ConflictView) = { unsaved () with State = SaveState.Conflict view }
    let unknown (left: int) (reads: int) (awaiting: Awaiting option) =
        { unsaved () with
            State = SaveState.UnknownOutcome left
            ReconcileReads = reads
            Mutations = 1
            Outstanding = awaiting |> Option.map (fun a -> outstanding, a) }
    let failed () = { unsaved () with State = SaveState.SaveFailed RepositoryKeptChanging }

    let stateName (s: SaveState) =
        match s with
        | SaveState.Saved _ -> "Saved"
        | SaveState.Unsaved -> "Unsaved"
        | SaveState.Saving -> "Saving"
        | SaveState.Offline -> "Offline"
        | SaveState.Conflict _ -> "Conflict"
        | SaveState.UnknownOutcome _ -> "UnknownOutcome"
        | SaveState.SaveFailed _ -> "SaveFailed"

    let step (c: RequestContext) (s: InvestigationSession) (input: SaveInput) =
        match (m ()).Step c s input with
        | Ok r -> r
        | Error f -> failwithf "%s/%s refused: %s" f.State f.Input f.Reason

    let sends (effects: SaveEffect list) = effects |> List.choose (function Send r -> Some r | _ -> None)
    let mutations (effects: SaveEffect list) = sends effects |> List.filter SaveKit.isMutation
    let schedules (effects: SaveEffect list) = effects |> List.choose (function ScheduleReconcileRead(_, ms) -> Some ms | _ -> None)
    let work (s: InvestigationSession) = s.Unsaved @ s.AfterInFlight

/// End-to-end save scenarios: a fresh machine (its own tab memory), a fake repository holding the fixture
/// investigation, and an executor that records every request it sends.
[<RequireQualifiedAccess>]
module SaveScenario =
    type T =
        { Machine: SaveMachine
          Fake: StatefulFake.Running
          Clock: SaveKit.Clock
          mutable Session: InvestigationSession
          mutable Sent: HttpRequest list
          mutable Events: SaveInput list
          /// Runs before each request reaches the fake (simulated other writers, injected faults).
          mutable Before: HttpRequest -> unit }

    let create () : T =
        let machine = SaveMachines.create SaveMachines.support
        let running, verified = SaveKit.repository ()
        { Machine = machine
          Fake = running
          Clock = SaveKit.clock ()
          Session = machine.Initial SaveKit.writable verified
          Sent = []
          Events = []
          Before = ignore }

    let execute (sc: T) : Execute =
        fun r ->
            sc.Before r
            sc.Sent <- sc.Sent @ [ r ]
            sc.Fake.Execute r

    let input (sc: T) (i: SaveInput) =
        let next, effects = SaveKit.stepOk sc.Machine sc.Clock sc.Session i
        sc.Session <- next
        sc.Events <- sc.Events @ [ i ]
        let final, events, _, others = SaveKit.driveAll sc.Machine sc.Clock (execute sc) next (SaveKit.requests effects)
        sc.Session <- final
        sc.Events <- sc.Events @ events
        effects @ others

    let edit (sc: T) (n: int) = input sc (SaveInput.Edit(SaveKit.edit n)) |> ignore

    /// The timer of a ScheduleReconcileRead fired (the engine calls ReconcileDue); the read is executed and driven.
    let reconcileDue (sc: T) =
        match sc.Machine.ReconcileDue (SaveKit.context sc.Clock) sc.Session with
        | Ok(next, effects) ->
            SaveKit.advance sc.Clock effects
            let final, events, _, others = SaveKit.driveAll sc.Machine sc.Clock (execute sc) next (SaveKit.requests effects)
            sc.Session <- final
            sc.Events <- sc.Events @ events
            effects @ others
        | Error f -> failwithf "ReconcileDue refused: %s" f.Reason

    let head (sc: T) = CommitOid((sc.Fake.Current()).Branches["main"])

    let mutations (sc: T) = sc.Sent |> List.filter SaveKit.isMutation

    let reads (sc: T) = sc.Sent |> List.filter (SaveKit.isMutation >> not)

    let otherWriter (sc: T) (files: (string * byte[]) list) =
        sc.Fake.Update(fun s -> StatefulFake.otherWriterCommits "main" "other writer" files [] s |> fst)

    let stateName (sc: T) = MachineKit.stateName sc.Session.State

/// The same save scenarios against the real test repository (PERSIST-025-O3): a fresh branch, a new investigation in
/// a fresh working copy created and saved by the application itself, then edits, saves and reads through the real API.
[<RequireQualifiedAccess>]
module LiveRun =
    open Causa.Domain

    type T =
        { Machine: SaveMachine
          Provider: GitHubProvider
          Clock: SaveKit.Clock
          Id: InvestigationId
          Writable: Writable.T
          mutable Session: InvestigationSession
          mutable Sent: HttpRequest list
          mutable Events: SaveInput list
          mutable Before: HttpRequest -> unit }

    let private truncate (t: DateTimeOffset) = DateTimeOffset(t.UtcTicks - t.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero)

    let execute (run: T) : Execute =
        fun r ->
            run.Before r
            run.Sent <- run.Sent @ [ r ]
            run.Provider.Execute r

    let input (run: T) (i: SaveInput) =
        run.Clock.Now <- truncate DateTimeOffset.UtcNow
        let next, effects = SaveKit.stepOk run.Machine run.Clock run.Session i
        run.Session <- next
        run.Events <- run.Events @ [ i ]
        let final, events, _, others = SaveKit.driveAll run.Machine run.Clock (execute run) next (SaveKit.requests effects)
        run.Session <- final
        run.Events <- run.Events @ events
        effects @ others

    let edit (run: T) (words: string) =
        let command: CommandEnvelope =
            { CommandId = Guid.NewGuid()
              Investigation = run.Id
              ExpectedVersion = None
              Command = Core(RecordObservation { ObservationId = ObservationId(Guid.NewGuid()); Original = SaveKit.text words; ObservedAt = None }) }
        input run (SaveInput.Edit command) |> ignore
        command

    let head (run: T) = Scenarios.readHead run.Provider |> Probes.ok "head"

    let prefix (run: T) =
        let (Workspace w) = (Writable.address run.Writable).Workspace
        let (InvestigationId g) = run.Id
        sprintf "%s/investigations/%s" w (g.ToString("D"))

    /// A new tab (its own machine and tab memory) on the same branch and investigation, opened from GitHub.
    let openInNewTab (run: T) : Result<VerifiedInvestigation, LoadFailure> =
        let machine = SaveMachines.create SaveMachines.support
        let clock = SaveKit.clock ()
        clock.Connection <- run.Provider.Connection
        clock.Next <- 900_000L + int64 (Random.Shared.Next 1000)
        let r: InvestigationRef = { Address = Writable.address run.Writable; Id = run.Id }
        match machine.StepLoad (SaveKit.context clock) NotLoaded (LoadInput.OpenRequested r) with
        | Error f -> failwithf "%A" f
        | Ok(state, effects) ->
            let rec loop state (requests: HttpRequest list) =
                match requests with
                | [] -> failwith "the load sent nothing"
                | req :: _ ->
                    clock.Next <- clock.Next + 1L
                    match machine.InterpretLoad (SaveKit.context clock) state (run.Provider.Execute req) with
                    | LoadEvent(LoadInput.LoadVerified v) -> Ok v
                    | LoadEvent(LoadInput.LoadRejected f) -> Error f
                    | LoadNextRequest(next, req') -> loop next [ req' ]
                    | other -> failwithf "%A" other
            loop state (SaveKit.requests effects)

    /// A fresh branch, a fresh working copy and a new investigation the application creates and saves (C1).
    let createOn (provider: GitHubProvider) (label: string) : T =
        let machine = SaveMachines.create SaveMachines.support
        let clock = SaveKit.clock ()
        clock.Connection <- provider.Connection
        let runId = "live-" + Guid.NewGuid().ToString("N").Substring(0, 12)
        let writable = WorkspaceAddress.workingCopy [] runId |> Option.bind Writable.ofAddress |> Option.get
        let id = InvestigationId(Guid.NewGuid())
        let head = Scenarios.readHead provider |> Probes.ok "head"
        let start: CommandEnvelope =
            { CommandId = Guid.NewGuid()
              Investigation = id
              ExpectedVersion = None
              Command =
                Core(
                    StartInvestigation
                        { Title = SaveKit.text ("Live provider conformance " + label)
                          ObservationId = ObservationId(Guid.NewGuid())
                          Original = SaveKit.text "Synthetic investigation written by the live suite." }
                ) }
        let run =
            { Machine = machine
              Provider = provider
              Clock = clock
              Id = id
              Writable = writable
              Session = Persistence.createInvestigation writable head start true
              Sent = []
              Events = []
              Before = ignore }
        input run SaveInput.SaveRequested |> ignore
        match run.Session.State with
        | SaveState.Saved _ -> run
        | other -> failwithf "live: the new investigation did not save: %A (events %A)" other run.Events

    let create (label: string) : T = createOn (LiveGitHub.freshBranch (LiveGitHub.require ()) label) label

    let mutations (run: T) = run.Sent |> List.filter SaveKit.isMutation

    /// The commits after `from` on the branch (oldest first), with their messages.
    let commitsSince (run: T) (CommitOid from) : (string * string) list =
        let request = GitHubRequests.compare (Scenarios.nextId ()) run.Provider.Connection (CommitOid from) 1
        match Scenarios.send run.Provider (CompareFrom(CommitOid from)) request with
        | _, Ok answer ->
            use doc = JsonDocument.Parse answer.Body
            [ for c in doc.RootElement.GetProperty("commits").EnumerateArray() ->
                  string (c.GetProperty("sha").GetString()), string (c.GetProperty("commit").GetProperty("message").GetString()) ]
        | _, Error e -> failwithf "compare: %A" e

    /// The paths a commit changed (REST commit `files`).
    let changedPaths (run: T) (CommitOid oid) : string list =
        let request = GitHubRequests.commit (Scenarios.nextId ()) run.Provider.Connection (CommitOid oid)
        match Scenarios.send run.Provider (ReadCommit(CommitOid oid)) request with
        | _, Ok answer ->
            use doc = JsonDocument.Parse answer.Body
            match doc.RootElement.TryGetProperty "files" with
            | true, files -> [ for f in files.EnumerateArray() -> string (f.GetProperty("filename").GetString()) ]
            | _ -> []
        | _, Error e -> failwithf "commit: %A" e

    let record (run: T) (test: string) (commits: CommitOid list) =
        ProviderEvidence.record run.Provider test commits [ "investigation", (let (InvestigationId g) = run.Id in g.ToString()) ] |> ignore

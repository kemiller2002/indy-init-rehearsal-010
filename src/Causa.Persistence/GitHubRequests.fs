// Owner: persistence.
namespace Causa.Persistence

open System
open System.Text
open System.Text.Json.Nodes

/// What a request asks GitHub to do. The classifier needs it to read an answer: GitHub gives one status several
/// meanings (github-error-matrix.json), so a 404 during Connect, on a file read and on a compare mean different things.
type GitHubOperation =
    /// GET the repository and the permissions GitHub states for it (EXT-DATA-005).
    | ConnectRepository
    /// GET the branch head.
    | ReadBranchHead
    /// GET one file (or directory listing) at a named commit.
    | ReadFileAt of RepoPath * CommitOid
    /// GET one blob through the Git data API (GH-LARGE-FILE re-read).
    | ReadBlob of sha: string
    /// GET one commit.
    | ReadCommit of CommitOid
    /// GET the commits after the expected head (reconciliation, PERSIST-015).
    | CompareFrom of expected: CommitOid
    /// The one save mutation: GraphQL createCommitOnBranch conditional on the expected head (PERSIST-006).
    | CommitOnBranch of expectedHead: CommitOid
    /// POST a new ref (test-repository set-up only; the application never creates or moves refs).
    | CreateBranch
    /// One GraphQL read of a commit (the branch head, or a named commit) and the given files at exactly that commit:
    /// the files of one investigation are read atomically, with the commit they were read at.
    | ReadInvestigationAt of revision: string

/// One file a commit adds or replaces, with its exact bytes.
type FileAddition = { Path: RepoPath; Contents: byte[] }

/// The commit a save sends: one createCommitOnBranch with every file of the change (PERSIST-006).
type CommitRequest =
    { ExpectedHead: CommitOid
      Headline: string
      Body: string
      /// The write marker trailer value (PERSIST-014), written as `Causa-Write-Marker: <value>` in the message body.
      Marker: string option
      Additions: FileAddition list
      Deletions: RepoPath list }

/// Why a commit was refused before it was sent (PERSIST-006: only files under the investigation's own prefix).
type CommitRefusal =
    | PathOutsidePrefix of RepoPath
    | EmptyCommit
    | UnsafePath of RepoPath

/// GitHub request construction. Requests are data (IF41-WIRE): the App executes them through the Limen Http effect and
/// hands the raw HttpOutcome back. The credential appears only in the Authorization header (PF-015, INV-21).
[<RequireQualifiedAccess>]
module GitHubRequests =

    [<Literal>]
    let ApiBase = "https://api.github.com"

    [<Literal>]
    let MarkerTrailer = "Causa-Write-Marker"

    let private escapeSegment (s: string) = Uri.EscapeDataString s

    /// A repository path is escaped segment by segment so `/` stays a separator.
    let private escapePath (RepoPath p) =
        p.Split('/') |> Array.map escapeSegment |> String.concat "/"

    let private repoUrl (repo: Repository) =
        sprintf "%s/repos/%s/%s" ApiBase (escapeSegment repo.Owner) (escapeSegment repo.Name)

    /// Headers every request carries: the REST media type, the pinned API version and the credential.
    let headers (connection: ConnectionContext) : (string * string) list =
        [ "Accept", "application/vnd.github+json"
          "X-GitHub-Api-Version", "2022-11-28"
          Credential.authorization connection.Credential ]

    let private get id connection url : HttpRequest =
        { Id = id; Method = Get; Url = url; Headers = headers connection; Body = None }

    let private postJson id connection url (body: JsonNode) : HttpRequest =
        { Id = id
          Method = Post
          Url = url
          Headers = headers connection @ [ "Content-Type", "application/json" ]
          Body = Some(body.ToJsonString()) }

    /// EXT-DATA-005: the repository and the permissions GitHub states for this credential.
    let connect (id: RequestId) (connection: ConnectionContext) : HttpRequest =
        get id connection (repoUrl connection.Repository)

    /// The connected branch's head commit.
    let branchHead (id: RequestId) (connection: ConnectionContext) : HttpRequest =
        let repo = connection.Repository
        get id connection (sprintf "%s/git/ref/heads/%s" (repoUrl repo) (escapePath (RepoPath repo.Branch)))

    /// One file or directory at a named commit (never at a branch name: a read is always at a commit, PERSIST-007).
    let fileAt (id: RequestId) (connection: ConnectionContext) (CommitOid commit) (path: RepoPath) : HttpRequest =
        get id connection (sprintf "%s/contents/%s?ref=%s" (repoUrl connection.Repository) (escapePath path) (escapeSegment commit))

    /// One blob through the Git data API (files over 1 MB, GH-LARGE-FILE).
    let blob (id: RequestId) (connection: ConnectionContext) (sha: string) : HttpRequest =
        get id connection (sprintf "%s/git/blobs/%s" (repoUrl connection.Repository) (escapeSegment sha))

    /// One commit, with its parents and message (the write marker trailer).
    let commit (id: RequestId) (connection: ConnectionContext) (CommitOid oid) : HttpRequest =
        get id connection (sprintf "%s/commits/%s" (repoUrl connection.Repository) (escapeSegment oid))

    /// The commits after the expected head up to the branch, one page of at most 100 (GH-TRUNCATED-LIST).
    let compare (id: RequestId) (connection: ConnectionContext) (CommitOid expected) (page: int) : HttpRequest =
        let repo = connection.Repository
        get
            id
            connection
            (sprintf "%s/compare/%s...%s?per_page=100&page=%d" (repoUrl repo) (escapeSegment expected) (escapePath (RepoPath repo.Branch)) page)

    /// A new branch at a commit. Used only to give each live test class a fresh branch of the test repository.
    let createBranch (id: RequestId) (connection: ConnectionContext) (branch: string) (CommitOid at) : HttpRequest =
        let body = JsonObject()
        body["ref"] <- JsonValue.Create("refs/heads/" + branch)
        body["sha"] <- JsonValue.Create(at)
        postJson id connection (repoUrl connection.Repository + "/git/refs") body

    /// The commit message of a save: the headline, the body and the write marker trailer.
    let messageBody (request: CommitRequest) : string =
        match request.Marker with
        | Some marker when request.Body = "" -> sprintf "%s: %s" MarkerTrailer marker
        | Some marker -> sprintf "%s\n\n%s: %s" request.Body MarkerTrailer marker
        | None -> request.Body

    /// The marker a commit message carries, if any.
    let markerOf (message: string) : string option =
        message.Split('\n')
        |> Array.tryPick (fun line ->
            let line = line.TrimEnd('\r')
            let prefix = MarkerTrailer + ": "
            if line.StartsWith(prefix, StringComparison.Ordinal) then Some(line.Substring(prefix.Length).Trim()) else None)

    let private isSafe (RepoPath p) =
        p <> ""
        && not (p.StartsWith "/")
        && not (p.EndsWith "/")
        && p.Split('/') |> Array.forall (fun s -> s <> "" && s <> "." && s <> "..")

    /// PERSIST-006: before sending, every changed path must be strictly under the investigation's prefix.
    let checkPrefix (RepoPath prefix) (request: CommitRequest) : Result<CommitRequest, CommitRefusal> =
        let paths = (request.Additions |> List.map _.Path) @ request.Deletions
        let under (RepoPath p) = p.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.Ordinal)
        match paths with
        | [] -> Error EmptyCommit
        | _ ->
            match paths |> List.tryFind (isSafe >> not) with
            | Some bad -> Error(UnsafePath bad)
            | None ->
                match paths |> List.tryFind (under >> not) with
                | Some outside -> Error(PathOutsidePrefix outside)
                | None -> Ok request

    [<Literal>]
    let CommitMutation =
        "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid committedDate } } }"

    /// The save mutation: one GraphQL createCommitOnBranch, conditional on the expected head, on the connected branch of
    /// the connected repository. No other write exists (no Contents API writes, no ref updates, PERSIST-006).
    let commitOnBranch (id: RequestId) (connection: ConnectionContext) (request: CommitRequest) : HttpRequest =
        let repo = connection.Repository
        let (CommitOid expected) = request.ExpectedHead
        let branch = JsonObject()
        branch["repositoryNameWithOwner"] <- JsonValue.Create(repo.Owner + "/" + repo.Name)
        branch["branchName"] <- JsonValue.Create(repo.Branch)
        let message = JsonObject()
        message["headline"] <- JsonValue.Create(request.Headline)
        message["body"] <- JsonValue.Create(messageBody request)
        let additions = JsonArray()
        for a in request.Additions do
            let (RepoPath p) = a.Path
            let o = JsonObject()
            o["path"] <- JsonValue.Create(p)
            o["contents"] <- JsonValue.Create(Convert.ToBase64String a.Contents)
            additions.Add(o)
        let deletions = JsonArray()
        for RepoPath p in request.Deletions do
            let o = JsonObject()
            o["path"] <- JsonValue.Create(p)
            deletions.Add(o)
        let changes = JsonObject()
        changes["additions"] <- additions
        changes["deletions"] <- deletions
        let input = JsonObject()
        input["branch"] <- branch
        input["expectedHeadOid"] <- JsonValue.Create(expected)
        input["message"] <- message
        input["fileChanges"] <- changes
        let variables = JsonObject()
        variables["input"] <- input
        let body = JsonObject()
        body["query"] <- JsonValue.Create(CommitMutation)
        body["variables"] <- variables
        postJson id connection (ApiBase + "/graphql") body

    /// Which commit a files read is at: the branch's current head, or a named commit.
    type Revision =
        | BranchHead
        | AtCommit of CommitOid

    [<Literal>]
    let FilesQuery =
        "query($owner: String!, $name: String!, $rev: String!, $m: String!, $e: String!, $s: String!) { repository(owner: $owner, name: $name) { object(expression: $rev) { ... on Commit { oid committedDate m: file(path: $m) { object { ... on Blob { oid text isTruncated byteSize } } } e: file(path: $e) { object { ... on Blob { oid text isTruncated byteSize } } } s: file(path: $s) { object { ... on Blob { oid text isTruncated byteSize } } } } } } }"

    let revisionText (connection: ConnectionContext) (revision: Revision) =
        match revision with
        | BranchHead -> "refs/heads/" + connection.Repository.Branch
        | AtCommit(CommitOid oid) -> oid

    /// One read of the three files of an investigation directory (manifest.json, events.ndjson, snapshot.json) at one
    /// commit, with that commit's oid (a GraphQL query: it writes nothing).
    let investigationFiles (id: RequestId) (connection: ConnectionContext) (revision: Revision) (RepoPath dir) : HttpRequest =
        let repo = connection.Repository
        let variables = JsonObject()
        variables["owner"] <- JsonValue.Create(repo.Owner)
        variables["name"] <- JsonValue.Create(repo.Name)
        variables["rev"] <- JsonValue.Create(revisionText connection revision)
        variables["m"] <- JsonValue.Create(dir + "/manifest.json")
        variables["e"] <- JsonValue.Create(dir + "/events.ndjson")
        variables["s"] <- JsonValue.Create(dir + "/snapshot.json")
        let body = JsonObject()
        body["query"] <- JsonValue.Create(FilesQuery)
        body["variables"] <- variables
        postJson id connection (ApiBase + "/graphql") body

    /// The operation a request built here performs, recovered from the request itself (for the fake and for logs).
    let operationOf (request: HttpRequest) : GitHubOperation option =
        let path = Uri(request.Url).AbsolutePath
        let segments = path.Trim('/').Split('/') |> Array.map Uri.UnescapeDataString
        let query = Uri(request.Url).Query
        let refOf () =
            query.TrimStart('?').Split('&')
            |> Array.tryPick (fun kv ->
                match kv.Split('=', 2) with
                | [| "ref"; v |] -> Some(Uri.UnescapeDataString v)
                | _ -> None)
        match request.Method, List.ofArray segments with
        | Post, [ "graphql" ] ->
            request.Body
            |> Option.bind (fun b ->
                try
                    use doc = System.Text.Json.JsonDocument.Parse b
                    let variables = doc.RootElement.GetProperty("variables")
                    match variables.TryGetProperty("rev") with
                    | true, rev -> rev.GetString() |> Option.ofObj |> Option.map ReadInvestigationAt
                    | _ ->
                        let expected = variables.GetProperty("input").GetProperty("expectedHeadOid").GetString()
                        match expected with
                        | null -> None
                        | e -> Some(CommitOnBranch(CommitOid e))
                with _ -> None)
        | Get, [ "repos"; _; _ ] -> Some ConnectRepository
        | Get, "repos" :: _ :: _ :: "git" :: "ref" :: "heads" :: _ -> Some ReadBranchHead
        | Get, [ "repos"; _; _; "git"; "blobs"; sha ] -> Some(ReadBlob sha)
        | Get, [ "repos"; _; _; "commits"; oid ] -> Some(ReadCommit(CommitOid oid))
        | Get, "repos" :: _ :: _ :: "compare" :: _ ->
            let range = String.Join("/", segments |> Array.skip 4)
            match range.Split("...") with
            | [| expected; _ |] when expected <> "" -> Some(CompareFrom(CommitOid expected))
            | _ -> None
        | Get, "repos" :: _ :: _ :: "contents" :: rest ->
            refOf () |> Option.map (fun r -> ReadFileAt(RepoPath(String.Join("/", rest)), CommitOid r))
        | Post, [ "repos"; _; _; "git"; "refs" ] -> Some CreateBranch
        | _ -> None

    /// The base64 contents of a file answer, decoded (contents API `encoding: base64`, line breaks ignored).
    let decodeBase64 (text: string) : byte[] option =
        try
            let clean = text.Replace("\n", "").Replace("\r", "")
            Some(Convert.FromBase64String clean)
        with _ -> None

    /// UTF-8 bytes of a string, for callers that build file additions from text.
    let utf8 (s: string) : byte[] = Encoding.UTF8.GetBytes s

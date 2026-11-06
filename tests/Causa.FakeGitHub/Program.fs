// Owner: integration. The stateful GitHub fake served over HTTP for the offline browser suites.
//
//   dotnet run --project tests/Causa.FakeGitHub -c Release -- --port 4610 [--owner harness] [--repo causa-test]
//                                                             [--branch main] [--token harness-token]
// (the token defaults to CAUSA_GITHUB_TOKEN when set)
//
// The browser specs route the application's https://api.github.com requests here (tests/browser/support/app.mjs,
// routeToDouble). Every GitHub request is answered by StatefulFake.step, the same fake the deterministic .NET suites
// use (REST repository, refs, contents, blobs, commits and compare; GraphQL reads and createCommitOnBranch with
// expectedHeadOid), so the application sees GitHub's recorded answer forms. One repository with one branch, whose root
// commit exists from the start: the working copy (workspaces/demo/causa-app-<run>) is created by the application's own
// first save, exactly as on the designated test repository.
//
// One route is the harness's, not the fake's: PUT /repos/{owner}/{repo}/contents/{path} (GitHub's contents API) writes
// a file as another writer's commit on the branch, so a spec can seed a stored history without the application
// (tests/browser/acceptance/workspace-data.mjs seedStoredHistory). The answers are never live evidence (PERSIST-025-O4).
module Causa.FakeGitHub.Program

open System
open System.IO
open System.Net
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Causa.Persistence
open Causa.TestSupport

type Options =
    { Port: int
      Owner: string
      Repo: string
      Branch: string
      Token: string }

/// The credential defaults to CAUSA_GITHUB_TOKEN when set (the specs connect with it), so a token never needs to be on
/// the command line; otherwise to the specs' harness credential.
let defaults =
    { Port = 4610
      Owner = "harness"
      Repo = "causa-test"
      Branch = "main"
      Token = Environment.GetEnvironmentVariable "CAUSA_GITHUB_TOKEN" |> Option.ofObj |> Option.filter ((<>) "") |> Option.defaultValue "harness-token" }

let rec parse (options: Options) (args: string list) : Result<Options, string> =
    match args with
    | [] -> Ok options
    | "--port" :: v :: rest ->
        match Int32.TryParse v with
        | true, p when p > 0 && p < 65536 -> parse { options with Port = p } rest
        | _ -> Error(sprintf "--port %s is not a port" v)
    | "--owner" :: v :: rest -> parse { options with Owner = v } rest
    | "--repo" :: v :: rest -> parse { options with Repo = v } rest
    | "--branch" :: v :: rest -> parse { options with Branch = v } rest
    | "--token" :: v :: rest -> parse { options with Token = v } rest
    | other :: _ -> Error(sprintf "unknown argument %s" other)

/// The repository at start: one root commit on the branch. A read-only credential is known as well (`<token>-read`).
let initialState (o: Options) : FakeState =
    StatefulFake.initial
        o.Owner
        o.Repo
        o.Branch
        [ "README.md", Encoding.UTF8.GetBytes "Causa browser-suite repository (stateful fake).\n" ]
        [ o.Token, ReadWrite; o.Token + "-read", ReadOnly ]

/// One request as it arrived, before it is understood.
type Inbound =
    { Method: string
      PathAndQuery: string
      Headers: (string * string) list
      Body: string }

type Reply =
    | Reply of status: int * headers: (string * string) list * body: string
    /// The connection is closed without an answer (a lost answer, a timeout, a request that never left).
    | Dropped

type Route =
    | Preflight
    | Seed of owner: string * repo: string * path: string
    | Fake of HttpRequest
    | Unsupported of string

let private methodOf (m: string) : HttpMethod option =
    match m.ToUpperInvariant() with
    | "GET" -> Some Get
    | "POST" -> Some Post
    | "PUT" -> Some Put
    | "PATCH" -> Some Patch
    | _ -> None

let private segments (pathAndQuery: string) =
    let path = pathAndQuery.Split('?', 2).[0]
    path.Trim('/').Split('/') |> Array.map Uri.UnescapeDataString |> List.ofArray

/// What the request is: pure.
let route (id: RequestId) (r: Inbound) : Route =
    match r.Method.ToUpperInvariant(), segments r.PathAndQuery with
    | "OPTIONS", _ -> Preflight
    | "PUT", "repos" :: owner :: repo :: "contents" :: path when not path.IsEmpty -> Seed(owner, repo, String.Join("/", path))
    | m, _ ->
        match methodOf m with
        | Some method ->
            Fake
                { Id = id
                  Method = method
                  Url = "https://api.github.com" + r.PathAndQuery
                  Headers = r.Headers
                  Body = if r.Body = "" then None else Some r.Body }
        | None -> Unsupported m

let private json = [ "content-type", "application/json; charset=utf-8" ]

let private bearer (headers: (string * string) list) =
    headers
    |> List.tryPick (fun (k, v) ->
        if String.Equals(k, "Authorization", StringComparison.OrdinalIgnoreCase) && v.StartsWith("Bearer ", StringComparison.Ordinal) then Some(v.Substring 7)
        else None)

let private stringProp (name: string) (e: JsonElement) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.String -> Option.ofObj (v.GetString())
    | _ -> None

/// GitHub's contents PUT, applied as another writer's commit: the given sha must be the file's current blob sha (or
/// absent for a new file), as GitHub requires. Pure: the new state and the answer.
let seed (state: FakeState) (owner: string, repo: string, path: string) (r: Inbound) : FakeState * Reply =
    let fail status message = state, Reply(status, json, JsonSerializer.Serialize {| message = message |})
    if owner <> state.Owner || repo <> state.Name then fail 404 "Not Found"
    else
        match bearer r.Headers |> Option.bind state.Tokens.TryFind with
        | None -> state, Reply(401, json, Recordings.badCredentials)
        | Some ReadOnly -> fail 403 "Resource not accessible by personal access token"
        | Some ReadWrite ->
            try
                use doc = JsonDocument.Parse(if r.Body = "" then "{}" else r.Body)
                let body = doc.RootElement
                let branch = stringProp "branch" body |> Option.defaultValue (state.Branches |> Map.toList |> List.head |> fst)
                match state.Branches.TryFind branch, stringProp "content" body with
                | None, _ -> fail 404 (sprintf "Branch %s not found" branch)
                | _, None -> fail 422 "Invalid request. \"content\" wasn't supplied."
                | Some head, Some content ->
                    let current = state.Commits[head].Tree.TryFind path |> Option.map StatefulFake.blobSha
                    let given = stringProp "sha" body
                    if current <> given then
                        fail 409 (sprintf "%s does not match %s" path (defaultArg given "nothing"))
                    else
                        let bytes = Convert.FromBase64String content
                        let next, commit = StatefulFake.otherWriterCommits branch "seed" [ path, bytes ] [] state
                        let answer = JsonSerializer.Serialize {| content = {| path = path; sha = StatefulFake.blobSha bytes |}; commit = {| sha = commit |} |}
                        next, Reply((if current.IsSome then 200 else 201), json, answer)
            with :? JsonException | :? FormatException -> fail 400 "Problems parsing JSON"

let private ofOutcome (outcome: HttpOutcome) : Reply =
    match outcome with
    | Answered(_, status, headers, body) -> Reply(status, headers, body)
    | NotAnswered _ -> Dropped

/// The cross-origin headers api.github.com sends, so the browser accepts the answer wherever it is fulfilled.
let private cors =
    [ "access-control-allow-origin", "*"
      "access-control-allow-headers", "Authorization, Accept, Content-Type, X-GitHub-Api-Version, If-None-Match"
      "access-control-allow-methods", "GET, POST, PATCH, PUT, DELETE"
      "access-control-expose-headers", "ETag, Link, Location, X-GitHub-Api-Version-Selected, X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset, X-RateLimit-Used, X-RateLimit-Resource" ]

/// One request against the running fake: the only place state changes.
let answer (running: StatefulFake.Running) (id: RequestId) (r: Inbound) : Reply =
    match route id r with
    | Preflight -> Reply(204, [], "")
    | Unsupported m -> Reply(405, json, JsonSerializer.Serialize {| message = sprintf "%s is not supported by the fake" m |})
    | Fake request -> running.Execute request |> ofOutcome
    | Seed(owner, repo, path) ->
        let result = ref Dropped
        running.Update(fun state ->
            let next, reply = seed state (owner, repo, path) r
            result.Value <- reply
            next)
        result.Value

// ---- The HTTP boundary -------------------------------------------------------------------------------------------

let private inbound (request: HttpListenerRequest) : Inbound =
    use reader = new StreamReader(request.InputStream, Encoding.UTF8)
    { Method = request.HttpMethod
      PathAndQuery = request.RawUrl |> Option.ofObj |> Option.defaultValue "/"
      Headers =
        [ for k in request.Headers.AllKeys do
              match Option.ofObj k with
              | Some key -> yield key, (request.Headers[key] |> Option.ofObj |> Option.defaultValue "")
              | None -> () ]
      Body = reader.ReadToEnd() }

let private write (response: HttpListenerResponse) (reply: Reply) =
    match reply with
    | Dropped -> response.Abort()
    | Reply(status, headers, body) ->
        response.StatusCode <- status
        for k, v in cors @ headers do
            if String.Equals(k, "content-type", StringComparison.OrdinalIgnoreCase) then response.ContentType <- v
            elif not (String.Equals(k, "content-length", StringComparison.OrdinalIgnoreCase)) then response.Headers[k] <- v
        let bytes = Encoding.UTF8.GetBytes body
        response.ContentLength64 <- int64 bytes.Length
        response.OutputStream.Write(bytes, 0, bytes.Length)
        response.Close()

let private serve (options: Options) =
    let running = StatefulFake.start (initialState options)
    use listener = new HttpListener()
    listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" options.Port)
    listener.Start()
    printfn "stateful GitHub fake for %s/%s (branch %s) on http://127.0.0.1:%d" options.Owner options.Repo options.Branch options.Port
    let mutable counter = 0L
    while listener.IsListening do
        let context = listener.GetContext()
        let id = RequestId(Threading.Interlocked.Increment(&counter))
        Task.Run(fun () ->
            try
                write context.Response (answer running id (inbound context.Request))
            with e ->
                eprintfn "request failed: %s" e.Message
                try context.Response.Abort() with _ -> ())
        |> ignore
    0

[<EntryPoint>]
let main argv =
    match parse defaults (List.ofArray argv) with
    | Ok options -> serve options
    | Error message ->
        eprintfn "%s" message
        2

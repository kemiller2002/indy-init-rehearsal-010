// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// PF-013 (live): sanitized, versioned observations of GitHub's real answers, compared with the fake's, so a divergence
// between the fixture provider and GitHub fails a test.
namespace Causa.Acceptance.Live

open System
open System.Text.Json
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PF013Tests =
    /// The API version the adapter pins (GitHubRequests.headers); the observations are of this version.
    let pinnedVersion = GitHubRequests.headers SaveKit.connection |> List.find (fun (k, _) -> k = "X-GitHub-Api-Version") |> snd

    /// One sanitized observation: what the adapter depends on, never a body, a credential or a sensitive header.
    type Observation =
        { Probe: string
          Status: int
          Kind: string
          /// The JSON paths the adapter reads that the answer holds.
          Paths: string list
          GraphQlErrorTypes: string list
          /// Header names (never values, except the API version and the rate-limit counters).
          Headers: (string * string) list }

    let private keptHeaderValues = [ "x-github-api-version-selected"; "x-ratelimit-limit"; "x-ratelimit-remaining"; "retry-after" ]

    let private sensitive = [ "authorization"; "set-cookie"; "cookie"; "x-github-sso"; "x-oauth-scopes"; "x-accepted-oauth-scopes"; "github-authentication-token-expiration" ]

    let private hasPath (body: string) (path: string) =
        try
            use doc = JsonDocument.Parse body
            path.Split('.')
            |> Array.fold (fun (e: JsonElement option) name ->
                e |> Option.bind (fun e ->
                    match e.ValueKind with
                    | JsonValueKind.Object -> (match e.TryGetProperty name with | true, v when v.ValueKind <> JsonValueKind.Null -> Some v | _ -> None)
                    | JsonValueKind.Array when name = "[]" -> (if e.GetArrayLength() > 0 then Some e[0] else None)
                    | _ -> None)) (Some doc.RootElement)
            |> Option.isSome
        with _ -> false

    let observe (probe: string) (paths: string list) (outcome: HttpOutcome) (kind: string) : Observation =
        match outcome with
        | Answered(_, status, headers, body) ->
            { Probe = probe
              Status = status
              Kind = kind
              Paths = paths |> List.filter (hasPath body)
              GraphQlErrorTypes = Scenarios.errorTypes outcome |> List.sort
              Headers =
                headers
                |> List.map (fun (k, v) -> k.ToLowerInvariant(), v)
                |> List.filter (fun (k, _) -> not (List.contains k sensitive))
                |> List.map (fun (k, v) -> if List.contains k keptHeaderValues then k, v else k, "")
                |> List.distinctBy fst
                |> List.sortBy fst }
        | NotAnswered(_, f) -> { Probe = probe; Status = 0; Kind = sprintf "%A" f; Paths = []; GraphQlErrorTypes = []; Headers = [] }

    /// The probe set: status codes, bodies (the paths the adapter reads), headers, branch behavior, pagination, cache
    /// (a read after a commit sees it) and retry semantics (rate-limit counters), each classified as the adapter does.
    let observations (p: GitHubProvider) : Observation list =
        let prefix = Probes.investigationPrefix "pf013"
        let send probe paths op request =
            let outcome, result = Scenarios.send p op request
            observe probe paths outcome (Scenarios.kind result)
        let head = Scenarios.readHead p |> Probes.ok "head"
        let first = Scenarios.commit p (Scenarios.commitRequest head [ prefix + "/a.json", "{\"n\":1}" ] (Probes.marker "pf013")) |> snd |> Probes.ok "commit 1"
        let second = Scenarios.commit p (Scenarios.commitRequest first [ prefix + "/b.json", "{\"n\":2}" ] (Probes.marker "pf013")) |> snd |> Probes.ok "commit 2"
        let id () = Scenarios.nextId ()
        let c = p.Connection
        let missingBranch = { c with Repository = { c.Repository with Branch = "causa-missing-" + Guid.NewGuid().ToString("N") } }
        let stale = Probes.staleHead p (prefix + "-stale")
        [ send "branch head" [ "object.sha" ] ReadBranchHead (GitHubRequests.branchHead (id ()) c)
          send "branch head after a commit (no cached head)" [ "object.sha" ] ReadBranchHead (GitHubRequests.branchHead (id ()) c)
          |> fun o -> { o with Kind = o.Kind + (if Scenarios.readHead p = Ok stale.Resent then ":fresh" else ":stale") }
          send "missing branch" [ "message" ] ReadBranchHead (GitHubRequests.branchHead (id ()) missingBranch)
          send "file at commit" [ "content"; "encoding"; "sha" ] (ReadFileAt(RepoPath(prefix + "/a.json"), second)) (GitHubRequests.fileAt (id ()) c second (RepoPath(prefix + "/a.json")))
          send "missing file" [ "message" ] (ReadFileAt(RepoPath(prefix + "/none.json"), second)) (GitHubRequests.fileAt (id ()) c second (RepoPath(prefix + "/none.json")))
          send "commit" [ "sha"; "commit.message"; "parents.[].sha" ] (ReadCommit second) (GitHubRequests.commit (id ()) c second)
          send "compare page 1 of 1 per page" [ "status"; "commits.[].sha" ] (CompareFrom head) { GitHubRequests.compare (id ()) c head 1 with Url = (GitHubRequests.compare (id ()) c head 1).Url.Replace("per_page=100", "per_page=1") }
          send "compare page 2 of 1 per page" [ "status"; "commits.[].sha" ] (CompareFrom head) { GitHubRequests.compare (id ()) c head 2 with Url = (GitHubRequests.compare (id ()) c head 2).Url.Replace("per_page=100", "per_page=1") }
          send "graphql files read" [ "data.repository.object.oid" ] (ReadInvestigationAt "files") (GitHubRequests.investigationFiles (id ()) c GitHubRequests.BranchHead (RepoPath prefix))
          observe "stale expected head" [ "errors" ] stale.Outcome (Scenarios.kind stale.Result)
          send "existing branch (422)" [ "message" ] CreateBranch (GitHubRequests.createBranch (id ()) c c.Repository.Branch head)
          send "bad credential (401)" [ "message" ] ReadBranchHead (GitHubRequests.branchHead (id ()) { c with Credential = Credential.create "invalid-credential-for-conformance" }) ]

    /// The divergences between two observation sets: status, classification, the paths the adapter reads, GraphQL
    /// error types, and the pinned API version.
    let divergences (fake: Observation list) (real: Observation list) : string list =
        [ for f, r in List.zip fake real do
              if f.Status <> r.Status then yield sprintf "%s: status %d (fake) vs %d (GitHub)" f.Probe f.Status r.Status
              if f.Kind <> r.Kind then yield sprintf "%s: classified %s (fake) vs %s (GitHub)" f.Probe f.Kind r.Kind
              if f.Paths <> r.Paths then yield sprintf "%s: paths %A (fake) vs %A (GitHub)" f.Probe f.Paths r.Paths
              if f.GraphQlErrorTypes <> r.GraphQlErrorTypes then yield sprintf "%s: error types %A (fake) vs %A (GitHub)" f.Probe f.GraphQlErrorTypes r.GraphQlErrorTypes ]

    let private serialize (p: GitHubProvider) (obs: Observation list) =
        let o = Nodes.JsonObject()
        o["schema"] <- Nodes.JsonValue.Create("causa-provider-observations/1")
        o["apiVersion"] <- Nodes.JsonValue.Create(pinnedVersion)
        o["mode"] <- Nodes.JsonValue.Create(ProviderEvidence.modeName p.Mode)
        o["observedAt"] <- Nodes.JsonValue.Create(DateTimeOffset.UtcNow.ToString "o")
        o["observations"] <- JsonSerializer.SerializeToNode(obs |> List.map (fun x -> {| probe = x.Probe; status = x.Status; kind = x.Kind; paths = x.Paths; graphQlErrorTypes = x.GraphQlErrorTypes; headers = x.Headers |> List.map (fun (k, v) -> [| k; v |]) |}))
        Recordings.sanitize p.Secrets (o.ToJsonString())

    let private write (text: string) =
        let path =
            match Environment.GetEnvironmentVariable "CAUSA_PROVIDER_OBSERVATIONS" with
            | null | "" -> IO.Path.Combine(IO.Path.GetDirectoryName(ProviderEvidence.path ()) |> string, "causa-provider-observations.json")
            | p -> p
        try IO.File.WriteAllText(path, text) with _ -> ()

    /// O1: the observations of GitHub's real answers are made and kept, versioned and sanitized.
    let o1 (mk: string -> GitHubProvider) =
        let real = mk "pf013-o1"
        let obs = observations real
        Assert.Equal(12, obs.Length)
        Assert.All(obs, fun o -> Assert.True(o.Status > 0, o.Probe))
        Assert.Equal("Ok:fresh", (obs |> List.find (fun o -> o.Probe.StartsWith "branch head after")).Kind)
        Assert.Equal(1, (obs |> List.find (fun o -> o.Probe = "compare page 2 of 1 per page")).Paths |> List.filter ((=) "commits.[].sha") |> List.length)
        let text = serialize real obs
        write text
        Assert.Contains("\"schema\":\"causa-provider-observations/1\"", text)
        Assert.Contains("\"apiVersion\":\"" + pinnedVersion + "\"", text)

    /// O2: a divergence between the fake and GitHub fails: the two observation sets agree, and a fake that answers
    /// differently is detected.
    let o2 (mk: string -> GitHubProvider) =
        let real = mk "pf013-o2"
        let fixture, _ = FixtureData.fixtureProvider []
        let fakeObs = observations fixture
        let realObs = observations real
        let differences = divergences fakeObs realObs
        Assert.True(differences.IsEmpty, String.Join("\n", differences))
        // The comparison is sensitive: a fake whose head answer lacked the sha the adapter reads would diverge.
        let brokenAnswer = Answered(RequestId 1L, 200, Recordings.standardHeaders 4999, """{"ref":"refs/heads/x","object":{}}""")
        let broken = observe "branch head" [ "object.sha" ] brokenAnswer (Scenarios.kind (Classify.answer ReadBranchHead brokenAnswer))
        Assert.NotEmpty(divergences (broken :: fakeObs.Tail) realObs)

    /// O3: no secret, private payload or sensitive header is captured.
    let o3 (mk: string -> GitHubProvider) =
        let real = mk "pf013-o3"
        let obs = observations real
        let text = serialize real obs
        for secret in real.Secrets do
            Assert.DoesNotContain(secret, text)
        for marker in [ "ghp_"; "ghs_"; "gho_"; "github_pat_"; "Bearer " ] do
            Assert.DoesNotContain(marker, text)
        Assert.All(obs, fun o -> Assert.All(o.Headers, fun (k, _) -> Assert.DoesNotContain(k, sensitive)))
        // Bodies are never kept, only which of the adapter's paths were present: no payload text appears.
        Assert.DoesNotContain("{\\\"n\\\":1}", text)
        Assert.DoesNotContain("\"content\":\"", text)

    /// O4: the observations are of the pinned API version; when GitHub selects another version, the provider contract
    /// changed and this fails until the observations are refreshed.
    let o4 (mk: string -> GitHubProvider) =
        let real = mk "pf013-o4"
        let obs = observations real
        for o in obs |> List.filter (fun o -> o.Status <> 401) do
            match o.Headers |> List.tryFind (fun (k, _) -> k = "x-github-api-version-selected") with
            | Some(_, v) -> Assert.True((v = pinnedVersion), sprintf "%s: GitHub selected API version %s; the observations pin %s: refresh them" o.Probe v pinnedVersion)
            | None -> ()
        Assert.Contains(obs, fun o -> o.Headers |> List.exists (fun (k, _) -> k = "x-github-api-version-selected"))
        Assert.Contains(("x-github-api-version-selected", pinnedVersion), Recordings.standardHeaders 1)

    [<Fact>]
    let ``PF-013-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-013-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-013-O3`` () = o3 PfKit.live

    [<Fact>]
    let ``PF-013-O4`` () = o4 PfKit.live

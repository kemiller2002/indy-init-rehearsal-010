// Owner: persistence.
namespace Causa.Acceptance.Deterministic

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

/// AR-014: least privilege and no secret echo, for the GitHub adapter and the provider test harness.
module AR014Tests =

    let private token = "ghs_" + String('Z', 36)
    let private repository = { Owner = "acme"; Name = "causa-data"; Branch = "causa-acceptance" }
    let private connection = { Repository = repository; Credential = Credential.create token }
    let private oid = CommitOid(String('1', 40))
    let private prefix = "workspaces/demo/causa-app-r1/investigations/0b5c1f8e-0000-4000-8000-000000000001"

    let private commitRequest paths =
        { ExpectedHead = oid
          Headline = "Save"
          Body = ""
          Marker = Some "session.1"
          Additions = paths |> List.map (fun p -> { Path = RepoPath p; Contents = "{}"B })
          Deletions = [] }

    /// Every request the adapter can build for this connection.
    let private everyRequest () : HttpRequest list =
        [ GitHubRequests.connect (RequestId 1L) connection
          GitHubRequests.branchHead (RequestId 2L) connection
          GitHubRequests.fileAt (RequestId 3L) connection oid (RepoPath(prefix + "/manifest.json"))
          GitHubRequests.blob (RequestId 4L) connection (String('b', 40))
          GitHubRequests.commit (RequestId 5L) connection oid
          GitHubRequests.compare (RequestId 6L) connection oid 1
          GitHubRequests.commitOnBranch (RequestId 7L) connection (commitRequest [ prefix + "/events.ndjson"; prefix + "/manifest.json" ]) ]

    [<Fact>]
    let ``AR-014-O1`` () =
        // Repository access: every request reaches only the connected repository and branch.
        let repoBase = sprintf "%s/repos/%s/%s" GitHubRequests.ApiBase repository.Owner repository.Name
        for request in everyRequest () do
            let url = request.Url
            if url = GitHubRequests.ApiBase + "/graphql" then
                use doc = Text.Json.JsonDocument.Parse(request.Body.Value)
                let branch = doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("branch")
                Assert.Equal("acme/causa-data", branch.GetProperty("repositoryNameWithOwner").GetString())
                Assert.Equal("causa-acceptance", branch.GetProperty("branchName").GetString())
                Assert.Equal(Post, request.Method)
            else
                Assert.True(url = repoBase || url.StartsWith(repoBase + "/"), sprintf "request outside the connected repository: %s" url)
                Assert.Equal(Get, request.Method)

        // Mutation permission: the only write is one conditional createCommitOnBranch (no ref updates, no Contents API
        // writes) and it may change only files under the investigation's own prefix.
        let writes = everyRequest () |> List.filter (fun r -> r.Method <> Get)
        Assert.Equal(1, writes.Length)
        Assert.Equal(Some(CommitOnBranch oid), GitHubRequests.operationOf writes.Head)
        let ok = commitRequest [ prefix + "/events.ndjson" ]
        Assert.Equal(Ok ok, GitHubRequests.checkPrefix (RepoPath prefix) ok)
        let outside = commitRequest [ prefix + "/events.ndjson"; "workspaces/demo/historical-v1/investigations/x/events.ndjson" ]
        Assert.Equal(Error(PathOutsidePrefix(RepoPath "workspaces/demo/historical-v1/investigations/x/events.ndjson")), GitHubRequests.checkPrefix (RepoPath prefix) outside)
        let sibling = commitRequest [ prefix + "-other/events.ndjson" ]
        Assert.True(Result.isError (GitHubRequests.checkPrefix (RepoPath prefix) sibling))
        let traversal = commitRequest [ prefix + "/../other/events.ndjson" ]
        Assert.Equal(Error(UnsafePath(RepoPath(prefix + "/../other/events.ndjson"))), GitHubRequests.checkPrefix (RepoPath prefix) traversal)
        Assert.Equal(Error EmptyCommit, GitHubRequests.checkPrefix (RepoPath prefix) (commitRequest []))

        // A credential without write access gets no write: GitHub's refusal is a permission refusal and nothing moves.
        let provider, running = Scenarios.fakeProvider "main" [ prefix + "/manifest.json", "{}"B ]
        let readOnly = { provider with Connection = { provider.Connection with Credential = Credential.create "fake-read-credential" } }
        let head = Scenarios.readHead readOnly |> Result.defaultWith (fun e -> failwithf "%A" e)
        let _, refused = Scenarios.commit readOnly (Scenarios.commitRequest head [ prefix + "/events.ndjson", "x" ] "s.1")
        Assert.Equal(Error(PermissionDenied "write to the branch"), refused)
        Assert.Equal(Ok head, Scenarios.readHead provider)
        Assert.Equal(1, running.Current().Commits.Count)

        // Credentials given to the provider harness: only CAUSA_GITHUB_TOKEN, for the designated test repository; a
        // broader token in the environment is never picked up as a fallback.
        let lookupOf (pairs: (string * string) list) = fun name -> pairs |> List.tryFind (fst >> (=) name) |> Option.map snd
        let noDedicated = LiveGitHub.configurationFrom (lookupOf [ "GH_TOKEN", "ghp_broad"; "GITHUB_TOKEN", "ghp_broad"; "CAUSA_WRITE_TOKEN", "x" ])
        Assert.Equal(Error LiveGitHub.blockedMessage, noDedicated)
        match LiveGitHub.configurationFrom (lookupOf [ "CAUSA_GITHUB_TOKEN", "t"; "GH_TOKEN", "ghp_broad" ]) with
        | Ok(credential, repo) ->
            Assert.Equal("t", credential)
            Assert.Equal({ Owner = "kemiller2002"; Name = "causa-acceptance-r10"; Branch = "causa-acceptance" }, repo)
        | Error e -> failwith e

    [<Fact>]
    let ``AR-014-O2`` () =
        let leaks (text: string) = text.Contains(token, StringComparison.Ordinal)

        // The credential prints as "<credential>" wherever it is formatted.
        Assert.Equal("<credential>", string connection.Credential)
        Assert.False(leaks (sprintf "%O" connection.Credential))
        Assert.False(leaks (string connection.Credential))

        // In every request it appears exactly once: as the Authorization header's value, never in the URL or body.
        for request in everyRequest () do
            Assert.False(leaks request.Url, "credential in a URL")
            Assert.False(request.Body |> Option.exists leaks, "credential in a body")
            let carrying = request.Headers |> List.filter (snd >> leaks)
            Assert.Equal<(string * string) list>([ "Authorization", "Bearer " + token ], carrying)

        // No classification and no user-facing message of any recorded answer names a credential.
        for recorded in Recordings.all do
            let classified = Classify.answer recorded.Operation recorded.Outcome
            Assert.False(leaks (sprintf "%A" classified))
            match classified with
            | Error e -> Assert.False(leaks (Classify.describe e))
            | Ok _ -> ()

        // Test artifacts: the recordings carry no credential, the fake's request log redacts Authorization, and an
        // evidence line redacts the provider's secret even when a fact repeats it.
        for recorded in Recordings.all do
            let text = sprintf "%A %A" recorded.Outcome recorded.Operation
            for marker in Recordings.credentialMarkers do
                Assert.False(text.Contains(marker, StringComparison.Ordinal), sprintf "recording %s contains %s" recorded.Form marker)
        let provider, running = Scenarios.fakeProvider "main" [ "a/b.json", "{}"B ]
        Scenarios.readHead provider |> ignore
        let log = sprintf "%A" (running.Current().Log)
        Assert.False(log.Contains("fake-write-credential", StringComparison.Ordinal))
        Assert.Contains("<redacted>", log)
        let evidence = ProviderEvidence.line { provider with Secrets = [ token ] } "AR-014-O2" [ oid ] [ "note", "header was Bearer " + token ]
        Assert.False(leaks evidence)
        Assert.Contains("<redacted>", evidence)
        Assert.Equal("Authorization: <redacted>", Recordings.sanitize [] ("Authorization: Bearer " + token))

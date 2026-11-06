// Owner: integration. Written by the persistence lane under CR-PF-TESTS (approved by the integration authority).
// PF-015 (live): credentials and security. No anonymous mode; separate read and write secrets that never stand in for
// each other; no credential in a URL, log, evidence or artifact; a missing secret is BLOCKED.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Persistence
open Causa.TestSupport

module PF015Tests =
    let private lookup (pairs: (string * string) list) (name: string) = pairs |> List.tryFind (fun (k, _) -> k = name) |> Option.map snd

    /// Every request the adapter builds, for one connection.
    let private everyRequest (c: ConnectionContext) =
        let id = RequestId 1L
        let head = CommitOid(String('a', 40))
        [ GitHubRequests.connect id c
          GitHubRequests.branchHead id c
          GitHubRequests.fileAt id c head (RepoPath "catalog/active.json")
          GitHubRequests.blob id c (String('b', 40))
          GitHubRequests.commit id c head
          GitHubRequests.compare id c head 1
          GitHubRequests.investigationFiles id c GitHubRequests.BranchHead (RepoPath "workspaces/demo/x/investigations/y")
          GitHubRequests.commitOnBranch id c (Scenarios.commitRequest head [ "workspaces/demo/x/investigations/y/a.json", "{}" ] "m") ]

    /// O1: every catalog read and every write carries the credential, and there is no anonymous mode: the same catalog
    /// read without it is refused by GitHub, and the listing fails instead of reading anything.
    let o1 (mk: string -> GitHubProvider) =
        let real = mk "pf015-o1"
        for r in everyRequest real.Connection do
            Assert.Contains(r.Headers, fun (k, v) -> k = "Authorization" && v.StartsWith "Bearer ")
        FixtureData.seed real (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        Assert.True(Result.isOk (ProviderPort.list real ActiveCatalog))
        let anonymous = { real with Execute = fun r -> real.Execute { r with Headers = r.Headers |> List.filter (fun (k, _) -> k <> "Authorization") } }
        match ProviderPort.list anonymous ActiveCatalog with
        | Error(ProviderFailed(NotFound _ | AuthenticationFailed | PermissionDenied _)) -> ()
        | other -> failwithf "an anonymous catalog read must be refused, not %A" (other |> Result.map ignore)

    /// O2: authenticated reads use CAUSA_GITHUB_TOKEN only (never GH_TOKEN, GITHUB_TOKEN or the write secret), and that
    /// credential is least-privilege read-only: GitHub states no write permission for it on the test repository.
    let o2 (mk: string -> GitHubProvider) =
        let others = [ "GH_TOKEN", "a"; "GITHUB_TOKEN", "b"; LiveCredentials.WriteTokenVariable, "c" ]
        Assert.True(Result.isError (LiveGitHub.configurationFrom (lookup others)))
        match LiveGitHub.configurationFrom (lookup (("CAUSA_GITHUB_TOKEN", "r") :: others)) with
        | Ok(token, _) -> Assert.Equal("r", token)
        | Error e -> failwith e
        let real = mk "pf015-o2"
        let request = Persistence.connector.ConnectRequest (Scenarios.nextId ()) real.Connection
        match Persistence.connector.InterpretConnect request.Id (real.Execute request) with
        | ConnectAccepted canWrite ->
            Assert.False(canWrite, "CAUSA_GITHUB_TOKEN can write to the test repository: the read credential must be read-only (PF-015-O2, ODR-R10-28 A)")
        | other -> failwithf "connect: %A" other

    /// O3: live writes use CAUSA_WRITE_TOKEN, limited to the test repository, and neither secret falls back to the
    /// other: each configuration is BLOCKED without its own variable even when the other is set.
    let o3 (mk: string -> GitHubProvider) (writer: unit -> GitHubProvider) =
        Assert.True(Result.isError (LiveCredentials.writeConfigurationFrom (lookup [ "CAUSA_GITHUB_TOKEN", "r" ])))
        Assert.True(Result.isError (LiveGitHub.configurationFrom (lookup [ LiveCredentials.WriteTokenVariable, "w" ])))
        match LiveCredentials.writeConfigurationFrom (lookup [ "CAUSA_GITHUB_TOKEN", "r"; LiveCredentials.WriteTokenVariable, "w" ]) with
        | Ok(token, _) -> Assert.Equal("w", token)
        | Error e -> failwith e
        let reader = mk "pf015-o3"
        let w = writer ()
        let fresh = { w with Connection = { w.Connection with Repository = reader.Connection.Repository } }
        let commit = FixtureData.seed fresh [ "workspaces/demo/causa-app-pf015/write.json", "{\"written\":true}\n"B ] "Causa: write-credential check"
        Assert.Equal(Some "{\"written\":true}\n"B, Scenarios.readFile reader commit "workspaces/demo/causa-app-pf015/write.json" |> Probes.ok "read back")
        Assert.DoesNotContain(fresh.Secrets, fun s -> List.contains s reader.Secrets)

    /// O4: a credential is never embedded or printed: not in a URL, an evidence line, a printed connection or request
    /// identity, or a failure description, during a real exchange that includes a refused credential.
    let o4 (mk: string -> GitHubProvider) =
        let real = mk "pf015-o4"
        let sent = ResizeArray<HttpRequest>()
        let recording = { real with Execute = fun r -> sent.Add r; real.Execute r }
        FixtureData.seed recording (FixtureData.frozenCatalog ()) "Causa fixtures: frozen v1.0.0 catalog" |> ignore
        let _ = ProviderPort.catalog recording
        let refused = Probes.authFailure recording
        let printed =
            [ yield! sent |> Seq.map (fun r -> r.Url)
              yield ProviderEvidence.record real "PF-015-O4" [] [ "requests", string sent.Count ]
              yield sprintf "%A" real.Connection
              yield sprintf "%A" real.Connection.Credential
              yield Classify.describe (match refused with Error e -> e | Ok _ -> failwith "refused") ]
        Assert.NotEmpty(sent)
        for secret in real.Secrets do
            for text in printed do
                Assert.DoesNotContain(secret, text)
        Assert.Equal("<credential>", string real.Connection.Credential)

    /// O5: a missing secret yields BLOCKED, a failing outcome, never a pass.
    let o5 (mk: string -> GitHubProvider) =
        for result in [ LiveGitHub.configurationFrom (fun _ -> None); LiveCredentials.writeConfigurationFrom (fun _ -> None) ] do
            match result with
            | Error reason ->
                Assert.StartsWith("BLOCKED", reason)
                Assert.Contains("not a pass", reason)
            | Ok _ -> failwith "a missing secret must be BLOCKED"
        let real = mk "pf015-o5"
        Assert.NotEmpty(real.Secrets)

    [<Fact>]
    let ``PF-015-O1`` () = o1 PfKit.live

    [<Fact>]
    let ``PF-015-O2`` () = o2 PfKit.live

    [<Fact>]
    let ``PF-015-O3`` () = o3 PfKit.live LiveCredentials.requireWrite

    [<Fact>]
    let ``PF-015-O4`` () = o4 PfKit.live

    [<Fact>]
    let ``PF-015-O5`` () = o5 PfKit.live

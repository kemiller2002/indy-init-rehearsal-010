// Owner: persistence. SM-LOAD transition tests (state-machine-contracts.json).
namespace Causa.Persistence.Tests.StateMachines

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.TestSupport.MachineKit

module SMLOAD =
    let private r: InvestigationRef = { Address = SaveKit.address; Id = SaveKit.investigationId }
    let private load (state: LoadState) (input: LoadInput) =
        match (m ()).StepLoad ctx state input with
        | Ok x -> x
        | Error f -> failwithf "%s/%s refused: %s" f.State f.Input f.Reason
    let private loadName (s: LoadState) =
        match s with
        | NotLoaded -> "NotLoaded"
        | Loading _ -> "Loading"
        | Loaded _ -> "Loaded"
        | LoadFailed _ -> "LoadFailed"
        | UnavailableOffline -> "UnavailableOffline"

    [<Fact>]
    let ``T-L01`` () =
        let next, effects = load NotLoaded (LoadInput.OpenRequested r)
        Assert.Equal(Loading(RequestId 40L, LoadHead), next)
        let sent = sends effects
        Assert.Equal(1, sent.Length)
        // One read of the branch head and of the investigation's three files at that head (never a cached branch read
        // followed by files at another commit).
        Assert.Equal(Some(ReadInvestigationAt "refs/heads/main"), GitHubRequests.operationOf sent.Head)
        Assert.Contains(SaveKit.dir SaveKit.investigationId + "/events.ndjson", sent.Head.Body.Value)
        // Against the fake, the answer verifies and loads.
        let running, _ = SaveKit.repository ()
        match (m ()).InterpretLoad ctx next (running.Execute sent.Head) with
        | LoadEvent(LoadInput.LoadVerified v) -> Assert.Equal(SaveKit.investigationId, v.Investigation.Id)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``T-L02`` () =
        match Persistence.openInvestigation ctx false NotLoaded r with
        | Ok(state, effects) ->
            Assert.Equal(UnavailableOffline, state)
            Assert.Empty(effects)
        | Error f -> failwithf "%A" f

    [<Fact>]
    let ``T-L03`` () =
        let next, effects = load (Loading(RequestId 7L, LoadHead)) (LoadInput.LoadVerified baseVerified)
        Assert.Equal(Loaded baseVerified, next)
        Assert.Empty(effects)
        // A working copy gets an SM-SAVE session, Saved at the loaded commit; a catalog investigation gets none.
        match Persistence.sessionOf baseVerified with
        | Some s ->
            Assert.Equal(baseVerified.ReadAt, s.BaseHead)
            Assert.Equal(SaveState.Saved(VerifiedAt baseVerified), s.State)
        | None -> failwith "a working copy has a session"
        let catalog = { baseVerified with Address = WorkspaceAddress.catalog (Workspace "workspaces/demo/demonstration-v1") }
        Assert.Equal(None, Persistence.sessionOf catalog)

    [<Fact>]
    let ``T-L04`` () =
        let failure = ProviderFailed(NotFound "investigation")
        let next, _ = load (Loading(RequestId 7L, LoadHead)) (LoadInput.LoadRejected failure)
        Assert.Equal(LoadFailed failure, next)
        // A file set that does not verify is rejected with the reader's stage, never partly loaded.
        let running, _ = SaveKit.repository ()
        running.Update(fun s ->
            StatefulFake.otherWriterCommits "main" "corrupter" [ SaveKit.dir SaveKit.investigationId + "/snapshot.json", "{}\n"B ] [] s |> fst)
        let loading, effects = load NotLoaded (LoadInput.OpenRequested r)
        match (m ()).InterpretLoad ctx loading (running.Execute (sends effects).Head) with
        | LoadEvent(LoadInput.LoadRejected(Unverifiable(VerificationFailure.ReaderStage(code, _, _, _) :: _))) ->
            Assert.Equal("CAUSA.SCHEMA_VIOLATION", code)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``T-L09`` () =
        let state = Loading(RequestId 7L, LoadHead)
        Assert.Equal(state, fst (load state (LoadInput.SupersededReply(RequestId 3L))))
        // A late answer to another request is interpreted as superseded.
        match (m ()).InterpretLoad ctx state (NotAnswered(RequestId 3L, LostAfterSending)) with
        | LoadEvent(LoadInput.SupersededReply id) -> Assert.Equal(RequestId 3L, id)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``T-L05`` () =
        let state = Loaded baseVerified
        let next, effects = load state LoadInput.WentOffline
        Assert.Equal(state, next)
        Assert.Empty(effects)

    [<Fact>]
    let ``T-L07`` () =
        let next, effects = load (Loaded baseVerified) LoadInput.RefreshRequested
        Assert.Equal(Loading(RequestId 40L, LoadHead), next)
        Assert.Equal(Some(ReadInvestigationAt "refs/heads/main"), GitHubRequests.operationOf (sends effects).Head)

    [<Fact>]
    let ``T-L10`` () =
        let state = Loaded baseVerified
        Assert.Equal(state, fst (load state (LoadInput.SupersededReply(RequestId 3L))))

    [<Fact>]
    let ``T-L08`` () =
        let next, effects = load (LoadFailed(ProviderFailed AuthenticationFailed)) (LoadInput.OpenRequested r)
        Assert.Equal(Loading(RequestId 40L, LoadHead), next)
        Assert.Equal(1, (sends effects).Length)

    [<Fact>]
    let ``T-L06`` () =
        Assert.Equal(NotLoaded, fst (load UnavailableOffline LoadInput.WentOnline))

    [<Fact>]
    let EveryIllegalPairFailsClosed () =
        let states =
            [ NotLoaded; Loading(RequestId 7L, LoadHead); Loaded baseVerified; LoadFailed(ProviderFailed AuthenticationFailed); UnavailableOffline ]
        let inputs =
            [ "OpenRequested", LoadInput.OpenRequested r
              "LoadVerified", LoadInput.LoadVerified baseVerified
              "LoadRejected", LoadInput.LoadRejected(ProviderFailed AuthenticationFailed)
              "WentOffline", LoadInput.WentOffline
              "WentOnline", LoadInput.WentOnline
              "RefreshRequested", LoadInput.RefreshRequested
              "SupersededReply", LoadInput.SupersededReply(RequestId 3L) ]
        let legal =
            set
                [ "NotLoaded", "OpenRequested"; "Loading", "LoadVerified"; "Loading", "LoadRejected"; "Loading", "SupersededReply"
                  "Loaded", "WentOffline"; "Loaded", "RefreshRequested"; "Loaded", "SupersededReply"; "LoadFailed", "OpenRequested"
                  "UnavailableOffline", "WentOnline" ]
        let illegal = [ for s in states do for i, input in inputs do if not (legal.Contains((loadName s, i))) then yield s, i, input ]
        Assert.Equal(26, illegal.Length)
        for s, i, input in illegal do
            match (m ()).StepLoad ctx s input with
            | Ok(next, _) -> failwithf "%s + %s accepted, reached %s" (loadName s) i (loadName next)
            | Error f ->
                Assert.Equal("SM-LOAD", f.Machine)
                Assert.Equal(loadName s, f.State)
                Assert.Equal(i, f.Input)

    // ---- T20: the read path when GraphQL cannot be used for the credential (REST at the same commit) ----

    /// Opens the kit investigation on `running` with a fresh machine, executing every request; the final load event
    /// and the operations sent, in order.
    let private openThrough (running: StatefulFake.Running) =
        let machine = SaveMachines.create SaveMachines.support
        let clock = SaveKit.clock ()
        match machine.StepLoad (SaveKit.context clock) NotLoaded (LoadInput.OpenRequested r) with
        | Error f -> failwith f.Reason
        | Ok(state, effects) ->
            SaveKit.advance clock effects
            let rec loop state (request: HttpRequest) sent =
                let outcome = running.Execute request
                let interpretation = machine.InterpretLoad (SaveKit.context clock) state outcome
                clock.Next <- clock.Next + 1L
                match interpretation with
                | LoadNextRequest(next, req) -> loop next req (request :: sent)
                | LoadEvent e -> e, List.rev (request :: sent) |> List.map (fun q -> GitHubRequests.operationOf q)
            loop state (SaveKit.requests effects).Head []

    let private refusing (status: int) (body: string) =
        let running, verified = SaveKit.repository ()
        running.Update(fun s -> { s with GraphQlReadRefusal = Some(status, body) })
        running, verified

    let private restSequence (head: CommitOid) =
        let d = SaveKit.dir SaveKit.investigationId
        [ Some(ReadInvestigationAt "refs/heads/main"); Some(ReadCommit(CommitOid "main")) ]
        @ [ for f in [ "manifest.json"; "events.ndjson"; "snapshot.json" ] -> Some(ReadFileAt(RepoPath(d + "/" + f), head)) ]

    [<Fact>]
    let ``T20 GraphQL refused for the installation token: the load reads the same files at the same commit through REST`` () =
        let running, verified = refusing 403 Recordings.graphQlEndpointRefused
        match openThrough running with
        | LoadInput.LoadVerified v, operations ->
            Assert.Equal(verified.ReadAt, v.ReadAt)
            Assert.Equal(verified.Bytes, v.Bytes)
            Assert.Equal(verified.Investigation, v.Investigation)
            Assert.Equal<GitHubOperation option list>(restSequence verified.ReadAt, operations)
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``T20 a schema without Commit.file: the load falls back to REST and verifies`` () =
        let running, verified = refusing 200 Recordings.graphQlFieldMissing
        match openThrough running with
        | LoadInput.LoadVerified v, operations ->
            Assert.Equal(verified.Bytes, v.Bytes)
            Assert.Equal(5, operations.Length)
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``T20 REST fallback reads a file over the contents limit through the Git blobs API`` () =
        let running, verified = refusing 403 Recordings.graphQlEndpointRefused
        running.Update(fun s -> { s with ContentsLargeAbove = 64 })
        match openThrough running with
        | LoadInput.LoadVerified v, operations ->
            Assert.Equal(verified.Bytes, v.Bytes)
            let blobs = operations |> List.filter (function Some(ReadBlob _) -> true | _ -> false)
            Assert.True(blobs.Length >= 2, sprintf "%A" operations)
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``T20 REST fallback: an investigation the commit does not hold is NotFound, and a refused credential is never retried another way`` () =
        let running, _ = refusing 403 Recordings.graphQlEndpointRefused
        running.Update(fun s ->
            let head = s.Branches["main"]
            let tree = s.Commits[head].Tree |> Map.filter (fun k _ -> not (k.StartsWith(SaveKit.dir SaveKit.investigationId)))
            { s with Commits = s.Commits.Add(head, { s.Commits[head] with Tree = tree }) })
        match openThrough running with
        | LoadInput.LoadRejected(ProviderFailed(NotFound _)), operations -> Assert.Equal(5, operations.Length)
        | other, _ -> failwithf "%A" other
        // GH-AUTH and rate limits mean the credential or the quota, not GraphQL: one request, the failure stands.
        for status, body, expected in
            [ 401, Recordings.badCredentials, AuthenticationFailed
              403, Recordings.primaryLimitMessage, RateLimited(Primary(Some 1793000000L)) ] do
            let running, _ = SaveKit.repository ()
            running.Update(StatefulFake.injectFault NextRequest (AnswerWith(status, Recordings.standardHeaders (if status = 401 then 4999 else 0), body)))
            match openThrough running with
            | LoadInput.LoadRejected(ProviderFailed e), operations ->
                Assert.Equal(expected, e)
                Assert.Equal(1, operations.Length)
            | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``T20 a superseded GraphQL answer starts no REST read`` () =
        let running, _ = refusing 403 Recordings.graphQlEndpointRefused
        let machine = SaveMachines.create SaveMachines.support
        let clock = SaveKit.clock ()
        let state, effects = machine.StepLoad (SaveKit.context clock) NotLoaded (LoadInput.OpenRequested r) |> Result.defaultWith (fun f -> failwith f.Reason)
        let request = (SaveKit.requests effects).Head
        let refused = running.Execute request
        // Another request is outstanding now: the refused answer to the earlier one is superseded, nothing is sent.
        match machine.InterpretLoad (SaveKit.context clock) (Loading(RequestId 999L, LoadHead)) refused with
        | LoadEvent(LoadInput.SupersededReply id) -> Assert.Equal(request.Id, id)
        | other -> failwithf "%A" other
        Assert.Equal(state, Loading(request.Id, LoadHead))

    // ---- EXT-DATA-013: a catalog investigation loads only as a case of its catalog manifest ----

    let private catalogRef (id: string) : InvestigationRef =
        { Address = WorkspaceAddress.catalog (Workspace "workspaces/demo/demonstration-v1"); Id = InvestigationId(Guid id) }

    let private catalogFake (change: Map<string, byte[]> -> Map<string, byte[]>) =
        StatefulFake.start (StatefulFake.initial "owner" "repo" "main" (Datasets.catalog.Value |> change |> Map.toList) [ "kit-token", ReadOnly ])

    let private openCatalog (running: StatefulFake.Running) (r: InvestigationRef) =
        let machine = SaveMachines.create SaveMachines.support
        let clock = SaveKit.clock ()
        let state, effects = machine.StepLoad (SaveKit.context clock) NotLoaded (LoadInput.OpenRequested r) |> Result.defaultWith (fun f -> failwith f.Reason)
        SaveKit.advance clock effects
        let rec loop state request sent =
            let interpretation = machine.InterpretLoad (SaveKit.context clock) state (running.Execute request)
            clock.Next <- clock.Next + 1L
            match interpretation with
            | LoadNextRequest(next, req) -> loop next req (request :: sent)
            | LoadEvent e -> e, List.rev (request :: sent)
        loop state (SaveKit.requests effects).Head []

    let private manifestPath = "catalog/demonstration-v1/manifest.json"

    let private editManifest (f: string -> string) (files: Map<string, byte[]>) =
        files.Add(manifestPath, Text.Encoding.UTF8.GetBytes(f (Text.Encoding.UTF8.GetString files[manifestPath])))

    [<Fact>]
    let ``EXT-DATA-013 a catalog case loads after its files verify and the manifest at the same commit records its state`` () =
        let running = catalogFake id
        match openCatalog running (catalogRef "fc3f71a9-4c14-5a31-8196-7ce17a7041c1") with
        | LoadInput.LoadVerified v, sent ->
            Assert.Equal(WorkspaceRole.Catalog, v.Address.Role)
            Assert.Equal(Some(ReadFileAt(RepoPath manifestPath, v.ReadAt)), GitHubRequests.operationOf (List.last sent))
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``EXT-DATA-013 an investigation the catalog manifest does not list is refused, however valid its files`` () =
        // A valid synthetic investigation placed in the catalog workspace is never presented as a catalog case.
        let synthetic = "6fd2ddc9-bedd-5fff-9093-5506c4d68cb3"
        let fs = FixtureData.frozenFiles synthetic
        let dir = sprintf "workspaces/demo/demonstration-v1/investigations/%s" synthetic
        let running = catalogFake (fun m -> m.Add(dir + "/manifest.json", fs.Manifest).Add(dir + "/events.ndjson", fs.Events).Add(dir + "/snapshot.json", fs.Snapshot))
        match openCatalog running (catalogRef synthetic) with
        | LoadInput.LoadRejected(Unverifiable [ ContractValueMismatch("investigationId", _, actual) ]), _ -> Assert.Equal(synthetic, actual)
        | other, _ -> failwithf "%A" other

    [<Fact>]
    let ``EXT-DATA-013 a catalog case whose replayed state or version is not the one the manifest records is refused`` () =
        let lightbulb = "fc3f71a9-4c14-5a31-8196-7ce17a7041c1"
        let digest = "40d11a632a97c1f94ff4fa48af94a373e5a2c7e9feadfc42bca9d891532a36bc"
        let wrongDigest = catalogFake (editManifest (fun t -> t.Replace(digest, String('0', 64))))
        match openCatalog wrongDigest (catalogRef lightbulb) with
        | LoadInput.LoadRejected(Unverifiable [ ContractValueMismatch("expectedStateSha256", expected, actual) ]), _ ->
            Assert.Equal(String('0', 64), expected)
            Assert.Equal(digest, actual)
        | other, _ -> failwithf "%A" other
        let wrongVersion = catalogFake (editManifest (fun t -> t.Replace("\"expectedVersion\":48,", "\"expectedVersion\":47,")))
        match openCatalog wrongVersion (catalogRef lightbulb) with
        | LoadInput.LoadRejected(Unverifiable [ ContractValueMismatch("expectedVersion", "47", "48") ]), _ -> ()
        | other, _ -> failwithf "%A" other
        let missing = catalogFake (fun m -> m.Remove manifestPath)
        match openCatalog missing (catalogRef lightbulb) with
        | LoadInput.LoadRejected(ProviderFailed(NotFound _)), _ -> ()
        | other, _ -> failwithf "%A" other

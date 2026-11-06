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

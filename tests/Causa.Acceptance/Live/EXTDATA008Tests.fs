// Owner: integration. EXT-DATA-008: a new investigation is command-created by the engine, written as a causa-storage
// v1 file set that validates and replays, and read back equal; no demonstration content is copied.
namespace Causa.Acceptance.Live

open System
open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport
open Causa.App

module EXTDATA008Tests =
    let private created (create: string -> GitHubProvider) label =
        let p = create label
        let t = AppDriver.tab p
        AppDriver.connect t |> ignore
        let problem = "New investigation " + Guid.NewGuid().ToString("N")
        let r = AppDriver.start t "Command-created" problem
        AppDriver.save t r |> ignore
        Assert.Equal("Saved", AppDriver.saveState t r)
        let fresh = AppDriver.tab p
        AppDriver.connect fresh |> ignore
        match Map.tryFind r fresh.Model.Loads with
        | Some(Loaded v) -> t, v, problem
        | other -> failwithf "not loaded: %A" other

    let o1 (create: string -> GitHubProvider) =
        let _, v, problem = created create "extdata008-o1"
        // The file set is the engine's own: the StartInvestigation command's single event, nothing else.
        Assert.Equal(AggregateVersion 1L, v.Investigation.Version)
        match v.History with
        | [ e ] ->
            match e.Payload with
            | CoreEvent(InvestigationStarted s) -> Assert.Equal(problem, s.Original.Value)
            | other -> failwithf "%A" other
        | other -> failwithf "history %A" other
        // No demonstration investigation's content.
        let text = Text.Encoding.UTF8.GetString(Array.concat [ v.Bytes.Manifest; v.Bytes.Events; v.Bytes.Snapshot ])
        for (id, title) in EXTDATA001Tests.fixtureCases () do
            Assert.DoesNotContain(id, text)
            if title.Length > 12 then Assert.DoesNotContain(title, text)

    let o2 (create: string -> GitHubProvider) =
        let t, v, _ = created create "extdata008-o2"
        // Schema-validated and replayable from GitHub's bytes; equal to what the tab wrote (read-back confirmed).
        Assert.True(Result.isOk (StorageReader.verify Kernel.domain (Some v.Investigation.Id) v.Address v.ReadAt v.Bytes))
        Assert.True(Result.isOk (Kernel.replay (List.rev v.Investigation.History)))
        let r = { Address = v.Address; Id = v.Investigation.Id }
        match t.Model.Sessions[r].State with
        | SaveState.Saved _ -> ()
        | other -> failwithf "%A" other
        Assert.Equal(1, AppDriver.mutations t |> List.length)

    [<Fact>]
    let ``EXT-DATA-008-O1`` () = o1 AppDriver.live

    [<Fact>]
    let ``EXT-DATA-008-O2`` () = o2 AppDriver.live

// Owner: persistence. PERSIST-011: a same-investigation change is a conflict, never rebased or overwritten.
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST011Tests =
    let private theirFiles () =
        SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf MachineKit.remoteHistory)

    /// Another writer saves the same investigation after this tab read it; this tab then saves.
    let private conflicted () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        SaveScenario.otherWriter sc (theirFiles ())
        let theirs = SaveScenario.head sc
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        sc, theirs

    [<Fact>]
    let ``PERSIST-011-O1`` () =
        let sc, theirs = conflicted ()
        Assert.Equal("Conflict", SaveScenario.stateName sc)
        Assert.Contains(SaveInput.StaleHeadSameInvestigation theirs, sc.Events)

    [<Fact>]
    let ``PERSIST-011-O2`` () =
        // Two tabs of the same user: two machines (two tab memories) on one repository.
        let tabA = SaveScenario.create ()
        let tabB = { SaveScenario.create () with Fake = tabA.Fake }
        tabB.Session <- tabB.Machine.Initial SaveKit.writable (SaveKit.verified (SaveScenario.head tabA) SaveKit.history)
        SaveScenario.edit tabA 1
        SaveScenario.edit tabB 2
        SaveScenario.input tabA SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", SaveScenario.stateName tabA)
        SaveScenario.input tabB SaveInput.SaveRequested |> ignore
        Assert.Equal("Conflict", SaveScenario.stateName tabB)
        Assert.Contains(SaveKit.edit 2, tabB.Session.Unsaved)

    [<Fact>]
    let ``PERSIST-011-O3`` () =
        let sc, theirs = conflicted ()
        // One mutation was sent (and refused as stale); no rebase, merge or overwrite followed.
        Assert.Equal(1, (SaveScenario.mutations sc).Length)
        Assert.Equal(theirs, SaveScenario.head sc)
        let tree = sc.Fake.Current().Commits[(let (CommitOid h) = theirs in h)].Tree
        let d = SaveKit.dir SaveKit.investigationId
        Assert.Equal<byte[]>((SaveKit.filesOf MachineKit.remoteHistory).Events, tree[d + "/events.ndjson"])

    [<Fact>]
    let ``PERSIST-011-O4`` () =
        let sc, _ = conflicted ()
        match sc.Session.State with
        | SaveState.Conflict _ -> ()
        | other -> failwithf "%A" other
        Assert.Equal<CommandEnvelope list>([ SaveKit.edit 1 ], sc.Session.Unsaved @ sc.Session.AfterInFlight)
        // Still visible and editable in the tab.
        SaveScenario.edit sc 2
        Assert.Equal<CommandEnvelope list>([ SaveKit.edit 1; SaveKit.edit 2 ], sc.Session.Unsaved)

    [<Fact>]
    let ``PERSIST-011-O5`` () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        let d = SaveKit.dir SaveKit.investigationId
        sc.Fake.Update(fun s -> StatefulFake.otherWriterCommits "main" "deleter" [] [ d + "/manifest.json"; d + "/events.ndjson"; d + "/snapshot.json" ] s |> fst)
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        match sc.Session.State with
        | SaveState.Conflict(ReadingRemote id) ->
            // The conflict read finds the investigation deleted: Unresolvable, with the work kept.
            let read = sc.Sent |> List.last
            match sc.Machine.InterpretConflict (SaveKit.context sc.Clock) (ReadingRemote id) (sc.Fake.Execute { read with Id = id }) with
            | ConflictEvent(ConflictInput.RemoteMissingOrInvalid None) -> ()
            | other -> failwithf "%A" other
        | other -> failwithf "%A" other
        Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved)

    [<Fact>]
    let ``PERSIST-011-O6`` () =
        let sc, theirs = conflicted ()
        Assert.Equal(1, (SaveScenario.mutations sc).Length)
        Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved)
        let state = sc.Fake.Current()
        Assert.True(StatefulFake.isAncestor state (let (CommitOid h) = theirs in h) state.Branches["main"])

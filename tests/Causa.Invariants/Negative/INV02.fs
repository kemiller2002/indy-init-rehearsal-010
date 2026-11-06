// Owner: domain. INV-02 negative cases (PERSIST-051-O2): late replies are dropped, never applied.
namespace Causa.Invariants.Negative

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV02 =
    let private lateAnswer (id: int64) = Answered(RequestId id, 200, [], "{\"data\":{}}")

    /// Rehearsal 7 third-review E1: a late read of head h1 after a re-read at h2 is dropped, and the other writer's
    /// change survives reapply.
    [<Fact>]
    let LateConflictReReadKeepsOtherWriter () =
        let m = machine ()
        let ctx = context 7L
        // The user asked to read GitHub's version again while the first read (request 5, head h1) was outstanding.
        let rereading =
            match m.StepConflict ctx (Reviewable baseVerified) ConflictInput.ReadAgain with
            | Ok(view, _) -> view
            | Error f -> failwithf "ReadAgain refused: %A" f
        Assert.Equal(ReadingRemote(RequestId 7L), rereading)
        // The late answer to request 5 is superseded and changes nothing.
        let afterLate =
            match m.InterpretConflict ctx rereading (lateAnswer 5L) with
            | ConflictEvent(ConflictInput.SupersededReply(RequestId 5L) as input) ->
                match m.StepConflict ctx rereading input with
                | Ok(view, effects) ->
                    Assert.Empty(effects)
                    view
                | Error _ -> rereading
            | other -> failwithf "a late reply was interpreted as %A" other
        Assert.Equal(rereading, afterLate)
        // The current read (head h2, with the other writer's observation) is reviewed and reapplied onto.
        let step view input =
            match m.StepConflict ctx view input with
            | Ok(next, _) -> next
            | Error f -> failwithf "%A refused in %A: %A" input view f
        let reapplied =
            [ ConflictInput.RemoteVerified remoteVerified; ConflictInput.ReapplyChosen; ConflictInput.ReapplyDone([ edit 1 ], []) ]
            |> List.fold step afterLate
        match reapplied with
        | Reapplied(remote, refused) ->
            Assert.Equal(CommitOid "c2", remote.ReadAt)
            Assert.Empty(refused)
            Assert.True(remote.Investigation.Observations.ContainsKey(Causa.Domain.ObservationId(guid 3)), "the other writer's change was lost")
        | other -> failwithf "expected Reapplied, got %A" other
        // SM-SAVE leaves Conflict onto GitHub's version: the base head is h2, and the reapplied command is unsaved.
        let session = { sessionIn m "Conflict" (SaveInput.ConflictResolvedReapplied [ edit 1 ]) with State = SaveState.Conflict reapplied }
        match m.Step ctx session (SaveInput.ConflictResolvedReapplied [ edit 1 ]) with
        | Ok(next, _) ->
            Assert.Equal("Unsaved", stateName next.State)
            Assert.Equal(CommitOid "c2", next.BaseHead)
            Assert.Contains(edit 1, next.Unsaved)
        | Error f -> failwithf "ConflictResolvedReapplied refused: %A" f

    /// Rehearsal 7 third-review E2: a refresh requested at base b1 that completes after a save moved the tab to b2 is
    /// dropped.
    [<Fact>]
    let StaleRefreshDropped () =
        let m = machine ()
        let ctx = context 9L
        // The load machine already holds the investigation at b2 (c2); the refresh's answer (request 5) arrives late.
        let loaded = Loaded remoteVerified
        let afterLoad =
            match m.InterpretLoad ctx loaded (lateAnswer 5L) with
            | LoadEvent(LoadInput.SupersededReply(RequestId 5L) as input) ->
                match m.StepLoad ctx loaded input with
                | Ok(state, effects) ->
                    Assert.Empty(effects)
                    state
                | Error _ -> loaded
            | other -> failwithf "a stale refresh was interpreted as %A" other
        Assert.Equal(loaded, afterLoad)
        // The save session at b2 drops it too: state, base head and unsaved changes are unchanged.
        let saved = { m.Initial writable remoteVerified with Unsaved = [] }
        let afterSave =
            match m.Interpret ctx saved (lateAnswer 5L) with
            | MachineEvent(SaveInput.SupersededReply(RequestId 5L) as input) ->
                match m.Step ctx saved input with
                | Ok(next, effects) ->
                    Assert.Empty(effects)
                    next
                | Error _ -> saved
            | other -> failwithf "a stale refresh reached the save session as %A" other
        Assert.Equal(CommitOid "c2", afterSave.BaseHead)
        Assert.Equal(saved.State, afterSave.State)
        Assert.Equal<Causa.Domain.CommandEnvelope list>(saved.Unsaved, afterSave.Unsaved)

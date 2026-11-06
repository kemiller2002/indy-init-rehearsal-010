// Owner: persistence. PERSIST-012: conflict resolution (review, reapply through the engine, confirmed discard).
namespace Causa.Acceptance.Deterministic

open Xunit
open Causa.Domain
open Causa.Persistence
open Causa.TestSupport

module PERSIST012Tests =
    /// A tab in Conflict after another writer saved the same investigation, with GitHub's version read and verified.
    let private reviewable () =
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        SaveScenario.otherWriter sc (SaveKit.repoFiles SaveKit.investigationId (SaveKit.filesOf MachineKit.remoteHistory))
        let theirs = SaveScenario.head sc
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        let id = match sc.Session.State with SaveState.Conflict(ReadingRemote id) -> id | other -> failwithf "%A" other
        let read = sc.Sent |> List.last
        let ctx = SaveKit.context sc.Clock
        match sc.Machine.InterpretConflict ctx (ReadingRemote id) (sc.Fake.Execute { read with Id = id }) with
        | ConflictEvent input ->
            match sc.Machine.StepConflict ctx (ReadingRemote id) input with
            | Ok(view, _) ->
                sc.Session <- { sc.Session with State = SaveState.Conflict view }
                sc, theirs, view
            | Error f -> failwithf "%A" f
        | other -> failwithf "%A" other

    let private conflictStep (sc: SaveScenario.T) (view: ConflictView) (input: ConflictInput) =
        match sc.Machine.StepConflict (SaveKit.context sc.Clock) view input with
        | Ok r -> r
        | Error f -> failwithf "%A" f

    let private remoteOf view = match view with Reviewable v -> v | other -> failwithf "%A" other

    [<Fact>]
    let ``PERSIST-012-O1`` () =
        let sc, _, _ = reviewable ()
        for input in [ SaveInput.Edit(SaveKit.edit 2); SaveInput.WentOffline; SaveInput.WentOnline ] do
            SaveScenario.input sc input |> ignore
            Assert.Equal("Conflict", SaveScenario.stateName sc)
        // Saving is not a way out of Conflict: the request is refused, nothing is sent.
        Assert.True(Result.isError (sc.Machine.Step (SaveKit.context sc.Clock) sc.Session SaveInput.SaveRequested))

    [<Fact>]
    let ``PERSIST-012-O2`` () =
        let sc, _, view = reviewable ()
        Assert.Equal(view, fst (conflictStep sc view ConflictInput.ReviewChosen))
        Assert.Equal(Reapplying(remoteOf view), fst (conflictStep sc view ConflictInput.ReapplyChosen))
        Assert.Equal(ConfirmingDiscard(remoteOf view), fst (conflictStep sc view ConflictInput.DiscardChosen))

    [<Fact>]
    let ``PERSIST-012-O3`` () =
        let sc, theirs, view = reviewable ()
        let remote = remoteOf view
        Assert.Equal(theirs, remote.ReadAt)
        // Re-decide each unsaved command, in order, through the F# engine at the GitHub version.
        let reapplying, _ = conflictStep sc view ConflictInput.ReapplyChosen
        let decided =
            sc.Session.Unsaved
            |> List.map (fun c -> c, Kernel.decide Transitions.corePolicy { ActorId = "causa-web" } (Some remote.Investigation) c)
        let accepted = decided |> List.choose (fun (c, d) -> match d with Allowed _ -> Some c | _ -> None)
        let reapplied, _ = conflictStep sc reapplying (ConflictInput.ReapplyDone(accepted, []))
        let session, effects = SaveKit.stepOk sc.Machine sc.Clock { sc.Session with State = SaveState.Conflict reapplied } (SaveInput.ConflictResolvedReapplied accepted)
        Assert.Equal(theirs, session.BaseHead)
        Assert.Empty(SaveKit.requests effects)
        // The user's save then appends after GitHub's version (version 3), on top of the other writer's commit.
        sc.Session <- session
        SaveScenario.input sc SaveInput.SaveRequested |> ignore
        Assert.Equal("Saved", SaveScenario.stateName sc)
        let state = sc.Fake.Current()
        Assert.Equal(Some(let (CommitOid t) = theirs in t), state.Commits[state.Branches["main"]].Parent)

    [<Fact>]
    let ``PERSIST-012-O4`` () =
        let sc, _, view = reviewable ()
        let remote = remoteOf view
        let refused = [ { Command = SaveKit.edit 1; Reason = "Blocked by the engine" } ]
        let reapplying, _ = conflictStep sc view ConflictInput.ReapplyChosen
        let reapplied, _ = conflictStep sc reapplying (ConflictInput.ReapplyDone([ SaveKit.edit 2 ], refused))
        Assert.Equal(Reapplied(remote, refused), reapplied)
        let session, _ = SaveKit.stepOk sc.Machine sc.Clock { sc.Session with State = SaveState.Conflict reapplied } (SaveInput.ConflictResolvedReapplied [ SaveKit.edit 2 ])
        Assert.Equal<CommandEnvelope list>([ SaveKit.edit 2 ], session.Unsaved)
        Assert.DoesNotContain(SaveKit.edit 1, session.Unsaved)
        Assert.Equal(remote.ReadAt, session.BaseHead)

    [<Fact>]
    let ``PERSIST-012-O5`` () =
        let sc, _, view = reviewable ()
        let sentBefore = sc.Sent.Length
        let reapplying, e1 = conflictStep sc view ConflictInput.ReapplyChosen
        let reapplied, e2 = conflictStep sc reapplying (ConflictInput.ReapplyDone([ SaveKit.edit 1 ], []))
        let session, e3 = SaveKit.stepOk sc.Machine sc.Clock { sc.Session with State = SaveState.Conflict reapplied } (SaveInput.ConflictResolvedReapplied [ SaveKit.edit 1 ])
        Assert.Empty(SaveKit.requests (e1 @ e2 @ e3))
        Assert.Equal("Unsaved", MachineKit.stateName session.State)
        Assert.Equal(sentBefore, sc.Sent.Length)

    [<Fact>]
    let ``PERSIST-012-O6`` () =
        let sc, _, view = reviewable ()
        let confirming, effects = conflictStep sc view ConflictInput.DiscardChosen
        Assert.Equal(ConfirmingDiscard(remoteOf view), confirming)
        Assert.Contains(effects, fun e -> match e with Announce(Assertive, _) -> true | _ -> false)
        // Cancel returns to review; only a confirmation discards.
        Assert.Equal(view, fst (conflictStep sc confirming ConflictInput.DiscardCancelled))
        Assert.Equal(Discarded(remoteOf view), fst (conflictStep sc confirming ConflictInput.DiscardConfirmed))
        Assert.True(Result.isError (sc.Machine.StepConflict (SaveKit.context sc.Clock) view ConflictInput.DiscardConfirmed))

    [<Fact>]
    let ``PERSIST-012-O7`` () =
        let sc, _, view = reviewable ()
        // The resolution events are exactly these; none of them sends a write, from any view reachable from review.
        let inputs =
            [ ConflictInput.ReadAgain; ConflictInput.ReviewChosen; ConflictInput.ReapplyChosen; ConflictInput.ReapplyDone([ SaveKit.edit 1 ], [])
              ConflictInput.DiscardChosen; ConflictInput.DiscardConfirmed; ConflictInput.DiscardCancelled; ConflictInput.ExportChosen ]
        let rec explore seen frontier =
            match frontier with
            | [] -> seen
            | v :: rest when List.contains v seen -> explore seen rest
            | v :: rest ->
                let next =
                    [ for i in inputs do
                          match sc.Machine.StepConflict (SaveKit.context sc.Clock) v i with
                          | Ok(v', effects) ->
                              Assert.Empty(MachineKit.mutations effects)
                              yield v'
                          | Error _ -> () ]
                explore (v :: seen) (rest @ next)
        let seen = explore [] [ view ]
        Assert.Contains(Discarded(remoteOf view), seen)
        let names = Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<ConflictInput>) |> Array.map _.Name
        Assert.DoesNotContain("Overwrite", names)
        Assert.DoesNotContain("ForcePush", names)

    [<Fact>]
    let ``PERSIST-012-O8`` () =
        // Discard: the tab loads the GitHub version; the other writer's commit is the branch head, untouched.
        let sc, theirs, view = reviewable ()
        let confirming, _ = conflictStep sc view ConflictInput.DiscardChosen
        let discarded, _ = conflictStep sc confirming ConflictInput.DiscardConfirmed
        let remote = remoteOf view
        let session, effects = SaveKit.stepOk sc.Machine sc.Clock { sc.Session with State = SaveState.Conflict discarded } (SaveInput.ConflictResolvedDiscarded remote)
        Assert.Equal(SaveState.Saved(VerifiedAt remote), session.State)
        Assert.Empty(SaveKit.requests effects)
        Assert.Equal(theirs, SaveScenario.head sc)
        // Review and reapply never write either; across the whole scenario only the first (refused) mutation was sent.
        Assert.Equal(1, (SaveScenario.mutations sc).Length)

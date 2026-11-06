// Owner: domain. INV-25 negative (PERSIST-012-O2): an unresolvable conflict offers an export and keeps the work.
namespace Causa.Invariants.Negative

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV25 =
    let private writes (effects: SaveEffect list) =
        effects
        |> List.exists (function
            | SaveEffect.Send r ->
                let body = r.Body |> Option.defaultValue ""
                body.Contains("mutation") || body.Contains("createCommitOnBranch")
            | _ -> false)

    [<Fact>]
    let UnresolvableConflictExports () =
        let m = machine ()
        let ctx = context 90L
        for cause in [ None; Some(VerificationFailure.DigestMismatch "eventsSha256") ] do
            // GitHub's version is deleted (None) or does not verify: the review cannot resolve.
            let unresolvable =
                match m.StepConflict ctx (ReadingRemote(RequestId 90L)) (ConflictInput.RemoteMissingOrInvalid cause) with
                | Ok(view, effects) ->
                    Assert.False(writes effects)
                    view
                | Error f -> failwithf "RemoteMissingOrInvalid refused: %A" f
            Assert.True((match unresolvable with Unresolvable _ -> true | _ -> false), $"reached {unresolvable}")
            // Export is offered, writes nothing to GitHub, and leaves the review where it is.
            match m.StepConflict ctx unresolvable ConflictInput.ExportChosen with
            | Ok(view, effects) ->
                Assert.Equal(unresolvable, view)
                Assert.False(writes effects)
                Assert.Contains(effects, fun e -> match e with SaveEffect.ExportCommands _ -> true | _ -> false)
            | Error f -> failwithf "ExportChosen refused while unresolvable: %A" f
            // The save session stays in Conflict with every unsaved command kept, and still accepts edits.
            let session = { sessionIn m "Conflict" (SaveInput.Edit(edit 2)) with State = SaveState.Conflict unresolvable }
            match m.Step ctx session (SaveInput.Edit(edit 2)) with
            | Ok(next, _) ->
                Assert.Equal("Conflict", stateName next.State)
                Assert.Contains(edit 1, next.Unsaved @ next.AfterInFlight)
                Assert.Contains(edit 2, next.Unsaved @ next.AfterInFlight)
            | Error f -> failwithf "an edit during an unresolvable conflict was refused: %A" f

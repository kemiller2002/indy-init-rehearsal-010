// Owner: domain. INV-03 negative (PERSIST-015-O5): an applied commit not yet visible is not this save's conflict.
namespace Causa.Invariants.Negative

open Xunit
open Causa.Persistence
open Causa.Invariants.SaveFixture

module INV03 =
    /// Rehearsal 7 KD-018: GitHub applied the commit, the answer was lost, and reconciliation cannot see it yet. Every
    /// reconciliation outcome that does not find the marker or another writer leaves the save undecided or retries
    /// it; none reports a conflict or a failure caused by this save's own commit.
    [<Fact>]
    let AppliedButNotYetVisible () =
        let m = machine ()
        let ctx = context 70L
        let step (s: InvestigationSession) input =
            match m.Step ctx s input with
            | Ok(next, _) -> next
            | Error f -> failwithf "%s refused in %s: %A" (inputName input) (stateName s.State) f
        let lost = step (sessionIn m "Saving" SaveInput.AnswerLost) SaveInput.AnswerLost
        Assert.Equal("UnknownOutcome", stateName lost.State)
        // Reads that fail or see nothing yet, the automatic reads running out, the user checking again and the network
        // returning: the outcome stays unknown.
        let waiting =
            [ SaveInput.ReconcileReadFailed; SaveInput.ReconcileReadFailed; SaveInput.ReconcileReadFailed; SaveInput.ReconcileReadsSpent
              SaveInput.WentOffline; SaveInput.WentOnline; SaveInput.CheckAgain; SaveInput.Edit(edit 5) ]
        let after =
            waiting
            |> List.fold
                (fun (s: InvestigationSession) input ->
                    match m.Step ctx s input with
                    | Ok(next, _) ->
                        Assert.Equal("UnknownOutcome", stateName next.State)
                        next
                    | Error _ -> s)
                lost
        Assert.Contains(edit 1, after.Unsaved @ after.AfterInFlight)
        // Nothing written and the head unchanged: the save is retried (or fails at the mutation limit), never a conflict.
        let session = sessionIn m "UnknownOutcome" SaveInput.ReconcileNothingWrittenHeadUnchanged
        let retried = step session SaveInput.ReconcileNothingWrittenHeadUnchanged
        Assert.NotEqual<string>("Conflict", stateName retried.State)
        // Its own marker found on equal bytes: Saved (or Unsaved with later edits), never a conflict with itself.
        let found = step (sessionIn m "UnknownOutcome" (SaveInput.ReconcileMarkerBytesMatch(confirmation "c1"))) (SaveInput.ReconcileMarkerBytesMatch(confirmation "c1"))
        Assert.Contains(stateName found.State, [ "Saved"; "Unsaved" ])

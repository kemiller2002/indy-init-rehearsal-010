// Owner: integration. Written by the persistence lane under CR-T21-TESTS (approved by the integration authority).
// AR-035: unique write markers, confirmation only from a verified read-back, and an unrecognised answer after a write
// treated as "may have saved".
namespace Causa.Acceptance.Deterministic

open System.Text.Json
open Xunit
open Causa.Persistence
open Causa.TestSupport

module AR035Tests =
    let private markerOf (r: HttpRequest) =
        use doc = JsonDocument.Parse(r.Body.Value)
        doc.RootElement.GetProperty("variables").GetProperty("input").GetProperty("message").GetProperty("body").GetString() |> string |> GitHubRequests.markerOf

    [<Fact>]
    let ``AR-035-O2`` () =
        // Two writers (tabs) and several saves each: every marker differs.
        let tabs = [ SaveScenario.create (); SaveScenario.create () ]
        for sc in tabs do
            for n in 1..3 do
                SaveScenario.edit sc n
                SaveScenario.input sc SaveInput.SaveRequested |> ignore
        let markers = tabs |> List.collect (SaveScenario.mutations >> List.map markerOf)
        Assert.Equal(6, markers.Length)
        Assert.All(markers, fun m -> Assert.True(m.IsSome))
        Assert.Equal(markers.Length, (List.distinct markers).Length)
        // Not confirmed until the committed content is read back and verified: GitHub's answer alone leaves Saving,
        // and a read-back of other bytes fails the save.
        let sc = SaveScenario.create ()
        SaveScenario.edit sc 1
        let next, effects = SaveKit.stepOk sc.Machine sc.Clock sc.Session SaveInput.SaveRequested
        let mutation = (SaveKit.requests effects).Head
        match sc.Machine.Interpret (SaveKit.context sc.Clock) next (sc.Fake.Execute mutation) with
        | Interpretation.NextRequest(awaiting, readBack) ->
            Assert.Equal("Saving", MachineKit.stateName awaiting.State)
            Assert.NotEqual(Some(CommitOnBranch next.BaseHead), GitHubRequests.operationOf readBack)
        | other -> failwithf "the commit answer confirmed without a read-back: %A" other
        let differ = SaveScenario.create ()
        SaveScenario.edit differ 1
        differ.Before <-
            fun r ->
                match GitHubRequests.operationOf r with
                | Some(ReadInvestigationAt rev) when rev <> "refs/heads/main" ->
                    let path = SaveKit.dir SaveKit.investigationId + "/snapshot.json"
                    differ.Fake.Update(fun s -> let c = s.Commits[rev] in { s with Commits = s.Commits.Add(rev, { c with Tree = c.Tree.Add(path, "{}\n"B) }) })
                | _ -> ()
        SaveScenario.input differ SaveInput.SaveRequested |> ignore
        match differ.Session.State with
        | SaveState.SaveFailed(BytesDiffer _) -> ()
        | other -> failwithf "a read-back of other bytes must not confirm: %A" other

    [<Fact>]
    let ``AR-035-O3`` () =
        for fault in
            [ AnswerWith(502, [], "<html>Bad Gateway</html>")
              AnswerWith(200, Recordings.standardHeaders 4000, """{"data":{"createCommitOnBranch":null},"errors":[{"type":"SOMETHING_NEW","message":"?"}]}""")
              AnswerWith(200, Recordings.standardHeaders 4000, "not json")
              AnswerWith(418, Recordings.standardHeaders 4000, "{}")
              LoseAnswerAfterApplying
              TimeOutBeforeApplying ] do
            let sc = SaveScenario.create ()
            SaveScenario.edit sc 1
            sc.Fake.Update(StatefulFake.injectFault NextMutation fault)
            SaveScenario.input sc SaveInput.SaveRequested |> ignore
            // "May have saved": Unknown commit outcome, the change kept, nothing resent before reconciliation.
            Assert.True((SaveScenario.stateName sc = "UnknownOutcome"), sprintf "%A reached %s" fault (SaveScenario.stateName sc))
            Assert.Contains(SaveKit.edit 1, sc.Session.Unsaved @ sc.Session.AfterInFlight)
            Assert.Equal(1, (SaveScenario.mutations sc).Length)
